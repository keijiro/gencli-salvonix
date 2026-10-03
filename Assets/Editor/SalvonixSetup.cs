using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Salvonix.Editor {

// Builds the game's materials, prefabs and the main scene from the generated
// assets. Re-runnable: everything is recreated from scratch.
public static class SalvonixSetup
{
    const string MatDir = "Assets/Materials";
    const string PrefabDir = "Assets/Prefabs";
    const string TexDir = "Assets/Generated/Textures";
    const string ModelDir = "Assets/Generated/Models";
    const string AudioDir = "Assets/Generated/Audio";
    const string ScenePath = "Assets/Main.unity";

    static readonly Color FogColor = new(0.63f, 0.71f, 0.8f);

    [MenuItem("Salvonix/Build Scene")]
    public static void Build()
    {
        ConfigureImporters();
        var mats = CreateMaterials();
        var prefabs = CreatePrefabs(mats);
        BuildScene(mats, prefabs);
        ConfigureProject();
        AssetDatabase.SaveAssets();
    }

    // Importers

    static void ConfigureImporters()
    {
        foreach (var name in new[] { "ColonyCity", "ColonyNature", "BuildingFacade", "ColonyEndCap" })
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath($"{TexDir}/{name}.png");
            ti.wrapMode = name == "ColonyEndCap" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.anisoLevel = 16;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
        foreach (var name in new[] { "FxSmoke", "FxFire", "FxCloud", "Logo" })
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath($"{TexDir}/{name}.png");
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = name != "Logo";
            ti.maxTextureSize = name == "Logo" ? 2048 : 1024;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ai = (AudioImporter)AssetImporter.GetAtPath(path);
            var s = ai.defaultSampleSettings;
            var music = path.Contains("bgm_");
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            ai.defaultSampleSettings = s;
            ai.forceToMono = !music;
            ai.SaveAndReimport();
        }
    }

    // Materials

    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");

    static Material Mat(string name, string shader, System.Action<Material> setup)
    {
        var path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = Shader.Find(shader);
        setup(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Dictionary<string, Material> CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets", "Materials");
        var m = new Dictionary<string, Material>();
        m["Surface"] = Mat("ColonySurface", "Salvonix/ColonySurface", x =>
        {
            x.SetTexture("_CityTex", Tex("ColonyCity"));
            x.SetTexture("_NatureTex", Tex("ColonyNature"));
            x.SetFloat("_Radius", Colony.Radius);
            x.SetFloat("_Length", Colony.Length);
            x.SetFloat("_CityTiles", 12);
            x.SetFloat("_NatureTiles", 7);
            x.SetFloat("_StripCount", 6);
            x.SetFloat("_StripWidth", 6);
            x.SetColor("_StripColor", new Color(2.6f, 2.9f, 3.4f));
        });
        m["Structure"] = Mat("ColonyStructure", "Salvonix/Structure", x => { });
        m["EndCap"] = Mat("ColonyEndCap", "Salvonix/EndCap", x => x.SetTexture("_MainTex", Tex("ColonyEndCap")));
        m["Building"] = Mat("ColonyBuilding", "Salvonix/Building", x =>
        {
            x.SetTexture("_FacadeTex", Tex("BuildingFacade"));
            x.SetColor("_RoofColor", new Color(0.74f, 0.76f, 0.78f));
        });
        m["Cloud"] = Mat("Cloud", "Salvonix/AlphaBlend", x =>
        {
            x.SetTexture("_MainTex", Tex("FxCloud"));
            x.SetColor("_Color", new Color(1, 1, 1, 0.9f));
        });
        m["SmokeTrail"] = Mat("MissileSmokeTrail", "Salvonix/SmokeTrail", x => { });
        m["FxFire"] = Mat("FxFire", "Salvonix/Additive", x =>
        {
            x.SetTexture("_MainTex", Tex("FxFire"));
            x.SetFloat("_Mode", 0);
            x.SetColor("_Color", new Color(2.6f, 2.2f, 1.9f));
        });
        m["FxSmoke"] = Mat("FxSmoke", "Salvonix/AlphaBlend", x => x.SetTexture("_MainTex", Tex("FxSmoke")));
        m["FxSpark"] = Mat("FxSpark", "Salvonix/Additive", x =>
        {
            x.SetFloat("_Mode", 1);
            x.SetFloat("_Sharpness", 1.5f);
            x.SetColor("_Color", new Color(6, 4, 2.2f));
        });
        m["FxFlash"] = Mat("FxFlash", "Salvonix/Additive", x =>
        {
            x.SetFloat("_Mode", 1);
            x.SetFloat("_Sharpness", 2.5f);
            x.SetColor("_Color", new Color(3, 2.4f, 1.8f));
        });
        m["EnemyEye"] = Mat("EnemyEye", "Salvonix/GlowBillboard", x =>
        {
            x.SetColor("_Color", new Color(5, 0.35f, 0.2f));
            x.SetFloat("_Sharpness", 3f);
        });
        m["Bullet"] = Mat("EnemyBullet", "Salvonix/GlowBillboard", x =>
        {
            x.SetColor("_Color", new Color(12, 2.2f, 1));
            x.SetFloat("_Sharpness", 1.6f);
        });
        m["BulletTrail"] = Mat("EnemyBulletTrail", "Salvonix/Additive", x =>
        {
            x.SetFloat("_Mode", 2);
            x.SetFloat("_Sharpness", 2);
            x.SetColor("_Color", new Color(7, 1, 0.4f));
        });
        m["MissileGlow"] = Mat("MissileGlow", "Salvonix/GlowBillboard", x =>
        {
            x.SetColor("_Color", new Color(5, 2.4f, 0.8f));
            x.SetFloat("_Sharpness", 3);
        });
        m["MissileFlame"] = Mat("MissileFlame", "Salvonix/FlameBillboard", x =>
        {
            x.SetColor("_CoreColor", new Color(5, 4.5f, 3.2f));
            x.SetColor("_Color", new Color(3.5f, 1.3f, 0.3f));
        });
        m["Thruster"] = Mat("Thruster", "Salvonix/FlameBillboard", x =>
        {
            x.SetColor("_CoreColor", new Color(2.5f, 3.2f, 5));
            x.SetColor("_Color", new Color(0.25f, 0.6f, 3.2f));
        });
        m["ThrusterGlow"] = Mat("ThrusterGlow", "Salvonix/GlowBillboard", x =>
        {
            x.SetColor("_Color", new Color(0.5f, 1.1f, 3.5f));
            x.SetFloat("_Sharpness", 3);
        });
        return m;
    }

    // Prefabs

    static Mesh QuadMesh => Resources.GetBuiltinResource<Mesh>("Quad.fbx");

    static GameObject Model(string name, Transform parent, float scale, Vector3 euler, bool shadows)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.glb");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        go.name = name + "Model";
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        // glTFast puts the glTF-to-Unity axis conversion on the root, so keep it
        go.transform.localRotation = Quaternion.Euler(euler) * src.transform.localRotation;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }

    static GameObject Quad(string name, Transform parent, Material mat, Vector3 pos, Vector3 scale, Quaternion? rot = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = QuadMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    static T SavePrefab<T>(GameObject go, string name) where T : Component
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets", "Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab.GetComponent<T>();
    }

    sealed class Prefabs
    {
        public Enemy Drone, Heavy;
        public Missile Missile;
        public GameObject Bullet;
    }

    static Prefabs CreatePrefabs(Dictionary<string, Material> m)
    {
        var p = new Prefabs();

        // Drone
        {
            var go = new GameObject("Drone");
            var e = go.AddComponent<Enemy>();
            e.Kind = EnemyKind.Drone;
            e.MaxHp = 1;
            e.Radius = 8;
            e.Speed = 115;
            e.Agility = 2.2f;
            e.Score = 100;
            e.SlotRadius = 150;
            Model("Drone", go.transform, 16, Vector3.zero, false);
            Quad("Eye", go.transform, m["EnemyEye"], new Vector3(0, 0, 5.6f), Vector3.one * 5);
            p.Drone = SavePrefab<Enemy>(go, "Drone");
        }

        // Heavy
        {
            var go = new GameObject("Heavy");
            var e = go.AddComponent<Enemy>();
            e.Kind = EnemyKind.Heavy;
            e.MaxHp = 4;
            e.Radius = 15;
            e.Speed = 70;
            e.Agility = 1.2f;
            e.Score = 500;
            e.SlotRadius = 110;
            Model("Heavy", go.transform, 30, Vector3.zero, false);
            Quad("Eye", go.transform, m["EnemyEye"], new Vector3(0, -0.5f, 9.5f), Vector3.one * 9);
            Quad("EngineL", go.transform, m["EnemyEye"], new Vector3(-9.8f, -0.5f, 6.5f), Vector3.one * 6);
            Quad("EngineR", go.transform, m["EnemyEye"], new Vector3(9.8f, -0.5f, 6.5f), Vector3.one * 6);
            p.Heavy = SavePrefab<Enemy>(go, "Heavy");
        }

        // Missile
        {
            var go = new GameObject("Missile");
            var missile = go.AddComponent<Missile>();
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            Model("Missile", body.transform, 3.2f, new Vector3(0, 90, 0), false);
            Quad("Flame", body.transform, m["MissileFlame"], new Vector3(0, 0, -1.55f),
                 new Vector3(1.1f, 1, 6), Quaternion.Euler(0, 180, 0));
            Quad("Glow", body.transform, m["MissileGlow"], new Vector3(0, 0, -1.7f), Vector3.one * 3.5f);

            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = m["SmokeTrail"];
            trail.time = 2.2f;
            trail.minVertexDistance = 1.2f;
            trail.widthMultiplier = 1;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 1.2f), new Keyframe(0.15f, 3.2f), new Keyframe(1, 8));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1, 0.95f, 0.85f), 0), new GradientColorKey(new Color(0.9f, 0.9f, 0.92f), 0.15f), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.03f), new GradientAlphaKey(0.65f, 0.45f), new GradientAlphaKey(0, 1) });
            trail.colorGradient = g;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.numCapVertices = 2;
            trail.emitting = false;

            missile.Trail = trail;
            missile.Body = body;
            p.Missile = SavePrefab<Missile>(go, "Missile");
        }

        // Enemy bullet
        {
            var go = new GameObject("Bullet");
            Quad("Core", go.transform, m["Bullet"], Vector3.zero, Vector3.one * 5);
            var t = new GameObject("Trail");
            t.transform.SetParent(go.transform, false);
            var trail = t.AddComponent<TrailRenderer>();
            trail.sharedMaterial = m["BulletTrail"];
            trail.time = 0.12f;
            trail.minVertexDistance = 1;
            trail.widthMultiplier = 2.4f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0.2f));
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/Bullet.prefab");
            Object.DestroyImmediate(go);
            p.Bullet = prefab;
        }

        return p;
    }

    // Scene

    static T Add<T>(GameObject go) where T : Component => go.AddComponent<T>();

    static void BuildScene(Dictionary<string, Material> m, Prefabs p)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);

        // Light
        var lightGo = new GameObject("Directional Light");
        var light = Add<Light>(lightGo);
        light.type = LightType.Directional;
        light.color = new Color(1, 0.96f, 0.9f);
        light.intensity = 2.2f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.35f, -0.45f, -1));

        // Environment
        var envGo = new GameObject("Environment");
        var env = Add<ColonyEnvironment>(envGo);
        var probeGo = new GameObject("Reflection Probe");
        probeGo.transform.SetParent(envGo.transform, false);
        var probe = Add<ReflectionProbe>(probeGo);
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(4000, 4000, 9000);
        probe.resolution = 256;
        probe.hdr = true;
        probe.farClipPlane = 9000;
        probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
        probe.backgroundColor = FogColor;
        env.Probe = probe;

        // Colony
        var colonyGo = new GameObject("Colony");
        var colony = Add<ColonyBuilder>(colonyGo);
        colony.SurfaceMaterial = m["Surface"];
        colony.StructureMaterial = m["Structure"];
        colony.EndCapMaterial = m["EndCap"];
        colony.BuildingMaterial = m["Building"];
        colony.CloudMaterial = m["Cloud"];

        // Player mech
        var playerGo = new GameObject("Player");
        var mech = Add<PlayerMech>(playerGo);
        var modelRoot = new GameObject("Model");
        modelRoot.transform.SetParent(playerGo.transform, false);
        Model("MechFly", modelRoot.transform, 18, Vector3.zero, true);
        var launchers = new List<Transform>();
        for (var i = 0; i < 9; i++)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var lp = new GameObject($"Launch{launchers.Count:00}").transform;
                lp.SetParent(modelRoot.transform, false);
                // 3x3 tubes on the front face of each shoulder pod
                var gx = (i % 3 - 1) * 0.7f;
                var gy = (i / 3 - 1) * 0.7f;
                var cx = side > 0 ? 2.6f : -2.85f;
                lp.localPosition = new Vector3(cx + gx, 7.7f + gy, 2.4f);
                launchers.Add(lp);
            }
        }
        var thrusters = new List<Renderer>();
        void Thruster(Vector3 pos, Vector3 dir, float width)
        {
            var rot = Quaternion.LookRotation(dir.normalized);
            var q = Quad("Thruster", modelRoot.transform, m["Thruster"], pos, new Vector3(width, 1, 6), rot);
            thrusters.Add(q.GetComponent<Renderer>());
            Quad("ThrusterGlow", modelRoot.transform, m["ThrusterGlow"], pos, Vector3.one * width * 1.6f);
        }
        // Backpack twin thrusters and waist thrusters
        Thruster(new Vector3(-0.75f, 4.6f, -3.0f), new Vector3(0, -0.5f, -1), 1.8f);
        Thruster(new Vector3(0.75f, 4.6f, -3.0f), new Vector3(0, -0.5f, -1), 1.8f);
        Thruster(new Vector3(-1.5f, 0.4f, -3.1f), new Vector3(-0.15f, -0.7f, -1), 1.2f);
        Thruster(new Vector3(1.5f, 0.4f, -3.1f), new Vector3(0.15f, -0.7f, -1), 1.2f);
        mech.Model = modelRoot.transform;
        mech.LaunchPoints = launchers.ToArray();
        mech.Thrusters = thrusters.ToArray();

        // Camera
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = Add<Camera>(camGo);
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane = 16000;
        cam.fieldOfView = 56;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = FogColor;
        var camData = Add<UniversalAdditionalCameraData>(camGo);
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;
        Add<AudioListener>(camGo);
        var camCtrl = Add<CameraController>(camGo);

        // Post processing
        var volGo = new GameObject("Global Volume");
        var vol = Add<Volume>(volGo);
        vol.isGlobal = true;
        vol.sharedProfile = CreateVolumeProfile();

        // Game systems
        var gameGo = new GameObject("Game");
        var director = Add<FlightDirector>(gameGo);
        var game = Add<GameManager>(gameGo);
        var enemies = Add<EnemyManager>(gameGo);
        var weapons = Add<WeaponSystem>(gameGo);
        var bullets = Add<BulletManager>(gameGo);
        var fx = Add<FxManager>(gameGo);
        var audio = Add<AudioManager>(gameGo);

        // HUD
        var hudGo = new GameObject("HUD");
        var doc = Add<UIDocument>(hudGo);
        doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/DefaultSettings.asset");
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Hud.uxml");
        var hud = Add<HudController>(hudGo);

        // Wiring
        mech.Director = director;
        camCtrl.Director = director;
        camCtrl.Mech = mech;

        enemies.DronePrefab = p.Drone;
        enemies.HeavyPrefab = p.Heavy;
        enemies.Director = director;
        enemies.Mech = mech;
        enemies.Bullets = bullets;
        enemies.Camera = cam;

        weapons.Mech = mech;
        weapons.CameraController = camCtrl;
        weapons.Enemies = enemies;
        weapons.Game = game;
        weapons.MissilePrefab = p.Missile;

        bullets.BulletPrefab = p.Bullet;
        bullets.Mech = mech;
        bullets.CameraController = camCtrl;

        fx.FireMaterial = m["FxFire"];
        fx.SmokeMaterial = m["FxSmoke"];
        fx.SparkMaterial = m["FxSpark"];
        fx.FlashMaterial = m["FxFlash"];

        AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{n}.mp3");
        audio.LockClip = Clip("sfx_lock");
        audio.LaunchClip = Clip("sfx_launch");
        audio.ExplosionClip = Clip("sfx_explosion");
        audio.EnemyShotClip = Clip("sfx_enemy_shot");
        audio.HitClip = Clip("sfx_hit");
        audio.ReloadClip = Clip("sfx_reload");
        audio.BoostClip = Clip("sfx_boost");
        audio.AlertClip = Clip("sfx_alert");
        audio.TitleMusic = Clip("bgm_title");
        audio.BattleMusic = new[] { Clip("bgm_battle"), Clip("bgm_battle2") };

        game.Director = director;
        game.Mech = mech;
        game.Weapons = weapons;
        game.Enemies = enemies;
        game.Bullets = bullets;
        game.CameraController = camCtrl;
        game.Hud = hud;

        hud.Document = doc;
        hud.Game = game;
        hud.Weapons = weapons;
        hud.Enemies = enemies;
        hud.Mech = mech;
        hud.CameraController = camCtrl;
        hud.Logo = Tex("Logo");

        // Static environment settings (also applied at runtime)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = env.FogDensity;
        RenderSettings.fogColor = env.FogColor;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = env.Ambient;
        RenderSettings.skybox = null;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static VolumeProfile CreateVolumeProfile()
    {
        const string path = "Assets/URP/GlobalVolume.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.75f);
        bloom.scatter.Override(0.68f);

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.15f);
        color.contrast.Override(14);
        color.saturation.Override(6);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.24f);
        vignette.smoothness.Override(0.45f);

        var ca = profile.Add<ChromaticAberration>(true);
        ca.intensity.Override(0.035f);

        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    // Project settings

    static void ConfigureProject()
    {
        // Panel scaling for the HUD
        var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/DefaultSettings.asset");
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(1920, 1080);
        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        ps.match = 0.5f;
        EditorUtility.SetDirty(ps);

        // Shadows close to the camera only (the mech)
        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/URP/URP.asset");
        urp.shadowDistance = 90;
        urp.supportsHDR = true;
        EditorUtility.SetDirty(urp);

        QualitySettings.realtimeReflectionProbes = true;

        // Scene in build
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.productName = "SALVONIX";
    }
}

} // namespace Salvonix.Editor
