using System.Collections.Generic;
using System.Linq;
using BoplFixedMath;
using CustomRules.Rules;
using HarmonyLib;
using UnityEngine;

namespace CaliGameModes.Modes.Rotation;

/// <summary>
/// One Rotation match (= one round of the game), created after the players spawn, gone with the level.
/// Sim (every machine alike): used-up abilities from earlier rounds come back; a slime that would die comes back at
/// the spawn point farthest from the other slimes, and its killer (another team) gets a point. A death with no such
/// killer (water, out of the map, own weapon...) goes to whoever of another team knocked the slime in the last few
/// seconds (<see cref="KnockTracker"/>); with nobody to blame it's a suicide: every player of the other teams gets a
/// point. A slime that comes back is invulnerable for <see cref="RespawnImmunity"/> once its revive animation ends.
/// Once every player is out
/// of abilities (every slot lost: <see cref="AbilityMaxUseRule.HasAbilitiesLeft"/>), the round ends a moment later and
/// the team with the highest score wins it (a tie at the top: every tied team does).
/// Visuals (local): the scores at the top of the screen.
/// </summary>
internal sealed class RotationMatch : MonoUpdatable, IRespawnMatch
{
    /// <summary>Time before the end is checked (the slimes get their abilities when they spawn).</summary>
    private static readonly Fix StartGrace = Fix.One;

    /// <summary>Time between the last ability used up and the end (its effect can still kill someone).</summary>
    private static readonly Fix EndDelay = (Fix)1.5f;

    /// <summary>Invulnerability after a respawn, from the end of the revive animation.</summary>
    private static readonly Fix RespawnImmunity = Fix.One;

    /// <summary>Our own invulnerability bit (the game's revive uses 4, Nothing Personal Kid 1&lt;&lt;10, Chad 1&lt;&lt;11).</summary>
    private const uint ImmunityMask = 1u << 12;

    /// <summary>Players killed and not yet back (their revive animation ends with SlimeController.Spawn).</summary>
    private readonly HashSet<int> _reviving = new();

    /// <summary>Respawn invulnerability left per player id (sorted: same order on every machine).</summary>
    private readonly SortedDictionary<int, Fix> _immune = new();
    private readonly Dictionary<int, SlimeController> _immuneSlime = new();
    private readonly List<int> _immuneIds = new();

    private readonly Dictionary<int, int> _scores = new();
    private readonly List<Vec2> _spawnPoints = new();
    private readonly List<int> _playerIds = new();

    private GameSessionHandler _session;
    private Fix _clock;
    private Fix _allOutFor;
    private bool _ended;

    /// <summary>Slimes still with abilities, counted every tick (shown at the top).</summary>
    private int _withAbilities = -1;

    // Visuals only.
    private GUIStyle _scoreStyle, _smallStyle;
    private string _scoreText, _scorePlain, _leftText;
    private int _shownScoreVersion = -1, _shownLeft = -2;
    private int _scoreVersion;

    internal static RotationMatch Current { get; private set; }

    /// <summary>Sim time since the round started.</summary>
    internal Fix Clock => _clock;

    private void Awake()
    {
        Updater.RegisterUpdatable(this);
        useGUILayout = false; // only GUI.Label: skips IMGUI's layout pass
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    public override void Init()
    {
    }

    internal void Begin(GameSessionHandler session)
    {
        Current = this;
        _session = session;
        AbilityMaxUseRule.ForgetUsedUp();
        KnockTracker.Reset();

        foreach (SlimeController slime in Slimes().OrderBy(s => s.playerNumber))
        {
            _spawnPoints.Add(slime.body.position);
            if (!_playerIds.Contains(slime.playerNumber))
            {
                _playerIds.Add(slime.playerNumber);
                _scores[slime.playerNumber] = 0;
            }
        }
    }

    internal IEnumerable<SlimeController> Slimes() =>
        _session.slimeControllers.Where(s => s != null && !s.IsDestroyed);

    /// <summary>The spawn point farthest from every other slime (the first one on ties: same on every machine).</summary>
    public Vec2 RespawnPosition(Player player)
    {
        if (_spawnPoints.Count == 0)
            return player.SpawnPosition;
        List<Vec2> others = Slimes().Where(s => s.playerNumber != player.Id).Select(s => s.body.position).ToList();
        Vec2 best = _spawnPoints[0];
        Fix bestDistance = -Fix.One;
        foreach (Vec2 point in _spawnPoints)
        {
            Fix nearest = (Fix)100000L;
            foreach (Vec2 other in others)
            {
                Fix distance = Vec2.Magnitude(other - point);
                if (distance < nearest)
                    nearest = distance;
            }
            if (nearest > bestDistance)
            {
                bestDistance = nearest;
                best = point;
            }
        }
        return best;
    }

    public void OnKilled(Player victim, int killerId)
    {
        int knocker = KnockTracker.RecentKnocker(victim, _clock);
        KnockTracker.Forget(victim.Id);
        _reviving.Add(victim.Id);
        if (_ended)
            return;

        Player killer = killerId != victim.Id ? PlayerHandler.Get().GetPlayer(killerId) : null;
        _scoreVersion++;
        if (killer != null && _scores.ContainsKey(killerId))
        {
            // Killed by another player: their point (none for a teammate).
            if (killer.Team != victim.Team)
                _scores[killerId]++;
            return;
        }
        // Killed by themselves or by the map: whoever knocked them there, else a suicide.
        if (knocker >= 0 && _scores.ContainsKey(knocker))
        {
            _scores[knocker]++;
            return;
        }
        foreach (int id in _playerIds)
        {
            Player other = PlayerHandler.Get().GetPlayer(id);
            if (other != null && other.Team != victim.Team)
                _scores[id]++;
        }
    }

    /// <summary>A slime finished spawning: after a respawn, it's invulnerable for a moment.</summary>
    private void OnSpawned(SlimeController slime)
    {
        if (!_reviving.Remove(slime.playerNumber) || RespawnImmunity <= Fix.Zero)
            return;
        slime.playerCollision.SetInvulnerable(true, ImmunityMask);
        _immune[slime.playerNumber] = RespawnImmunity;
        _immuneSlime[slime.playerNumber] = slime;
    }

    private void UpdateImmunity(Fix simDeltaTime)
    {
        if (_immune.Count == 0)
            return;
        _immuneIds.Clear();
        _immuneIds.AddRange(_immune.Keys);
        foreach (int id in _immuneIds)
        {
            Fix left = _immune[id] - simDeltaTime;
            SlimeController slime = _immuneSlime[id];
            bool present = slime != null && !slime.IsDestroyed;
            if (left > Fix.Zero && present)
            {
                _immune[id] = left;
                continue;
            }
            _immune.Remove(id);
            _immuneSlime.Remove(id);
            if (present)
                slime.playerCollision.SetInvulnerable(false, ImmunityMask);
        }
    }

    [HarmonyPatch(typeof(SlimeController), nameof(SlimeController.Spawn))]
    private static class SpawnPatch
    {
        private static void Postfix(SlimeController __instance) => Current?.OnSpawned(__instance);
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        if (_session != null)
            UpdateImmunity(simDeltaTime);
        if (_ended || _session == null || !_session.gameInProgress)
            return;
        _clock += simDeltaTime;
        if (_clock < StartGrace)
            return;

        int left = 0;
        foreach (SlimeController slime in _session.slimeControllers)
        {
            if (slime != null && !slime.IsDestroyed && AbilityMaxUseRule.HasAbilitiesLeft(slime))
                left++;
        }
        _withAbilities = left;
        bool anyLeft = left > 0;
        _allOutFor = anyLeft ? Fix.Zero : _allOutFor + simDeltaTime;
        if (!anyLeft && _allOutFor >= EndDelay)
            End();
    }

    /// <summary>Like GameSessionHandler.WinGame, for every player of the best team(s).</summary>
    private void End()
    {
        _ended = true;
        List<Player> players = PlayerHandler.Get().PlayerList();
        var teamScores = new Dictionary<int, int>();
        foreach (Player player in players)
        {
            teamScores.TryGetValue(player.Team, out int sum);
            teamScores[player.Team] = sum + (_scores.TryGetValue(player.Id, out int score) ? score : 0);
        }
        int best = teamScores.Count > 0 ? teamScores.Values.Max() : 0;
        bool draw = teamScores.Values.Count(s => s == best) > 1;

        _session.gameInProgress = false;
        AudioManager.Get()?.OnMatchEnded(isDraw: draw);
        _session.ActivateSuddenDeath(activate: false);
        foreach (Player player in players)
            player.IsMostRecentWinner = false;
        foreach (Player player in players)
        {
            if (teamScores[player.Team] == best)
                player.WinAGame();
        }
        AchievementHandler.CheckAchievementsOnWinRound();
        AchievementHandler.OnRoundEnded();
        _session.prepareNextlevel();
        if (!_session.winnerTriangle)
        {
            _session.winnerTriangleCanvas = WinnerTriangleCanvas.instance.gameObject;
            _session.winnerTriangle = WinnerTriangleCanvas.instance.charStatsList.transform.parent.gameObject;
        }
        _session.playerInitRoutine.Delay(_session.DelayBeforeNextLevelInput - (Fix)2L);
        _session.playerInitRoutine.Function(_session.ActivateWinnerTriangle);
    }

    private void OnGUI()
    {
        // Drawn once per frame (other events have nothing to do here).
        if (_session == null || Event.current.type != EventType.Repaint)
            return;
        _scoreStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold, richText = true };
        _smallStyle ??= new GUIStyle(_scoreStyle) { fontStyle = FontStyle.Normal };
        _scoreStyle.fontSize = Mathf.Max(24, Screen.height / 14);
        _smallStyle.fontSize = Mathf.Max(14, Screen.height / 40);

        // The texts are rebuilt only when they change.
        if (_scoreText == null || _shownScoreVersion != _scoreVersion)
        {
            _shownScoreVersion = _scoreVersion;
            var parts = new List<string>();
            var plain = new List<string>();
            foreach (int id in _playerIds)
            {
                Player player = PlayerHandler.Get().GetPlayer(id);
                Color color = player != null && _session.teamColors != null && player.Team < _session.teamColors.Length
                    ? _session.teamColors[player.Team].fill
                    : Color.white;
                parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{_scores[id]}</color>");
                plain.Add(_scores[id].ToString());
            }
            _scoreText = string.Join("   ", parts);
            _scorePlain = string.Join("   ", plain);
        }
        float y = Screen.height * 0.02f;
        Shadowed(new Rect(0, y, Screen.width, _scoreStyle.fontSize * 1.4f), _scoreText, _scorePlain, _scoreStyle);
        y += _scoreStyle.fontSize * 1.3f;

        if (!_ended && _clock >= StartGrace && _withAbilities >= 0)
        {
            if (_shownLeft != _withAbilities)
            {
                _shownLeft = _withAbilities;
                _leftText = _withAbilities == 0 ? "Out of abilities!" : $"{_withAbilities} still with abilities";
            }
            Shadowed(new Rect(0, y, Screen.width, _smallStyle.fontSize * 1.5f), _leftText, _leftText, _smallStyle);
        }
    }

    private static void Shadowed(Rect rect, string text, string plain, GUIStyle style)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), plain, style);
        GUI.color = old;
        GUI.Label(rect, text, style);
    }
}
