using UnityEngine;

namespace CaliSkills.Abilities.Rps;

/// <summary>
/// Everything a <see cref="RpsDuel"/> shows (visual only, read from the duel's sim state; floats and Unity time are fine):
/// - the Time stop sphere (<see cref="RpsSphere"/>);
/// - the VS screen: a dark veil, a band split diagonally in the two players' team colors sliding in from the sides, the
///   two slimes facing each other on it and a big "VS" (like the victory screen's colored band);
/// - during the choice: the duelists' ability icons become rock / paper / scissors, a countdown, and "Ready!" over a
///   player who picked (not what: in local games everyone sees the same screen);
/// - the reveal: each pick drawn big over its player, with WIN / LOSE / DRAW.
/// </summary>
internal sealed class RpsScreen : MonoBehaviour
{
    private const int VeilOrder = 32000;
    private const float SlideTime = 0.2f;

    private RpsDuel _duel;
    private bool _finished;
    private RpsSphere _sphere;

    private SpriteRenderer _veil, _leftPanel, _rightPanel, _leftSlime, _rightSlime;
    private readonly SpriteRenderer[] _picks = new SpriteRenderer[2];

    private GUIStyle _big, _medium;

    internal static RpsScreen Create(RpsDuel duel)
    {
        var go = new GameObject("CaliSkills.RpsScreen");
        RpsScreen screen = go.AddComponent<RpsScreen>();
        screen._duel = duel;
        screen.Build();
        return screen;
    }

    private void Build()
    {
        int layer = TopSortingLayer();
        _veil = Renderer("Veil", RpsSkill.WhiteSprite, layer, VeilOrder);
        _veil.color = new Color(0f, 0f, 0f, 0.55f);
        _leftPanel = Renderer("LeftPanel", RpsSkill.LeftPanelSprite, layer, VeilOrder + 1);
        _leftPanel.color = TeamColor(_duel.Challenger);
        _rightPanel = Renderer("RightPanel", RpsSkill.RightPanelSprite, layer, VeilOrder + 1);
        _rightPanel.color = TeamColor(_duel.Challenged);
        _leftSlime = Renderer("LeftSlime", null, layer, VeilOrder + 2);
        _rightSlime = Renderer("RightSlime", null, layer, VeilOrder + 2);
        for (int d = 0; d < 2; d++)
        {
            _picks[d] = Renderer("Pick" + d, null, layer, VeilOrder + 3);
            _picks[d].enabled = false;
        }

        Vector2 a = (Vector2)_duel.Challenger.body.position, b = (Vector2)_duel.Challenged.body.position;
        _sphere = RpsSphere.Create((a + b) / 2f);
    }

    private SpriteRenderer Renderer(string name, Sprite sprite, int layer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, worldPositionStays: false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
        return renderer;
    }

    private static int TopSortingLayer()
    {
        SortingLayer[] layers = SortingLayer.layers;
        return layers.Length > 0 ? layers[layers.Length - 1].id : 0;
    }

    private static Color TeamColor(SlimeController slime)
    {
        Player player = PlayerHandler.Get().GetPlayer(slime.GetPlayerId());
        TeamColors colors = slime.playerCollision?.reviveEffectPrefab?.teamColors;
        if (player != null && colors != null && player.Team >= 0 && player.Team < colors.Length)
        {
            Color c = colors[player.Team].fill;
            c.a = 1f;
            return c;
        }
        return Color.gray;
    }

    private static Camera Cam => Camera.main != null ? Camera.main : FindObjectOfType<Camera>();

    /// <summary>The duel is over: icons back, screen gone, the sphere fades out like Time stop's.</summary>
    internal void Finish()
    {
        if (_finished)
            return;
        _finished = true;
        if (_duel != null)
        {
            RestoreIcons(_duel.Challenger);
            RestoreIcons(_duel.Challenged);
        }
        if (_sphere != null)
            _sphere.Exit();
        if (this != null)
            Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (_finished || _duel == null)
            return;
        Camera cam = Cam;
        if (cam == null)
            return;

        float t = (float)_duel.Elapsed, vsEnd = (float)_duel.VsEnd;
        float height = cam.orthographicSize * 2f, width = height * cam.aspect;
        Vector3 center = cam.transform.position;
        center.z = 0f;
        transform.position = center;

        // VS screen: slides in, stays, slides out.
        bool versus = t < vsEnd;
        _veil.enabled = _leftPanel.enabled = _rightPanel.enabled = _leftSlime.enabled = _rightSlime.enabled = versus;
        if (versus)
        {
            float slide = (1f - Mathf.SmoothStep(0f, 1f, t / SlideTime)) + Mathf.SmoothStep(0f, 1f, (t - (vsEnd - SlideTime)) / SlideTime);
            float offset = slide * width;
            Place(_veil.transform, Vector2.zero, new Vector2(width * 1.1f, height * 1.1f));
            Place(_leftPanel.transform, new Vector2(-offset, 0f), new Vector2(width * 1.02f, height * 0.5f));
            Place(_rightPanel.transform, new Vector2(offset, 0f), new Vector2(width * 1.02f, height * 0.5f));
            Color veil = _veil.color;
            veil.a = 0.55f * (1f - slide);
            _veil.color = veil;
            PlaceSlime(_leftSlime, _duel.Challenger, new Vector2(-width * 0.27f - offset, -height * 0.02f), height * 0.32f, faceRight: true);
            PlaceSlime(_rightSlime, _duel.Challenged, new Vector2(width * 0.27f + offset, -height * 0.02f), height * 0.32f, faceRight: false);
        }

        // Choice and reveal: rock / paper / scissors on the duelists' ability icons.
        if (!versus)
        {
            ShowChoiceIcons(0);
            ShowChoiceIcons(1);
        }

        bool reveal = _duel.CurrentPhase == RpsDuel.Phase.Reveal;
        for (int d = 0; d < 2; d++)
        {
            SpriteRenderer pick = _picks[d];
            pick.enabled = reveal && _duel.Choices[d] >= 0;
            if (!pick.enabled)
                continue;
            SlimeController slime = d == 0 ? _duel.Challenger : _duel.Challenged;
            pick.sprite = RpsSkill.ChoiceSprites[_duel.Choices[d]];
            float pop = Mathf.Clamp01((t - (float)_duel.ChoiceEnd) / 0.15f);
            float size = 3f * (float)slime.body.fixtrans.Scale * (0.6f + 0.4f * pop + 0.15f * Mathf.Sin(pop * Mathf.PI));
            pick.transform.position = (Vector2)slime.body.position + Vector2.up * (2.6f * (float)slime.body.fixtrans.Scale);
            float spriteSize = Mathf.Max(0.001f, pick.sprite.bounds.size.x);
            pick.transform.localScale = new Vector3(size / spriteSize, size / spriteSize, 1f);
        }
    }

    private static void Place(Transform transform, Vector2 localPosition, Vector2 size)
    {
        transform.localPosition = localPosition;
        transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    /// <summary>A copy of the slime as it looks now (sprite and player material), facing the middle.</summary>
    private static void PlaceSlime(SpriteRenderer copy, SlimeController slime, Vector2 localPosition, float height, bool faceRight)
    {
        SpriteRenderer source = slime.spriteRen;
        if (source == null || source.sprite == null)
        {
            copy.enabled = false;
            return;
        }
        copy.sprite = source.sprite;
        copy.sharedMaterial = source.sharedMaterial;
        // The game faces slimes by flipping a parent transform: which way the bare sprite looks follows from that.
        bool spriteLooksRight = (source.transform.lossyScale.x > 0f) == slime.isFacingRight();
        copy.flipX = spriteLooksRight != faceRight;
        float scale = height / Mathf.Max(0.001f, source.sprite.bounds.size.y);
        copy.transform.localPosition = localPosition;
        copy.transform.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>
    /// Rock / paper / scissors on the duelist's icons. Their pick is shown (bigger, the others dimmed) when the
    /// opponent can't see this screen: online, or against a bot. Two players on one screen only see "Ready!".
    /// </summary>
    private void ShowChoiceIcons(int duelist)
    {
        SlimeController slime = duelist == 0 ? _duel.Challenger : _duel.Challenged;
        if (slime == null || slime.IsDestroyed || slime.AbilityReadyIndicators == null)
            return;
        int pick = ShowsPick(duelist) || _duel.CurrentPhase == RpsDuel.Phase.Reveal ? _duel.Choices[duelist] : -1;
        for (int i = 0; i < slime.AbilityReadyIndicators.Length && i < RpsSkill.ChoiceSprites.Length; i++)
        {
            AbilityReadyIndicator indicator = slime.AbilityReadyIndicators[i];
            if (indicator == null || indicator.GetSpriteRen() == null)
                continue;
            indicator.UpdateOverriden(RpsSkill.ChoiceSprites[i], shouldOverride: true);
            indicator.SetCooldownProgress(1f);
            Color color = indicator.GetSpriteRen().color;
            color.a = pick < 0 || pick == i ? 1f : 0.35f;
            indicator.GetSpriteRen().color = color;
            // The indicator sets its own scale every frame (Update), before this.
            if (pick == i)
                indicator.transform.localScale *= 1.35f;
        }
    }

    internal bool ShowsPick(int duelist) => SeesThisScreen(_duel.PlayerOf(duelist)) && !SeesThisScreen(_duel.PlayerOf(1 - duelist));

    /// <summary>A human playing on this machine (keyboard and mouse or a gamepad; bots have neither).</summary>
    private static bool SeesThisScreen(Player player) =>
        player != null && player.IsLocalPlayer && (player.UsesKeyboardAndMouse || player.PlayerHasValidGamepad());

    private static void RestoreIcons(SlimeController slime)
    {
        if (slime == null || slime.IsDestroyed || slime.AbilityReadyIndicators == null)
            return;
        foreach (AbilityReadyIndicator indicator in slime.AbilityReadyIndicators)
        {
            if (indicator != null && indicator.GetSpriteRen() != null)
                indicator.UpdateOverriden(null, shouldOverride: false);
        }
    }

    private void OnGUI()
    {
        if (_finished || _duel == null)
            return;
        Camera cam = Cam;
        if (cam == null)
            return;
        if (_big == null)
        {
            _big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _medium = new GUIStyle(_big);
        }
        _big.fontSize = Mathf.RoundToInt(Screen.height * 0.16f);
        _medium.fontSize = Mathf.RoundToInt(Screen.height * 0.05f);

        float t = (float)_duel.Elapsed, vsEnd = (float)_duel.VsEnd;
        if (t < vsEnd)
        {
            // "VS" pops in the middle once the halves are in.
            float pop = Mathf.Clamp01((t - SlideTime * 0.6f) / 0.15f);
            float leave = Mathf.Clamp01((t - (vsEnd - SlideTime)) / SlideTime);
            if (pop > 0f && leave < 1f)
            {
                int size = _big.fontSize;
                _big.fontSize = Mathf.Max(1, Mathf.RoundToInt(size * (0.5f + 0.5f * pop + 0.25f * Mathf.Sin(pop * Mathf.PI))));
                Outlined(new Rect(0, 0, Screen.width, Screen.height), "VS", _big, new Color(1f, 0.85f, 0.15f, 1f - leave));
                _big.fontSize = size;
            }
            return;
        }

        if (_duel.CurrentPhase == RpsDuel.Phase.Choice)
        {
            // The chant: ROCK... PAPER... SCISSORS... then SHOOT! at the reveal.
            float choice01 = Mathf.Clamp01((t - vsEnd) / Mathf.Max(0.01f, (float)(_duel.ChoiceEnd - _duel.VsEnd)));
            string word = RpsSkill.ChoiceNames[Mathf.Min(2, (int)(choice01 * 3f))].ToUpperInvariant() + "...";
            Outlined(new Rect(0, Screen.height * 0.06f, Screen.width, Screen.height * 0.16f), word, _big, new Color(1f, 0.85f, 0.15f));
            Outlined(new Rect(0, Screen.height * 0.22f, Screen.width, Screen.height * 0.06f),
                "Pick with your ability buttons", _medium, new Color(1f, 1f, 1f, 0.8f));
            for (int d = 0; d < 2; d++)
            {
                int pick = _duel.Choices[d];
                string text = pick < 0 ? "..." : ShowsPick(d) ? RpsSkill.ChoiceNames[pick] + "!" : "Ready!";
                OverHead(cam, d, text, pick < 0 ? Color.white : new Color(0.4f, 1f, 0.4f));
            }
        }
        else
        {
            if (t - (float)_duel.ChoiceEnd < 0.6f)
                Outlined(new Rect(0, Screen.height * 0.06f, Screen.width, Screen.height * 0.16f), "SHOOT!", _big, new Color(1f, 0.85f, 0.15f));
            for (int d = 0; d < 2; d++)
            {
                string text = _duel.Winner < 0 ? "DRAW" : _duel.Winner == d ? "WIN" : "LOSE";
                Color color = _duel.Winner < 0 ? Color.white : _duel.Winner == d ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.35f, 0.35f);
                OverHead(cam, d, text, color, extraUp: 3.4f);
            }
        }
    }

    private void OverHead(Camera cam, int duelist, string text, Color color, float extraUp = 1.6f)
    {
        SlimeController slime = duelist == 0 ? _duel.Challenger : _duel.Challenged;
        if (slime == null || slime.IsDestroyed)
            return;
        Vector3 world = (Vector2)slime.body.position + Vector2.up * (extraUp * (float)slime.body.fixtrans.Scale);
        Vector3 screen = cam.WorldToScreenPoint(world);
        float w = Screen.height * 0.4f, h = Screen.height * 0.08f;
        Outlined(new Rect(screen.x - w / 2f, Screen.height - screen.y - h, w, h), text, _medium, color);
    }

    private static void Outlined(Rect rect, string text, GUIStyle style, Color color)
    {
        Color previous = style.normal.textColor;
        float o = Mathf.Max(2f, style.fontSize * 0.05f);
        style.normal.textColor = new Color(0.1f, 0.1f, 0.12f, color.a);
        foreach (Vector2 offset in new[] { new Vector2(-o, 0), new Vector2(o, 0), new Vector2(0, -o), new Vector2(0, o) })
            GUI.Label(new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height), text, style);
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
        style.normal.textColor = previous;
    }
}

/// <summary>
/// The Time stop ability's inversion sphere, without its logic: grows like Time stop's (its scale curve), and on
/// <see cref="Exit"/> fades out with its exit curve. Visual only.
/// </summary>
internal sealed class RpsSphere : MonoBehaviour
{
    private TimeStop _template;
    private SpriteRenderer _renderer;
    private float _time;
    private float _exitTime = -1f;

    internal static RpsSphere Create(Vector2 position)
    {
        TimeStop template = RpsSkill.TimeStopTemplate;
        SpriteRenderer source = template != null ? template.GetComponent<SpriteRenderer>() : null;
        if (source == null || template.InversionSphereScale == null)
            return null;

        var go = new GameObject("CaliSkills.RpsSphere");
        go.transform.position = new Vector3(position.x, position.y, template.transform.position.z);
        go.transform.localScale = new Vector3(0f, 0f, template.transform.localScale.z);
        RpsSphere sphere = go.AddComponent<RpsSphere>();
        sphere._template = template;
        sphere._renderer = go.AddComponent<SpriteRenderer>();
        sphere._renderer.sprite = source.sprite;
        sphere._renderer.sharedMaterial = source.sharedMaterial;
        sphere._renderer.color = source.color;
        sphere._renderer.sortingLayerID = source.sortingLayerID;
        sphere._renderer.sortingOrder = source.sortingOrder;
        return sphere;
    }

    internal void Exit()
    {
        if (this != null && _exitTime < 0f)
            _exitTime = 0f;
    }

    private void Update()
    {
        _time += Time.deltaTime;
        float scale = Mathf.Max(_template.InversionSphereScale.Evaluate(_time), 0f);
        transform.localScale = new Vector3(scale, scale, transform.localScale.z);
        if (_exitTime < 0f)
            return;

        _exitTime += Time.deltaTime;
        AnimationCurve exit = _template.ExitAlpha;
        Color color = _renderer.color;
        color.a = exit != null && exit.length > 0 ? exit.Evaluate(_exitTime) : 0f;
        _renderer.color = color;
        if (exit == null || exit.length == 0 || _exitTime > exit.keys[exit.length - 1].time)
            Destroy(gameObject);
    }
}
