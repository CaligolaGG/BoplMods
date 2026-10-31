using BoplFixedMath;
using UnityEngine;

namespace CaliGameModes.Modes.Football;

/// <summary>
/// One goal, measured every tick from the platforms around its probe point (so it's right while the level animates
/// in, and on every machine alike): the crossbar is the platform straight above, the goal line its inner side (facing
/// the middle of the map), the back its outer side, the bottom the platform below.
/// A ball scores once it's entirely past the goal line, between the floor and the crossbar.
/// </summary>
internal sealed class FootballGoal
{
    private const long ProbeReach = 200L;

    internal FootballGoal(Vec2 probe, int side)
    {
        Probe = probe;
        Side = side;
    }

    internal Vec2 Probe { get; }

    /// <summary>0 = left goal (defended by the left team), 1 = right goal.</summary>
    internal int Side { get; }

    internal bool Valid { get; private set; }

    internal Fix Left { get; private set; }

    internal Fix Right { get; private set; }

    internal Fix Bottom { get; private set; }

    internal Fix Top { get; private set; }

    /// <summary>The open side, towards the middle of the map.</summary>
    internal Fix GoalLine => Side == 0 ? Right : Left;

    internal void Measure()
    {
        Valid = false;
        DetPhysics physics = DetPhysics.Get();
        RaycastInformation up = physics.RaycastToClosestRoundedRect(Probe, Vec2.up, (Fix)ProbeReach);
        RaycastInformation down = physics.RaycastToClosestRoundedRect(Probe, Vec2.down, (Fix)ProbeReach);
        if (!up || !down)
            return;

        // The crossbar platform's horizontal extent: its box (half-extent vectors) plus the rounding radius.
        RoundedRect bar = physics.roundedRects.colliders[physics.roundedRects.ColliderIndex(up.pp.instanceId)];
        Fix halfWidth = Fix.Abs(bar.box.right.x) + Fix.Abs(bar.box.up.x) + bar.radius;
        Left = bar.box.center.x - halfWidth;
        Right = bar.box.center.x + halfWidth;
        Top = up.nearPos.y;
        Bottom = down.nearPos.y;
        Valid = Right > Left && Top > Bottom;
    }

    internal bool Contains(Vec2 ball, Fix radius)
    {
        if (!Valid || ball.y < Bottom || ball.y > Top)
            return false;
        // Entirely past the goal line (or its center past the middle of the goal, for balls wider than it).
        Fix depth = Fix.Min(radius, (Right - Left) / (Fix)2L);
        return Side == 0
            ? ball.x <= Right - depth && ball.x >= Left - radius
            : ball.x >= Left + depth && ball.x <= Right + radius;
    }

    internal Rect WorldRect => new((float)Left, (float)Bottom, (float)(Right - Left), (float)(Top - Bottom));
}
