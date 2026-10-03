using UnityEngine;

namespace Salvonix {

// Homing micro missile. Flies along a cubic Bezier from the launcher to the
// (moving) target, with a two-frequency spiral wrapped around the path so
// that a salvo weaves and intertwines like anime missile barrages.
public sealed class Missile : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public TrailRenderer Trail { get; set; }
    [field:SerializeField] public GameObject Body { get; set; }

    public bool InFlight { get; private set; }

    // Public methods

    public void Launch(Vector3 position, Vector3 launchDir, Enemy target, Salvo salvo)
    {
        _target = target;
        _generation = target.Generation;
        _targetPos = target.Position;
        _salvo = salvo;
        _p0 = position;
        _prevPos = position;
        _time = 0;

        var dist = Vector3.Distance(position, _targetPos);
        _duration = 0.55f + dist / Random.Range(230f, 300f);
        _c1 = launchDir.normalized * Mathf.Clamp(dist * Random.Range(0.35f, 0.6f), 50, 220);
        _c2 = Random.onUnitSphere * dist * Random.Range(0.25f, 0.55f);

        _amp1 = Random.Range(14f, 38f);
        _freq1 = Random.Range(3.5f, 7f) * (Random.value < 0.5f ? -1 : 1);
        _phase1 = Random.Range(0, Mathf.PI * 2);
        _amp2 = Random.Range(1.5f, 4.5f);
        _freq2 = Random.Range(14f, 22f);
        _phase2 = Random.Range(0, Mathf.PI * 2);
        _frameU = Vector3.Cross(launchDir, Random.onUnitSphere).normalized;

        transform.SetPositionAndRotation(position, Quaternion.LookRotation(launchDir));
        Body.SetActive(true);
        gameObject.SetActive(true);
        Trail.Clear();
        Trail.emitting = true;
        InFlight = true;
        _fade = 0;
    }

    // Private members

    Enemy _target;
    int _generation;
    Vector3 _targetPos;
    Salvo _salvo;
    Vector3 _p0, _c1, _c2;
    Vector3 _prevPos;
    Vector3 _frameU;
    float _time, _duration;
    float _amp1, _freq1, _phase1;
    float _amp2, _freq2, _phase2;
    float _fade;

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        var u = 1 - t;
        return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
    }

    static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        var u = 1 - t;
        return 3 * u * u * (b - a) + 6 * u * t * (c - b) + 3 * t * t * (d - c);
    }

    void Detonate()
    {
        InFlight = false;
        Trail.emitting = false;
        Body.SetActive(false);
        _fade = Trail.time;

        var killed = false;
        if (_target != null && _target.IsAliveAs(_generation))
        {
            _target.PendingHits = Mathf.Max(0, _target.PendingHits - 1);
            killed = _target.Hit(1);
            if (killed) _salvo.RegisterKill(_target);
        }
        else
        {
            FxManager.Instance.Explosion(transform.position, 0.6f);
        }
        _salvo.RegisterDetonation();
    }

    // MonoBehaviour implementation

    void Update()
    {
        var dt = Time.deltaTime;
        if (!InFlight)
        {
            _fade -= dt;
            if (_fade <= 0) gameObject.SetActive(false);
            return;
        }

        _time += dt;
        var t = Mathf.Clamp01(_time / _duration);
        if (_target != null && _target.IsAliveAs(_generation)) _targetPos = _target.Position;

        // Accelerating progress along the path
        var s = 0.18f * t + 0.82f * t * t;
        var p3 = _targetPos;
        var p2 = p3 + _c2;
        var p1 = _p0 + _c1;
        var basePos = Bezier(_p0, p1, p2, p3, s);
        var tangent = BezierTangent(_p0, p1, p2, p3, s);
        if (tangent.sqrMagnitude < 1e-6f) tangent = transform.forward;
        tangent.Normalize();

        // Parallel-transported frame around the path
        _frameU = Vector3.ProjectOnPlane(_frameU, tangent);
        if (_frameU.sqrMagnitude < 1e-6f) _frameU = Vector3.Cross(tangent, Vector3.up);
        _frameU.Normalize();
        var frameV = Vector3.Cross(tangent, _frameU);

        // Spiral weave (vanishes at both ends of the path)
        var env = Mathf.Sin(Mathf.PI * s);
        var a1 = _phase1 + _freq1 * _time;
        var a2 = _phase2 + _freq2 * _time;
        var offset = (_frameU * Mathf.Cos(a1) + frameV * Mathf.Sin(a1)) * _amp1 * env
                   + (_frameU * Mathf.Cos(a2) + frameV * Mathf.Sin(a2)) * _amp2 * env;

        var pos = basePos + offset;
        var delta = pos - _prevPos;
        var rot = delta.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(delta) : transform.rotation;
        transform.SetPositionAndRotation(pos, rot);
        _prevPos = pos;

        if (t >= 1) Detonate();
    }
}

} // namespace Salvonix
