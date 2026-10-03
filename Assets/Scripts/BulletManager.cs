using System.Collections.Generic;
using UnityEngine;

namespace Salvonix {

// Enemy energy bolts: pooled, moved linearly and tested against the player's
// hit sphere with a swept test.
public sealed class BulletManager : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public GameObject BulletPrefab { get; set; }
    [field:SerializeField] public PlayerMech Mech { get; set; }
    [field:SerializeField] public CameraController CameraController { get; set; }
    [field:SerializeField] public float Lifetime { get; set; } = 6;

    // Public methods

    public void Fire(Vector3 position, Vector3 direction, float speed, float damage)
    {
        var b = Get();
        b.Position = position;
        b.Velocity = direction.normalized * speed;
        b.Damage = damage;
        b.Age = 0;
        b.Transform.position = position;
        b.Trail.Clear();
        b.Transform.gameObject.SetActive(true);
        _active.Add(b);
        AudioManager.Instance.PlayEnemyShot();
    }

    public void Clear()
    {
        foreach (var b in _active) b.Transform.gameObject.SetActive(false);
        _active.Clear();
    }

    // Private members

    sealed class Bullet
    {
        public Transform Transform;
        public TrailRenderer Trail;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Damage;
        public float Age;
    }

    readonly List<Bullet> _pool = new();
    readonly List<Bullet> _active = new();

    Bullet Get()
    {
        foreach (var b in _pool) if (!b.Transform.gameObject.activeSelf) return b;
        var go = Instantiate(BulletPrefab, transform);
        var nb = new Bullet { Transform = go.transform, Trail = go.GetComponentInChildren<TrailRenderer>() };
        _pool.Add(nb);
        return nb;
    }

    static bool SegmentHitsSphere(Vector3 a, Vector3 b, Vector3 c, float r)
    {
        var ab = b - a;
        var t = Mathf.Clamp01(Vector3.Dot(c - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return (a + ab * t - c).sqrMagnitude < r * r;
    }

    // MonoBehaviour implementation

    void Update()
    {
        var dt = Time.deltaTime;
        var hitCenter = Mech.HitCenter;
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var b = _active[i];
            var prev = b.Position;
            b.Position += b.Velocity * dt;
            b.Age += dt;
            b.Transform.position = b.Position;

            var remove = b.Age > Lifetime;
            if (Mech.IsAlive && SegmentHitsSphere(prev, b.Position, hitCenter, Mech.HitRadius))
            {
                Mech.Damage(b.Damage);
                FxManager.Instance.HitSpark(b.Position, 2.5f);
                CameraController.Shake(0.55f);
                remove = true;
            }

            if (remove)
            {
                b.Transform.gameObject.SetActive(false);
                _active.RemoveAt(i);
            }
        }
    }
}

} // namespace Salvonix
