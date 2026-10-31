using System.Collections.Generic;
using BoplFixedMath;

namespace CaliGameModes.Modes.Football;

/// <summary>
/// The maps football is played on (Map Loader maps, shipped in Maps/ and recognised by their MapUUID). Goals are given
/// as a point inside each goal mouth: the goal is measured from the platforms around it (see <see cref="FootballGoal"/>).
/// Add a map: put its zip in Maps/ and an entry here.
/// </summary>
internal static class FootballMaps
{
    private static readonly Dictionary<int, FootballMap> Maps = new()
    {
        // Football Field: goals under the two upright capsules (x ±60), above the long floor.
        [583028914] = new FootballMap(
            name: "Football Field",
            ballSpawn: new Vec2(Fix.Zero, (Fix)18L),
            playerRespawn: new Vec2(Fix.Zero, (Fix)24L),
            leftGoal: new Vec2((Fix)(-60L), (Fix)2L),
            rightGoal: new Vec2((Fix)60L, (Fix)2L)),
    };

    /// <summary>On a map that isn't a football map (e.g. without Map Loader): the ball and respawns, no goals.</summary>
    internal static readonly FootballMap NoGoals = new("", new Vec2(Fix.Zero, (Fix)15L), new Vec2(Fix.Zero, (Fix)20L), null, null);

    internal static bool IsFootballMap(int uuid) => Maps.ContainsKey(uuid);

    internal static FootballMap ForMap(int uuid) => Maps.TryGetValue(uuid, out FootballMap map) ? map : NoGoals;
}

internal sealed class FootballMap
{
    public FootballMap(string name, Vec2 ballSpawn, Vec2 playerRespawn, Vec2? leftGoal, Vec2? rightGoal)
    {
        Name = name;
        BallSpawn = ballSpawn;
        PlayerRespawn = playerRespawn;
        LeftGoal = leftGoal;
        RightGoal = rightGoal;
    }

    public string Name { get; }

    /// <summary>Where balls appear (several balls side by side around it).</summary>
    public Vec2 BallSpawn { get; }

    /// <summary>Where players come back after "dying".</summary>
    public Vec2 PlayerRespawn { get; }

    /// <summary>A point inside the left goal's mouth (defended by the left team), null = no goal.</summary>
    public Vec2? LeftGoal { get; }

    public Vec2? RightGoal { get; }
}
