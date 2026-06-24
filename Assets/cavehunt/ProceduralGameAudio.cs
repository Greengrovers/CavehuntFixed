using UnityEngine;

public class ProceduralGameAudio : MonoBehaviour
{
    private const int SampleRate = 44100;

    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.234f;
    [SerializeField] private string externalMusicResourcePath = "Audio/Music/gravity_breaker_power_ambition_synthrock";
    [SerializeField] private string bossMusicResourcePath = "Audio/Music/boss_battle_arkpiercer";
    [SerializeField, Range(0f, 1f)] private float bossMusicVolume = 0.36f;
    [SerializeField] private string gameWonMusicResourcePath = "Audio/Music/level_won";
    [SerializeField, Range(0f, 1f)] private float gameWonMusicVolume = 0.62f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.75f;

    private static ProceduralGameAudio instance;

    private AudioSource musicSource;
    private AudioClip musicClip;
    private AudioClip bossMusicClip;
    private AudioClip gameWonMusicClip;
    private AudioClip airPickupClip;
    private AudioClip pickupClip;
    private AudioClip bowReadyClip;
    private AudioClip arrowShotClip;
    private AudioClip explosionClip;
    private AudioClip trapClip;
    private uint noiseSeed = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance().StartMusic();
    }

    public static void PlayAirPickup(Vector3 position)
    {
        ProceduralGameAudio audio = EnsureInstance();
        audio.PlayAt(position, audio.airPickupClip ??= audio.CreateAirPickupClip(), 0.7f, 0.65f);
    }

    public static void PlayPickup(Vector3 position)
    {
        ProceduralGameAudio audio = EnsureInstance();
        audio.PlayAt(position, audio.pickupClip ??= audio.CreatePickupClip(), 0.55f, 0.55f);
    }

    public static void PlayBowReady(Vector3 position)
    {
        ProceduralGameAudio audio = EnsureInstance();
        audio.PlayAt(position, audio.bowReadyClip ??= audio.CreateBowReadyClip(), 0.8f, 0.75f);
    }

    public static void PlayArrowShot(Vector3 position)
    {
        ProceduralGameAudio audio = EnsureInstance();
        audio.PlayAt(position, audio.arrowShotClip ??= audio.CreateArrowShotClip(), 0.85f, 0.8f);
    }

    public static void PlayExplosion(Vector3 position)
    {
        ProceduralGameAudio audio = EnsureInstance();
        audio.PlayAt(position, audio.explosionClip ??= audio.CreateExplosionClip(), 1f, 0.95f);
    }

    public static void PlayTrap(Vector3 position)
    {
        ProceduralGameAudio audio = EnsureInstance();
        audio.PlayAt(position, audio.trapClip ??= audio.CreateTrapClip(), 0.9f, 0.85f);
    }

    public static void StartBossMusic()
    {
        try
        {
            EnsureInstance().SwitchToBossMusic();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Boss music could not start: {exception.Message}");
        }
    }

    public static void StartGameWonMusic()
    {
        try
        {
            EnsureInstance().SwitchToGameWonMusic();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Game won music could not start: {exception.Message}");
        }
    }

    public static void StopBossMusic(bool resumeDefaultMusic = true)
    {
        if (instance == null) return;

        try
        {
            instance.StopBossMusicInternal(resumeDefaultMusic);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Boss music could not stop cleanly: {exception.Message}");
        }
    }

    private static ProceduralGameAudio EnsureInstance()
    {
        if (instance != null) return instance;

        ProceduralGameAudio existing = FindAnyObjectByType<ProceduralGameAudio>();
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject audioObject = new GameObject("Procedural Game Audio");
        DontDestroyOnLoad(audioObject);
        instance = audioObject.AddComponent<ProceduralGameAudio>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void StartMusic()
    {
        EnsureMusicSource();

        if (musicSource.isPlaying) return;

        musicClip ??= LoadExternalMusicClip();
        musicClip ??= CreatePowerRockLoop();
        musicSource.ignoreListenerPause = false;
        musicSource.clip = musicClip;
        musicSource.Play();
    }

    private void SwitchToBossMusic()
    {
        EnsureMusicSource();

        bossMusicClip ??= LoadBossMusicClip();
        if (bossMusicClip == null) return;

        if (musicSource.clip == bossMusicClip && musicSource.isPlaying) return;

        musicSource.Stop();
        musicSource.ignoreListenerPause = false;
        musicSource.clip = bossMusicClip;
        musicSource.volume = bossMusicVolume;
        musicSource.loop = true;
        musicSource.Play();
    }

    private void SwitchToGameWonMusic()
    {
        EnsureMusicSource();

        gameWonMusicClip ??= LoadGameWonMusicClip();
        if (gameWonMusicClip == null) return;

        if (musicSource.clip == gameWonMusicClip && musicSource.isPlaying) return;

        musicSource.Stop();
        musicSource.ignoreListenerPause = true;
        musicSource.clip = gameWonMusicClip;
        musicSource.volume = gameWonMusicVolume;
        musicSource.loop = false;
        musicSource.Play();
    }

    private void StopBossMusicInternal(bool resumeDefaultMusic)
    {
        if (musicSource == null) return;

        bool wasSpecialMusic = musicSource.clip == bossMusicClip || musicSource.clip == gameWonMusicClip;
        if (musicSource.isPlaying && wasSpecialMusic)
        {
            musicSource.Stop();
        }

        musicSource.ignoreListenerPause = false;
        if (!resumeDefaultMusic || !wasSpecialMusic) return;

        musicClip ??= LoadExternalMusicClip();
        musicClip ??= CreatePowerRockLoop();
        musicSource.clip = musicClip;
        musicSource.volume = musicVolume;
        musicSource.loop = true;
        musicSource.Play();
    }

    private void EnsureMusicSource()
    {
        if (musicSource != null) return;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.ignoreListenerPause = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;
    }

    private AudioClip LoadExternalMusicClip()
    {
        if (string.IsNullOrWhiteSpace(externalMusicResourcePath)) return null;
        return Resources.Load<AudioClip>(externalMusicResourcePath);
    }

    private AudioClip LoadGameWonMusicClip()
    {
        if (string.IsNullOrWhiteSpace(gameWonMusicResourcePath)) return null;

        AudioClip clip = Resources.Load<AudioClip>(gameWonMusicResourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"Game won music not found at Resources/{gameWonMusicResourcePath}.", this);
        }

        return clip;
    }

    private AudioClip LoadBossMusicClip()
    {
        if (string.IsNullOrWhiteSpace(bossMusicResourcePath)) return null;

        AudioClip clip = Resources.Load<AudioClip>(bossMusicResourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"Boss music not found at Resources/{bossMusicResourcePath}.", this);
        }

        return clip;
    }

    private void PlayAt(Vector3 position, AudioClip clip, float localVolume, float spatialBlend)
    {
        if (clip == null) return;

        GameObject soundObject = new GameObject("Procedural SFX");
        soundObject.transform.position = position;

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(localVolume * sfxVolume);
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.minDistance = 1.2f;
        source.maxDistance = 25f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.Play();

        Destroy(soundObject, clip.length + 0.1f);
    }

    private AudioClip CreatePowerRockLoop()
    {
        float seconds = 16f;
        int sampleCount = Mathf.CeilToInt(seconds * SampleRate);
        float[] samples = new float[sampleCount];
        float[] roots = { 82.41f, 98.0f, 110.0f, 130.81f };

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SampleRate;
            float beat = time * 2f;
            int bar = Mathf.FloorToInt(beat / 4f) % roots.Length;
            float root = roots[bar];
            float eighth = Mathf.Repeat(beat * 2f, 1f);
            float gate = eighth < 0.62f ? 1f : 0.25f;

            float guitar = Distort(
                Saw(time, root) * 0.55f +
                Saw(time, root * 1.5f) * 0.28f +
                Saw(time, root * 2f) * 0.2f,
                2.7f) * 0.24f * gate;

            float bass = Mathf.Sin(2f * Mathf.PI * root * 0.5f * time) * 0.22f;
            float kick = DrumPulse(time, 0f, 0.18f, 58f, 0.9f) + DrumPulse(time, 2f, 0.16f, 62f, 0.7f);
            float snare = NoisePulse(time, 1f, 0.12f, 0.38f) + NoisePulse(time, 3f, 0.12f, 0.38f);
            float hat = NoisePulse(time, 0.5f, 0.035f, 0.08f) + NoisePulse(time, 1.5f, 0.035f, 0.08f) + NoisePulse(time, 2.5f, 0.035f, 0.08f) + NoisePulse(time, 3.5f, 0.035f, 0.08f);

            samples[i] = Mathf.Clamp((guitar + bass + kick + snare + hat) * 0.75f, -0.95f, 0.95f);
        }

        return CreateClip("DMCA Free Procedural Power Rock", samples);
    }

    private AudioClip CreateAirPickupClip()
    {
        return CreateNoiseSweepClip("DMCA Free Wind Breeze Pickup", 1.15f, 0.38f, 0.04f, 0.55f, 0.22f);
    }

    private AudioClip CreatePickupClip()
    {
        float seconds = 0.55f;
        float[] samples = CreateSamples(seconds, (time, progress) =>
        {
            float env = Mathf.Exp(-progress * 5.5f);
            return (Sine(time, 659.25f) + Sine(time, 987.77f) * 0.55f) * env * 0.34f;
        });

        return CreateClip("DMCA Free Pickup Chime", samples);
    }

    private AudioClip CreateBowReadyClip()
    {
        float seconds = 0.62f;
        float[] samples = CreateSamples(seconds, (time, progress) =>
        {
            float freq = Mathf.Lerp(170f, 440f, progress);
            float env = Mathf.Sin(progress * Mathf.PI) * 0.75f;
            float creak = NextNoise() * 0.08f * env;
            return (Sine(time, freq) * 0.5f + Saw(time, freq * 0.5f) * 0.18f + creak) * env;
        });

        return CreateClip("DMCA Free Bow String Tension", samples);
    }

    private AudioClip CreateArrowShotClip()
    {
        float seconds = 0.48f;
        float[] samples = CreateSamples(seconds, (time, progress) =>
        {
            float twang = Sine(time, Mathf.Lerp(420f, 120f, progress)) * Mathf.Exp(-progress * 7f) * 0.75f;
            float whoosh = NextNoise() * Mathf.Exp(-progress * 5f) * 0.22f;
            return twang + whoosh;
        });

        return CreateClip("DMCA Free Arrow Shot", samples);
    }

    private AudioClip CreateExplosionClip()
    {
        float seconds = 0.82f;
        float[] samples = CreateSamples(seconds, (time, progress) =>
        {
            float thump = Sine(time, Mathf.Lerp(82f, 32f, progress)) * Mathf.Exp(-progress * 8f) * 0.9f;
            float crack = NextNoise() * Mathf.Exp(-progress * 16f) * 0.75f;
            float rumble = NextNoise() * Mathf.Exp(-progress * 3.2f) * 0.32f;
            return thump + crack + rumble;
        });

        return CreateClip("DMCA Free Grenade Explosion", samples);
    }

    private AudioClip CreateTrapClip()
    {
        float seconds = 0.85f;
        float[] samples = CreateSamples(seconds, (time, progress) =>
        {
            float clang = (Sine(time, 170f) + Sine(time, 242f) * 0.7f + Sine(time, 391f) * 0.35f) * Mathf.Exp(-progress * 8f);
            float scrape = NextNoise() * Mathf.Sin(progress * Mathf.PI) * 0.22f;
            float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 38f, progress) * time) * Mathf.Exp(-progress * 12f) * 0.8f;
            return clang * 0.55f + scrape + thump;
        });

        return CreateClip("DMCA Free Spike Trap", samples);
    }

    private AudioClip CreateNoiseSweepClip(string clipName, float seconds, float startStrength, float endStrength, float toneFrequency, float toneVolume)
    {
        float[] samples = CreateSamples(seconds, (time, progress) =>
        {
            float env = Mathf.Sin(progress * Mathf.PI);
            float noiseAmount = Mathf.Lerp(startStrength, endStrength, progress);
            float breeze = NextNoise() * noiseAmount * env;
            float tone = Sine(time, Mathf.Lerp(280f, 620f, progress) * toneFrequency) * toneVolume * env;
            return breeze + tone;
        });

        return CreateClip(clipName, samples);
    }

    private float[] CreateSamples(float seconds, System.Func<float, float, float> generator)
    {
        int sampleCount = Mathf.CeilToInt(seconds * SampleRate);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SampleRate;
            float progress = sampleCount <= 1 ? 1f : i / (float)(sampleCount - 1);
            samples[i] = Mathf.Clamp(generator(time, progress), -0.95f, 0.95f);
        }

        return samples;
    }

    private AudioClip CreateClip(string clipName, float[] samples)
    {
        AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private float DrumPulse(float time, float beatOffset, float duration, float frequency, float amount)
    {
        float beatTime = Mathf.Repeat(time * 2f - beatOffset, 4f);
        if (beatTime > duration) return 0f;

        float progress = beatTime / Mathf.Max(0.001f, duration);
        return Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(frequency * 1.4f, frequency, progress) * time) * Mathf.Exp(-progress * 7f) * amount;
    }

    private float NoisePulse(float time, float beatOffset, float duration, float amount)
    {
        float beatTime = Mathf.Repeat(time * 2f - beatOffset, 4f);
        if (beatTime > duration) return 0f;

        float progress = beatTime / Mathf.Max(0.001f, duration);
        return NextNoise() * Mathf.Exp(-progress * 9f) * amount;
    }

    private float Sine(float time, float frequency)
    {
        return Mathf.Sin(2f * Mathf.PI * frequency * time);
    }

    private float Saw(float time, float frequency)
    {
        return Mathf.Repeat(time * frequency, 1f) * 2f - 1f;
    }

    private float Distort(float value, float drive)
    {
        return Mathf.Atan(value * drive) / Mathf.Atan(drive);
    }

    private float NextNoise()
    {
        noiseSeed = noiseSeed * 1664525u + 1013904223u;
        return ((noiseSeed >> 8) / 16777215f) * 2f - 1f;
    }
}
