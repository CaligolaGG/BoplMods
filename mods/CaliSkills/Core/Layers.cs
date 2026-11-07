using UnityEngine;

namespace CaliSkills.Core;

/// <summary>Layer ids looked up once: collision callbacks ask for them every tick (NameToLayer is a string lookup).</summary>
internal static class Layers
{
    internal static readonly int Player = LayerMask.NameToLayer("Player");
    internal static readonly int Projectile = LayerMask.NameToLayer("Projectile");
    internal static readonly int Explosion = LayerMask.NameToLayer("Explosion");
    internal static readonly int Wall = LayerMask.NameToLayer("wall");
    internal static readonly int NonLethalTerrain = LayerMask.NameToLayer("NonLethalTerrain");
    internal static readonly int LethalTerrain = LayerMask.NameToLayer("LethalTerrain");
}
