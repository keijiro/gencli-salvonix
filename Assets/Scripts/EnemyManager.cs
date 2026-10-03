using System.Collections.Generic;
using UnityEngine;

namespace Salvonix {

// Spawns and pools enemies. Population, aggression and fire rate scale with
// the difficulty level.
public sealed class EnemyManager : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public Enemy DronePrefab { get; set; }
    [field:SerializeField] public Enemy HeavyPrefab { get; set; }
    [field:SerializeField] public FlightDirector Director { get; set; }
    [field:SerializeField] public PlayerMech Mech { get; set; }
    [field:SerializeField] public BulletManager Bullets { get; set; }
    [field:SerializeField] public Camera Camera { get; set; }

    public List<Enemy> Active { get; } = new();
    public float Level { get; set; } = 1;
    public bool FiringEnabled { get; set; }
    public bool DemoMode { get; set; } = true;

    public float BulletSpeed => Mathf.Min(115 + (Level - 1) * 9, 230);
    public float AttackProbability => DemoMode ? 0 : Mathf.Min(0.08f + (Level - 1) * 0.04f, 0.45f);

    int TargetDrones => DemoMode ? 8 : Mathf.Min(7 + Mathf.FloorToInt((Level - 1) * 2.6f), 44);
    int TargetHeavies => DemoMode || Level < 3 ? 0 : Mathf.Min(1 + Mathf.FloorToInt((Level - 3) * 0.7f), 6);

    // Public methods

    public float NextFireInterval(EnemyKind kind)
    {
        var scale = 1 + (Level - 1) * 0.15f;
        return kind == EnemyKind.Heavy ? Random.Range(4.0f, 6.0f) / scale
                                       : Random.Range(4.5f, 8.0f) / scale;
    }

    public void Clear()
    {
        foreach (var e in Active) e.Despawn();
        Active.Clear();
        _spawnTimer = 0.5f;
        _heavyTimer = 6;
    }

    // Private members

    readonly List<Enemy> _dronePool = new();
    readonly List<Enemy> _heavyPool = new();
    float _spawnTimer = 0.5f;
    float _heavyTimer = 6;

    Enemy Get(Enemy prefab, List<Enemy> pool)
    {
        foreach (var e in pool) if (!e.gameObject.activeSelf) return e;
        var inst = Instantiate(prefab, transform);
        inst.gameObject.SetActive(false);
        pool.Add(inst);
        return inst;
    }

    int Count(EnemyKind kind)
    {
        var n = 0;
        foreach (var e in Active) if (e.Kind == kind) n++;
        return n;
    }

    Vector3 SpawnOrigin()
    {
        // Behind the swarm as seen from the player, so arrivals are visible
        var c = Director.SwarmCenter;
        var away = (c - Director.Position).normalized;
        var dir = Vector3.Slerp(away, Random.onUnitSphere, Random.Range(0.1f, 0.45f)).normalized;
        return Colony.ClampToEnemyVolume(c + dir * Random.Range(450f, 650f));
    }

    void SpawnWave(int count)
    {
        var origin = SpawnOrigin();
        var vel = (Director.SwarmCenter - origin).normalized * 120;
        for (var i = 0; i < count; i++)
        {
            var e = Get(DronePrefab, _dronePool);
            e.Spawn(this, origin + Random.insideUnitSphere * 40, vel);
            Active.Add(e);
        }
    }

    void SpawnHeavy()
    {
        var origin = SpawnOrigin();
        var e = Get(HeavyPrefab, _heavyPool);
        e.Spawn(this, origin, (Director.SwarmCenter - origin).normalized * 60);
        Active.Add(e);
    }

    // MonoBehaviour implementation

    void Update()
    {
        Active.RemoveAll(e => !e.Alive);

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0)
        {
            var missing = TargetDrones - Count(EnemyKind.Drone);
            if (missing > 0) SpawnWave(Mathf.Min(missing, Random.Range(4, 9)));
            _spawnTimer = Random.Range(1.5f, 3.5f) / (1 + (Level - 1) * 0.12f);
        }

        _heavyTimer -= Time.deltaTime;
        if (_heavyTimer <= 0)
        {
            if (Count(EnemyKind.Heavy) < TargetHeavies) SpawnHeavy();
            _heavyTimer = Random.Range(5f, 9f);
        }
    }
}

} // namespace Salvonix
