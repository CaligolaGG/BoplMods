using System.Collections.Generic;
using System.Linq;
using BoplFixedMath;
using CaliGameModes.Maps;
using CaliSkills.Abilities.NothingPersonal;
using CaliSkills.Abilities.Pinball;
using UnityEngine;

namespace CaliGameModes.Modes.Football;

/// <summary>
/// One football match (= one round of the game), created after the players spawn, gone with the level.
/// Sim (every machine alike): players are split by where they spawned (left / right of the ball spawn); the mode's
/// balls (only those: Pinball skill balls never score) score when they're inside a goal, which gives the point to the
/// other side, removes every ball and brings <see cref="FootballMode.Balls"/> new ones after 2 s (a smoke cloud first);
/// a ball lost any other way comes back at once. The round ends at <see cref="FootballMode.FirstTo"/> goals or after
/// <see cref="FootballMode.TimeLimit"/>: the side with more goals wins it (a tie: everyone does, like a draw).
/// Visuals (local): the goals' sprites, the score at the top of the screen.
/// </summary>
internal sealed class FootballMatch : MonoUpdatable, IRespawnMatch
{
    private static readonly Fix RespawnDelay = (Fix)2L;
    private static readonly Fix CloudLead = (Fix)0.6f;
    private static readonly Fix BallSpacing = (Fix)4L;
    private const float CloudDuration = 1.2f;
    private const float GoalBannerSeconds = 1.6f;

    private readonly List<PinballBall> _balls = new();
    private readonly List<int> _lostSlots = new();
    private readonly Dictionary<int, int> _sideOfPlayer = new();
    private readonly int[] _scores = new int[2];
    private readonly Color[] _sideColors = { Color.white, Color.white };

    private GameSessionHandler _session;
    private FootballMap _map;
    private FootballGoal[] _goals;
    private Fix _clock;
    private Fix _respawnIn = -Fix.One;
    private bool _cloudsShown;
    private bool _ended;

    // Visuals only.
    private SpriteRenderer[] _goalRenderers;
    private float _goalBannerUntil;
    private int _lastScorer;
    private GUIStyle _scoreStyle, _smallStyle;

    internal static FootballMatch Current { get; private set; }

    public Vec2 RespawnPosition(Player player) => _map.PlayerRespawn;

    public void OnKilled(Player victim, int killerId)
    {
    }

    private void Awake() => Updater.RegisterUpdatable(this);

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
        _map = FootballMaps.ForMap(MapLoaderBridge.CurrentMapUuid);
        if (_map == FootballMaps.NoGoals)
            Plugin.Log.LogWarning("Football: this isn't a football map (is Map Loader installed?), playing without goals.");

        var goals = new List<FootballGoal>();
        if (_map.LeftGoal.HasValue)
            goals.Add(new FootballGoal(_map.LeftGoal.Value, 0));
        if (_map.RightGoal.HasValue)
            goals.Add(new FootballGoal(_map.RightGoal.Value, 1));
        _goals = goals.ToArray();

        // Sides from where the slimes spawned (just placed by SpawnPlayers).
        foreach (SlimeController slime in session.slimeControllers.Where(s => s != null))
        {
            int side = slime.body.position.x < _map.BallSpawn.x ? 0 : 1;
            _sideOfPlayer[slime.playerNumber] = side;
            Player player = PlayerHandler.Get().GetPlayer(slime.playerNumber);
            if (player != null && _sideColors[side] == Color.white && session.teamColors != null && player.Team < session.teamColors.Length)
                _sideColors[side] = session.teamColors[player.Team].fill;
        }

        SpawnBalls(FootballMode.Balls.ActiveInt);
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        foreach (FootballGoal goal in _goals)
            goal.Measure();
        if (_ended || _session == null || !_session.gameInProgress)
            return;

        _clock += simDeltaTime;

        // Balls destroyed by something else than a goal (water, out of the map...).
        for (int i = _balls.Count - 1; i >= 0; i--)
        {
            if (_balls[i] == null || _balls[i].IsDestroyed)
            {
                _balls.RemoveAt(i);
                _lostSlots.Add(i);
            }
        }

        if (_respawnIn >= Fix.Zero)
        {
            _respawnIn -= simDeltaTime;
            if (!_cloudsShown && _respawnIn <= CloudLead)
            {
                _cloudsShown = true;
                int count = FootballMode.Balls.ActiveInt;
                for (int i = 0; i < count; i++)
                    ShowCloud(SlotPosition(i, count));
            }
            if (_respawnIn <= Fix.Zero)
            {
                _respawnIn = -Fix.One;
                SpawnBalls(FootballMode.Balls.ActiveInt);
            }
        }
        else
        {
            int count = FootballMode.Balls.ActiveInt;
            foreach (int slot in _lostSlots)
            {
                Vec2 position = SlotPosition(Mathf.Min(slot, count - 1), count);
                ShowCloud(position);
                SpawnBall(position);
            }
            CheckGoals();
        }
        _lostSlots.Clear();

        int limit = FootballMode.TimeLimit.ActiveInt;
        if (!_ended && limit > 0 && _clock >= (Fix)limit)
            End();
    }

    private void CheckGoals()
    {
        Fix radius = (Fix)FootballSettings.BallSize;
        foreach (PinballBall ball in _balls)
        {
            foreach (FootballGoal goal in _goals)
            {
                if (goal.Contains(ball.Position, radius))
                {
                    Score(1 - goal.Side);
                    return;
                }
            }
        }
    }

    private void Score(int side)
    {
        _scores[side]++;
        foreach (PinballBall ball in _balls)
            Updater.DestroyFix(ball.gameObject);
        _balls.Clear();
        _respawnIn = RespawnDelay;
        _cloudsShown = false;
        _lastScorer = side;
        _goalBannerUntil = Time.unscaledTime + GoalBannerSeconds;
        AudioManager.Get()?.Play("rockBounce");

        int needed = FootballMode.FirstTo.ActiveInt;
        if (needed > 0 && _scores[side] >= needed)
            End();
    }

    /// <summary>Like GameSessionHandler.WinGame, for a whole side (in free-for-all a side can hold several teams).</summary>
    private void End()
    {
        _ended = true;
        bool draw = _scores[0] == _scores[1];
        int winner = _scores[0] > _scores[1] ? 0 : 1;
        List<Player> players = PlayerHandler.Get().PlayerList();

        _session.gameInProgress = false;
        AudioManager.Get()?.OnMatchEnded(isDraw: draw);
        _session.ActivateSuddenDeath(activate: false);
        foreach (Player player in players)
            player.IsMostRecentWinner = false;
        foreach (Player player in players)
        {
            if (draw || (_sideOfPlayer.TryGetValue(player.Id, out int side) && side == winner))
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

    private Vec2 SlotPosition(int index, int count) =>
        _map.BallSpawn + new Vec2((Fix)(2 * index - (count - 1)) * BallSpacing / (Fix)2L, Fix.Zero);

    private void SpawnBalls(int count)
    {
        for (int i = 0; i < count; i++)
            SpawnBall(SlotPosition(i, count));
    }

    private void SpawnBall(Vec2 position)
    {
        PinballBall ball = PinballSkill.Instance?.SpawnBall(position, Vec2.zero, Fix.One, FootballTuning.Instance,
            FootballSprites.Ball(FootballSettings.BallSize));
        if (ball != null)
            _balls.Add(ball);
    }

    private static void ShowCloud(Vec2 position) =>
        NothingPersonalVisuals.SpawnSmokeCloud(position, 0.45f * Mathf.Max(1f, FootballSettings.BallSize), CloudDuration);

    private void LateUpdate()
    {
        _goalRenderers ??= _goals.Select(g =>
        {
            var go = new GameObject("FootballGoal");
            go.transform.SetParent(transform, worldPositionStays: false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = -1;
            return renderer;
        }).ToArray();

        for (int i = 0; i < _goals.Length; i++)
        {
            FootballGoal goal = _goals[i];
            _goalRenderers[i].enabled = goal.Valid;
            if (!goal.Valid)
                continue;
            Rect rect = goal.WorldRect;
            _goalRenderers[i].sprite = FootballSprites.Goal(rect.width, rect.height, goal.Side == 1);
            _goalRenderers[i].transform.position = new Vector3(rect.x, rect.y, 0.5f);
        }
    }

    private void OnGUI()
    {
        _scoreStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold, richText = true };
        _smallStyle ??= new GUIStyle(_scoreStyle) { fontStyle = FontStyle.Normal };
        _scoreStyle.fontSize = Mathf.Max(24, Screen.height / 14);
        _smallStyle.fontSize = Mathf.Max(14, Screen.height / 36);

        float y = Screen.height * 0.02f;
        string score = $"<color=#{Hex(_sideColors[0])}>{_scores[0]}</color>  -  <color=#{Hex(_sideColors[1])}>{_scores[1]}</color>";
        Shadowed(new Rect(0, y, Screen.width, _scoreStyle.fontSize * 1.4f), score, _scoreStyle);
        y += _scoreStyle.fontSize * 1.3f;

        int limit = FootballMode.TimeLimit.ActiveInt;
        if (limit > 0)
        {
            int left = Mathf.Max(0, Mathf.CeilToInt(limit - (float)_clock));
            Shadowed(new Rect(0, y, Screen.width, _smallStyle.fontSize * 1.5f), $"{left / 60}:{left % 60:00}", _smallStyle);
            y += _smallStyle.fontSize * 1.4f;
        }

        if (Time.unscaledTime < _goalBannerUntil)
            Shadowed(new Rect(0, y, Screen.width, _scoreStyle.fontSize * 1.4f), $"<color=#{Hex(_sideColors[_lastScorer])}>GOAL!</color>", _scoreStyle);
    }

    private static void Shadowed(Rect rect, string text, GUIStyle style)
    {
        string plain = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), plain, style);
        GUI.color = old;
        GUI.Label(rect, text, style);
    }

    private static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
}
