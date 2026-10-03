using UnityEngine;
using UnityEngine.InputSystem;

namespace Salvonix {

// The player's mech: follows the flight director, applies the WASD evasion
// offset, leans with its motion, drives the thruster effects and holds HP.
[DefaultExecutionOrder(-90)]
public sealed class PlayerMech : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public FlightDirector Director { get; set; }
    [field:SerializeField] public Transform Model { get; set; }
    [field:SerializeField] public Transform[] LaunchPoints { get; set; }
    [field:SerializeField] public Renderer[] Thrusters { get; set; }
    [field:SerializeField] public float MaxHp { get; set; } = 100;
    [field:SerializeField] public float DodgeRange { get; set; } = 16;
    [field:SerializeField] public float DodgeResponse { get; set; } = 7;
    [field:SerializeField] public float HitRadius { get; set; } = 6;
    [field:SerializeField] public Vector3 HitCenterOffset { get; set; } = new(0, 2, 0);
    [field:SerializeField] public float BaseLean { get; set; } = 12;
    [field:SerializeField] public float RegenDelay { get; set; } = 5;
    [field:SerializeField] public float RegenRate { get; set; } = 2.5f;

    public float Hp { get; private set; }
    public bool IsAlive => Hp > 0;
    public bool ControlsEnabled { get; set; }
    public Vector2 Dodge => _dodge;
    public Vector3 Velocity { get; private set; }
    public float Boost => _boost;
    public Vector3 HitCenter => transform.TransformPoint(HitCenterOffset);
    public float LastHitTime { get; private set; } = -100;

    public event System.Action<float> Damaged;

    // Public methods

    public void ResetState()
    {
        Hp = MaxHp;
        LastHitTime = -100;
        _dodge = Vector2.zero;
        Model.gameObject.SetActive(true);
    }

    public void SetVisible(bool visible) => Model.gameObject.SetActive(visible);

    public void Damage(float amount)
    {
        if (!IsAlive || Time.time - LastHitTime < 0.25f) return;
        Hp = Mathf.Max(0, Hp - amount);
        LastHitTime = Time.time;
        Damaged?.Invoke(amount);
    }

    // Private members

    Vector2 _dodge;
    Vector3 _prevPos;
    Quaternion _lean = Quaternion.identity;
    MaterialPropertyBlock _mpb;
    float _boost;

    static readonly int IntensityID = Shader.PropertyToID("_Intensity");

    Vector2 ReadInput()
    {
        var kb = Keyboard.current;
        if (kb == null || !ControlsEnabled) return Vector2.zero;
        var v = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1;
        return v.sqrMagnitude > 1 ? v.normalized : v;
    }

    // MonoBehaviour implementation

    void Awake()
    {
        Hp = MaxHp;
        _mpb = new MaterialPropertyBlock();
    }

    void Start()
      => _prevPos = transform.position;

    void Update()
    {
        var dt = Time.deltaTime;
        if (dt <= 0) return;

        // Self repair after a while without damage
        if (IsAlive && Time.time - LastHitTime > RegenDelay)
            Hp = Mathf.Min(MaxHp, Hp + RegenRate * dt);

        // Evasion offset
        var input = ReadInput();
        var prevDodge = _dodge;
        _dodge = Vector2.Lerp(_dodge, input * DodgeRange, 1 - Mathf.Exp(-DodgeResponse * dt));
        var dodgeVel = (_dodge - prevDodge) / dt;

        var rot = Director.Rotation;
        transform.SetPositionAndRotation(Director.Position + rot * (Vector3)_dodge, rot);
        Velocity = (transform.position - _prevPos) / dt;
        _prevPos = transform.position;

        // Lean into the motion
        var lv = Quaternion.Inverse(rot) * Director.Velocity;
        lv += (Vector3)dodgeVel * 2.5f;
        var pitch = BaseLean + Mathf.Clamp(lv.z * 0.1f - lv.y * 0.08f, -30, 30);
        var roll = Mathf.Clamp(-lv.x * 0.12f, -40, 40);
        var yaw = Mathf.Clamp(lv.x * 0.04f, -15, 15);
        var bob = Mathf.Sin(Time.time * 1.4f) * 2;
        var targetLean = Quaternion.Euler(pitch + bob, yaw, roll);
        _lean = Quaternion.Slerp(_lean, targetLean, 1 - Mathf.Exp(-5 * dt));
        Model.localRotation = _lean;
        Model.localPosition = new Vector3(0, Mathf.Sin(Time.time * 1.1f) * 0.35f, 0);

        // Thrusters
        var speed = lv.magnitude;
        var boostTarget = Mathf.Clamp01(speed / 120) + input.magnitude * 0.5f;
        _boost = Mathf.Lerp(_boost, boostTarget, 1 - Mathf.Exp(-8 * dt));
        var flicker = 0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 20, 0);
        _mpb.SetFloat(IntensityID, (0.55f + _boost * 1.2f) * flicker);
        foreach (var r in Thrusters)
        {
            r.SetPropertyBlock(_mpb);
            var s = r.transform.localScale;
            s.z = Mathf.Lerp(3.5f, 11, _boost) * flicker;
            r.transform.localScale = s;
        }
    }
}

} // namespace Salvonix
