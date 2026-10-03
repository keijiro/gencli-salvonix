using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Salvonix {

// UI Toolkit HUD: stats, gauges, radar, lock-on overlay and the title /
// game-over screens.
[DefaultExecutionOrder(200)]
public sealed class HudController : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public UIDocument Document { get; set; }
    [field:SerializeField] public GameManager Game { get; set; }
    [field:SerializeField] public WeaponSystem Weapons { get; set; }
    [field:SerializeField] public EnemyManager Enemies { get; set; }
    [field:SerializeField] public PlayerMech Mech { get; set; }
    [field:SerializeField] public CameraController CameraController { get; set; }
    [field:SerializeField] public Texture2D Logo { get; set; }
    [field:SerializeField] public float RadarRange { get; set; } = 900;

    // Public methods

    public void ShowTitle()
    {
        _hud.style.display = DisplayStyle.None;
        _title.style.display = DisplayStyle.Flex;
        _gameOver.style.display = DisplayStyle.None;
        _best.text = Game.BestScore > 0 ? $"BEST SCORE  {Game.BestScore:N0}" : "";
    }

    public void ShowPlaying()
    {
        _hud.style.display = DisplayStyle.Flex;
        _title.style.display = DisplayStyle.None;
        _gameOver.style.display = DisplayStyle.None;
        _bonusTime = -10;
    }

    public void ShowGameOver(int score, int best, int kills, float time)
    {
        _gameOver.style.display = DisplayStyle.Flex;
        var newRecord = score >= best && score > 0 ? "   NEW RECORD!" : "";
        _result.text = $"SCORE  {score:N0}{newRecord}\n" +
                       $"DESTROYED  {kills}     SURVIVED  {FormatTime(time)}\n" +
                       $"BEST  {best:N0}";
        _gameOverTime = Time.unscaledTime;
    }

    public void ShowSalvoBonus(int kills, int bonus)
    {
        _bonus.text = $"{kills} HIT!   +{bonus:N0}";
        _bonusTime = Time.unscaledTime;
    }

    public void FlashDamage() => _damageFlash = 1;

    // Private members

    VisualElement _hud, _title, _gameOver, _damage, _hpFill, _boostFill, _reloadFill;
    Label _time, _score, _level, _missileState, _lockCount, _bonus, _warning;
    Label _start, _retry, _best, _result;
    TargetOverlay _overlay;
    RadarDisplay _radar;
    readonly List<Label> _tags = new();
    float _bonusTime = -10;
    float _gameOverTime;
    float _damageFlash;

    static string FormatTime(float t)
      => $"{(int)(t / 60):00}:{t % 60:00.00}";

    Label GetTag(int i)
    {
        while (_tags.Count <= i)
        {
            var l = new Label("LOCK") { pickingMode = PickingMode.Ignore };
            l.AddToClassList("lock-tag");
            _overlay.Add(l);
            _tags.Add(l);
        }
        return _tags[i];
    }

    static Texture2D CreateVignette()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = new Vector2(x / (size - 1f) * 2 - 1, y / (size - 1f) * 2 - 1).magnitude;
                var a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.55f, 1.35f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    Vector2 ScreenToPanel(Vector3 sp)
      => RuntimePanelUtils.ScreenToPanel(_overlay.panel, new Vector2(sp.x, Screen.height - sp.y));

    // MonoBehaviour implementation

    void OnEnable()
    {
        var root = Document.rootVisualElement;
        _hud = root.Q("hud");
        _title = root.Q("title");
        _gameOver = root.Q("gameover");
        _damage = root.Q("damage");
        _hpFill = root.Q("hp-fill");
        _boostFill = root.Q("boost-fill");
        _reloadFill = root.Q("reload-fill");
        _time = root.Q<Label>("time");
        _score = root.Q<Label>("score");
        _level = root.Q<Label>("level");
        _missileState = root.Q<Label>("missile-state");
        _lockCount = root.Q<Label>("lock-count");
        _bonus = root.Q<Label>("bonus");
        _warning = root.Q<Label>("warning");
        _start = root.Q<Label>("start");
        _retry = root.Q<Label>("retry");
        _best = root.Q<Label>("best");
        _result = root.Q<Label>("result");

        _overlay = new TargetOverlay();
        root.Q("overlay-holder").Add(_overlay);
        _radar = new RadarDisplay();
        _radar.style.flexGrow = 1;
        root.Q("radar-holder").Add(_radar);

        root.Q("logo").style.backgroundImage = new StyleBackground(Logo);
        _damage.style.backgroundImage = new StyleBackground(CreateVignette());

        _hud.style.display = DisplayStyle.None;
        _gameOver.style.display = DisplayStyle.None;
    }

    void LateUpdate()
    {
        if (_overlay?.panel == null) return;
        var cam = CameraController.Camera;
        var t = Time.unscaledTime;
        var playing = Game.State == GameState.Playing;

        // Blinking prompts
        var blink = Mathf.Repeat(t, 1.1f) < 0.75f ? 1 : 0.15f;
        _start.style.opacity = blink;
        _retry.style.opacity = t - _gameOverTime > 2.5f ? blink : 0;

        // Stats
        _time.text = FormatTime(Game.Elapsed);
        _score.text = Game.Score.ToString("00000000");
        _level.text = $"LV {Mathf.FloorToInt(Game.Level)}";

        var hp = Mech.Hp / Mech.MaxHp;
        _hpFill.style.width = Length.Percent(hp * 100);
        _hpFill.EnableInClassList("danger", hp < 0.3f);
        _boostFill.style.width = Length.Percent(Mathf.Clamp01(Mech.Boost) * 100);
        _warning.style.display = playing && hp < 0.3f && Mech.IsAlive ? DisplayStyle.Flex : DisplayStyle.None;
        _warning.style.opacity = Mathf.Repeat(t, 0.6f) < 0.35f ? 1 : 0.2f;

        // Weapon status
        var locks = Weapons.Locks.Count;
        _lockCount.text = $"{locks:00} / {Weapons.MaxLocks}";
        _missileState.text = Weapons.Reloading ? "RELOAD" : locks > 0 ? "LOCKED" : "READY";
        _missileState.EnableInClassList("reloading", Weapons.Reloading);
        _missileState.EnableInClassList("ready", !Weapons.Reloading && locks == 0);
        _missileState.EnableInClassList("locked", !Weapons.Reloading && locks > 0);
        _reloadFill.style.width = Length.Percent((Weapons.Reloading ? Weapons.ReloadProgress : 1) * 100);

        // Salvo bonus popup
        var bt = t - _bonusTime;
        _bonus.style.opacity = bt < 1.6f ? 1 : Mathf.Clamp01(1 - (bt - 1.6f) / 0.5f);
        var pop = 1 + 0.4f * Mathf.Exp(-bt * 10);
        _bonus.style.scale = new Scale(new Vector3(pop, pop, 1));

        // Damage flash
        _damageFlash = Mathf.Max(0, _damageFlash - Time.unscaledDeltaTime * 2.2f);
        var lowHp = playing && hp < 0.3f ? 0.35f + 0.15f * Mathf.Sin(t * 6) : 0;
        _damage.style.opacity = Mathf.Max(_damageFlash, lowHp);

        // Target overlay
        _overlay.Markers.Clear();
        var scale = ScreenToPanel(new Vector3(1, 0, 0)).x - ScreenToPanel(Vector3.zero).x;
        var tanHalf = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2);
        var tagIndex = 0;
        foreach (var e in Enemies.Active)
        {
            if (!e.Alive) continue;
            var sp = cam.WorldToScreenPoint(e.Position);
            if (sp.z <= 0) continue;
            var size = e.Radius / (sp.z * tanHalf) * Screen.height * 0.5f * scale;
            var lockCount = Weapons.CountLocks(e);
            var age = 1f;
            if (lockCount > 0)
                foreach (var l in Weapons.Locks)
                    if (l.Target == e) age = Mathf.Min(age, Time.time - l.Time);
            var pos = ScreenToPanel(sp);
            _overlay.Markers.Add(new TargetOverlay.Marker
              { Position = pos, Size = size, Locks = lockCount, LockAge = age });
            if (lockCount > 0)
            {
                var tag = GetTag(tagIndex++);
                tag.style.display = DisplayStyle.Flex;
                tag.text = $"LOCK {sp.z:0}m";
                tag.style.left = pos.x + Mathf.Max(size * 1.25f, 15) + 6;
                tag.style.top = pos.y - 8;
            }
        }
        for (var i = tagIndex; i < _tags.Count; i++) _tags[i].style.display = DisplayStyle.None;

        _overlay.Cursor = ScreenToPanel(Weapons.Cursor);
        _overlay.LockCount = locks;
        _overlay.MaxLocks = Weapons.MaxLocks;
        _overlay.Reloading = playing && Weapons.Reloading;
        _overlay.ReloadProgress = Weapons.ReloadProgress;
        _overlay.MarkDirtyRepaint();

        // Radar
        _radar.Blips.Clear();
        var inv = Quaternion.Inverse(Mech.transform.rotation);
        foreach (var e in Enemies.Active)
        {
            if (!e.Alive) continue;
            var lp = inv * (e.Position - Mech.transform.position);
            var v = new Vector2(lp.x, -lp.z) / RadarRange;
            if (v.sqrMagnitude > 1) v.Normalize();
            _radar.Blips.Add((v, Weapons.CountLocks(e) > 0));
        }
        _radar.MarkDirtyRepaint();
    }
}

} // namespace Salvonix
