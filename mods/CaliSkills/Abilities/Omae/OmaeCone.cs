using UnityEngine;

namespace CaliSkills.Abilities.Omae;

/// <summary>
/// On the Omae ability prefab: shows the poke's hitbox while aiming, as a red semitransparent cone from the slime
/// towards the aimed direction (the same cone <see cref="OmaePoke"/> tests). Visual only. Also marks a ThrowItem2
/// as an Omae ability for <see cref="OmaePoke"/>.
/// The cone is its own unparented GameObject so the ability's flipping (negative scale) doesn't affect it.
/// </summary>
public class OmaeCone : MonoBehaviour
{
    private ThrowItem2 _thrower;
    private SpriteRenderer _cone;

    private void Awake()
    {
        _thrower = GetComponent<ThrowItem2>();
        var go = new GameObject("OmaeCone");
        _cone = go.AddComponent<SpriteRenderer>();
        go.SetActive(false);
    }

    private void LateUpdate()
    {
        bool aiming = _thrower != null && _thrower.isActiveAndEnabled && !_thrower.hasFired &&
                      _thrower.player != null && _thrower.body != null;
        _cone.gameObject.SetActive(aiming);
        if (!aiming)
            return;

        Vector2 dir = (Vector2)_thrower.dir;
        if (dir.sqrMagnitude < 1e-6f)
            dir = Vector2.up;

        _cone.sprite = OmaeSkill.ConeSprite;
        if (_thrower.spriteRen != null)
        {
            _cone.sortingLayerID = _thrower.spriteRen.sortingLayerID;
            _cone.sortingOrder = _thrower.spriteRen.sortingOrder - 1;
        }

        // The sprite is a cone of radius 1 unit pointing right: rotate to the aim, scale to the reach.
        float reach = OmaeSettings.HitboxSize * (float)_thrower.player.Scale;
        Transform cone = _cone.transform;
        cone.position = (Vector3)(Vector2)_thrower.body.position;
        cone.rotation = Quaternion.FromToRotation(Vector3.right, dir);
        cone.localScale = new Vector3(reach, reach, 1f);
    }

    private void OnDisable()
    {
        if (_cone != null)
            _cone.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_cone != null)
            Destroy(_cone.gameObject);
    }
}
