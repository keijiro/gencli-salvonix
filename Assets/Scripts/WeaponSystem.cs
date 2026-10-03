using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Salvonix {

// A volley of missiles fired with one click. Collects kills to award the
// multi-lock bonus once every missile has detonated.
public sealed class Salvo
{
    public int Missiles { get; }
    public int Kills { get; private set; }

    public Salvo(GameManager game, int missiles)
    {
        _game = game;
        Missiles = missiles;
        _remaining = missiles;
    }

    public void RegisterKill(Enemy enemy)
    {
        Kills++;
        _game.AddKill(enemy);
    }

    public void RegisterDetonation()
    {
        if (--_remaining == 0) _game.CompleteSalvo(this);
    }

    readonly GameManager _game;
    int _remaining;
}

// Cursor lock-on (hover to lock, up to 12) and missile launching.
// Firing starts a reload period during which no locks can be made.
[DefaultExecutionOrder(100)]
public sealed class WeaponSystem : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public PlayerMech Mech { get; set; }
    [field:SerializeField] public CameraController CameraController { get; set; }
    [field:SerializeField] public EnemyManager Enemies { get; set; }
    [field:SerializeField] public GameManager Game { get; set; }
    [field:SerializeField] public Missile MissilePrefab { get; set; }
    [field:SerializeField] public int MaxLocks { get; set; } = 12;
    [field:SerializeField] public float ReloadTime { get; set; } = 3.2f;
    [field:SerializeField] public float LockRadius { get; set; } = 0.045f;
    [field:SerializeField] public float LockRange { get; set; } = 1300;
    [field:SerializeField] public float LaunchInterval { get; set; } = 0.04f;

    public struct LockInfo
    {
        public Enemy Target;
        public int Generation;
        public float Time;
        public bool Valid => Target.IsAliveAs(Generation);
    }

    public IReadOnlyList<LockInfo> Locks => _locks;
    public bool Reloading => _reloadTimer > 0;
    public float ReloadProgress => 1 - Mathf.Clamp01(_reloadTimer / ReloadTime);
    public bool ControlsEnabled { get; set; }
    public Vector2 Cursor { get; private set; }

    // Public methods

    public void ResetState()
    {
        _locks.Clear();
        _queue.Clear();
        _reloadTimer = 0;
    }

    public int CountLocks(Enemy e)
    {
        var n = 0;
        foreach (var l in _locks) if (l.Target == e) n++;
        return n;
    }

    // Private members

    struct Launch
    {
        public float Delay;
        public int Index;
        public Enemy Target;
        public Salvo Salvo;
    }

    readonly List<LockInfo> _locks = new();
    readonly List<Launch> _queue = new();
    readonly List<Missile> _pool = new();
    float _reloadTimer;

    Missile GetMissile()
    {
        foreach (var m in _pool) if (!m.gameObject.activeSelf) return m;
        var inst = Instantiate(MissilePrefab, transform);
        inst.gameObject.SetActive(false);
        _pool.Add(inst);
        return inst;
    }

    float LastLockTime(Enemy e)
    {
        var t = -100f;
        foreach (var l in _locks) if (l.Target == e) t = Mathf.Max(t, l.Time);
        return t;
    }

    void TryLock()
    {
        var cam = CameraController.Camera;
        var tanHalf = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2);
        var baseRadius = Screen.height * LockRadius;
        foreach (var e in Enemies.Active)
        {
            if (_locks.Count >= MaxLocks) return;
            if (!e.Alive) continue;
            var claims = CountLocks(e);
            if (e.Unclaimed - claims <= 0) continue;
            if (claims > 0 && Time.time - LastLockTime(e) < 0.18f) continue;
            var sp = cam.WorldToScreenPoint(e.Position);
            if (sp.z <= 0 || sp.z > LockRange) continue;
            var r = baseRadius + e.Radius / (sp.z * tanHalf) * Screen.height * 0.5f;
            if (((Vector2)sp - Cursor).sqrMagnitude > r * r) continue;
            _locks.Add(new LockInfo { Target = e, Generation = e.Generation, Time = Time.time });
            AudioManager.Instance.PlayLock(_locks.Count);
        }
    }

    void Fire()
    {
        var salvo = new Salvo(Game, _locks.Count);
        for (var i = 0; i < _locks.Count; i++)
        {
            var target = _locks[i].Target;
            target.PendingHits++;
            _queue.Add(new Launch { Delay = i * LaunchInterval, Index = i, Target = target, Salvo = salvo });
        }
        _locks.Clear();
        _reloadTimer = ReloadTime;
        AudioManager.Instance.PlayLaunch();
    }

    void ProcessQueue(float dt)
    {
        for (var i = _queue.Count - 1; i >= 0; i--)
        {
            var q = _queue[i];
            q.Delay -= dt;
            if (q.Delay > 0) { _queue[i] = q; continue; }
            _queue.RemoveAt(i);

            var points = Mech.LaunchPoints;
            var point = points[q.Index % points.Length];
            var mt = Mech.transform;
            var side = Mathf.Sign(mt.InverseTransformPoint(point.position).x);
            var dir = mt.forward * Random.Range(0.6f, 1.2f)
                    + mt.up * Random.Range(0.3f, 1.3f)
                    + mt.right * side * Random.Range(0.2f, 1.2f)
                    + Random.insideUnitSphere * 0.35f;
            GetMissile().Launch(point.position, dir, q.Target, q.Salvo);
            FxManager.Instance.LaunchPuff(point.position, dir.normalized);
        }
    }

    // MonoBehaviour implementation

    void LateUpdate()
    {
        var dt = Time.deltaTime;
        _locks.RemoveAll(l => !l.Valid);
        ProcessQueue(dt);

        if (_reloadTimer > 0)
        {
            _reloadTimer -= dt;
            if (_reloadTimer <= 0 && ControlsEnabled) AudioManager.Instance.PlayReload();
        }

        var mouse = Mouse.current;
        if (mouse != null) Cursor = mouse.position.ReadValue();
        if (!ControlsEnabled || mouse == null || !Mech.IsAlive) return;

        if (!Reloading) TryLock();
        if (mouse.leftButton.wasPressedThisFrame && _locks.Count > 0 && !Reloading) Fire();
    }
}

} // namespace Salvonix
