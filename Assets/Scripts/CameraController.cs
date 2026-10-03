using UnityEngine;
using UnityEngine.InputSystem;

namespace Salvonix {

// Over-the-shoulder chase camera. Follows the flight rig, offsets slightly
// toward the mouse cursor, widens the FOV during maneuvers and shakes.
public sealed class CameraController : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public FlightDirector Director { get; set; }
    [field:SerializeField] public PlayerMech Mech { get; set; }
    [field:SerializeField] public Vector3 Offset { get; set; } = new(13, 7, -42);
    [field:SerializeField] public Vector2 MouseAngles { get; set; } = new(7, 4.5f);
    [field:SerializeField] public float BaseFov { get; set; } = 56;
    [field:SerializeField] public float ManeuverFov { get; set; } = 12;
    [field:SerializeField] public float DodgeFollow { get; set; } = 0.55f;

    public Camera Camera => _camera;

    // Public methods

    public void Shake(float amount)
      => _trauma = Mathf.Clamp01(_trauma + amount);

    // Private members

    Camera _camera;
    Vector2 _mouse;
    float _trauma;
    float _fov;

    // MonoBehaviour implementation

    void Awake()
    {
        _camera = GetComponent<Camera>();
        _fov = BaseFov;
    }

    void LateUpdate()
    {
        var dt = Time.unscaledDeltaTime;

        // Cursor offset in [-1, 1]
        var target = Vector2.zero;
        if (Mouse.current != null && Screen.width > 0)
        {
            var m = Mouse.current.position.ReadValue();
            target = new Vector2(m.x / Screen.width * 2 - 1, m.y / Screen.height * 2 - 1);
            target = Vector2.ClampMagnitude(target, 1.4f);
        }
        _mouse = Vector2.Lerp(_mouse, target, 1 - Mathf.Exp(-4 * dt));

        var rot = Director.Rotation * Quaternion.Euler(-_mouse.y * MouseAngles.y, _mouse.x * MouseAngles.x, 0);
        var pivot = Director.Position + Director.Rotation * (Vector3)(Mech.Dodge * DodgeFollow);
        var pos = pivot + rot * Offset;

        // Shake
        _trauma = Mathf.Max(0, _trauma - dt * 1.6f);
        var shake = _trauma * _trauma;
        if (shake > 0)
        {
            var t = Time.unscaledTime * 25;
            pos += rot * new Vector3(Mathf.PerlinNoise(t, 0) - 0.5f,
                                     Mathf.PerlinNoise(0, t) - 0.5f, 0) * 5 * shake;
            rot *= Quaternion.Euler((Mathf.PerlinNoise(t, 9) - 0.5f) * 6 * shake,
                                    (Mathf.PerlinNoise(9, t) - 0.5f) * 6 * shake,
                                    (Mathf.PerlinNoise(t, t) - 0.5f) * 8 * shake);
        }

        transform.SetPositionAndRotation(pos, rot);

        var fovTarget = BaseFov + ManeuverFov * Director.Intensity;
        _fov = Mathf.Lerp(_fov, fovTarget, 1 - Mathf.Exp(-3 * dt));
        _camera.fieldOfView = _fov;
    }
}

} // namespace Salvonix
