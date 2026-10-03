using UnityEngine;

namespace Salvonix {

public enum EnemyKind { Drone, Heavy }

// Enemy unit. Holds a slot around the swarm center, wanders, occasionally
// makes attack runs at the player and fires energy bolts.
public sealed class Enemy : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public EnemyKind Kind { get; set; }
    [field:SerializeField] public int MaxHp { get; set; } = 1;
    [field:SerializeField] public float Radius { get; set; } = 7;
    [field:SerializeField] public float Speed { get; set; } = 110;
    [field:SerializeField] public float Agility { get; set; } = 2.2f;
    [field:SerializeField] public int Score { get; set; } = 100;
    [field:SerializeField] public float SlotRadius { get; set; } = 150;

    public int Hp { get; private set; }
    public int PendingHits { get; set; }
    public int Generation { get; private set; }
    public bool Alive { get; private set; }
    public Vector3 Position => transform.position;
    public Vector3 Velocity => _velocity;

    // Remaining hits not yet claimed by missiles in flight
    public int Unclaimed => Hp - PendingHits;

    public bool IsAliveAs(int generation) => Alive && Generation == generation;

    // Public methods

    public void Spawn(EnemyManager manager, Vector3 position, Vector3 velocity)
    {
        _manager = manager;
        transform.position = position;
        _velocity = velocity;
        Hp = MaxHp;
        PendingHits = 0;
        Generation++;
        Alive = true;
        _seed = Random.value * 100;
        _entering = true;
        _attacking = false;
        _stateTimer = Random.Range(4f, 8f);
        _fireTimer = manager.NextFireInterval(Kind) + 1.5f;
        _burst = 0;
        PickSlot();
        gameObject.SetActive(true);
    }

    public void Despawn()
    {
        Alive = false;
        gameObject.SetActive(false);
    }

    // Returns true when destroyed
    public bool Hit(int damage)
    {
        if (!Alive) return false;
        Hp -= damage;
        if (Hp > 0)
        {
            FxManager.Instance.HitSpark(Position, 1.5f);
            return false;
        }
        FxManager.Instance.Explosion(Position, Kind == EnemyKind.Heavy ? 2.4f : 1.1f);
        AudioManager.Instance.PlayExplosion(Kind == EnemyKind.Heavy ? 1.2f : 1);
        Despawn();
        return true;
    }

    // Private members

    EnemyManager _manager;
    Vector3 _velocity;
    Vector3 _slot;
    Vector3 _attackTarget;
    float _seed;
    float _stateTimer;
    float _fireTimer;
    bool _entering;
    bool _attacking;
    int _burst;
    float _burstTimer;
    Vector3 _burstDir;

    void PickSlot()
      => _slot = Vector3.Scale(Random.insideUnitSphere, new Vector3(1, 0.75f, 1)) * SlotRadius;

    Vector3 Wander()
    {
        var t = Time.time * 0.35f + _seed;
        return new Vector3(Mathf.PerlinNoise(t, _seed) - 0.5f,
                           Mathf.PerlinNoise(_seed, t) - 0.5f,
                           Mathf.PerlinNoise(t + 13, _seed + 7) - 0.5f) * 70;
    }

    // MonoBehaviour implementation

    void Update()
    {
        if (!Alive) return;
        var dt = Time.deltaTime;
        if (dt <= 0) return;

        var player = _manager.Mech;
        var center = _manager.Director.SwarmCenter;

        // Behavior state
        _stateTimer -= dt;
        Vector3 target;
        var speed = Speed;
        if (_attacking)
        {
            target = _attackTarget;
            speed *= 1.6f;
            if (_stateTimer <= 0 || (Position - _attackTarget).sqrMagnitude < 50 * 50)
            {
                _attacking = false;
                _stateTimer = Random.Range(4f, 8f);
                PickSlot();
            }
        }
        else
        {
            target = center + _slot + Wander();
            if (_entering && (Position - target).sqrMagnitude < 120 * 120) _entering = false;
            if (_entering) speed *= 1.8f;
            if (_stateTimer <= 0)
            {
                _stateTimer = Random.Range(4f, 8f);
                if (!_entering && Random.value < _manager.AttackProbability)
                {
                    _attacking = true;
                    _stateTimer = 4;
                    var toPlayer = player.transform.position - Position;
                    var side = Vector3.ProjectOnPlane(Random.onUnitSphere, toPlayer.normalized).normalized;
                    _attackTarget = player.transform.position + side * Random.Range(45f, 80f)
                                  + toPlayer.normalized * 60;
                }
                else
                {
                    PickSlot();
                }
            }
        }

        // Steering
        var toTarget = target - Position;
        var dist = toTarget.magnitude;
        var desired = dist > 1e-3f ? toTarget / dist * speed * Mathf.Min(1, dist / 80) : Vector3.zero;
        _velocity = Vector3.Lerp(_velocity, desired, 1 - Mathf.Exp(-Agility * dt));
        var pos = Colony.ClampToEnemyVolume(Position + _velocity * dt);

        // Face the player, banking slightly with the motion
        var camUp = _manager.Director.Rotation * Vector3.up;
        var toMech = player.transform.position - pos;
        var bank = Vector3.Cross(toMech.normalized, camUp);
        var up = camUp + bank * Mathf.Clamp(Vector3.Dot(_velocity, bank) * 0.006f, -0.6f, 0.6f);
        var look = Quaternion.LookRotation(toMech, up);
        transform.SetPositionAndRotation(pos, Quaternion.Slerp(transform.rotation, look, 1 - Mathf.Exp(-4 * dt)));

        UpdateFiring(dt, toMech);
    }

    void UpdateFiring(float dt, Vector3 toMech)
    {
        if (_burst > 0)
        {
            _burstTimer -= dt;
            if (_burstTimer <= 0)
            {
                var spread = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-3f, 3f), 0);
                _manager.Bullets.Fire(Position + _burstDir * Radius, spread * _burstDir, _manager.BulletSpeed, 10);
                _burst--;
                _burstTimer = 0.09f;
            }
            return;
        }

        if (!_manager.FiringEnabled || _entering) return;
        _fireTimer -= dt;
        if (_fireTimer > 0) return;
        _fireTimer = _manager.NextFireInterval(Kind);

        // Only fire while visible and within range
        if (toMech.sqrMagnitude > 900 * 900) return;
        var vp = _manager.Camera.WorldToViewportPoint(Position);
        if (vp.z < 0 || vp.x < 0.03f || vp.x > 0.97f || vp.y < 0.03f || vp.y > 0.97f) return;

        // Aim with partial lead
        var mech = _manager.Mech;
        var aim = mech.HitCenter;
        var tof = toMech.magnitude / _manager.BulletSpeed;
        aim += mech.Velocity * tof * 0.5f;
        var dir = (aim - Position).normalized;

        if (Kind == EnemyKind.Heavy)
        {
            _burst = 5;
            _burstTimer = 0;
            _burstDir = dir;
        }
        else
        {
            _manager.Bullets.Fire(Position + dir * Radius, dir, _manager.BulletSpeed, 8);
        }
    }
}

} // namespace Salvonix
