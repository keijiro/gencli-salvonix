using UnityEngine;

namespace Salvonix {

// Automatic flight control of the player rig. Alternates between hovering
// (facing the enemy swarm) and dynamic maneuvers that swing around the swarm
// with free rolling, keeping inside the colony's flight volume.
[DefaultExecutionOrder(-100)]
public sealed class FlightDirector : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public Vector2 HoverDuration { get; set; } = new(4.5f, 7);
    [field:SerializeField] public Vector2 ManeuverDuration { get; set; } = new(2.6f, 3.6f);
    [field:SerializeField] public Vector2 OrbitDistance { get; set; } = new(200, 290);
    [field:SerializeField, Range(0, 1)] public float AxialViewProbability { get; set; } = 0.6f;
    [field:SerializeField] public float SwarmDriftSpeed { get; set; } = 16;
    [field:SerializeField] public float SwarmWanderRadius { get; set; } = 170;
    [field:SerializeField, Range(0, 1)] public float DashProbability { get; set; } = 0.3f;
    [field:SerializeField] public float MaxTurnRate { get; set; } = 150;

    public Vector3 Position { get; private set; }
    public Quaternion Rotation { get; private set; } = Quaternion.identity;
    public Vector3 Velocity { get; private set; }
    public Vector3 SwarmCenter { get; private set; }
    public bool IsManeuvering { get; private set; }

    // 0 while hovering, peaks in the middle of a maneuver
    public float Intensity { get; private set; }

    public event System.Action<bool> ManeuverStarted; // arg: dash maneuver

    // Private members

    float _timer;
    float _duration;
    float _swarmDir = 1;
    float _swarmTime;

    // Hover state
    Vector3 _hoverOffset;

    // Maneuver state
    Vector3 _d0, _d1;
    Vector3 _swoop;
    float _dip;
    float _velocityWeight;
    Vector3 _up0, _up1;
    float _rollExtra;
    bool _barrelRoll;

    static float SmootherStep(float x)
      => x * x * x * (x * (x * 6 - 15) + 10);

    static float RandomRange(Vector2 r) => Random.Range(r.x, r.y);

    // MonoBehaviour implementation

    void Start()
    {
        _swarmTime = Random.value * 100;
        SwarmCenter = new Vector3(0, 0, -1200);
        _hoverOffset = new Vector3(60, 40, -OrbitDistance.y);
        Position = SwarmCenter + _hoverOffset;
        Rotation = Quaternion.LookRotation(-_hoverOffset, Vector3.up);
        BeginHover();
    }

    void Update()
    {
        var dt = Time.deltaTime;
        if (dt <= 0) return;

        UpdateSwarmCenter(dt);

        var prevPos = Position;
        _timer += dt;

        Vector3 pos;
        Quaternion target;
        float response;

        if (IsManeuvering)
        {
            var t = Mathf.Clamp01(_timer / _duration);
            var e = SmootherStep(t);
            var bump = Mathf.Sin(Mathf.PI * e);

            // Swing around the swarm center
            var dir = Vector3.Slerp(_d0.normalized, _d1.normalized, e);
            var dist = Mathf.Lerp(_d0.magnitude, _d1.magnitude, e) * (1 - _dip * bump);
            pos = SwarmCenter + dir * dist + _swoop * bump;
            pos = Colony.ClampToFlightVolume(pos);

            // Look toward the swarm, blended with the travel direction
            var toCenter = (SwarmCenter - pos).normalized;
            var vel = (pos - prevPos) / dt;
            var look = toCenter;
            if (vel.sqrMagnitude > 1)
            {
                var w = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 2) * _velocityWeight;
                look = Vector3.Slerp(toCenter, vel.normalized, w);
            }

            // Roll
            var up = Vector3.Slerp(_up0, _up1, e);
            var roll = _barrelRoll ? _rollExtra * e : _rollExtra * bump;
            up = Quaternion.AngleAxis(roll, look) * up;
            target = Quaternion.LookRotation(look, up);
            response = 7;

            Intensity = bump;
            if (t >= 1) BeginHover(pos);
        }
        else
        {
            // Hover with a gentle drift, keeping the swarm in front
            var n = _timer * 0.25f;
            var drift = Rotation * new Vector3((Mathf.PerlinNoise(n, 3.1f) - 0.5f) * 14,
                                               (Mathf.PerlinNoise(7.7f, n) - 0.5f) * 10, 0);
            drift *= Mathf.SmoothStep(0, 1, _timer / 1.5f);
            pos = Colony.ClampToFlightVolume(SwarmCenter + _hoverOffset + drift);
            var look = (SwarmCenter - pos).normalized;
            var up = Vector3.ProjectOnPlane(Rotation * Vector3.up, look);
            target = Quaternion.LookRotation(look, up);
            response = 3;
            Intensity = 0;
            if (_timer >= _duration) BeginManeuver(pos);
        }

        Position = pos;
        var smoothed = Quaternion.Slerp(Rotation, target, 1 - Mathf.Exp(-response * dt));
        Rotation = Quaternion.RotateTowards(Rotation, smoothed, MaxTurnRate * dt);
        Velocity = Vector3.Lerp(Velocity, (pos - prevPos) / dt, 1 - Mathf.Exp(-12 * dt));
    }

    // Swarm center: drifts along the colony axis and wanders around it

    void UpdateSwarmCenter(float dt)
    {
        _swarmTime += dt;
        var c = SwarmCenter;
        c.z += _swarmDir * SwarmDriftSpeed * dt;
        if (c.z > Colony.FlightHalfLength - 500) _swarmDir = -1;
        if (c.z < -Colony.FlightHalfLength + 500) _swarmDir = 1;
        var t = _swarmTime * 0.045f;
        c.x = (Mathf.PerlinNoise(t, 1.3f) - 0.5f) * 2 * SwarmWanderRadius;
        c.y = (Mathf.PerlinNoise(5.9f, t) - 0.5f) * 2 * SwarmWanderRadius;
        SwarmCenter = c;
    }

    // Phase transitions

    void BeginHover(Vector3? pos = null)
    {
        IsManeuvering = false;
        _timer = 0;
        _duration = RandomRange(HoverDuration);
        if (pos.HasValue) _hoverOffset = pos.Value - SwarmCenter;
    }

    void BeginManeuver(Vector3 pos)
    {
        var dash = Random.value < DashProbability;
        _d0 = pos - SwarmCenter;
        var d0n = _d0.normalized;

        // Pick a destination around the swarm inside the flight volume.
        // Views along the colony axis are favored (the most spectacular).
        var axial = Random.value < AxialViewProbability;
        var found = false;
        for (var i = 0; i < 64 && !found; i++)
        {
            var axis = Vector3.ProjectOnPlane(Random.onUnitSphere, d0n).normalized;
            var angle = dash ? Random.Range(140f, 175f) : Random.Range(55f, 130f);
            var dist = RandomRange(OrbitDistance);
            _d1 = Quaternion.AngleAxis(angle, axis) * d0n * dist;
            found = Colony.InFlightVolume(SwarmCenter + _d1, 40);
            if (found && axial && i < 48) found = Mathf.Abs(_d1.normalized.z) > 0.6f;
        }
        if (!found)
        {
            var dest = Colony.ClampToFlightVolume(SwarmCenter - _d0, 60);
            _d1 = dest - SwarmCenter;
        }

        // Path shape
        var normal = Vector3.Cross(_d0, _d1).normalized;
        if (normal.sqrMagnitude < 0.5f) normal = Vector3.ProjectOnPlane(Random.onUnitSphere, d0n).normalized;
        var swoopSign = Random.value < 0.5f ? -1 : 1;
        _swoop = normal * swoopSign * (dash ? Random.Range(70f, 110f) : Random.Range(50f, 130f));
        _dip = dash ? Random.Range(0.3f, 0.45f) : Random.Range(-0.15f, 0.2f);
        _velocityWeight = dash ? 0.85f : 0.4f;

        // Orientation: roll to a new "up" chosen relative to the current one
        _up0 = Rotation * Vector3.up;
        var finalLook = -_d1.normalized;
        var upProj = Vector3.ProjectOnPlane(_up0, finalLook);
        if (upProj.sqrMagnitude < 1e-3f) upProj = Vector3.ProjectOnPlane(-Colony.GroundDirection(pos), finalLook);
        upProj.Normalize();
        if (Random.value < 0.25f)
        {
            // Occasionally settle with the ground below
            var groundUp = Vector3.ProjectOnPlane(-Colony.GroundDirection(SwarmCenter + _d1), finalLook);
            if (groundUp.sqrMagnitude > 1e-3f) upProj = groundUp.normalized;
        }
        else
        {
            var sign = Random.value < 0.5f ? -1 : 1;
            upProj = Quaternion.AngleAxis(sign * Random.Range(25f, 110f), finalLook) * upProj;
        }
        _up1 = upProj;

        _barrelRoll = dash && Random.value < 0.4f;
        _rollExtra = _barrelRoll ? (Random.value < 0.5f ? -360 : 360) : Random.Range(-60f, 60f);

        IsManeuvering = true;
        _timer = 0;
        _duration = RandomRange(ManeuverDuration) * (dash ? 1.15f : 1);
        ManeuverStarted?.Invoke(dash);
    }
}

} // namespace Salvonix
