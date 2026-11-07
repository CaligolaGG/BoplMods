using System.Runtime.CompilerServices;
using HarmonyLib;
using Object = UnityEngine.Object;

namespace CaliSkills.Core;

/// <summary>
/// Scene searches shared by the skills: several of them list the players, flying bodies, platforms or smoke clouds
/// every tick while in use, and <c>FindObjectsOfType</c> walks every object of the type each time. Here each type is
/// searched at most once per sim tick (and frame), and again as soon as anything new registers with the Updater (a spawn) in that
/// tick, so a caller sees the same objects a fresh search would.
/// Inactive objects are included (an ability form activated later in the tick must be seen): every caller filters on
/// <c>activeInHierarchy</c> itself, as it did before. The order is Unity's (not deterministic): callers sort.
/// </summary>
internal static class SceneScan
{
    private static int _spawnVersion;

    private static class Cache<T> where T : Object
    {
        public static T[] Items;
        public static long Tick = -1;
        public static int Frame = -1;
        public static int SpawnVersion = -1;
    }

    /// <summary>Every <typeparamref name="T"/> in the loaded scenes, active or not.</summary>
    internal static T[] All<T>() where T : Object
    {
        long tick = Updater.SimulationTicks;
        // The frame too: Unity destroys objects at the end of a frame, and a frame can pass without a sim tick.
        int frame = UnityEngine.Time.frameCount;
        if (Cache<T>.Items == null || Cache<T>.Tick != tick || Cache<T>.Frame != frame || Cache<T>.SpawnVersion != _spawnVersion)
        {
            Cache<T>.Items = Object.FindObjectsOfType<T>(includeInactive: true);
            Cache<T>.Tick = tick;
            Cache<T>.Frame = frame;
            Cache<T>.SpawnVersion = _spawnVersion;
        }
        return Cache<T>.Items;
    }

    /// <summary>Anything spawned: the next search looks again.</summary>
    [HarmonyPatch(typeof(Updater), nameof(Updater.RegisterUpdatable))]
    private static class OnSpawn
    {
        private static void Postfix() => _spawnVersion++;
    }

    private sealed class BodyKind
    {
        public bool PlayerOrForm;
        public bool BlackHole;
        public bool PlatformOrSmoke;
        public Grenade Grenade;
    }

    private static readonly ConditionalWeakTable<BoplBody, BodyKind> Kinds = new();

    private static BodyKind KindOf(BoplBody body)
    {
        if (!Kinds.TryGetValue(body, out BodyKind kind))
        {
            kind = new BodyKind
            {
                PlayerOrForm = body.GetComponent<PlayerCollision>() != null || body.GetComponent<AbilityMonoBehaviour>() != null,
                BlackHole = body.GetComponent<BlackHole>() != null,
                PlatformOrSmoke = body.GetComponent<StickyRoundedRectangle>() != null || body.GetComponent<FlammableSmoke>() != null,
                Grenade = body.GetComponent<Grenade>(),
            };
            Kinds.Add(body, kind);
        }
        return kind;
    }

    /// <summary>
    /// The body is a flying projectile or item: not a player or ability form, not a black hole, not a grenade still in
    /// a hand, and (with <paramref name="skipPlatformsAndSmoke"/>) not a platform or smoke cloud. Its components are
    /// looked up once per body; whether a grenade was thrown is read live.
    /// </summary>
    internal static bool IsFlyingBody(BoplBody body, bool skipPlatformsAndSmoke)
    {
        BodyKind kind = KindOf(body);
        if (kind.PlayerOrForm || kind.BlackHole || (skipPlatformsAndSmoke && kind.PlatformOrSmoke))
            return false;
        return kind.Grenade == null || kind.Grenade.hasBeenThrown;
    }
}
