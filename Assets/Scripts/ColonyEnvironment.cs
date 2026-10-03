using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Salvonix {

// Atmosphere and environment lighting of the colony interior: haze fog,
// flat ambient light and a reflection probe captured once the colony exists.
public sealed class ColonyEnvironment : MonoBehaviour
{
    [field:SerializeField] public Color FogColor { get; set; } = new(0.63f, 0.71f, 0.8f);
    [field:SerializeField] public float FogDensity { get; set; } = 0.0002f;
    [field:SerializeField, ColorUsage(false, true)] public Color Ambient { get; set; } = new(0.55f, 0.6f, 0.68f);
    [field:SerializeField] public ReflectionProbe Probe { get; set; }

    void Awake()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = FogDensity;
        RenderSettings.fogColor = FogColor;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Ambient;
        var sh = new SphericalHarmonicsL2();
        sh.AddAmbientLight(Ambient);
        RenderSettings.ambientProbe = sh;
    }

    IEnumerator Start()
    {
        yield return null;
        if (Probe != null) Probe.RenderProbe();
    }
}

} // namespace Salvonix
