using System.Collections.Generic;
using System.Linq;
using BoplFixedMath;
using UnityEngine;

namespace CaliSkills.Abilities.Omae;

/// <summary>
/// Marked players ("you are already dead"). A mark counts down in sim time; at zero every body of that player is
/// killed the way a squished slime dies. Poking a marked player again shortens the mark. A mark ends when its player
/// dies of something else (a Revive brings them back unmarked).
///
/// Lives on a scene GameObject created with the first mark of a level, so marks never carry over to the next level.
/// The flashing (<see cref="LateUpdate"/>) is visual only and may use floats and Unity time.
/// </summary>
public class OmaeMarks : MonoUpdatable
{
    private sealed class MarkState
    {
        public Fix Remaining;
        public int MarkerId;
    }

    private static OmaeMarks _instance;

    // Sorted: the countdown and the kills happen in the same order on every machine.
    private readonly SortedDictionary<int, MarkState> _marks = new();

    private readonly Dictionary<SpriteRenderer, SpriteRenderer> _overlays = new();
    private readonly Dictionary<int, bool> _isRedPlayer = new();
    private readonly Dictionary<int, float> _flashPhases = new();
    private PlayerCollision[] _bodies = new PlayerCollision[0];
    private float _nextBodySearch;
    private Material _flashMaterial;
    private static bool _loggedMaterial;

    /// <summary>Marks a player, or shortens their mark if they already have one. Sim code only.</summary>
    internal static void Mark(int playerId, int markerId)
    {
        if (_instance == null)
            _instance = new GameObject("CaliSkills.OmaeMarks").AddComponent<OmaeMarks>();

        if (_instance._marks.TryGetValue(playerId, out MarkState mark))
        {
            mark.Remaining -= (Fix)OmaeSettings.MarkDurationReduction;
            mark.MarkerId = markerId;
        }
        else
        {
            _instance._marks[playerId] = new MarkState { Remaining = (Fix)OmaeSettings.MarkDuration, MarkerId = markerId };
        }
    }

    private void Awake() => Updater.RegisterUpdatable(this);

    public override void Init()
    {
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        if (_marks.Count == 0)
            return;

        var ended = new List<int>();
        foreach (KeyValuePair<int, MarkState> pair in _marks)
        {
            Player player = PlayerHandler.Get().GetPlayer(pair.Key);
            if (player == null || !player.IsAlive)
            {
                ended.Add(pair.Key);
                continue;
            }

            // Same clock as abilities: time stop pauses the countdown.
            pair.Value.Remaining -= GameTime.FixedDeltaTime(pair.Key, simDeltaTime);
            if (pair.Value.Remaining <= Fix.Zero && KillAllBodies(pair.Key, pair.Value.MarkerId))
                ended.Add(pair.Key);
        }
        foreach (int id in ended)
            _marks.Remove(id);
    }

    /// <returns>True if at least one body died (otherwise, e.g. invulnerable right now, try again next tick).</returns>
    private static bool KillAllBodies(int playerId, int killerId)
    {
        // Deterministic order: FindObjectsOfType's isn't guaranteed to match between machines.
        List<PlayerCollision> bodies = FindObjectsOfType<PlayerCollision>()
            .Where(b => b.GetPlayerId() == playerId && b.gameObject.activeInHierarchy && b.body != null)
            .OrderBy(b => b.body.position.x.m_rawValue)
            .ThenBy(b => b.body.position.y.m_rawValue)
            .ToList();

        bool killed = false;
        foreach (PlayerCollision body in bodies)
            killed |= body.killPlayer(killerId, spawnEffect: true, ignoreInvulnerability: false, CauseOfDeath.Squished);
        return killed;
    }

    // --- Visual: marked players flash red (white if they are red) ---

    private void LateUpdate()
    {
        if (Time.unscaledTime >= _nextBodySearch)
        {
            _bodies = _marks.Count > 0 ? FindObjectsOfType<PlayerCollision>() : new PlayerCollision[0];
            _nextBodySearch = Time.unscaledTime + 0.25f;
        }

        // Per player flash phase and strength; the closer the mark is to 0, the faster and stronger the flashing.
        var pulses = new Dictionary<int, float>();
        foreach (KeyValuePair<int, MarkState> pair in _marks)
        {
            float duration = Mathf.Max(0.01f, OmaeSettings.MarkDuration);
            float urgency = 1f - Mathf.Clamp01((float)pair.Value.Remaining / duration);
            float flashesPerSecond = Mathf.Lerp(OmaeSettings.FlashesPerSecond, OmaeSettings.FlashesPerSecondAtEnd, urgency);
            _flashPhases.TryGetValue(pair.Key, out float phase);
            phase = (phase + Time.deltaTime * flashesPerSecond) % 1f;
            _flashPhases[pair.Key] = phase;
            float strength = Mathf.Lerp(0.45f, 0.95f, urgency);
            pulses[pair.Key] = strength * (0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f));
        }

        var shown = new HashSet<SpriteRenderer>();
        foreach (PlayerCollision body in _bodies)
        {
            if (body == null || !body.gameObject.activeInHierarchy || !pulses.TryGetValue(body.GetPlayerId(), out float pulse))
                continue;
            SpriteRenderer target = BodySprite(body);
            if (target == null || !target.enabled)
                continue;

            SpriteRenderer overlay = GetOverlay(target);
            Color color = IsRedPlayer(body.GetPlayerId(), target) ? Color.white : Color.red;
            // Invisible players stay invisible.
            color.a = pulse * target.color.a;
            overlay.color = color;
            overlay.sprite = target.sprite;
            overlay.flipX = target.flipX;
            overlay.flipY = target.flipY;
            overlay.sortingLayerID = target.sortingLayerID;
            overlay.sortingOrder = target.sortingOrder + 1;
            overlay.enabled = true;
            shown.Add(target);
        }

        foreach (KeyValuePair<SpriteRenderer, SpriteRenderer> pair in _overlays.ToList())
        {
            if (pair.Key == null || pair.Value == null)
                _overlays.Remove(pair.Key);
            else if (!shown.Contains(pair.Key))
                pair.Value.enabled = false;
        }
    }

    /// <summary>
    /// The renderer that draws a body: a slime's is a child object tagged "PlayerSprite" (SlimeController.spriteRen),
    /// an ability form's is on the form itself (Ability.spriteRen).
    /// </summary>
    private static SpriteRenderer BodySprite(PlayerCollision body)
    {
        SlimeController slime = body.GetComponent<SlimeController>();
        if (slime != null && slime.spriteRen != null)
            return slime.spriteRen;
        Ability ability = body.GetComponent<Ability>();
        if (ability != null && ability.spriteRen != null)
            return ability.spriteRen;
        return body.GetComponent<SpriteRenderer>();
    }

    /// <summary>A child renderer drawing the target's sprite as a solid color silhouette.</summary>
    private SpriteRenderer GetOverlay(SpriteRenderer target)
    {
        if (_overlays.TryGetValue(target, out SpriteRenderer overlay) && overlay != null)
            return overlay;

        if (_flashMaterial == null)
        {
            // GUI/Text Shader draws the texture's alpha in the vertex color: a flat silhouette. Always in builds (IMGUI).
            Shader shader = Shader.Find("GUI/Text Shader") ?? Shader.Find("Sprites/Default");
            _flashMaterial = new Material(shader);
        }

        var go = new GameObject("OmaeMarkFlash");
        go.transform.SetParent(target.transform, worldPositionStays: false);
        overlay = go.AddComponent<SpriteRenderer>();
        overlay.sharedMaterial = _flashMaterial;
        _overlays[target] = overlay;
        return overlay;
    }

    /// <summary>
    /// Whether a red flash wouldn't show on this player. The slime color comes from its material: check its color
    /// properties, then its name. Logged once to check what the game's materials look like.
    /// </summary>
    private bool IsRedPlayer(int playerId, SpriteRenderer target)
    {
        if (_isRedPlayer.TryGetValue(playerId, out bool red))
            return red;

        Material material = target.sharedMaterial;
        red = false;
        if (material != null)
        {
            foreach (string property in new[] { "_Color", "_MainColor", "_BaseColor", "_PlayerColor", "_Color1", "_TintColor" })
            {
                if (!material.HasProperty(property))
                    continue;
                Color c = material.GetColor(property);
                if (c.r > 0.55f && c.g < 0.45f && c.b < 0.45f)
                    red = true;
            }
            if (material.name.ToLowerInvariant().Contains("red"))
                red = true;

            if (!_loggedMaterial)
            {
                _loggedMaterial = true;
                Shader shader = material.shader;
                var props = new List<string>();
                for (int i = 0; shader != null && i < shader.GetPropertyCount(); i++)
                    props.Add($"{shader.GetPropertyName(i)}:{shader.GetPropertyType(i)}");
                Plugin.Log.LogInfo($"Omae mark: player material '{material.name}', shader '{shader?.name}', properties [{string.Join(", ", props)}]");
            }
        }

        _isRedPlayer[playerId] = red;
        Plugin.Log.LogInfo($"Omae mark: player {playerId} material '{material?.name}' -> flashes {(red ? "white" : "red")}");
        return red;
    }

    private void OnDestroy()
    {
        foreach (SpriteRenderer overlay in _overlays.Values)
        {
            if (overlay != null)
                Destroy(overlay.gameObject);
        }
        if (_instance == this)
            _instance = null;
    }
}
