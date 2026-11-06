using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;

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
        public Vec2[] External;
        public Vec2[] SelfImposed;
    }

    private static readonly Dictionary<int, (int by, Fix at)> LastKnock = new();
    private static readonly Dictionary<AbilityMonoBehaviour, int> AbilityOwners = new();
    private static readonly List<Tracked> Bodies = new();
    private static readonly Stack<Frame> Frames = new();
    private static long _refreshedTick = -1;
    private static bool _patched;

    /// <summary>A new match: nobody has been knocked yet. Patches the sim methods the first time.</summary>
    internal static void Reset()
    {
        LastKnock.Clear();
        Frames.Clear();
        _refreshedTick = -1;
        if (!_patched)
        {
            _patched = true;
            PatchAll();
        }
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
        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID + ".knocks");
        var prefix = new HarmonyMethod(typeof(KnockTracker), nameof(Prefix));
        var finalizer = new HarmonyMethod(typeof(KnockTracker), nameof(Finalizer));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        int count = 0;
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
                    if (method == null || method.IsAbstract || method.GetMethodBody() == null)
                        continue;
                    try
                    {
                        harmony.Patch(method, prefix: prefix, finalizer: finalizer);
                        count++;
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"Rotation: can't track knocks from {type.FullName}.{method.Name}: {e.Message}");
                    }
                }
            }
        }
        Plugin.Log.LogInfo($"Rotation: tracking knocks from {count} methods.");
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
        var frame = new Frame { Owner = owner, External = new Vec2[Bodies.Count], SelfImposed = new Vec2[Bodies.Count] };
        Snapshot(frame);
        Frames.Push(frame);
        __state = true;
    }

    private static void Finalizer(bool __state)
    {
        if (!__state || Frames.Count == 0)
            return;
        Frame frame = Frames.Pop();
        RotationMatch match = RotationMatch.Current;
        if (match == null)
            return;
        Player owner = PlayerHandler.Get().GetPlayer(frame.Owner);
        int n = Math.Min(frame.External.Length, Bodies.Count);
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
        int n = Math.Min(frame.External.Length, Bodies.Count);
        for (int i = 0; i < n; i++)
        {
            PlayerBody body = Bodies[i].Body;
            if (body == null || body.IsDestroyed)
                continue;
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
        Frames.Clear();

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
        IPlayerIdHolder holder = component.GetComponent<IPlayerIdHolder>();
        if (holder != null)
            return holder.GetPlayerId();
        Item item = component.GetComponent<Item>();
        if (item != null)
            return item.OwnerId;
        holder = component.GetComponentInParent<IPlayerIdHolder>();
        return holder != null ? holder.GetPlayerId() : -1;
    }
}
