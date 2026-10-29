using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CaliSkills.Core;

/// <summary>
/// Makes the "all abilities" grid in character select scroll vertically when it doesn't fit on screen
/// (every added skill adds grid entries; the vanilla layout only grows downward).
///
/// The vanilla layout is kept as is. Each frame, entries are shifted by a scroll offset so the selected one
/// stays inside the visible window (from the grid's first row down to just above the screen bottom), and entries
/// outside that window are hidden. Keyboard/gamepad navigation follows the selection; the mouse wheel scrolls.
/// </summary>
[HarmonyPatch(typeof(AbilityGrid))]
internal static class AbilityGridScroll
{
    /// <summary>Space kept free at the bottom of the screen, as a fraction of its height.</summary>
    private const float BottomMarginFraction = 0.04f;

    /// <summary>How fast the view eases toward its target (higher = snappier).</summary>
    private const float ScrollSharpness = 15f;

    private sealed class State
    {
        public Vector3[] BaseLocal;
        public float Size;
        public float ContentTop;
        public float ContentBottom;
        public Camera Camera;
        public float Offset;
        public float Target;
        public int LastSelected = -1;
    }

    private static readonly ConditionalWeakTable<AbilityGrid, State> States = new();

    [HarmonyPatch(nameof(AbilityGrid.Awake))]
    [HarmonyPostfix]
    private static void CaptureLayout(AbilityGrid __instance)
    {
        AbilityGridEntry[] grid = __instance.grid;
        if (grid == null || grid.Length == 0)
            return;

        var state = new State
        {
            BaseLocal = new Vector3[grid.Length],
            Size = __instance.entrySize,
            ContentTop = float.MinValue,
            ContentBottom = float.MaxValue,
        };
        for (int i = 0; i < grid.Length; i++)
        {
            Vector3 p = grid[i].rectTrans.localPosition;
            state.BaseLocal[i] = p;
            state.ContentTop = Mathf.Max(state.ContentTop, p.y + state.Size / 2f);
            state.ContentBottom = Mathf.Min(state.ContentBottom, p.y - state.Size / 2f);
        }

        Canvas canvas = __instance.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas = canvas.rootCanvas;
            state.Camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        States.Remove(__instance);
        States.Add(__instance, state);
    }

    [HarmonyPatch(nameof(AbilityGrid.Init))]
    [HarmonyPostfix]
    private static void OnOpen(AbilityGrid __instance)
    {
        // Re-center on the initially selected ability every time the grid opens.
        if (States.TryGetValue(__instance, out State state))
            state.LastSelected = -1;
    }

    [HarmonyPatch(nameof(AbilityGrid.Update))]
    [HarmonyPostfix]
    private static void Scroll(AbilityGrid __instance)
    {
        if (!States.TryGetValue(__instance, out State state))
            return;

        AbilityGridEntry[] grid = __instance.grid;
        if (!TryGetVisibleWindow(__instance, state, out float windowTop, out float windowBottom))
            return;

        // Offsets that align the content's top / bottom with the window's top / bottom.
        float alignTop = windowTop - state.ContentTop;
        float alignBottom = windowBottom - state.ContentBottom;
        float min = Mathf.Min(alignTop, alignBottom);
        float max = Mathf.Max(alignTop, alignBottom);
        bool fits = alignBottom <= alignTop;

        int selected = Mathf.Clamp(__instance.SelectedIcon, 0, grid.Length - 1);
        bool justOpened = state.LastSelected < 0;
        if (selected != state.LastSelected)
        {
            state.LastSelected = selected;
            float rowTop = state.BaseLocal[selected].y + state.Size / 2f + state.Target;
            float rowBottom = rowTop - state.Size;
            if (rowTop > windowTop)
                state.Target -= rowTop - windowTop;
            else if (rowBottom < windowBottom)
                state.Target += windowBottom - rowBottom;
        }

        if (__instance.usesKeyboardAndMouse && Mouse.current != null)
        {
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (wheel != 0f)
                state.Target -= Mathf.Sign(wheel) * state.Size;
        }

        // When everything fits, rest at the vanilla position whenever possible.
        state.Target = fits ? Mathf.Clamp(0f, min, max) : Mathf.Clamp(state.Target, min, max);
        if (justOpened)
            state.Offset = state.Target;
        state.Offset = Mathf.Lerp(state.Offset, state.Target, 1f - Mathf.Exp(-ScrollSharpness * Time.unscaledDeltaTime));

        float tolerance = state.Size * 0.1f;
        for (int i = 0; i < grid.Length; i++)
        {
            Vector3 p = state.BaseLocal[i] + new Vector3(0f, state.Offset, 0f);
            grid[i].rectTrans.localPosition = p;

            bool visible = p.y + state.Size / 2f <= windowTop + tolerance && p.y - state.Size / 2f >= windowBottom - tolerance;
            if (grid[i].gameObject.activeSelf != visible)
                grid[i].gameObject.SetActive(visible);
        }

        // Vanilla placed the selection circle before we moved the entries.
        __instance.selectionCircle.anchoredPosition = grid[selected].rectTrans.anchoredPosition;
    }

    /// <summary>The window in the grid's local space: from its first row down to just above the screen bottom.</summary>
    private static bool TryGetVisibleWindow(AbilityGrid grid, State state, out float top, out float bottom)
    {
        top = state.ContentTop;
        bottom = state.ContentBottom;

        var screenBottom = new Vector2(Screen.width * 0.5f, Screen.height * BottomMarginFraction);
        var screenTop = new Vector2(Screen.width * 0.5f, Screen.height * (1f - BottomMarginFraction));
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(grid.recTrans, screenBottom, state.Camera, out Vector2 bottomLocal) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(grid.recTrans, screenTop, state.Camera, out Vector2 topLocal))
            return false;

        top = Mathf.Min(state.ContentTop, topLocal.y);
        bottom = Mathf.Max(state.ContentBottom, bottomLocal.y);
        // Always show at least one row.
        if (top - bottom < state.Size)
            bottom = top - state.Size;
        return true;
    }
}
