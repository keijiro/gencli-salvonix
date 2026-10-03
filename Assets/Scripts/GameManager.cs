using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Salvonix {

public enum GameState { Title, Playing, GameOver }

// Game flow (title / playing / game over), score and difficulty.
public sealed class GameManager : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public FlightDirector Director { get; set; }
    [field:SerializeField] public PlayerMech Mech { get; set; }
    [field:SerializeField] public WeaponSystem Weapons { get; set; }
    [field:SerializeField] public EnemyManager Enemies { get; set; }
    [field:SerializeField] public BulletManager Bullets { get; set; }
    [field:SerializeField] public CameraController CameraController { get; set; }
    [field:SerializeField] public HudController Hud { get; set; }
    [field:SerializeField] public float SecondsPerLevel { get; set; } = 30;

    public GameState State { get; private set; } = GameState.Title;
    public int Score { get; private set; }
    public int Kills { get; private set; }
    public int BestScore { get; private set; }
    public float Elapsed { get; private set; }
    public float Level => 1 + Elapsed / SecondsPerLevel;

    // Public methods

    public void AddKill(Enemy enemy)
    {
        if (State != GameState.Playing) return;
        Score += enemy.Score;
        Kills++;
    }

    public void CompleteSalvo(Salvo salvo)
    {
        if (State != GameState.Playing || salvo.Kills < 2) return;
        var bonus = salvo.Kills * (salvo.Kills - 1) * 25;
        Score += bonus;
        Hud.ShowSalvoBonus(salvo.Kills, bonus);
    }

    // Private members

    float _stateTime;
    bool _alerted;

    void EnterTitle()
    {
        State = GameState.Title;
        _stateTime = 0;
        Mech.ResetState();
        Mech.ControlsEnabled = false;
        Weapons.ControlsEnabled = false;
        Weapons.ResetState();
        Enemies.DemoMode = true;
        Enemies.FiringEnabled = false;
        Enemies.Level = 1;
        AudioManager.Instance.PlayTitleMusic();
        Hud.ShowTitle();
    }

    void StartGame()
    {
        State = GameState.Playing;
        _stateTime = 0;
        Score = 0;
        Kills = 0;
        Elapsed = 0;
        _alerted = false;
        Mech.ResetState();
        Mech.ControlsEnabled = true;
        Weapons.ResetState();
        Weapons.ControlsEnabled = true;
        Enemies.Clear();
        Bullets.Clear();
        Enemies.DemoMode = false;
        Enemies.FiringEnabled = true;
        AudioManager.Instance.PlayBattleMusic();
        Hud.ShowPlaying();
    }

    void EnterGameOver()
    {
        State = GameState.GameOver;
        _stateTime = 0;
        Mech.ControlsEnabled = false;
        Weapons.ControlsEnabled = false;
        Weapons.ResetState();
        Enemies.FiringEnabled = false;
        Bullets.Clear();
        if (Score > BestScore)
        {
            BestScore = Score;
            PlayerPrefs.SetInt("BestScore", BestScore);
        }
        StartCoroutine(GameOverSequence());
    }

    IEnumerator GameOverSequence()
    {
        var pos = Mech.HitCenter;
        FxManager.Instance.Explosion(pos, 3);
        AudioManager.Instance.PlayExplosion(1.6f);
        CameraController.Shake(1);
        Time.timeScale = 0.3f;
        yield return new WaitForSecondsRealtime(0.25f);
        Mech.SetVisible(false);
        FxManager.Instance.Explosion(pos + Random.insideUnitSphere * 5, 2.2f);
        yield return new WaitForSecondsRealtime(1.1f);
        Time.timeScale = 1;
        Enemies.DemoMode = true;
        Hud.ShowGameOver(Score, BestScore, Kills, Elapsed);
    }

    void OnDamaged(float amount)
    {
        AudioManager.Instance.PlayHit();
        Hud.FlashDamage();
        if (!_alerted && Mech.Hp < Mech.MaxHp * 0.3f && Mech.IsAlive)
        {
            _alerted = true;
            AudioManager.Instance.PlayAlert();
        }
    }

    void OnManeuverStarted(bool dash)
    {
        if (dash || Random.value < 0.6f) AudioManager.Instance.PlayBoost();
    }

    // MonoBehaviour implementation

    void Start()
    {
        BestScore = PlayerPrefs.GetInt("BestScore", 0);
        Mech.Damaged += OnDamaged;
        Director.ManeuverStarted += OnManeuverStarted;
        EnterTitle();
    }

    void Update()
    {
        _stateTime += Time.unscaledDeltaTime;
        var click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        var key = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        switch (State)
        {
            case GameState.Title:
                if ((click || key) && _stateTime > 0.5f) StartGame();
                break;
            case GameState.Playing:
                Elapsed += Time.deltaTime;
                Enemies.Level = Level;
                if (!Mech.IsAlive) EnterGameOver();
                break;
            case GameState.GameOver:
                if ((click || key) && _stateTime > 2.5f) StartGame();
                break;
        }

        // The HUD draws its own reticle
        UnityEngine.Cursor.visible = false;
        UnityEngine.Cursor.lockState = CursorLockMode.Confined;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Application.Quit();
    }
}

} // namespace Salvonix
