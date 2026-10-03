using UnityEngine;

namespace Salvonix {

// Explosion / spark / smoke effects using a few shared particle systems that
// are emitted into on demand.
public sealed class FxManager : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public Material FireMaterial { get; set; }
    [field:SerializeField] public Material SmokeMaterial { get; set; }
    [field:SerializeField] public Material SparkMaterial { get; set; }
    [field:SerializeField] public Material FlashMaterial { get; set; }

    public static FxManager Instance { get; private set; }

    // Public methods

    public void Explosion(Vector3 pos, float scale)
    {
        Emit(_flash, pos, Vector3.zero, 70 * scale, 0.18f, Color.white, 0);
        for (var i = 0; i < 9; i++)
            Emit(_fire, pos + Random.insideUnitSphere * 3 * scale,
                 Random.insideUnitSphere * 22 * scale,
                 Random.Range(12f, 24f) * scale, Random.Range(0.5f, 0.9f),
                 Color.Lerp(new Color(1, 0.85f, 0.6f), Color.white, Random.value), Random.Range(0, 360f));
        for (var i = 0; i < 7; i++)
            Emit(_smoke, pos + Random.insideUnitSphere * 4 * scale,
                 Random.insideUnitSphere * 9 * scale,
                 Random.Range(16f, 30f) * scale, Random.Range(1.8f, 3.2f),
                 new Color(0.42f, 0.4f, 0.4f, 0.85f), Random.Range(0, 360f));
        for (var i = 0; i < 28; i++)
            Emit(_sparks, pos, Random.onUnitSphere * Random.Range(60f, 170f) * Mathf.Sqrt(scale),
                 Random.Range(0.6f, 1.3f) * scale, Random.Range(0.35f, 0.9f),
                 new Color(1, Random.Range(0.55f, 0.85f), 0.3f), 0);
    }

    public void HitSpark(Vector3 pos, float scale)
    {
        Emit(_flash, pos, Vector3.zero, 16 * scale, 0.12f, new Color(0.7f, 0.85f, 1), 0);
        for (var i = 0; i < 14; i++)
            Emit(_sparks, pos, Random.onUnitSphere * Random.Range(40f, 110f),
                 Random.Range(0.4f, 0.8f) * scale, Random.Range(0.2f, 0.5f),
                 new Color(1, 0.9f, 0.7f), 0);
    }

    public void LaunchPuff(Vector3 pos, Vector3 dir)
    {
        Emit(_flash, pos, Vector3.zero, 9, 0.1f, new Color(1, 0.8f, 0.5f), 0);
        for (var i = 0; i < 2; i++)
            Emit(_smoke, pos, -dir * 8 + Random.insideUnitSphere * 4,
                 Random.Range(4f, 7f), Random.Range(0.8f, 1.4f),
                 new Color(0.9f, 0.9f, 0.92f, 0.6f), Random.Range(0, 360f));
    }

    // Private members

    ParticleSystem _fire, _smoke, _sparks, _flash;

    static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size,
                     float life, Color color, float rotation)
    {
        var p = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = vel,
            startSize = size,
            startLifetime = life,
            startColor = color,
            rotation = rotation,
            applyShapeToPosition = false
        };
        ps.Emit(p, 1);
    }

    ParticleSystem CreateSystem(string name, Material material,
                                AnimationCurve size, Gradient color, float drag,
                                bool stretch, bool sortByDistance)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 3000;
        main.startSpeed = 0;
        main.scalingMode = ParticleSystemScalingMode.Shape;

        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1, size);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(color);

        if (drag > 0)
        {
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.drag = drag;
            lv.multiplyDragByParticleSize = false;
            lv.multiplyDragByParticleVelocity = false;
        }

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        if (stretch)
        {
            r.velocityScale = 0.06f;
            r.lengthScale = 1.5f;
        }
        r.sortMode = sortByDistance ? ParticleSystemSortMode.Distance : ParticleSystemSortMode.None;
        r.minParticleSize = 0;
        r.maxParticleSize = 10;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        return ps;
    }

    static Gradient MakeGradient(Color c0, Color c1, float a0, float aMid, float midTime)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c0, 0), new GradientColorKey(c1, 1) },
                  new[] { new GradientAlphaKey(a0, 0), new GradientAlphaKey(aMid, midTime),
                          new GradientAlphaKey(0, 1) });
        return g;
    }

    // MonoBehaviour implementation

    void Awake()
    {
        Instance = this;
        _fire = CreateSystem("Fire", FireMaterial,
            new AnimationCurve(new Keyframe(0, 0.35f), new Keyframe(0.4f, 0.9f), new Keyframe(1, 1.1f)),
            MakeGradient(Color.white, new Color(1, 0.35f, 0.1f), 1, 0.8f, 0.4f), 3, false, false);
        _smoke = CreateSystem("Smoke", SmokeMaterial,
            new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(1, 1.6f)),
            MakeGradient(Color.white, new Color(0.7f, 0.7f, 0.7f), 0, 0.8f, 0.12f), 1.5f, false, true);
        _sparks = CreateSystem("Sparks", SparkMaterial,
            new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0.2f)),
            MakeGradient(Color.white, new Color(1, 0.5f, 0.2f), 1, 1, 0.5f), 2.5f, true, false);
        _flash = CreateSystem("Flash", FlashMaterial,
            new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(1, 1)),
            MakeGradient(Color.white, Color.white, 1, 0.6f, 0.3f), 0, false, false);
    }
}

} // namespace Salvonix
