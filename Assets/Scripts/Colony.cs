using UnityEngine;

namespace Salvonix {

// Dimensions of the cylindrical colony. The cylinder axis is the world Z axis.
public static class Colony
{
    public const float Radius = 1000;
    public const float Length = 7000;

    // Volume the player is allowed to fly in (keeps clear of the ground,
    // buildings, clouds and end-cap hubs)
    public const float FlightRadius = 420;
    public const float FlightHalfLength = 2600;

    // Volume the enemies are allowed to fly in
    public const float EnemyRadius = 600;
    public const float EnemyHalfLength = 3000;

    public static Vector3 ClampToFlightVolume(Vector3 p, float margin = 0)
      => ClampToVolume(p, FlightRadius - margin, FlightHalfLength - margin);

    public static Vector3 ClampToEnemyVolume(Vector3 p)
      => ClampToVolume(p, EnemyRadius, EnemyHalfLength);

    public static bool InFlightVolume(Vector3 p, float margin = 0)
      => new Vector2(p.x, p.y).magnitude < FlightRadius - margin &&
         Mathf.Abs(p.z) < FlightHalfLength - margin;

    static Vector3 ClampToVolume(Vector3 p, float radius, float halfLength)
    {
        var r = new Vector2(p.x, p.y);
        if (r.magnitude > radius) r = r.normalized * radius;
        return new Vector3(r.x, r.y, Mathf.Clamp(p.z, -halfLength, halfLength));
    }

    // Direction from the axis toward the ground at the given point
    public static Vector3 GroundDirection(Vector3 p)
    {
        var r = new Vector3(p.x, p.y, 0);
        return r.sqrMagnitude > 1e-4f ? r.normalized : Vector3.down;
    }
}

} // namespace Salvonix
