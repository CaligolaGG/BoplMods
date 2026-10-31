using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CustomRules.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CustomRules.UI;

/// <summary>
/// Adds "Replays : on / off" to the game's settings menu (main menu → Settings), right above Save, as a copy of the
/// "Screen shake" row so it looks and works like the game's own options (◀ ▶, keyboard/gamepad navigation).
/// The copy is relabeled, loses its localization (English only) and drives <see cref="ReplaySaving.Enabled"/> instead
/// of the game's setting. The rows below it move down one row; the list then scrolls (<see cref="SettingsMenuScroll"/>).
/// </summary>
[HarmonyPatch(typeof(MainMenu), "Start")]
internal static class ReplaysSettingRow
{
    private const string RowName = "CustomRules.ReplaysSetting";
    private const string TemplateSetting = "AllowScreenShake";

    // Before Start: MainMenu reads its items and their positions there.
    private static void Prefix(MainMenu __instance)
    {
        List<GameObject> items = __instance.ConfiguredMenuItems;
        if (items == null || items.Any(g => g != null && g.name == RowName))
            return;
        int template = items.FindIndex(g => g != null && g.GetComponent<SettingSelector>()?.setting == TemplateSetting);
        if (template < 0)
            return; // not the settings menu
        int last = items.FindLastIndex(g => g != null && g.GetComponent<SettingSelector>() != null);
        if (last < 1)
            return;

        // The list is too long for the screen with one more row.
        if (__instance.GetComponent<SettingsMenuScroll>() == null)
            __instance.gameObject.AddComponent<SettingsMenuScroll>();

        try
        {
            GameObject row = Object.Instantiate(items[template], items[template].transform.parent);
            row.name = RowName;
            if (!Setup(row))
            {
                Object.Destroy(row);
                return;
            }

            // Below the last option (above Save), one row further than the gap between the last two options.
            var rowRect = (RectTransform)row.transform;
            var lastRect = (RectTransform)items[last].transform;
            float step = lastRect.anchoredPosition.y - ((RectTransform)items[last - 1].transform).anchoredPosition.y;
            rowRect.anchoredPosition = new Vector2(rowRect.anchoredPosition.x, lastRect.anchoredPosition.y + step);
            row.transform.SetSiblingIndex(items[last].transform.GetSiblingIndex() + 1);
            for (int i = last + 1; i < items.Count; i++)
                Move(items[i], step);
            items.Insert(last + 1, row);

            RebindHoverSelect(__instance);
            Plugin.Log.LogInfo("Added the Replays option to the settings menu.");
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogError($"Couldn't add the Replays option to the settings menu: {e}");
        }
    }

    private static void Move(GameObject item, float dy)
    {
        if (item != null && item.transform is RectTransform rect)
            rect.anchoredPosition += new Vector2(0f, dy);
    }

    /// <returns>False if the copied row isn't the expected two-option (on/off) selector.</returns>
    private static bool Setup(GameObject row)
    {
        SettingSelector selector = row.GetComponent<SettingSelector>();
        if (selector.Images == null || selector.Images.Length != 2)
        {
            Plugin.Log.LogWarning("Settings menu: the Screen shake row isn't an on/off row anymore (game update?).");
            return false;
        }

        // The label becomes "replays" (its LocalizedText would put "screen shake" back); the "on" / "off" texts keep
        // theirs, which also gives them the game's font and style.
        foreach (TextMeshProUGUI text in row.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string label = Relabel(text.text);
            if (label == text.text)
                continue;
            text.text = label;
            LocalizedText localized = text.GetComponent<LocalizedText>();
            if (localized != null)
                Object.DestroyImmediate(localized);
        }

        // Index 0 = on, 1 = off (like the game's bool settings).
        selector.settings = null;
        selector.setting = "";
        selector.SetSetting = new[] { Setter(true), Setter(false) };
        row.AddComponent<ReplaysRowMarker>();
        selector.optionIndex = ReplaySaving.Enabled ? 0 : 1;
        return true;
    }

    private static UnityEvent Setter(bool value)
    {
        var e = new UnityEvent();
        e.AddListener(() => ReplaySaving.Enabled = value);
        return e;
    }

    private static string Relabel(string text)
    {
        string label = Regex.Replace(text, @"screen\s*shake", "replays", RegexOptions.IgnoreCase);
        label = Regex.Replace(label, "screen", "replays", RegexOptions.IgnoreCase);
        label = Regex.Replace(label, "shake", "", RegexOptions.IgnoreCase);
        return text == text.ToUpperInvariant() ? label.ToUpperInvariant() : label;
    }

    /// <summary>
    /// Rows select themselves on hover with a fixed index (CallOnHover.onHover → MainMenu.Select(n), wired in the scene),
    /// and Save only reacts to a click while it's the selected row. After inserting a row those indices are off by one,
    /// so each such call gets the row's actual index. The calls are edited in place, keeping their order with the
    /// event's other calls (a replacement listener would run last).
    /// </summary>
    private static void RebindHoverSelect(MainMenu menu)
    {
        int index = 0;
        foreach (GameObject item in menu.ConfiguredMenuItems)
        {
            if (item == null || item.GetComponent<IMenuItem>() == null)
                continue;
            int itemIndex = index++;
            foreach (EventTrigger trigger in item.GetComponentsInChildren<EventTrigger>(true))
            {
                foreach (EventTrigger.Entry entry in trigger.triggers)
                    SetSelectIndex(entry.callback, menu, itemIndex);
            }
            // Any UnityEvent field: the rows use CallOnHover (onHover/onClick/onExitHover), the arrows Button.onClick.
            foreach (MonoBehaviour behaviour in item.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;
                foreach (FieldInfo field in AccessTools.GetDeclaredFields(behaviour.GetType()))
                {
                    if (typeof(UnityEventBase).IsAssignableFrom(field.FieldType) && field.GetValue(behaviour) is UnityEventBase e)
                        SetSelectIndex(e, menu, itemIndex);
                }
            }
        }
    }

    /// <summary>Sets the argument of the event's scene-wired calls to <paramref name="menu"/>.Select.</summary>
    private static void SetSelectIndex(UnityEventBase e, MainMenu menu, int index)
    {
        // UnityEvent keeps them in m_PersistentCalls.m_Calls (PersistentCall: m_Target, m_MethodName, m_Arguments).
        Traverse traverse = Traverse.Create(e);
        if (traverse.Field("m_PersistentCalls").Field("m_Calls").GetValue() is not IList calls)
            return;
        bool changed = false;
        foreach (object call in calls)
        {
            Traverse c = Traverse.Create(call);
            if (c.Field("m_Target").GetValue<Object>() != menu || c.Field("m_MethodName").GetValue<string>() != nameof(MainMenu.Select))
                continue;
            Traverse argument = c.Field("m_Arguments").Field("m_IntArgument");
            if (argument.GetValue<int>() != index)
            {
                argument.SetValue(index);
                changed = true;
            }
        }
        // Rebuild the cached invocation list from the edited calls.
        if (changed)
            traverse.Method("DirtyPersistentCalls").GetValue();
    }

    private sealed class ReplaysRowMarker : MonoBehaviour
    {
    }

    /// <summary>The menu re-reads every option when opened: show the replay setting, not the (null) game setting.</summary>
    [HarmonyPatch(typeof(SettingSelector), nameof(SettingSelector.Init))]
    private static class ShowCurrentValue
    {
        private static void Postfix(SettingSelector __instance)
        {
            if (__instance.GetComponent<ReplaysRowMarker>() != null)
                __instance.optionIndex = ReplaySaving.Enabled ? 0 : 1;
        }
    }
}
