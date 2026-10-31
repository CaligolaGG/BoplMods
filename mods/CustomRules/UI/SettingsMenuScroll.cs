using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CustomRules.UI;

/// <summary>
/// Makes the game's settings menu scroll when its rows don't fit on screen (it's one row longer with the Replays
/// option): the selected row is always brought into view (keyboard/gamepad), and the mouse wheel scrolls.
/// Scrolling moves the menu's own RectTransform (the rows' parent), not the rows: MainMenu.Update re-places every row
/// each frame, and moving them afterwards would leave clicks (EventSystem, also in Update) hitting the unscrolled
/// positions. Added to the settings menu by <see cref="ReplaysSettingRow"/>.
/// </summary>
internal sealed class SettingsMenuScroll : MonoBehaviour
{
    /// <summary>Space kept free at the top and bottom of the screen, as a fraction of its height.</summary>
    private const float Margin01 = 0.03f;

    /// <summary>Scroll per mouse wheel notch, as a fraction of the screen height.</summary>
    private const float WheelStep01 = 0.08f;

    /// <summary>
    /// Seconds the selected row is kept in view after the menu opens or the selection changes. Not just once: on the
    /// frame the menu opens the rows are still where the closing animation left them.
    /// </summary>
    private const float FollowSeconds = 0.5f;

    private readonly Vector3[] _corners = new Vector3[4];
    private MainMenu _menu;
    private RectTransform _container;
    private Vector2 _basePosition;
    private Camera _camera;
    private int _lastSelected = -1;
    private float _followUntil;
    private float _target;
    private float _offset;

    /// <summary>The offset (pixels) the container is moved by right now.</summary>
    private float _applied;

    private void Awake()
    {
        _menu = GetComponent<MainMenu>();
        _container = (RectTransform)transform;
        _basePosition = _container.anchoredPosition;
        Canvas canvas = GetComponentInParent<Canvas>();
        _camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    private void LateUpdate()
    {
        if (_menu == null || _menu.MenuItemTransforms == null || _menu.MenuItemTransforms.Count == 0)
            return;

        if (!_menu.IsEnabled)
        {
            // Closed: back to the top for next time, and let the rows slide out as usual.
            _lastSelected = -1;
            _target = _offset = 0f;
            Apply(0f);
            return;
        }

        // Extents of the rows on screen (pixels) without the scroll. A row's own rect is smaller than what it draws:
        // measure its visible pictures and texts.
        float top = float.MinValue, bottom = float.MaxValue;
        float selectedTop = 0f, selectedBottom = 0f;
        for (int i = 0; i < _menu.MenuItemTransforms.Count; i++)
        {
            RectTransform row = _menu.MenuItemTransforms[i];
            if (row == null || !Measure(row, out float rowTop, out float rowBottom))
                continue;
            rowTop -= _applied;
            rowBottom -= _applied;
            top = Mathf.Max(top, rowTop);
            bottom = Mathf.Min(bottom, rowBottom);
            if (i == _menu.yPos)
            {
                selectedTop = rowTop;
                selectedBottom = rowBottom;
            }
        }
        if (top <= bottom)
            return;

        float margin = Screen.height * Margin01;
        // Offset (pixels, + moves the rows up) range that keeps the list's ends inside the screen.
        float lowest = margin - bottom; // bottom row at the bottom edge
        float highest = Screen.height - margin - top; // top row at the top edge
        if (lowest > highest)
        {
            // Too long: scroll with the wheel, and to the selected row when the selection changes.
            if (_menu.yPos != _lastSelected)
                _followUntil = Time.unscaledTime + FollowSeconds;
            if (Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (wheel != 0f)
                {
                    _target -= Mathf.Sign(wheel) * Screen.height * WheelStep01;
                    _followUntil = 0f;
                }
            }
            if (Time.unscaledTime < _followUntil)
            {
                _target = Mathf.Min(_target, Screen.height - margin - selectedTop);
                _target = Mathf.Max(_target, margin - selectedBottom);
            }
            _target = Mathf.Clamp(_target, highest, lowest);
        }
        else
        {
            _target = 0f;
        }
        _lastSelected = _menu.yPos;

        _offset = Mathf.Lerp(_offset, _target, 1f - Mathf.Exp(-15f * Time.unscaledDeltaTime));
        Apply(_offset);
    }

    /// <summary>Moves the container up by <paramref name="pixels"/> from where the scene placed it.</summary>
    private void Apply(float pixels)
    {
        float pixelsPerUnit = PixelsPerUnit(_container);
        if (pixelsPerUnit <= 0f)
            return;
        _container.anchoredPosition = _basePosition + new Vector2(0f, pixels / pixelsPerUnit);
        _applied = pixels;
    }

    /// <summary>Screen top and bottom (pixels) of the row's active Images and texts.</summary>
    private bool Measure(RectTransform row, out float top, out float bottom)
    {
        top = float.MinValue;
        bottom = float.MaxValue;
        foreach (Graphic graphic in row.GetComponentsInChildren<Graphic>())
        {
            if (!graphic.enabled)
                continue;
            graphic.rectTransform.GetWorldCorners(_corners);
            top = Mathf.Max(top, RectTransformUtility.WorldToScreenPoint(_camera, _corners[1]).y);
            bottom = Mathf.Min(bottom, RectTransformUtility.WorldToScreenPoint(_camera, _corners[0]).y);
        }
        return top > bottom;
    }

    /// <summary>Screen pixels per unit of <paramref name="rect"/>'s anchoredPosition (i.e. in its parent).</summary>
    private float PixelsPerUnit(RectTransform rect)
    {
        if (rect == null || rect.parent == null)
            return 0f;
        float a = RectTransformUtility.WorldToScreenPoint(_camera, rect.parent.TransformPoint(Vector3.zero)).y;
        float b = RectTransformUtility.WorldToScreenPoint(_camera, rect.parent.TransformPoint(Vector3.up * 100f)).y;
        return (b - a) / 100f;
    }
}
