using System;
using UnityEngine;

namespace CaliSkills.Core;

/// <summary>
/// One ability of the pack. The registry asks for its icon and prefab once, the first time the
/// game's ability list is available, and appends them as a new entry of that list.
/// </summary>
internal abstract class Skill
{
    /// <summary>Name shown in the ability grid. Also the key the game uses for the entry.</summary>
    public abstract string Name { get; }

    /// <summary>Whether the game should treat it as a killing ability (used by "Random" and achievements).</summary>
    public abstract bool IsOffensive { get; }

    /// <param name="abilityList">The game's full ability list, to reuse vanilla prefabs and sprites from.</param>
    public abstract Sprite CreateIcon(NamedSpriteList abilityList);

    /// <summary>
    /// The prefab the game instantiates for each player that picked the skill. Create it under
    /// <see cref="SkillRegistry.PrefabRoot"/> so it stays inactive and survives scene loads.
    /// </summary>
    public abstract GameObject CreatePrefab(NamedSpriteList abilityList);

    /// <summary>
    /// Static class whose public static float/int/bool fields are the skill's stats: with CustomRules installed, the
    /// host edits them in its rules menu (Abilities → right-click the skill) and every player uses the host's values.
    /// Null = not editable.
    /// </summary>
    public virtual Type SettingsType => null;

    /// <summary>
    /// Called at match start after the settings were set to the host's values. Re-apply values that were baked into
    /// prefabs at creation (cooldown, sizes, physics...); values read at runtime need nothing.
    /// </summary>
    public virtual void OnSettingsChanged()
    {
    }
}
