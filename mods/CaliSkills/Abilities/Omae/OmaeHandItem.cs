using BoplFixedMath;

namespace CaliSkills.Abilities.Omae;

/// <summary>
/// The invisible "item" the Omae ability holds (ThrowItem2 requires one). Removes itself once released, whether by
/// the poke or because the ability was interrupted. No-op <see cref="IGrenadeDetonation"/>: the Grenade component expects one.
/// </summary>
public class OmaeHandItem : MonoUpdatable, IGrenadeDetonation
{
    private Grenade _grenade;
    private bool _removed;

    private void Awake()
    {
        Updater.RegisterUpdatable(this);
        _grenade = GetComponent<Grenade>();
    }

    public override void Init()
    {
    }

    public void Detonate()
    {
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        if (_removed || (_grenade != null && !_grenade.hasBeenThrown))
            return;
        _removed = true;
        Updater.DestroyFix(gameObject);
    }
}
