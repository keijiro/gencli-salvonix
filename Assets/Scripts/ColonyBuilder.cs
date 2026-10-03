using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Salvonix {

// Procedurally builds the colony interior: the inner ground surface, the
// structural rings, the end caps with their hubs, the buildings and clouds.
public sealed class ColonyBuilder : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public Material SurfaceMaterial { get; set; }
    [field:SerializeField] public Material StructureMaterial { get; set; }
    [field:SerializeField] public Material EndCapMaterial { get; set; }
    [field:SerializeField] public Material BuildingMaterial { get; set; }
    [field:SerializeField] public Material CloudMaterial { get; set; }
    [field:SerializeField] public int Seed { get; set; } = 1234;

    // Private members

    const int MaskWidth = 512;
    const int MaskHeight = 512;
    const float RingSpacing = 700;
    const float RingDepth = 36;
    const float RingWidth = 30;
    const float HubRadius = 150;
    const float HubLength = 420;
    const int StripCount = 6;

    float[] _mask;
    System.Random _rand;

    float Rand() => (float)_rand.NextDouble();
    float Rand(float min, float max) => min + (max - min) * Rand();

    // MonoBehaviour implementation

    void Awake()
    {
        _rand = new System.Random(Seed);
        var maskTex = BuildMask();
        var surface = CreatePart("Surface", BuildSurface(), SurfaceMaterial);
        var mpb = new MaterialPropertyBlock();
        mpb.SetTexture("_MaskTex", maskTex);
        surface.SetPropertyBlock(mpb);
        CreatePart("Structures", BuildStructures(), StructureMaterial);
        CreatePart("EndCaps", BuildEndCaps(), EndCapMaterial);
        CreatePart("Buildings", BuildBuildings(), BuildingMaterial);
        CreatePart("Clouds", BuildClouds(), CloudMaterial);
    }

    MeshRenderer CreatePart(string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    // Zone mask (0 = nature, 1 = city), periodic around the circumference

    static float Hash(int x, int y, int z)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }
    }

    static float ValueNoise(Vector3 p)
    {
        var i = Vector3Int.FloorToInt(p);
        var f = p - (Vector3)i;
        var u = new Vector3(f.x * f.x * (3 - 2 * f.x),
                            f.y * f.y * (3 - 2 * f.y),
                            f.z * f.z * (3 - 2 * f.z));
        float N(int dx, int dy, int dz) => Hash(i.x + dx, i.y + dy, i.z + dz);
        var x00 = Mathf.Lerp(N(0, 0, 0), N(1, 0, 0), u.x);
        var x10 = Mathf.Lerp(N(0, 1, 0), N(1, 1, 0), u.x);
        var x01 = Mathf.Lerp(N(0, 0, 1), N(1, 0, 1), u.x);
        var x11 = Mathf.Lerp(N(0, 1, 1), N(1, 1, 1), u.x);
        return Mathf.Lerp(Mathf.Lerp(x00, x10, u.y), Mathf.Lerp(x01, x11, u.y), u.z);
    }

    static float ZoneNoise(float angle, float z)
    {
        var p = new Vector3(Mathf.Cos(angle) * Colony.Radius,
                            Mathf.Sin(angle) * Colony.Radius, z) / 900;
        var n = ValueNoise(p) * 0.6f + ValueNoise(p * 2.1f + Vector3.one * 17) * 0.3f
              + ValueNoise(p * 4.3f + Vector3.one * 41) * 0.1f;
        return Mathf.Clamp01((n - 0.5f) * 3 + 0.5f);
    }

    Texture2D BuildMask()
    {
        _mask = new float[MaskWidth * MaskHeight];
        var tex = new Texture2D(MaskWidth, MaskHeight, TextureFormat.R8, false, true);
        tex.wrapModeU = TextureWrapMode.Repeat;
        tex.wrapModeV = TextureWrapMode.Clamp;
        var pixels = new byte[MaskWidth * MaskHeight];
        for (var y = 0; y < MaskHeight; y++)
        {
            var z = ((y + 0.5f) / MaskHeight - 0.5f) * Colony.Length;
            for (var x = 0; x < MaskWidth; x++)
            {
                var a = (x + 0.5f) / MaskWidth * Mathf.PI * 2;
                var v = ZoneNoise(a, z);
                _mask[y * MaskWidth + x] = v;
                pixels[y * MaskWidth + x] = (byte)(v * 255);
            }
        }
        tex.SetPixelData(pixels, 0);
        tex.Apply(false, true);
        return tex;
    }

    float SampleMask(float angle, float z)
    {
        var x = Mathf.FloorToInt(Mathf.Repeat(angle / (Mathf.PI * 2), 1) * MaskWidth) % MaskWidth;
        var y = Mathf.Clamp(Mathf.FloorToInt((z / Colony.Length + 0.5f) * MaskHeight), 0, MaskHeight - 1);
        return _mask[y * MaskWidth + x];
    }

    // Mesh builder helper

    sealed class MeshData
    {
        public List<Vector3> Vertices = new();
        public List<Vector3> Normals = new();
        public List<Vector2> UV0 = new();
        public List<Vector2> UV1 = new();
        public List<Color> Colors = new();
        public List<int> Indices = new();

        public void AddQuad(int a, int b, int c, int d)
        {
            Indices.Add(a); Indices.Add(c); Indices.Add(b);
            Indices.Add(b); Indices.Add(c); Indices.Add(d);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, UV0);
            if (UV1.Count > 0) mesh.SetUVs(1, UV1);
            if (Colors.Count > 0) mesh.SetColors(Colors);
            mesh.SetTriangles(Indices, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1, 1, 1) * Colony.Length * 2);
            return mesh;
        }
    }

    static Vector3 Cyl(float r, float angle, float z)
      => new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, z);

    // Band between two circles (r0, z0) and (r1, z1). With r0 < r1 at equal z
    // the face points to +Z; with equal r and z0 < z1 it points to the axis.
    static void AddBand(MeshData md, int segments, float r0, float z0, float r1, float z1,
                        System.Func<float, float, float, Vector2> uv, float lightFlag)
    {
        var baseIndex = md.Vertices.Count;
        var p0 = Cyl(r0, 0, z0);
        var p1 = Cyl(r0, 2 * Mathf.PI / segments, z0);
        var p2 = Cyl(r1, 0, z1);
        var normal0 = Vector3.Cross(p2 - p0, p1 - p0).normalized;
        for (var j = 0; j <= segments; j++)
        {
            var a = 2 * Mathf.PI * j / segments;
            var rot = Quaternion.AngleAxis(a * Mathf.Rad2Deg, Vector3.forward);
            md.Vertices.Add(Cyl(r0, a, z0));
            md.Vertices.Add(Cyl(r1, a, z1));
            md.Normals.Add(rot * normal0);
            md.Normals.Add(rot * normal0);
            md.UV0.Add(uv(a, r0, z0));
            md.UV0.Add(uv(a, r1, z1));
            md.UV1.Add(new Vector2(lightFlag, 0));
            md.UV1.Add(new Vector2(lightFlag, 0));
        }
        for (var j = 0; j < segments; j++)
        {
            var i = baseIndex + j * 2;
            md.AddQuad(i, i + 2, i + 1, i + 3);
        }
    }

    // Inner ground surface

    Mesh BuildSurface()
    {
        const int around = 384, along = 64;
        var md = new MeshData();
        var dz = Colony.Length / along;
        for (var i = 0; i < along; i++)
        {
            var z0 = -Colony.Length / 2 + dz * i;
            AddBand(md, around, Colony.Radius, z0, Colony.Radius, z0 + dz,
                    (a, r, z) => new Vector2(a / (2 * Mathf.PI), z), 0);
        }
        return md.ToMesh("ColonySurface");
    }

    // Structural rings and end-cap hubs

    Mesh BuildStructures()
    {
        var md = new MeshData();
        const int seg = 384;
        var rIn = Colony.Radius - RingDepth;

        for (var z = -Colony.Length / 2 + RingSpacing / 2; z < Colony.Length / 2; z += RingSpacing)
        {
            var z0 = z - RingWidth / 2;
            var z1 = z + RingWidth / 2;
            // Inner face (lights)
            AddBand(md, seg, rIn, z0, rIn, z1,
                    (a, r, zz) => new Vector2(a * rIn, (zz - z0) / RingWidth), 1);
            // Side faces
            AddBand(md, seg, Colony.Radius, z0, rIn, z0,
                    (a, r, zz) => new Vector2(a * rIn, (Colony.Radius - r) / RingDepth), 0);
            AddBand(md, seg, rIn, z1, Colony.Radius, z1,
                    (a, r, zz) => new Vector2(a * rIn, (Colony.Radius - r) / RingDepth), 0);
        }

        // Hubs protruding from the end caps
        for (var side = -1; side <= 1; side += 2)
        {
            var zBase = side * Colony.Length / 2;
            var zTip = zBase - side * HubLength;
            var zMid = zBase - side * HubLength * 0.6f;
            var rTip = HubRadius * 0.55f;
            // Cylinder body (facing outward)
            if (side < 0)
            {
                AddBand(md, 96, HubRadius, zMid, HubRadius, zBase,
                        (a, r, z) => new Vector2(a * HubRadius, (z - zBase) / HubLength), 1);
                AddBand(md, 96, rTip, zTip, HubRadius, zMid,
                        (a, r, z) => new Vector2(a * HubRadius, (z - zBase) / HubLength), 0);
                AddBand(md, 96, 1, zTip, rTip, zTip,
                        (a, r, z) => new Vector2(a * rTip, r / rTip), 0);
            }
            else
            {
                AddBand(md, 96, HubRadius, zBase, HubRadius, zMid,
                        (a, r, z) => new Vector2(a * HubRadius, (zBase - z) / HubLength), 1);
                AddBand(md, 96, HubRadius, zMid, rTip, zTip,
                        (a, r, z) => new Vector2(a * HubRadius, (zBase - z) / HubLength), 0);
                AddBand(md, 96, rTip, zTip, 1, zTip,
                        (a, r, z) => new Vector2(a * rTip, r / rTip), 0);
            }
        }
        return md.ToMesh("ColonyStructures");
    }

    // End-cap discs

    Mesh BuildEndCaps()
    {
        var md = new MeshData();
        System.Func<float, float, float, Vector2> planar =
          (a, r, z) => new Vector2(Mathf.Cos(a) * r / Colony.Radius * 0.5f + 0.5f,
                                   Mathf.Sin(a) * r / Colony.Radius * 0.5f + 0.5f);
        var h = Colony.Length / 2;
        const int rings = 8;
        for (var i = 0; i < rings; i++)
        {
            var r0 = Colony.Radius * i / rings + 0.5f;
            var r1 = Colony.Radius * (i + 1) / rings;
            AddBand(md, 192, r0, -h, r1, -h, planar, 0);
            AddBand(md, 192, r1, h, r0, h, planar, 0);
        }
        return md.ToMesh("ColonyEndCaps");
    }

    // Buildings

    bool IsBuildable(float angle, float z)
    {
        if (Mathf.Abs(z) > Colony.Length / 2 - 180) return false;
        // Away from the structural rings
        var rz = Mathf.Repeat(z + Colony.Length / 2, RingSpacing) - RingSpacing / 2;
        if (Mathf.Abs(rz) < RingWidth + 30) return false;
        // Away from the light strips
        var s = Mathf.Repeat(angle / (2 * Mathf.PI) * StripCount + 0.5f, 1) - 0.5f;
        if (Mathf.Abs(s) * 2 * Mathf.PI * Colony.Radius / StripCount < 45) return false;
        return true;
    }

    Mesh BuildBuildings()
    {
        var md = new MeshData();
        const float cell = 62;
        var around = Mathf.FloorToInt(2 * Mathf.PI * Colony.Radius / cell);
        var along = Mathf.FloorToInt(Colony.Length / cell);
        for (var i = 0; i < along; i++)
        {
            for (var j = 0; j < around; j++)
            {
                var angle = (j + Rand(0.25f, 0.75f)) / around * 2 * Mathf.PI;
                var z = -Colony.Length / 2 + (i + Rand(0.25f, 0.75f)) * cell;
                if (!IsBuildable(angle, z)) continue;
                var m = SampleMask(angle, z);
                var city = Mathf.InverseLerp(0.5f, 0.85f, m);
                if (m < 0.5f || Rand() > 0.35f + city * 0.55f) continue;
                var w = Rand(18, 42);
                var d = Rand(18, 42);
                var hmax = Mathf.Lerp(25, 120, city);
                var hgt = Mathf.Lerp(10, hmax, Mathf.Pow(Rand(), 2.5f));
                AddBuilding(md, angle, z, w, d, hgt);
            }
        }
        return md.ToMesh("ColonyBuildings");
    }

    void AddBuilding(MeshData md, float angle, float z, float w, float d, float h)
    {
        var up = -Cyl(1, angle, 0);                      // toward the axis
        var right = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0);
        var fwd = Vector3.forward;
        var basePos = Cyl(Colony.Radius + 2, angle, z);
        var tint = Color.Lerp(new Color(0.78f, 0.82f, 0.88f), Color.white, Rand());
        tint.a = Rand(0.75f, 1.15f);
        const float tile = 24;

        Vector3 P(float x, float y, float zz) => basePos + right * x + up * y + fwd * zz;
        var hw = w / 2;
        var hd = d / 2;
        var top = h + 2;

        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 e, Vector3 n, float width, bool roof)
        {
            var i = md.Vertices.Count;
            md.Vertices.Add(a); md.Vertices.Add(b); md.Vertices.Add(c); md.Vertices.Add(e);
            for (var k = 0; k < 4; k++)
            {
                md.Normals.Add(n);
                md.Colors.Add(tint);
                md.UV1.Add(new Vector2(roof ? 1 : 0, 0));
            }
            md.UV0.Add(new Vector2(0, 0));
            md.UV0.Add(new Vector2(width / tile, 0));
            md.UV0.Add(new Vector2(0, top / tile));
            md.UV0.Add(new Vector2(width / tile, top / tile));
            // a-b bottom edge, c-e top edge; viewed from outside: a left, b right
            md.Indices.Add(i); md.Indices.Add(i + 2); md.Indices.Add(i + 1);
            md.Indices.Add(i + 1); md.Indices.Add(i + 2); md.Indices.Add(i + 3);
        }

        // Front (-fwd), right (+right), back (+fwd), left (-right)
        Face(P(-hw, 0, -hd), P(hw, 0, -hd), P(-hw, top, -hd), P(hw, top, -hd), -fwd, w, false);
        Face(P(hw, 0, -hd), P(hw, 0, hd), P(hw, top, -hd), P(hw, top, hd), right, d, false);
        Face(P(hw, 0, hd), P(-hw, 0, hd), P(hw, top, hd), P(-hw, top, hd), fwd, w, false);
        Face(P(-hw, 0, hd), P(-hw, 0, -hd), P(-hw, top, hd), P(-hw, top, -hd), -right, d, false);
        // Roof
        Face(P(-hw, top, -hd), P(hw, top, -hd), P(-hw, top, hd), P(hw, top, hd), up, w, true);
    }

    // Clouds

    Mesh BuildClouds()
    {
        var md = new MeshData();
        for (var i = 0; i < 90; i++)
        {
            var angle = Rand(0, 2 * Mathf.PI);
            var z = Rand(-Colony.Length / 2 + 400, Colony.Length / 2 - 400);
            var r = Colony.Radius - Rand(260, 400);
            var center = Cyl(r, angle, z);
            var n = -Cyl(1, angle, 0);
            var t = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0);
            var spin = Quaternion.AngleAxis(Rand(0, 360), n);
            var u = spin * t;
            var v = spin * Vector3.forward;
            var size = Rand(160, 380);
            var hu = u * size * 0.75f;
            var hv = v * size * 0.5f;
            var a = Color.white;
            a.a = Rand(0.6f, 0.95f);
            var baseIndex = md.Vertices.Count;
            md.Vertices.Add(center - hu - hv); md.UV0.Add(new Vector2(0, 0));
            md.Vertices.Add(center + hu - hv); md.UV0.Add(new Vector2(1, 0));
            md.Vertices.Add(center - hu + hv); md.UV0.Add(new Vector2(0, 1));
            md.Vertices.Add(center + hu + hv); md.UV0.Add(new Vector2(1, 1));
            for (var k = 0; k < 4; k++) { md.Normals.Add(n); md.Colors.Add(a); }
            md.AddQuad(baseIndex, baseIndex + 1, baseIndex + 2, baseIndex + 3);
        }
        return md.ToMesh("ColonyClouds");
    }
}

} // namespace Salvonix
