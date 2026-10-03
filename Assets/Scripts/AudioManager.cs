using UnityEngine;

namespace Salvonix {

// Sound effects and background music (title theme / battle playlist with
// crossfades).
public sealed class AudioManager : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public AudioClip LockClip { get; set; }
    [field:SerializeField] public AudioClip LaunchClip { get; set; }
    [field:SerializeField] public AudioClip ExplosionClip { get; set; }
    [field:SerializeField] public AudioClip EnemyShotClip { get; set; }
    [field:SerializeField] public AudioClip HitClip { get; set; }
    [field:SerializeField] public AudioClip ReloadClip { get; set; }
    [field:SerializeField] public AudioClip BoostClip { get; set; }
    [field:SerializeField] public AudioClip AlertClip { get; set; }
    [field:SerializeField] public AudioClip TitleMusic { get; set; }
    [field:SerializeField] public AudioClip[] BattleMusic { get; set; }
    [field:SerializeField, Range(0, 1)] public float MusicVolume { get; set; } = 0.55f;

    public static AudioManager Instance { get; private set; }

    // Public methods

    public void PlayLock(int count)
      => Play(LockClip, 0.55f, 1 + (count - 1) * 0.055f);

    public void PlayLaunch() => Play(LaunchClip, 0.9f, Random.Range(0.95f, 1.05f));
    public void PlayReload() => Play(ReloadClip, 0.6f, 1);
    public void PlayBoost() => Play(BoostClip, 0.45f, Random.Range(0.9f, 1.1f));
    public void PlayAlert() => Play(AlertClip, 0.5f, 1);

    public void PlayHit() => Play(HitClip, 0.9f, Random.Range(0.9f, 1.1f));

    public void PlayEnemyShot()
    {
        if (Time.unscaledTime - _lastShot < 0.06f) return;
        _lastShot = Time.unscaledTime;
        Play(EnemyShotClip, 0.25f, Random.Range(0.85f, 1.15f));
    }

    public void PlayExplosion(float size)
    {
        if (Time.unscaledTime - _lastExplosion < 0.045f) return;
        _lastExplosion = Time.unscaledTime;
        Play(ExplosionClip, 0.55f * size, Random.Range(0.85f, 1.1f) / size);
    }

    public void PlayTitleMusic() => StartMusic(TitleMusic, -1);
    public void PlayBattleMusic() => StartMusic(BattleMusic[0], 0);

    // Private members

    readonly AudioSource[] _sfx = new AudioSource[16];
    int _sfxIndex;
    AudioSource[] _music = new AudioSource[2];
    int _current;
    int _playlistIndex = -1;
    float _lastShot, _lastExplosion;

    void Play(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;
        // Round-robin sources so per-sound pitch changes don't interfere
        var src = _sfx[_sfxIndex];
        _sfxIndex = (_sfxIndex + 1) % _sfx.Length;
        src.pitch = pitch;
        src.volume = volume;
        src.clip = clip;
        src.Play();
    }

    void StartMusic(AudioClip clip, int playlistIndex)
    {
        _playlistIndex = playlistIndex;
        _current ^= 1;
        var src = _music[_current];
        src.clip = clip;
        src.loop = playlistIndex < 0;
        src.volume = 0;
        src.Play();
    }

    // MonoBehaviour implementation

    void Awake()
    {
        Instance = this;
        for (var i = 0; i < _sfx.Length; i++)
        {
            _sfx[i] = gameObject.AddComponent<AudioSource>();
            _sfx[i].playOnAwake = false;
        }
        for (var i = 0; i < 2; i++)
        {
            _music[i] = gameObject.AddComponent<AudioSource>();
            _music[i].playOnAwake = false;
        }
    }

    void Update()
    {
        var dt = Time.unscaledDeltaTime;

        // Advance the battle playlist slightly before the track ends
        var cur = _music[_current];
        if (_playlistIndex >= 0 && cur.clip != null && cur.isPlaying &&
            cur.time > cur.clip.length - 1.2f)
        {
            var next = (_playlistIndex + 1) % BattleMusic.Length;
            StartMusic(BattleMusic[next], next);
        }

        // Crossfade
        for (var i = 0; i < 2; i++)
        {
            var target = i == _current ? MusicVolume : 0;
            var src = _music[i];
            src.volume = Mathf.MoveTowards(src.volume, target, dt / 1.2f * MusicVolume);
            if (i != _current && src.volume <= 0 && src.isPlaying) src.Stop();
        }
    }
}

} // namespace Salvonix
