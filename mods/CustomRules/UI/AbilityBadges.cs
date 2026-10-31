using CustomRules.Core;
using CustomRules.Rules;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CustomRules.UI;

/// <summary>
/// Marks on ability icons, in character select (grid and slots) and the rules menu's abilities window:
/// a yellow ring around abilities whose stats were changed (a yellow button border in the abilities window) (<see cref="AbilityStats"/>), a red "no" sign over disabled
/// ones. Also middle-click on an ability in the character select grid to disable / re-enable it.
/// Lobby marks show the rules as set in the lobby (the host's for online guests).
/// </summary>
internal static class AbilityBadges
{
    private const string RingName = "CustomRules.ModifiedRing";
    private const string NoName = "CustomRules.DisabledSign";
    internal static readonly Color Yellow = new(1f, 0.85f, 0.1f);
    private static readonly Color Red = new(0.9f, 0.08f, 0.08f);

    private static Texture2D _ringTex, _noTex;
    private static Sprite _ringSprite, _noSprite;

    /// <summary>Rebuilt if a scene change unloaded it.</summary>
    private static Texture2D RingTexture => _ringTex != null ? _ringTex : _ringTex = Make(ring: true);

    /// <summary>No sign texture, also drawn by the abilities window (IMGUI).</summary>
    internal static Texture2D NoTexture => _noTex != null ? _noTex : _noTex = Make(ring: false);

    private static Sprite RingSprite => _ringSprite != null && _ringSprite.texture != null ? _ringSprite : _ringSprite = ToSprite(RingTexture);

    private static Sprite NoSprite => _noSprite != null && _noSprite.texture != null ? _noSprite : _noSprite = ToSprite(NoTexture);

    /// <summary>A circle outline (ring), or a circle with a diagonal bar (no sign).</summary>
    private static Texture2D Make(bool ring)
    {
        const int size = 128;
        Color color = ring ? Yellow : Red;
        float outer = size / 2f - 1f;
        float thickness = ring ? size * 0.06f : size * 0.1f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontUnloadUnusedAsset,
        };
        float c = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                // Coverage of the circle outline, anti-aliased over ~1 pixel.
                float a = Mathf.Clamp01(outer - r + 0.5f) * Mathf.Clamp01(r - (outer - thickness) + 0.5f);
                if (!ring)
                {
                    // Bar from top-left to bottom-right, inside the circle.
                    float bar = Mathf.Abs(dx + dy) / Mathf.Sqrt(2f);
                    a = Mathf.Max(a, Mathf.Clamp01(thickness / 2f - bar + 0.5f) * Mathf.Clamp01(outer - thickness / 2f - r + 0.5f));
                }
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, a));
            }
        }
        tex.Apply();
        return tex;
    }

    private static Sprite ToSprite(Texture2D tex)
    {
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    /// <summary>Shows or hides the marks on a character select icon for the ability at <paramref name="index"/>.</summary>
    internal static void Show(Image icon, NamedSpriteList list, int index)
    {
        if (icon == null)
            return;
        string name = list != null && index > 0 && index < list.sprites.Count ? list.sprites[index].name : null;
        bool visible = icon.enabled && name != null;
        SetMark(icon, RingName, RingSprite, 0.02f, visible && AbilityStats.IsModifiedInLobby(name));
        SetMark(icon, NoName, NoSprite, -0.05f, visible && AbilityRules.LobbyDisabled.Contains(name));
    }

    /// <param name="grow">How much bigger than the icon the mark is, as a fraction of its size per side.</param>
    private static void SetMark(Image icon, string name, Sprite sprite, float grow, bool on)
    {
        Transform existing = icon.transform.Find(name);
        if (existing == null)
        {
            if (!on)
                return;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(icon.transform, worldPositionStays: false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(-grow, -grow);
            rect.anchorMax = new Vector2(1f + grow, 1f + grow);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false; // clicks go to the ability under it
            image.preserveAspect = true;
            existing = go.transform;
        }

        Image mark = existing.GetComponent<Image>();
        if (mark.sprite != sprite)
            mark.sprite = sprite;
        if (existing.gameObject.activeSelf != on)
            existing.gameObject.SetActive(on);
    }

    /// <summary>The marks on the grid, and middle-click on an entry to disable / re-enable it.</summary>
    [HarmonyPatch(typeof(AbilityGrid), nameof(AbilityGrid.Update))]
    private static class GridMarksAndMiddleClick
    {
        private static void Postfix(AbilityGrid __instance)
        {
            if (__instance.grid == null)
                return;
            for (int k = 0; k < __instance.grid.Length; k++)
            {
                if (__instance.grid[k] != null)
                    Show(__instance.grid[k].iconRenderer, __instance.abilityIcons, k + 1);
            }

            if (RulesMenu.BlocksMenuInput || !__instance.usesKeyboardAndMouse || Mouse.current == null ||
                !Mouse.current.middleButton.wasPressedThisFrame)
                return;

            int index = EntryUnderMouse(__instance);
            if (index < 0)
                return;
            string name = __instance.abilityIcons.sprites[index].name;
            AudioManager.Get()?.Play(AbilityRules.ToggleDisabledLocal(name) ? "navigateMenu2" : "decline");
        }

        /// <summary>The ability list index of the entry under the mouse, or -1.</summary>
        private static int EntryUnderMouse(AbilityGrid grid)
        {
            Vector2 mouse = Mouse.current.position.ReadValue();
            for (int k = 0; k < grid.grid.Length; k++)
            {
                AbilityGridEntry entry = grid.grid[k];
                if (entry == null || entry.rectTrans == null || k + 1 >= grid.abilityIcons.sprites.Count)
                    continue;
                Canvas canvas = entry.GetComponentInParent<Canvas>();
                Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (RectTransformUtility.RectangleContainsScreenPoint(entry.rectTrans, mouse, camera))
                    return k + 1;
            }
            return -1;
        }
    }

    [HarmonyPatch(typeof(SelectAbility), nameof(SelectAbility.Update))]
    private static class SlotMarks
    {
        private static void Postfix(SelectAbility __instance) =>
            Show(__instance.abilityIconRenderer, __instance.abilityIcons, __instance.Skip ? -1 : __instance.SelectedIndex);
    }
}
