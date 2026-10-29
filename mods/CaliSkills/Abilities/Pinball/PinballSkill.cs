using BoplFixedMath;
using CaliSkills.Core;
using UnityEngine;

namespace CaliSkills.Abilities.Pinball;

/// <summary>
/// Pinball: a copy of the vanilla Grenade ability (ThrowItem2: the slime holds the item in its hand, aims, throws)
/// whose grenade is replaced by a bouncing ball. See <see cref="PinballBall"/> for the ball and its hits.
/// </summary>
internal sealed class PinballSkill : Skill
{
    private const int BallSpritePixels = 64;

    private Ability _pinballAbility;
    private BoplBody _ballPrefab;
    private SpriteRenderer _handDummy;
    private float _ballSpriteSize = -1f;
    private float _dummySpriteSize = -1f;

    public override string Name => "Pinball";

    // It knocks players back but never kills them.
    public override bool IsOffensive => false;

    public override System.Type SettingsType => typeof(PinballSettings);

    // Everything else is read live by PinballBall / PinballHitPatch. Abilities are instantiated from the
    // prefab at the start of every round, so changes apply from the next round.
    public override void OnSettingsChanged() => ApplyBakedSettings();

    public override Sprite CreateIcon(NamedSpriteList abilityList) =>
        SpriteFactory.LikeReference(abilityList.sprites[2].sprite, Name, (u, v) => SpriteFactory.Ball(u, v, 0.36f));

    public override GameObject CreatePrefab(NamedSpriteList abilityList)
    {
        GameObject grenadePrefab = abilityList.sprites[abilityList.IndexOf("Grenade")].associatedGameObject;
        GameObject pinball = Object.Instantiate(grenadePrefab, SkillRegistry.PrefabRoot);
        pinball.name = Name;

        _pinballAbility = pinball.GetComponent<Ability>();

        ThrowItem2 thrower = pinball.GetComponent<ThrowItem2>();
        LogVanillaReferenceValues(thrower);

        // The ball sets its own launch speed (see PinballBall.Launch); a throw force would add to it.
        thrower.minThrowForce = Fix.Zero;
        thrower.maxThrowForce = Fix.Zero;
        thrower.isExplosiveGrenade = false;

        _ballPrefab = CreateBallPrefab(thrower.ItemPrefab);
        thrower.ItemPrefab = _ballPrefab;

        // Child 1 is the grenade drawn in the slime's hand during the wind-up animation (see ThrowItem2.Awake).
        _handDummy = thrower.transform.GetChild(1).GetComponentInChildren<SpriteRenderer>(includeInactive: true);

        ApplyBakedSettings();
        return pinball;
    }

    /// <summary>Settings stored on the prefabs rather than read at runtime.</summary>
    private void ApplyBakedSettings()
    {
        if (_pinballAbility == null || _ballPrefab == null)
            return;

        _pinballAbility.Cooldown = (Fix)PinballSettings.AbilityCooldown;
        _ballPrefab.gravityScale = (Fix)PinballSettings.BallGravity;
        _ballPrefab.GetComponent<DPhysicsCircle>().radius = (Fix)PinballSettings.BallSize;

        if (!Mathf.Approximately(_ballSpriteSize, PinballSettings.BallSize))
        {
            _ballSpriteSize = PinballSettings.BallSize;
            _ballPrefab.GetComponent<SpriteRenderer>().sprite = BallSprite(PinballSettings.BallSize);
        }

        // The hand dummy is shown before the real ball spawns: draw it at the uncharged size.
        float dummySize = PinballSettings.BallSize * PinballSettings.SizeAtMinCharge;
        if (_handDummy != null && !Mathf.Approximately(_dummySpriteSize, dummySize))
        {
            _dummySpriteSize = dummySize;
            _handDummy.sprite = BallSprite(dummySize);
        }
    }

    private static Sprite BallSprite(float radius) =>
        SpriteFactory.Square(BallSpritePixels, radius * 2f, "PinballBall", (u, v) => SpriteFactory.Ball(u, v, 0.5f));

    /// <summary>
    /// Turns a copy of the grenade into a ball. Keeps Item and Grenade (ThrowItem2 needs them to hold and release
    /// it), removes the explosion, and makes it bouncy.
    /// </summary>
    private static BoplBody CreateBallPrefab(BoplBody grenadePrefab)
    {
        BoplBody ball = Object.Instantiate(grenadePrefab, SkillRegistry.PrefabRoot);
        ball.name = "PinballBall";

        foreach (MonoBehaviour behaviour in ball.GetComponents<MonoBehaviour>())
        {
            if (behaviour is IGrenadeDetonation)
                Object.DestroyImmediate(behaviour);
        }
        ball.GetComponent<Grenade>().timedExplosion = false;

        // The grenade's own visuals: fuse sparks etc. (children) and its sprite animation.
        for (int i = ball.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(ball.transform.GetChild(i).gameObject);
        SpriteAnimator animator = ball.GetComponent<SpriteAnimator>();
        if (animator != null)
            Object.DestroyImmediate(animator);
        if (ball.GetComponent<SpriteRenderer>() == null)
            ball.gameObject.AddComponent<SpriteRenderer>();

        // Keep the grenade's "item" layer: other abilities recognise items by layer. Player hits: PinballBall.OnCollide.
        ball.bounciness = Fix.One;
        ball.friction = Fix.Zero;
        ball.dynamicFriction = Fix.Zero;
        ball.airFriction01 = Fix.One;
        ball.transform.localScale = Vector3.one;
        // Gravity, radius and sprite: see ApplyBakedSettings.

        ball.gameObject.AddComponent<PinballBall>();
        return ball;
    }

    private static void LogVanillaReferenceValues(ThrowItem2 thrower)
    {
        BoplBody grenade = thrower.ItemPrefab;
        Plugin.Log.LogInfo(
            $"Vanilla Grenade: throw force {thrower.minThrowForce}..{thrower.maxThrowForce}, " +
            $"full charge after {Fix.One / thrower.ThrowForceGainSpeed}s, item radius={grenade.GetComponent<DPhysicsCircle>()?.radius} " +
            $"gravity={grenade.gravityScale} layer={LayerMask.LayerToName(grenade.gameObject.layer)}");
    }
}
