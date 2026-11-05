using System.Collections.Generic;
using UnityEngine;

namespace CaliSkills.Abilities.Flamethrower;

/// <summary>
/// The Flamethrower's particles (visual only, scene object created on first use: cleared every level): flames of the
/// cone, water drops of melting ice and steam. Pooled SpriteRenderers moved in Update; each particle's color and size
/// follow its kind over its life.
/// </summary>
internal sealed class FlameParticles : MonoBehaviour
{
    internal enum Kind
    {
        Flame,
        Drip,
        Steam,
    }

    private struct Particle
    {
        public Kind Kind;
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 Start;
        public float MaxTravel;   // flames stop where a platform is
        public float Life;
        public float Age;
        public float Size;
        public float Spin;
        public float Angle;
        public SpriteRenderer Renderer;
    }

    private static FlameParticles _instance;
    private readonly List<Particle> _live = new();
    private readonly Stack<SpriteRenderer> _free = new();

    internal static void Emit(Kind kind, Vector2 position, Vector2 velocity, float life, float size, SpriteRenderer sortingReference,
        float maxTravel = float.MaxValue)
    {
        if (_instance == null)
            _instance = new GameObject("CaliSkills.FlameParticles").AddComponent<FlameParticles>();
        _instance.Add(kind, position, velocity, life, size, sortingReference, maxTravel);
    }

    private void Add(Kind kind, Vector2 position, Vector2 velocity, float life, float size, SpriteRenderer reference, float maxTravel)
    {
        SpriteRenderer renderer = _free.Count > 0 ? _free.Pop() : null;
        if (renderer == null)
        {
            renderer = new GameObject("Particle").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, worldPositionStays: false);
        }
        renderer.sprite = kind == Kind.Drip ? FlamethrowerSkill.DropSprite : FlamethrowerSkill.PuffSprite;
        if (reference != null)
        {
            renderer.sortingLayerID = reference.sortingLayerID;
            renderer.sortingOrder = reference.sortingOrder + (kind == Kind.Flame ? 2 : 4);
        }
        renderer.enabled = true;
        _live.Add(new Particle
        {
            Kind = kind,
            Position = position,
            Start = position,
            Velocity = velocity,
            MaxTravel = maxTravel,
            Life = Mathf.Max(0.05f, life),
            Size = size,
            Spin = Random.Range(-200f, 200f),
            Angle = Random.Range(0f, 360f),
            Renderer = renderer,
        });
        Place(_live[_live.Count - 1]);
    }

    private void Update()
    {
        float dt = GameSessionHandler.GameIsPaused ? 0f : Time.deltaTime;
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            Particle p = _live[i];
            p.Age += dt;
            if (p.Age >= p.Life || p.Renderer == null)
            {
                if (p.Renderer != null)
                {
                    p.Renderer.enabled = false;
                    _free.Push(p.Renderer);
                }
                _live.RemoveAt(i);
                continue;
            }
            switch (p.Kind)
            {
                case Kind.Flame:
                    // Slows down a little; stopped by a platform it flattens against it and burns out faster.
                    p.Velocity *= 1f - 1.2f * dt;
                    p.Velocity += Vector2.up * (4f * dt);
                    if ((p.Position - p.Start).magnitude >= p.MaxTravel)
                    {
                        p.Velocity *= 0.25f;
                        p.Age += dt;
                        p.MaxTravel = float.MaxValue;
                    }
                    break;
                case Kind.Drip:
                    p.Velocity += Vector2.down * (30f * dt);
                    break;
                case Kind.Steam:
                    p.Velocity *= 1f - 2f * dt;
                    break;
            }
            p.Position += p.Velocity * dt;
            p.Angle += p.Spin * dt;
            Place(p);
            _live[i] = p;
        }
    }

    private static void Place(Particle p)
    {
        float t = Mathf.Clamp01(p.Age / p.Life);
        float size;
        Color color;
        switch (p.Kind)
        {
            case Kind.Flame:
                size = p.Size * Mathf.Lerp(0.35f, 1.6f, Mathf.Sqrt(t));
                color = FlameColor(t);
                break;
            case Kind.Drip:
                size = p.Size;
                color = new Color(0.75f, 0.9f, 1f, 0.9f * (1f - t * t));
                break;
            default:
                size = p.Size * Mathf.Lerp(0.5f, 1.5f, t);
                color = new Color(1f, 1f, 1f, 0.45f * (1f - t));
                break;
        }
        Transform tr = p.Renderer.transform;
        tr.position = p.Position;
        tr.rotation = p.Kind == Kind.Drip ? Quaternion.identity : Quaternion.Euler(0f, 0f, p.Angle);
        tr.localScale = new Vector3(size, size, 1f);
        p.Renderer.color = color;
    }

    /// <summary>White-yellow at the nozzle, then orange, red, and dark smoke fading out.</summary>
    private static Color FlameColor(float t)
    {
        if (t < 0.15f)
            return Color.Lerp(new Color(1f, 1f, 0.8f, 0.95f), new Color(1f, 0.85f, 0.25f, 0.95f), t / 0.15f);
        if (t < 0.45f)
            return Color.Lerp(new Color(1f, 0.85f, 0.25f, 0.95f), new Color(1f, 0.4f, 0.08f, 0.9f), (t - 0.15f) / 0.3f);
        if (t < 0.75f)
            return Color.Lerp(new Color(1f, 0.4f, 0.08f, 0.9f), new Color(0.75f, 0.12f, 0.05f, 0.7f), (t - 0.45f) / 0.3f);
        return Color.Lerp(new Color(0.75f, 0.12f, 0.05f, 0.7f), new Color(0.2f, 0.18f, 0.18f, 0f), (t - 0.75f) / 0.25f);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
