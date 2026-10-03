using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Salvonix {

// Full-screen overlay drawing target brackets, lock-on markers and the
// cursor reticle with the lock capacity / reload gauge.
public sealed class TargetOverlay : VisualElement
{
    public struct Marker
    {
        public Vector2 Position;
        public float Size;
        public int Locks;
        public float LockAge;
    }

    public List<Marker> Markers { get; } = new();
    public Vector2 Cursor { get; set; }
    public bool ShowCursor { get; set; } = true;
    public int LockCount { get; set; }
    public int MaxLocks { get; set; } = 12;
    public bool Reloading { get; set; }
    public float ReloadProgress { get; set; }

    static readonly Color Cyan = new(0.45f, 0.92f, 1, 0.95f);
    static readonly Color CyanDim = new(0.45f, 0.92f, 1, 0.3f);
    static readonly Color Red = new(1, 0.22f, 0.16f, 1);
    static readonly Color RedFaint = new(1, 0.3f, 0.25f, 0.55f);
    static readonly Color Amber = new(1, 0.75f, 0.2f, 0.95f);

    public TargetOverlay()
    {
        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;
        style.left = style.top = style.right = style.bottom = 0;
        generateVisualContent += Draw;
    }

    void Draw(MeshGenerationContext mgc)
    {
        var p = mgc.painter2D;
        p.lineJoin = LineJoin.Miter;
        foreach (var m in Markers)
        {
            if (m.Locks == 0) DrawBrackets(p, m.Position, Mathf.Max(m.Size, 9));
            else DrawLock(p, m);
        }
        if (ShowCursor) DrawCursor(p);
    }

    static void DrawBrackets(Painter2D p, Vector2 c, float s)
    {
        var l = s * 0.45f;
        p.strokeColor = RedFaint;
        p.lineWidth = 1.5f;
        p.BeginPath();
        for (var i = 0; i < 4; i++)
        {
            var sx = (i & 1) == 0 ? -1 : 1;
            var sy = (i & 2) == 0 ? -1 : 1;
            var corner = c + new Vector2(sx * s, sy * s);
            p.MoveTo(corner - new Vector2(sx * l, 0));
            p.LineTo(corner);
            p.LineTo(corner - new Vector2(0, sy * l));
        }
        p.Stroke();
    }

    static void DrawLock(Painter2D p, Marker m)
    {
        var k = Mathf.Clamp01(m.LockAge / 0.16f);
        var s = Mathf.Max(m.Size * 1.25f, 15) * (1 + 1.8f * (1 - k) * (1 - k));
        var c = m.Position;
        p.strokeColor = Red;
        for (var i = 0; i < m.Locks; i++)
        {
            var r = s + i * 6;
            p.lineWidth = i == 0 ? 2.5f : 1.5f;
            p.BeginPath();
            p.MoveTo(c + new Vector2(0, -r));
            p.LineTo(c + new Vector2(r, 0));
            p.LineTo(c + new Vector2(0, r));
            p.LineTo(c + new Vector2(-r, 0));
            p.ClosePath();
            p.Stroke();
        }
        // Center pip
        p.fillColor = Red;
        p.BeginPath();
        p.Arc(c, 2.5f, 0, 360);
        p.Fill();
    }

    void DrawCursor(Painter2D p)
    {
        var c = Cursor;
        var col = Reloading ? CyanDim : Cyan;

        // Segmented ring
        p.strokeColor = col;
        p.lineWidth = 2;
        for (var i = 0; i < 4; i++)
        {
            p.BeginPath();
            var a = i * 90 + 15;
            p.Arc(c, 26, a, a + 60);
            p.Stroke();
        }

        // Crosshair ticks
        p.BeginPath();
        p.MoveTo(c + new Vector2(-34, 0)); p.LineTo(c + new Vector2(-20, 0));
        p.MoveTo(c + new Vector2(34, 0)); p.LineTo(c + new Vector2(20, 0));
        p.MoveTo(c + new Vector2(0, -34)); p.LineTo(c + new Vector2(0, -20));
        p.MoveTo(c + new Vector2(0, 34)); p.LineTo(c + new Vector2(0, 20));
        p.Stroke();

        p.fillColor = col;
        p.BeginPath();
        p.Arc(c, 2, 0, 360);
        p.Fill();

        // Lock capacity pips
        for (var i = 0; i < MaxLocks; i++)
        {
            var ang = (-90 + 360f * i / MaxLocks) * Mathf.Deg2Rad;
            var pos = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 40;
            p.fillColor = Reloading ? CyanDim : (i < LockCount ? Red : CyanDim);
            p.BeginPath();
            p.Arc(pos, i < LockCount ? 3.5f : 2.5f, 0, 360);
            p.Fill();
        }

        // Reload gauge
        if (Reloading)
        {
            p.strokeColor = Amber;
            p.lineWidth = 3;
            p.BeginPath();
            p.Arc(c, 48, -90, -90 + 360 * ReloadProgress);
            p.Stroke();
        }
    }
}

// Circular radar showing enemies around the player (top-down in the
// player's frame).
public sealed class RadarDisplay : VisualElement
{
    public List<(Vector2 pos, bool locked)> Blips { get; } = new();

    public RadarDisplay()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    void Draw(MeshGenerationContext mgc)
    {
        var p = mgc.painter2D;
        var r = Mathf.Min(contentRect.width, contentRect.height) / 2;
        var c = contentRect.center;

        p.fillColor = new Color(0.02f, 0.08f, 0.12f, 0.45f);
        p.BeginPath();
        p.Arc(c, r, 0, 360);
        p.Fill();

        p.strokeColor = new Color(0.45f, 0.92f, 1, 0.6f);
        p.lineWidth = 1.5f;
        p.BeginPath();
        p.Arc(c, r - 1, 0, 360);
        p.Stroke();
        p.strokeColor = new Color(0.45f, 0.92f, 1, 0.25f);
        p.lineWidth = 1;
        p.BeginPath();
        p.Arc(c, r * 0.5f, 0, 360);
        p.MoveTo(c - new Vector2(r, 0)); p.LineTo(c + new Vector2(r, 0));
        p.MoveTo(c - new Vector2(0, r)); p.LineTo(c + new Vector2(0, r));
        p.Stroke();

        foreach (var (pos, locked) in Blips)
        {
            p.fillColor = locked ? new Color(1, 0.85f, 0.3f) : new Color(1, 0.25f, 0.2f);
            p.BeginPath();
            p.Arc(c + pos * (r - 4), locked ? 3.2f : 2.6f, 0, 360);
            p.Fill();
        }

        // Player marker
        p.fillColor = new Color(0.45f, 0.92f, 1, 1);
        p.BeginPath();
        p.MoveTo(c + new Vector2(0, -7));
        p.LineTo(c + new Vector2(5, 5));
        p.LineTo(c + new Vector2(-5, 5));
        p.ClosePath();
        p.Fill();
    }
}

} // namespace Salvonix
