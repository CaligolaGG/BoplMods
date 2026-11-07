using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CaliGameModes.Modes.Rotation;

/// <summary>
/// Rotation mode: who last knocked each player around, so a death by the water, the edge of the map or the like
/// can be credited to them. The game doesn't track it, and knocks come from everywhere (Push, explosions, black
/// holes, ability mods...), so this works on what they all have in common: while an object owned by a player runs
/// its sim step or a collision callback (every <see cref="MonoUpdatable"/> type's UpdateSim / OnCollide, patched
/// once), a change of another player's slime (or ability form) velocity is a knock by that object's owner. Nested
/// owned calls (a collision during another object's step) are credited to the innermost owner only.
/// Sim code: only sim state (velocities, owners, the match clock); the same on every machine.
/// </summary>
internal static class KnockTracker
{
    /// <summary>A knock older than this doesn't count anymore.</summary>
    internal static readonly Fix Window = (Fix)5L;

    /// <summary>Smallest velocity change that counts as a knock.</summary>
    private static readonly Fix MinChange = (Fix)0.05f;

    /// <summary>Their own movement code: never another player's knock, and run very often.</summary>
    private static readonly HashSet<Type> Skipped = new()
    {
        typeof(SlimeController), typeof(PlayerPhysics), typeof(PlayerBody), typeof(PlayerCollision),
        typeof(RotationMatch),
    };

    private struct Tracked
    {
        public int PlayerId;
        public PlayerBody Body;
    }

    private sealed class Frame
    {
        public int Owner;
        public int Count;
        public Vec2[] External = new Vec2[8];
        public Vec2[] SelfImposed = new Vec2[8];
    }

    /// <summary>Where an object's owner comes from (found once per object and round; the id itself is read live).</summary>
    private enum OwnerKind
    {
        None,
        BlackHole,
        Ability,
        Holder,
        Item,
    }

    private struct OwnerSource
    {
        public OwnerKind Kind;
        public IPlayerIdHolder Holder;
        public Item Item;
    }

    private static readonly Dictionary<int, (int by, Fix at)> LastKnock = new();
    private static readonly Dictionary<AbilityMonoBehaviour, int> AbilityOwners = new();
    private static readonly Dictionary<Component, OwnerSource> Owners = new();
    private static readonly List<Tracked> Bodies = new();
    private static readonly Stack<Frame> Frames = new();
    private static readonly Stack<Frame> FreeFrames = new();
    private static long _refreshedTick = -1;
    private static Harmony _harmony;
    private static List<MethodInfo> _targets;
    private static bool _patched;

    /// <summary>A new match: nobody has been knocked yet. Patches the sim methods (again after a round without Rotation).</summary>
    internal static void Reset()
    {
        LastKnock.Clear();
        ClearFrames();
        Owners.Clear();
        _refreshedTick = -1;
        if (!_patched)
        {
            _patched = true;
            PatchAll();
        }
    }

    /// <summary>
    /// A round without Rotation: removes the patches, so the hundreds of sim methods they wrap stop paying for them.
    /// Same point on every machine (the level start); it only changes the knock bookkeeping, which nothing reads then.
    /// </summary>
    internal static void Unpatch()
    {
        if (!_patched)
            return;
        _patched = false;
        try
        {
            _harmony?.UnpatchSelf();
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Rotation: removing the knock tracking failed: {e.Message}");
        }
        LastKnock.Clear();
        ClearFrames();
        Owners.Clear();
        Bodies.Clear();
        AbilityOwners.Clear();
    }

    private static void ClearFrames()
    {
        while (Frames.Count > 0)
            FreeFrames.Push(Frames.Pop());
    }

    /// <summary>The player of another team who knocked <paramref name="victim"/> in the last few seconds, or -1.</summary>
    internal static int RecentKnocker(Player victim, Fix now)
    {
        if (!LastKnock.TryGetValue(victim.Id, out (int by, Fix at) knock) || now - knock.at > Window)
            return -1;
        Player knocker = PlayerHandler.Get().GetPlayer(knock.by);
        return knocker != null && knocker.Team != victim.Team ? knock.by : -1;
    }

    internal static void Forget(int playerId) => LastKnock.Remove(playerId);

    private static void PatchAll()
    {
        _harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID + ".knocks");
        var prefix = new HarmonyMethod(typeof(KnockTracker), nameof(Prefix));
        var finalizer = new HarmonyMethod(typeof(KnockTracker), nameof(Finalizer));
        int count = 0;
        foreach (MethodInfo method in _targets ??= FindTargets())
        {
            try
            {
                _harmony.Patch(method, prefix: prefix, finalizer: finalizer);
                count++;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Rotation: can't track knocks from {method.DeclaringType?.FullName}.{method.Name}: {e.Message}");
            }
        }
        Plugin.Log.LogInfo($"Rotation: tracking knocks from {count} methods.");
    }

    /// <summary>Every declared UpdateSim / OnCollide of the MonoUpdatable types (searched once).</summary>
    private static List<MethodInfo> FindTargets()
    {
        var targets = new List<MethodInfo>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic))
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }
            foreach (Type type in types)
            {
                if (!type.IsClass || type.ContainsGenericParameters || Skipped.Contains(type) ||
                    !typeof(MonoUpdatable).IsAssignableFrom(type))
                    continue;
                foreach (MethodInfo method in new[]
                         {
                             type.GetMethod("UpdateSim", flags, null, new[] { typeof(Fix) }, null),
                             type.GetMethod("OnCollide", flags, null, new[] { typeof(CollisionInformation) }, null),
                         })
                {
                    if (method != null && !method.IsAbstract && method.GetMethodBody() != null)
                        targets.Add(method);
                }
            }
        }
        return targets;
    }

    private static void Prefix(object __instance, out bool __state)
    {
        __state = false;
        RotationMatch match = RotationMatch.Current;
        if (match == null || __instance is not Component component)
            return;
        RefreshBodies(match);
        int owner = OwnerOf(component);
        if (owner < 0 || PlayerHandler.Get().GetPlayer(owner) == null)
            return;
        Frame frame = FreeFrames.Count > 0 ? FreeFrames.Pop() : new Frame();
        frame.Owner = owner;
        frame.Count = Bodies.Count;
        if (frame.External.Length < frame.Count)
        {
            frame.External = new Vec2[frame.Count * 2];
            frame.SelfImposed = new Vec2[frame.Count * 2];
        }
        Snapshot(frame);
        Frames.Push(frame);
        __state = true;
    }

    private static void Finalizer(bool __state)
    {
        if (!__state || Frames.Count == 0)
            return;
        Frame frame = Frames.Pop();
        FreeFrames.Push(frame); // read below before anything can reuse it
        RotationMatch match = RotationMatch.Current;
        if (match == null)
            return;
        Player owner = PlayerHandler.Get().GetPlayer(frame.Owner);
        int n = Math.Min(frame.Count, Bodies.Count);
        for (int i = 0; i < n; i++)
        {
            PlayerBody body = Bodies[i].Body;
            if (body == null || body.IsDestroyed)
                continue;
            Fix change = Vec2.Magnitude(body.externalVelocity - frame.External[i]) +
                         Vec2.Magnitude(body.selfImposedVelocity - frame.SelfImposed[i]);
            if (change < MinChange)
                continue;
            Player victim = PlayerHandler.Get().GetPlayer(Bodies[i].PlayerId);
            if (owner != null && victim != null && owner.Team != victim.Team)
                LastKnock[victim.Id] = (frame.Owner, match.Clock);
        }
        // The calls this one ran inside of only count what happens after it.
        foreach (Frame outer in Frames)
            Snapshot(outer);
    }

    private static void Snapshot(Frame frame)
    {
        int n = Math.Min(frame.Count, Bodies.Count);
        for (int i = 0; i < n; i++)
        {
            PlayerBody body = Bodies[i].Body;
            if (body == null || body.IsDestroyed)
            {
                // A reused frame: no stale value from an earlier call.
                frame.External[i] = frame.SelfImposed[i] = default;
                continue;
            }
            frame.External[i] = body.externalVelocity;
            frame.SelfImposed[i] = body.selfImposedVelocity;
        }
    }

    /// <summary>Once per tick: the players' bodies (slimes and ability forms) and who owns which ability.</summary>
    private static void RefreshBodies(RotationMatch match)
    {
        long tick = Updater.SimulationTicks;
        if (tick == _refreshedTick)
            return;
        _refreshedTick = tick;
        ClearFrames();

        Bodies.Clear();
        foreach (PlayerCollision collision in UnityEngine.Object.FindObjectsOfType<PlayerCollision>())
        {
            PlayerBody body = collision.GetComponent<PlayerBody>();
            if (body != null && !body.IsDestroyed)
                Bodies.Add(new Tracked { PlayerId = collision.GetPlayerId(), Body = body });
        }

        AbilityOwners.Clear();
        foreach (SlimeController slime in match.Slimes())
        {
            if (slime.abilities == null)
                continue;
            foreach (AbilityMonoBehaviour ability in slime.abilities)
            {
                if (ability != null)
                    AbilityOwners[ability] = slime.playerNumber;
            }
        }
    }

    /// <summary>The player an object belongs to, or -1.</summary>
    private static int OwnerOf(Component component)
    {
        switch (component)
        {
            case BlackHole blackHole:
                return blackHole.ownerId;
            case AbilityMonoBehaviour ability:
                return AbilityOwners.TryGetValue(ability, out int id) ? id : -1;
        }
        // Every sim object runs this every tick: the component lookups are done once per object and round.
        if (!Owners.TryGetValue(component, out OwnerSource source))
        {
            source = FindOwnerSource(component);
            Owners[component] = source;
        }
        switch (source.Kind)
        {
            case OwnerKind.Holder when source.Holder is not Object dead || dead != null:
                return source.Holder.GetPlayerId();
            case OwnerKind.Item when source.Item != null:
                return source.Item.OwnerId;
            default:
                return -1;
        }
    }

    private static OwnerSource FindOwnerSource(Component component)
    {
        IPlayerIdHolder holder = component.GetComponent<IPlayerIdHolder>();
        if (holder != null)
            return new OwnerSource { Kind = OwnerKind.Holder, Holder = holder };
        Item item = component.GetComponent<Item>();
        if (item != null)
            return new OwnerSource { Kind = OwnerKind.Item, Item = item };
        holder = component.GetComponentInParent<IPlayerIdHolder>();
        return holder != null ? new OwnerSource { Kind = OwnerKind.Holder, Holder = holder } : default;
    }
}
