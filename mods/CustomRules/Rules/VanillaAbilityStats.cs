using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BoplFixedMath;
using CustomRules.Core;
using HarmonyLib;
using UnityEngine;

namespace CustomRules.Rules;

/// <summary>
/// Stats of the game's own abilities, as multipliers the host sets in the abilities window (right-click a vanilla
/// ability): cooldown, duration, charge time, charge power, knockback, speed and size. Each ability offers only the
/// stats it has; each stat's hint says what it changes for that ability.
///
/// How they're applied (in the simulation, the same on every machine):
/// - cooldown: postfix on the abilities' GetCooldown;
/// - fields of the ability object (e.g. Bow's ArrowSpeed): when the game creates it for a player
///   (FixTransform.InstantiateFixed of an ability list prefab), which is also when it gets a <see cref="Tag"/>;
/// - fields of what the ability spawns (grenades, shockwaves, spikes...): while an ability's own code runs (UpdateSim /
///   EnterAbility / ExitAbility / OnEnterAbility of its components, patched at the first character select), every
///   object registering with the Updater is that ability's spawn; they're scaled when that code returns, after the
///   game set their size and before their (deferred) Init;
/// - Invisibility's duration (hardcoded 5 s) and Beam's width: own patches.
/// Never the prefabs: their values stay the defaults, so 1 always means "as in the game".
/// </summary>
internal static class VanillaAbilityStats
{
    private const string Cooldown = "Cooldown";
    private const string Duration = "Duration";
    private const string ChargeTime = "ChargeTime";
    private const string ChargePower = "ChargePower";
    private const string Knockback = "Knockback";
    private const string Speed = "Speed";
    private const string Size = "Size";

    private static readonly Dictionary<string, string> Labels = new()
    {
        [Cooldown] = "Cooldown ×",
        [Duration] = "Duration ×",
        [ChargeTime] = "Charge time ×",
        [ChargePower] = "Charge power ×",
        [Knockback] = "Knockback ×",
        [Speed] = "Speed ×",
        [Size] = "Size ×",
    };

    /// <summary>Lowest multiplier used to divide by (charge time 0 = instant full charge, not a division by zero).</summary>
    private static readonly Fix MinDivisor = (Fix)0.05f;

    private sealed class StatDef
    {
        public string Key;
        public string Description;
        public Action<GameObject, Fix> OnAbility;
        public Action<GameObject, Fix> OnSpawn;
        public Rule Rule;
    }

    private sealed class AbilityDef
    {
        public string Name;
        public List<StatDef> Stats;

        public bool HasSpawnStats;

        // Derived from the active values (Refresh).
        public Fix CooldownMultiplier = Fix.One;

        public Rule RuleOf(string key)
        {
            foreach (StatDef stat in Stats)
            {
                if (stat.Key == key)
                    return stat.Rule;
            }
            return null;
        }
    }

    /// <summary>On every vanilla ability object created in a match: which ability it is.</summary>
    internal sealed class Tag : MonoBehaviour
    {
        public string Ability;
    }

    private static readonly List<AbilityDef> Defs = new()
    {
        Def("Dash",
            Stat(Duration, "How long the dash lasts.", Fields("Dash", "FixedUpdatesInDash")),
            Stat(Size, "How far the dash goes.", Fields("Dash", "dashLength"))),
        Def("Grenade",
            Stat(Duration, "Time before the grenade explodes.", onSpawn: Fields("Grenade", "detonationTime")),
            Stat(ChargeTime, "Time to reach a full throw.", DivFields("ThrowItem2", "ThrowForceGainSpeed")),
            Stat(ChargePower, "Strength of a fully charged throw.", Fields("ThrowItem2", "maxThrowForce")),
            Stat(Speed, "Throw speed.", Fields("ThrowItem2", "maxThrowForce", "minThrowForce")),
            Stat(Size, "Grenade size, and so its explosion.", onSpawn: ColliderScale("Grenade"))),
        Def("Bow",
            Stat(Speed, "Arrow speed.", Fields("BowTransform", "ArrowSpeed")),
            Stat(Size, "Arrow size.", onSpawn: ColliderScale("Arrow"))),
        Def("Engine",
            Stat(Duration, "How long the engine burns.", onSpawn: CurveTimes("RocketEngine", "ForceAnim")),
            Stat(Knockback, "Engine thrust.", onSpawn: CurveValues("RocketEngine", "ForceAnim"))),
        Def("Blink gun",
            Stat(Duration, "How long hit walls, objects and players stay blinked out.",
                Fields("ShootQuantum", "WallDuration", "minPlayerDuration"))),
        Def("Gust",
            Stat(Duration, "How long the gust blows.", onSpawn: CurveTimes("Shockwave", "forceCurve01")),
            Stat(Knockback, "Push on players and platforms.", onSpawn: Fields("Shockwave", "defaultForce", "platformForce")),
            Stat(Size, "Gust radius.", onSpawn: Fields("Shockwave", "radius"))),
        Def("Growth ray"),
        Def("Rock",
            Stat(Duration, "How long you stay a rock.", Fields("BounceBall", "maxDuration")),
            Stat(Knockback, "Bounce off players and projectiles.", Fields("BounceBall", "playerCollisionBounce")),
            Stat(Size, "How much bigger the rock gets.", CurveValues("BounceBall", "scaleOnEnter"))),
        Def("Missile",
            Stat(Speed, "Missile speed (launch, minimum and acceleration).", Fields("SparkShot", "projectileStartSpeed"),
                Fields("Missile", "minimumForwardsSpeed", "accel")),
            Stat(Size, "Missile size, and so its explosion.", onSpawn: ColliderScale("Missile"))),
        Def("Spike",
            Stat(Knockback, "Push on hit players and walls.", onSpawn: Fields("SpikeAttack", "knockAwayStr", "knockAwayWallStr")),
            Stat(Size, "Spike size.", onSpawn: ColliderScale("SpikeAttack"))),
        Def("Time stop",
            Stat(Duration, "How long time stays stopped.", onSpawn: Fields("TimeStop", "duration")),
            Stat(ChargeTime, "How long to hold before it casts.", Fields("CastSpell", "castTime"))),
        Def("Smoke",
            Stat(Duration, "Time before the smoke grenade bursts.", onSpawn: Fields("Grenade", "detonationTime")),
            Stat(ChargeTime, "Time to reach a full throw.", DivFields("ThrowItem2", "ThrowForceGainSpeed")),
            Stat(ChargePower, "Strength of a fully charged throw.", Fields("ThrowItem2", "maxThrowForce")),
            Stat(Speed, "Throw speed.", Fields("ThrowItem2", "maxThrowForce", "minThrowForce")),
            Stat(Size, "Smoke grenade size, and so its clouds.", onSpawn: ColliderScale("Grenade"))),
        Def("Platform",
            Stat(Size, "Largest platform you can make.", Fields("PlatformTransform", "maxArea"))),
        Def("Revival"),
        Def("Roll",
            Stat(Duration, "How long the roll lasts.", Fields("Roll", "dashTime")),
            Stat(ChargeTime, "How long to hold for a roll (shorter = a hop).", Fields("Roll", "chargeTime")),
            Stat(Speed, "Roll speed.", Fields("Roll", "MaxSpeed"))),
        Def("Shrink ray"),
        Def("Black hole",
            Stat(Knockback, "Pull strength.", onSpawn: Fields("BlackHole", "G", "playerForce"))),
        Def("Invisibility",
            Stat(Duration, "How long you stay invisible (5 s in the game).")),
        Def("Meteor",
            Stat(ChargeTime, "Time to reach a full charge.", Fields("MeteorSmash", "maxHoldTime")),
            Stat(Knockback, "Push from the impact.", Fields("MeteorSmash", "groundForce", "playerPushForce"),
                Fields("Shockwave", "defaultForce", "platformForce")),
            Stat(Size, "Impact shockwave radius.", onSpawn: Fields("Shockwave", "radius"))),
        Def("Throw",
            Stat(ChargePower, "Extra strength per moment of charging.", Fields("MachoThrow2", "quickThrowChargeScaling")),
            Stat(Speed, "Throw speed.", Fields("MachoThrow2", "throwSpeed", "quickThrowStr"))),
        Def("Push",
            Stat(Knockback, "Push on the platform.", Fields("ControlPlatform", "platformForce"))),
        Def("Tesla coil"),
        Def("Mine",
            Stat(Speed, "Throw speed.", Fields("ThrowItem", "throwStrength", "airealThrowStrength", "minThrowStrength")),
            Stat(Size, "Mine size, and so its explosion.", onSpawn: ColliderScale("Mine"))),
        Def("Teleport",
            Stat(Size, "Teleport range.", Fields("Teleport", "radius"), Fields("TeleportIndicator", "teleportRadius"))),
        Def("Drill",
            Stat(Speed, "Digging speed.", Fields("Drill", "drillstr", "minUndergroundSpeed"))),
        Def("Grappling hook",
            Stat(Speed, "Hook speed.", Fields("HookshotInstant", "hookSpeed"))),
        Def("Beam",
            Stat(Duration, "How long the beam lasts.", Fields("Beam", "maxTime", "maxTimeAir")),
            Stat(Size, "Beam width (a wider beam also pushes harder).")),
        Def("Duplicator"),
        Def("Magnet gun",
            Stat(Duration, "How long it can pull.", Fields("MagnetGun", "maxPullDuration")),
            Stat(Knockback, "Launch force when firing what it holds.", Fields("MagnetGun", "FireForce", "FireRagdollForce",
                "FireRockForce", "FireBoulderForce", "FireBlackHoleForce", "FireForceMines", "FireMissileForce")),
            Stat(Size, "Pull width and pickup radius.", Fields("MagnetGun", "pullWidth", "pickupRadius"))),
    };

    private static readonly Dictionary<string, AbilityDef> ByName = Defs.ToDictionary(d => d.Name);

    // --- Table helpers ---

    private static AbilityDef Def(string name, params StatDef[] stats)
    {
        var all = new List<StatDef> { Stat(Cooldown, "Time before it can be used again.") };
        all.AddRange(stats);
        return new AbilityDef { Name = name, Stats = all };
    }

    /// <param name="onAbility">Applied to the ability object when it's created.</param>
    /// <param name="onSpawn">Applied to each object the ability spawns.</param>
    private static StatDef Stat(string key, string description, Action<GameObject, Fix> onAbility = null,
        Action<GameObject, Fix> onSpawn = null) =>
        new() { Key = key, Description = description, OnAbility = onAbility, OnSpawn = onSpawn };

    /// <summary>Multiplies the fields of every <paramref name="typeName"/> component on (or under) the object.</summary>
    private static Action<GameObject, Fix> Fields(string typeName, params string[] fields) => (go, m) =>
    {
        foreach (Component c in Components(go, typeName))
        {
            foreach (string field in fields)
                Multiply(c, field, m);
        }
    };

    /// <summary>Divides instead (a rate: twice the charge time = half the charge speed).</summary>
    private static Action<GameObject, Fix> DivFields(string typeName, params string[] fields) =>
        (go, m) => Fields(typeName, fields)(go, Fix.One / Fix.Max(m, MinDivisor));

    /// <summary>Stretches an AnimationCurveFixed in time (its length, e.g. a lifetime).</summary>
    private static Action<GameObject, Fix> CurveTimes(string typeName, string field) =>
        (go, m) => ScaleCurves(go, typeName, field, m, inTime: true);

    /// <summary>Scales an AnimationCurveFixed's values (e.g. a force over time).</summary>
    private static Action<GameObject, Fix> CurveValues(string typeName, string field) =>
        (go, m) => ScaleCurves(go, typeName, field, m, inTime: false);

    /// <summary>Scales the collider of a spawned object that has a <paramref name="typeName"/> component.</summary>
    private static Action<GameObject, Fix> ColliderScale(string typeName) => (go, m) =>
    {
        foreach (Component c in Components(go, typeName))
        {
            if (c.GetComponent<IPhysicsCollider>() is IPhysicsCollider collider)
                collider.Scale *= m;
        }
    };

    private static IEnumerable<Component> Components(GameObject go, string typeName)
    {
        Type type = AccessTools.TypeByName(typeName);
        return type == null ? Enumerable.Empty<Component>() : go.GetComponentsInChildren(type, true);
    }

    private static readonly Dictionary<(Type, string), FieldInfo> FieldCache = new();
    private static readonly HashSet<string> Warned = new();

    private static FieldInfo FindField(Type type, string name)
    {
        if (!FieldCache.TryGetValue((type, name), out FieldInfo field))
        {
            field = AccessTools.Field(type, name);
            FieldCache[(type, name)] = field;
            if (field == null && Warned.Add(type.Name + "." + name))
                Plugin.Log.LogWarning($"Ability stats: {type.Name}.{name} not found (game update?).");
        }
        return field;
    }

    private static void Multiply(Component c, string name, Fix m)
    {
        FieldInfo field = FindField(c.GetType(), name);
        switch (field?.GetValue(c))
        {
            case Fix f:
                field.SetValue(c, f * m);
                break;
            case float f:
                field.SetValue(c, f * (float)m);
                break;
            case int n:
                field.SetValue(c, Math.Max(1, (int)Math.Round(n * (float)m)));
                break;
            case Vec2 v:
                field.SetValue(c, v * m);
                break;
        }
    }

    private static void ScaleCurves(GameObject go, string typeName, string name, Fix m, bool inTime)
    {
        foreach (Component c in Components(go, typeName))
        {
            if (FindField(c.GetType(), name)?.GetValue(c) is not AnimationCurveFixed curve || curve.keys == null)
                continue;
            // Tangents are slopes (value per time): they shrink when time stretches and grow with the values.
            Fix tangent = inTime ? Fix.One / Fix.Max(m, MinDivisor) : m;
            for (int i = 0; i < curve.keys.Length; i++)
            {
                KeyFrameF key = curve.keys[i];
                if (inTime)
                    key.time *= m;
                else
                    key.value *= m;
                key.inTangent *= tangent;
                key.outTangent *= tangent;
                curve.keys[i] = key;
            }
        }
    }

    // --- Registration ---

    internal static void Register()
    {
        foreach (AbilityDef def in Defs)
        {
            AbilityStatSet set = AbilityStats.RegisterMultipliers(def.Name,
                def.Stats.Select(s => (s.Key, Labels[s.Key], s.Description)));
            for (int i = 0; i < def.Stats.Count; i++)
                def.Stats[i].Rule = set.Stats[i].Rule;
            def.HasSpawnStats = def.Stats.Any(s => s.OnSpawn != null);
        }
    }

    private static Fix Multiplier(AbilityDef def, string key) => def.RuleOf(key)?.ActiveFix ?? Fix.One;

    // What the hot patches need, derived from the active values once per change (Rule.ActiveVersion), not per call.
    private static int _refreshedVersion = -1;
    private static bool _anyCooldown, _anySpawn;
    private static Fix _invisibilityDuration = Fix.One, _beamSize = Fix.One;

    private static void Refresh()
    {
        if (_refreshedVersion == Rule.ActiveVersion)
            return;
        _refreshedVersion = Rule.ActiveVersion;
        _anyCooldown = _anySpawn = false;
        foreach (AbilityDef def in Defs)
        {
            def.CooldownMultiplier = Multiplier(def, Cooldown);
            _anyCooldown |= def.CooldownMultiplier != Fix.One;
            foreach (StatDef stat in def.Stats)
            {
                if (stat.OnSpawn != null && stat.Rule != null && stat.Rule.ActiveFix != Fix.One)
                    _anySpawn = true;
            }
        }
        _invisibilityDuration = ByName.TryGetValue("Invisibility", out AbilityDef invisibility) ? Multiplier(invisibility, Duration) : Fix.One;
        _beamSize = ByName.TryGetValue("Beam", out AbilityDef beam) ? Multiplier(beam, Size) : Fix.One;
    }

    /// <summary>Ability objects (and their components) → their ability, filled when they're created.</summary>
    private static readonly ConditionalWeakTable<Component, AbilityDef> Known = new();

    private static AbilityDef DefOf(Component c)
    {
        if (c == null)
            return null;
        if (Known.TryGetValue(c, out AbilityDef known))
            return known;
        // Not seen created (e.g. a copy of a live ability object). includeInactive: ability objects are inactive while
        // not in use, and their cooldown is still asked for.
        Tag tag = c.GetComponentInParent<Tag>(true);
        if (tag == null || tag.Ability == null || !ByName.TryGetValue(tag.Ability, out AbilityDef def))
            return null;
        Known.Add(c, def);
        return def;
    }

    // --- Ability objects ---

    private static NamedSpriteList _mappedList;
    private static int _mappedCount;
    private static readonly Dictionary<GameObject, string> PrefabNames = new();

    private static string PrefabName(GameObject prefab)
    {
        NamedSpriteList list = AbilityRules.AbilityList;
        if (list == null || prefab == null)
            return null;
        if (list != _mappedList || list.sprites.Count != _mappedCount)
        {
            PrefabNames.Clear();
            foreach (NamedSprite entry in list.sprites)
            {
                if (entry.associatedGameObject != null && ByName.ContainsKey(entry.name))
                    PrefabNames[entry.associatedGameObject] = entry.name;
            }
            _mappedList = list;
            _mappedCount = list.sprites.Count;
        }
        return PrefabNames.TryGetValue(prefab, out string name) ? name : null;
    }

    /// <summary>A vanilla ability object created for a player: tag it and scale its own stats.</summary>
    [HarmonyPatch]
    private static class OnAbilityCreated
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
            AccessTools.GetDeclaredMethods(typeof(FixTransform))
                .Where(m => m.Name == nameof(FixTransform.InstantiateFixed) && !m.IsGenericMethod);

        private static void Postfix(GameObject prefab, GameObject __result)
        {
            string name = PrefabName(prefab);
            if (name == null || __result == null || !ByName.TryGetValue(name, out AbilityDef def))
                return;
            __result.AddComponent<Tag>().Ability = name;
            foreach (MonoBehaviour component in __result.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Known.Remove(component);
                Known.Add(component, def);
            }
            foreach (StatDef stat in def.Stats)
            {
                Fix m = stat.Rule.ActiveFix;
                if (stat.OnAbility != null && m != Fix.One)
                    stat.OnAbility(__result, m);
            }
        }
    }

    // --- Cooldown ---

    [HarmonyPatch]
    private static class ScaleCooldown
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Ability), nameof(Ability.GetCooldown));
            yield return AccessTools.Method(typeof(InstantAbility), nameof(InstantAbility.GetCooldown));
        }

        private static void Postfix(AbilityMonoBehaviour __instance, ref Fix __result)
        {
            // Called several times per slot per slime every tick: nothing to do unless a cooldown was changed.
            Refresh();
            // Invisibility parks its cooldown at 10000 while you're invisible: leave that alone.
            if (!_anyCooldown || __result >= (Fix)1000L || DefOf(__instance) is not AbilityDef def ||
                def.CooldownMultiplier == Fix.One)
                return;
            __result *= def.CooldownMultiplier;
        }
    }

    // --- Spawned objects ---

    private sealed class Scope
    {
        public AbilityDef Def;
        public readonly List<MonoUpdatable> Spawned = new();
    }

    private static readonly Stack<Scope> Scopes = new();
    private static readonly Stack<Scope> FreeScopes = new();
    private static readonly HashSet<GameObject> Roots = new();
    private static bool _scopesPatched;

    /// <summary>
    /// Once, at the first character select (the ability list is loaded then): wraps the code of every component type on
    /// the vanilla ability prefabs that spawns things (ability classes and IAbilityComponents) in a <see cref="Scope"/>.
    /// </summary>
    internal static void PatchSpawnScopes(Harmony harmony)
    {
        NamedSpriteList list = AbilityRules.AbilityList;
        if (_scopesPatched || list == null)
            return;
        _scopesPatched = true;

        var prefix = new HarmonyMethod(typeof(VanillaAbilityStats), nameof(ScopePrefix));
        var finalizer = new HarmonyMethod(typeof(VanillaAbilityStats), nameof(ScopeFinalizer));
        var patched = new HashSet<MethodBase>();
        string[] methodNames = { "UpdateSim", "EnterAbility", "ExitAbility", "OnEnterAbility" };
        foreach (NamedSprite entry in list.sprites)
        {
            if (entry.associatedGameObject == null || !ByName.TryGetValue(entry.name, out AbilityDef def) || !def.HasSpawnStats)
                continue;
            foreach (MonoBehaviour component in entry.associatedGameObject.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component is not AbilityMonoBehaviour && component is not IAbilityComponent)
                    continue;
                for (Type type = component.GetType(); type != null && type != typeof(MonoUpdatable) && type != typeof(MonoBehaviour); type = type.BaseType)
                {
                    // Every overload: AccessTools.DeclaredMethod(type, name) throws when a name has several.
                    foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
                    {
                        if (Array.IndexOf(methodNames, method.Name) < 0 || method.IsStatic || method.IsAbstract ||
                            method.IsGenericMethodDefinition || method.GetMethodBody() == null || !patched.Add(method))
                            continue;
                        try
                        {
                            harmony.Patch(method, prefix: prefix, finalizer: finalizer);
                        }
                        catch (Exception e)
                        {
                            Plugin.Log.LogWarning($"Ability stats: couldn't watch {type.Name}.{method.Name}: {e.Message}");
                        }
                    }
                }
            }
        }
        Plugin.Log.LogInfo($"Ability stats: watching {patched.Count} ability methods for spawned objects.");
    }

    private static void ScopePrefix(MonoBehaviour __instance, out bool __state)
    {
        __state = false;
        // Runs for every ability in use, every tick: with no spawn stat changed, no scope can scale anything.
        Refresh();
        if (!_anySpawn)
            return;
        AbilityDef def = DefOf(__instance);
        __state = def != null && def.HasSpawnStats;
        if (!__state)
            return;
        Scope scope = FreeScopes.Count > 0 ? FreeScopes.Pop() : new Scope();
        scope.Def = def;
        Scopes.Push(scope);
    }

    private static Exception ScopeFinalizer(Exception __exception, bool __state)
    {
        if (__state && Scopes.Count > 0)
        {
            Scope scope = Scopes.Pop();
            try
            {
                ApplyToSpawned(scope);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Ability stats: scaling what {scope.Def.Name} spawned failed: {e}");
            }
            scope.Spawned.Clear();
            scope.Def = null;
            FreeScopes.Push(scope);
        }
        return __exception;
    }

    private static void ApplyToSpawned(Scope scope)
    {
        if (scope.Spawned.Count == 0)
            return;
        HashSet<GameObject> roots = Roots;
        roots.Clear();
        foreach (MonoUpdatable updatable in scope.Spawned)
        {
            if (updatable == null)
                continue;
            GameObject root = updatable.transform.root.gameObject;
            // Not the ability object itself, nor a player.
            if (root.GetComponent<Tag>() == null && root.GetComponent<SlimeController>() == null)
                roots.Add(root);
        }
        foreach (GameObject root in roots)
        {
            foreach (StatDef stat in scope.Def.Stats)
            {
                Fix m = stat.Rule.ActiveFix;
                if (stat.OnSpawn != null && m != Fix.One)
                    stat.OnSpawn(root, m);
            }
        }
        roots.Clear();
    }

    /// <summary>What registers with the Updater while an ability's code runs was spawned by it.</summary>
    [HarmonyPatch(typeof(Updater), nameof(Updater.RegisterUpdatable))]
    private static class CollectSpawned
    {
        private static void Postfix(MonoUpdatable updatable)
        {
            if (Scopes.Count > 0 && updatable != null)
                Scopes.Peek().Spawned.Add(updatable);
        }
    }

    // --- Special cases ---

    /// <summary>
    /// Invisibility lasts until Player.UpdatePlayer's timer passes a hardcoded 5 s: make the timer advance 1/× as fast
    /// (it adds this frame's time right after this prefix).
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlayer))]
    private static class InvisibilityDuration
    {
        private static void Prefix(Player __instance, Fix deltaTime)
        {
            if (!__instance.isInvisible)
                return;
            Refresh();
            Fix m = _invisibilityDuration;
            if (m == Fix.One)
                return;
            Fix step = GameTime.FixedDeltaTime(__instance.Id, deltaTime);
            __instance.timeSpentInvisible += step / Fix.Max(m, MinDivisor) - step;
        }
    }

    /// <summary>Beam width: the beam body's scale (only Beam adds beam bodies).</summary>
    [HarmonyPatch(typeof(DetPhysics), nameof(DetPhysics.AddBeamBody))]
    private static class BeamSize
    {
        private static void Prefix(ref DetPhysics.BeamBody body)
        {
            Refresh();
            if (_beamSize != Fix.One)
                body.scale *= _beamSize;
        }
    }
}
