using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬별 BGM 크로스페이드와 고정 크기 풀링 기반 SFX 재생·스로틀링을 총괄하는 오디오 매니저입니다.
/// </summary>
public class AudioManager : MonoBehaviour, IAudioService
{
    [Serializable]
    public struct NamedAudioClipEntry
    {
        public string key;
        public AudioClip clip;
    }

    public const string PREF_MASTER_VOLUME = "Settings_MasterVolume";
    public const string PREF_BGM_VOLUME = "Settings_BgmVolume";
    public const string PREF_SFX_VOLUME = "Settings_SfxVolume";

    private const int MinSfxPoolSize = 4;
    private const int MaxSfxPoolSize = 32;
    private const float DefaultSfxCooldownSeconds = 0.05f;

    private static AudioManager _instance;
    private static bool _isShuttingDown = false;
    private static float _masterVolume = 1f;
    private static float _bgmVolume = 1f;
    private static float _sfxVolume = 1f;

    public static event Action OnVolumeSettingsChanged;

    public static float SavedMasterVolume => _masterVolume;
    public static float SavedBgmVolume => _bgmVolume;
    public static float SavedSfxVolume => _sfxVolume;

    public static bool HasInstance => _instance != null && !_isShuttingDown;

    public static AudioManager Instance
    {
        get
        {
            if (_isShuttingDown) return null;

            if (_instance == null)
            {
                _instance = FindFirstObjectByType<AudioManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[AudioManager]");
                    _instance = go.AddComponent<AudioManager>();
                    if (Application.isPlaying)
                    {
                        DontDestroyOnLoad(go);
                    }
                }
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
        _isShuttingDown = false;
        OnVolumeSettingsChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadSavedAudioSettingsOnStartup()
    {
        _masterVolume = PlayerPrefs.HasKey(PREF_MASTER_VOLUME)
            ? Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, 1f))
            : 1f;
        _bgmVolume = PlayerPrefs.HasKey(PREF_BGM_VOLUME)
            ? Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_BGM_VOLUME, 1f))
            : 1f;
        _sfxVolume = PlayerPrefs.HasKey(PREF_SFX_VOLUME)
            ? Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_SFX_VOLUME, 1f))
            : 1f;

        AudioListener.volume = _masterVolume;
    }

    public static void SetMasterVolume(float volume, bool savePrefs = true)
    {
        float clamped = Mathf.Clamp01(volume);
        _masterVolume = clamped;
        AudioListener.volume = clamped;

        if (savePrefs)
        {
            PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, clamped);
            PlayerPrefs.Save();
        }

        if (_instance != null)
        {
            _instance.RefreshActiveBgmVolumes();
        }

        OnVolumeSettingsChanged?.Invoke();
    }

    public static void SetBgmVolume(float volume, bool savePrefs = true)
    {
        float clamped = Mathf.Clamp01(volume);
        _bgmVolume = clamped;

        if (savePrefs)
        {
            PlayerPrefs.SetFloat(PREF_BGM_VOLUME, clamped);
            PlayerPrefs.Save();
        }

        if (_instance != null)
        {
            _instance.RefreshActiveBgmVolumes();
        }

        OnVolumeSettingsChanged?.Invoke();
    }

    public static void SetSfxVolume(float volume, bool savePrefs = true)
    {
        float clamped = Mathf.Clamp01(volume);
        _sfxVolume = clamped;

        if (savePrefs)
        {
            PlayerPrefs.SetFloat(PREF_SFX_VOLUME, clamped);
            PlayerPrefs.Save();
        }

        OnVolumeSettingsChanged?.Invoke();
    }

    [Header("SFX Pool Settings")]
    [SerializeField] private int sfxPoolSize = 12;
    [SerializeField] private float defaultSfxCooldown = DefaultSfxCooldownSeconds;

    [Header("Optional Pre-Registered Clips")]
    [SerializeField] private List<NamedAudioClipEntry> registeredClips = new List<NamedAudioClipEntry>();

    private readonly Dictionary<string, AudioClip> _clipRegistry = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _lastSfxPlayedTime = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _sfxCooldownOverrides = new Dictionary<string, float>(StringComparer.Ordinal)
    {
        { AudioCueKeys.SFX_CARD_HOVER, 0.06f },
        { AudioCueKeys.SFX_CARD_DRAW, 0.045f },
        { AudioCueKeys.SFX_COMBAT_HIT, 0.05f }
    };

    private AudioSource _bgmSourceA;
    private AudioSource _bgmSourceB;
    private AudioSource _activeBgmSource;
    private AudioSource _fadingOutBgmSource;
    private AudioSource[] _sfxPool;
    private int _sfxNextIndex = 0;
    private bool _audioSourcesInitialized = false;
    private bool _combatEventsSubscribed = false;

    private bool _isCrossfading = false;
    private float _crossfadeElapsed = 0f;
    private float _crossfadeDuration = 0f;
    private float _fadeOutStartVolume = 0f;
    private string _currentBgmKey = string.Empty;
    private string _lastPlayedSfxKey = string.Empty;

    public Func<float> ClockProviderForTest { get; set; }

    public float MasterVolume => _masterVolume;
    public float BgmVolume => _bgmVolume;
    public float SfxVolume => _sfxVolume;
    public string CurrentBgmKey => _currentBgmKey;
    public string LastPlayedSfxKey => _lastPlayedSfxKey;
    public int SfxPoolCount => _sfxPool != null ? _sfxPool.Length : 0;
    public bool IsCrossfading => _isCrossfading;
    public float ActiveBgmVolume => _activeBgmSource != null ? _activeBgmSource.volume : 0f;
    public AudioClip ActiveBgmClip => _activeBgmSource != null ? _activeBgmSource.clip : null;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (_instance != this)
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
            return;
        }

        LoadSavedAudioSettingsOnStartup();
        EnsureAudioSourcesInitialized();
        RebuildInspectorClipRegistry();
        AudioServiceLocator.Register(this);
        SubscribeCombatAudioEvents();
    }

    private void OnEnable()
    {
        if (_instance == this)
        {
            AudioServiceLocator.Register(this);
            SubscribeCombatAudioEvents();
        }
    }

    private void OnApplicationQuit()
    {
        _isShuttingDown = true;
    }

    private void OnDestroy()
    {
        UnsubscribeCombatAudioEvents();
        AudioServiceLocator.Unregister(this);

        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Update()
    {
        if (!_isCrossfading)
        {
            return;
        }

        UpdateCrossfade(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// 테스트 및 외부 제어용으로 BGM 크로스페이드 보간을 지정한 deltaTime만큼 수동 진행합니다.
    /// </summary>
    public void UpdateCrossfade(float unscaledDeltaTime)
    {
        if (!_isCrossfading)
        {
            return;
        }

        if (_crossfadeDuration <= 0f)
        {
            CompleteCrossfade();
            return;
        }

        _crossfadeElapsed += Mathf.Max(0f, unscaledDeltaTime);
        float t = Mathf.Clamp01(_crossfadeElapsed / _crossfadeDuration);

        if (_activeBgmSource != null && _activeBgmSource.clip != null)
        {
            _activeBgmSource.volume = Mathf.Lerp(0f, _bgmVolume, t);
        }

        if (_fadingOutBgmSource != null)
        {
            _fadingOutBgmSource.volume = Mathf.Lerp(_fadeOutStartVolume, 0f, t);
        }

        if (t >= 1f)
        {
            CompleteCrossfade();
        }
    }

    private void CompleteCrossfade()
    {
        _isCrossfading = false;
        _crossfadeElapsed = 0f;
        _crossfadeDuration = 0f;

        if (_activeBgmSource != null && _activeBgmSource.clip != null)
        {
            _activeBgmSource.volume = _bgmVolume;
        }

        if (_fadingOutBgmSource != null)
        {
            _fadingOutBgmSource.Stop();
            _fadingOutBgmSource.clip = null;
            _fadingOutBgmSource.volume = 0f;
        }
    }

    private void EnsureAudioSourcesInitialized()
    {
        if (_audioSourcesInitialized)
        {
            return;
        }
        _audioSourcesInitialized = true;

        _bgmSourceA = CreateChildAudioSource("BGM_DeckA", loop: true);
        _bgmSourceB = CreateChildAudioSource("BGM_DeckB", loop: true);
        _activeBgmSource = _bgmSourceA;
        _fadingOutBgmSource = _bgmSourceB;

        int poolCount = Mathf.Clamp(sfxPoolSize, MinSfxPoolSize, MaxSfxPoolSize);
        _sfxPool = new AudioSource[poolCount];
        for (int i = 0; i < poolCount; i++)
        {
            _sfxPool[i] = CreateChildAudioSource($"SFX_Slot_{i}", loop: false);
        }
    }

    private AudioSource CreateChildAudioSource(string childName, bool loop)
    {
        Transform existing = transform.Find(childName);
        GameObject childGo = existing != null ? existing.gameObject : new GameObject(childName);
        if (existing == null)
        {
            childGo.transform.SetParent(transform, false);
        }

        AudioSource src = childGo.GetComponent<AudioSource>();
        if (src == null)
        {
            src = childGo.AddComponent<AudioSource>();
        }

        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f;
        src.volume = 0f;
        return src;
    }

    private void RebuildInspectorClipRegistry()
    {
        if (registeredClips == null)
        {
            return;
        }

        for (int i = 0; i < registeredClips.Count; i++)
        {
            NamedAudioClipEntry entry = registeredClips[i];
            if (!string.IsNullOrEmpty(entry.key) && entry.clip != null)
            {
                _clipRegistry[entry.key] = entry.clip;
            }
        }
    }

    public void RegisterClip(string key, AudioClip clip)
    {
        if (string.IsNullOrEmpty(key) || clip == null)
        {
            return;
        }

        _clipRegistry[key] = clip;
    }

    public void UnregisterClip(string key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _clipRegistry.Remove(key);
        }
    }

    public bool TryResolveClip(string key, out AudioClip clip)
    {
        clip = null;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (_clipRegistry.TryGetValue(key, out AudioClip registered) && registered != null)
        {
            clip = registered;
            return true;
        }

        if (AssetCacheManager.instance != null && AssetCacheManager.instance.TryGetAudio(key, out AudioClip cached) && cached != null)
        {
            clip = cached;
            return true;
        }

        return false;
    }

    public void PlayBGM(string key, float fadeDuration = 0.8f)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        EnsureAudioSourcesInitialized();

        if (string.Equals(_currentBgmKey, key, StringComparison.Ordinal))
        {
            if (_activeBgmSource != null && (_activeBgmSource.isPlaying || _activeBgmSource.clip == null))
            {
                return;
            }
        }

        if (TryResolveClip(key, out AudioClip clip))
        {
            PlayBGM(clip, fadeDuration, key);
            return;
        }

        // 아직 실제 오디오 클립 에셋이 임포트/등록되지 않은 상태에서도 파이프라인 상태 추적만 깔끔히 유지
        _currentBgmKey = key;
        if (_activeBgmSource != null && _activeBgmSource.isPlaying)
        {
            FadeOutSourceOnly(fadeDuration);
        }
    }

    public void PlayBGM(AudioClip clip, float fadeDuration = 0.8f, string clipKey = null)
    {
        EnsureAudioSourcesInitialized();

        string resolvedKey = !string.IsNullOrEmpty(clipKey) ? clipKey : (clip != null ? clip.name : string.Empty);
        if (clip == null)
        {
            StopBGM(fadeDuration);
            return;
        }

        if (_activeBgmSource != null &&
            ReferenceEquals(_activeBgmSource.clip, clip) &&
            _activeBgmSource.isPlaying)
        {
            _currentBgmKey = resolvedKey;
            return;
        }

        _currentBgmKey = resolvedKey;

        AudioSource previousActive = _activeBgmSource;
        AudioSource nextActive = ReferenceEquals(previousActive, _bgmSourceA) ? _bgmSourceB : _bgmSourceA;

        _activeBgmSource = nextActive;
        _fadingOutBgmSource = previousActive;

        _activeBgmSource.clip = clip;
        _activeBgmSource.loop = true;

        if (fadeDuration <= 0f)
        {
            _isCrossfading = false;
            _activeBgmSource.volume = _bgmVolume;
            _activeBgmSource.Play();

            if (_fadingOutBgmSource != null)
            {
                _fadingOutBgmSource.Stop();
                _fadingOutBgmSource.clip = null;
                _fadingOutBgmSource.volume = 0f;
            }
            return;
        }

        _activeBgmSource.volume = 0f;
        _activeBgmSource.Play();

        _fadeOutStartVolume = (_fadingOutBgmSource != null && _fadingOutBgmSource.isPlaying) ? _fadingOutBgmSource.volume : 0f;
        _crossfadeDuration = fadeDuration;
        _crossfadeElapsed = 0f;
        _isCrossfading = true;
    }

    public void StopBGM(float fadeDuration = 0.5f)
    {
        EnsureAudioSourcesInitialized();
        _currentBgmKey = string.Empty;
        FadeOutSourceOnly(fadeDuration);
    }

    private void FadeOutSourceOnly(float fadeDuration)
    {
        if (fadeDuration <= 0f)
        {
            _isCrossfading = false;
            if (_bgmSourceA != null)
            {
                _bgmSourceA.Stop();
                _bgmSourceA.clip = null;
                _bgmSourceA.volume = 0f;
            }
            if (_bgmSourceB != null)
            {
                _bgmSourceB.Stop();
                _bgmSourceB.clip = null;
                _bgmSourceB.volume = 0f;
            }
            return;
        }

        _fadingOutBgmSource = _activeBgmSource;
        _activeBgmSource = ReferenceEquals(_fadingOutBgmSource, _bgmSourceA) ? _bgmSourceB : _bgmSourceA;
        if (_activeBgmSource != null)
        {
            _activeBgmSource.Stop();
            _activeBgmSource.clip = null;
            _activeBgmSource.volume = 0f;
        }

        _fadeOutStartVolume = (_fadingOutBgmSource != null && _fadingOutBgmSource.isPlaying) ? _fadingOutBgmSource.volume : 0f;
        if (_fadeOutStartVolume <= 0f)
        {
            if (_fadingOutBgmSource != null)
            {
                _fadingOutBgmSource.Stop();
                _fadingOutBgmSource.clip = null;
            }
            _isCrossfading = false;
            return;
        }

        _crossfadeDuration = fadeDuration;
        _crossfadeElapsed = 0f;
        _isCrossfading = true;
    }

    public bool PlaySFX(string key, float pitchVariance = 0.05f, float volumeScale = 1f)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (!TryResolveClip(key, out AudioClip clip))
        {
            return false;
        }

        return PlaySFX(clip, key, pitchVariance, volumeScale);
    }

    public bool PlaySFX(AudioClip clip, string throttleKey = null, float pitchVariance = 0.05f, float volumeScale = 1f)
    {
        if (clip == null)
        {
            return false;
        }

        EnsureAudioSourcesInitialized();

        string effectiveKey = !string.IsNullOrEmpty(throttleKey) ? throttleKey : clip.name;
        float now = ClockProviderForTest != null ? ClockProviderForTest() : Time.unscaledTime;
        float cooldown = GetSfxCooldown(effectiveKey);

        if (cooldown > 0f && _lastSfxPlayedTime.TryGetValue(effectiveKey, out float lastTime))
        {
            if ((now - lastTime) < cooldown)
            {
                return false;
            }
        }

        AudioSource source = AcquireSfxSource();
        if (source == null)
        {
            return false;
        }

        _lastSfxPlayedTime[effectiveKey] = now;
        _lastPlayedSfxKey = effectiveKey;

        float clampedVariance = Mathf.Clamp(pitchVariance, 0f, 0.5f);
        source.pitch = clampedVariance > 0f
            ? 1f + UnityEngine.Random.Range(-clampedVariance, clampedVariance)
            : 1f;
        source.volume = Mathf.Clamp01(_sfxVolume * Mathf.Clamp01(volumeScale));
        source.clip = clip;
        source.Play();
        return true;
    }

    private float GetSfxCooldown(string key)
    {
        if (!string.IsNullOrEmpty(key) && _sfxCooldownOverrides.TryGetValue(key, out float customCooldown))
        {
            return customCooldown;
        }
        return Mathf.Max(0f, defaultSfxCooldown);
    }

    private AudioSource AcquireSfxSource()
    {
        if (_sfxPool == null || _sfxPool.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < _sfxPool.Length; i++)
        {
            int idx = (_sfxNextIndex + i) % _sfxPool.Length;
            AudioSource candidate = _sfxPool[idx];
            if (candidate != null && !candidate.isPlaying)
            {
                _sfxNextIndex = (idx + 1) % _sfxPool.Length;
                return candidate;
            }
        }

        AudioSource stolen = _sfxPool[_sfxNextIndex];
        _sfxNextIndex = (_sfxNextIndex + 1) % _sfxPool.Length;
        if (stolen != null)
        {
            stolen.Stop();
        }
        return stolen;
    }

    private void RefreshActiveBgmVolumes()
    {
        if (_isCrossfading)
        {
            return;
        }

        if (_activeBgmSource != null && _activeBgmSource.clip != null)
        {
            _activeBgmSource.volume = _bgmVolume;
        }
    }

    private void SubscribeCombatAudioEvents()
    {
        if (_combatEventsSubscribed)
        {
            return;
        }
        _combatEventsSubscribed = true;

        Character.OnCharacterDamagedIndicator -= HandleCharacterDamaged;
        Character.OnCharacterDamagedIndicator += HandleCharacterDamaged;
        Character.OnCharacterHealedIndicator -= HandleCharacterHealed;
        Character.OnCharacterHealedIndicator += HandleCharacterHealed;
        Character.OnCharacterShieldGained -= HandleCharacterShieldGained;
        Character.OnCharacterShieldGained += HandleCharacterShieldGained;
        Character.OnCharacterAnyDied -= HandleCharacterDied;
        Character.OnCharacterAnyDied += HandleCharacterDied;
    }

    private void UnsubscribeCombatAudioEvents()
    {
        if (!_combatEventsSubscribed)
        {
            return;
        }
        _combatEventsSubscribed = false;

        Character.OnCharacterDamagedIndicator -= HandleCharacterDamaged;
        Character.OnCharacterHealedIndicator -= HandleCharacterHealed;
        Character.OnCharacterShieldGained -= HandleCharacterShieldGained;
        Character.OnCharacterAnyDied -= HandleCharacterDied;
    }

    private void HandleCharacterDamaged(Character target, float hpDamageAmount, EventStructs.DamageFlag flags)
    {
        AudioServiceLocator.PlaySFX(AudioCueKeys.SFX_COMBAT_HIT, 0.06f);
    }

    private void HandleCharacterHealed(Character target, float healAmount)
    {
        if (healAmount > 0f)
        {
            AudioServiceLocator.PlaySFX(AudioCueKeys.SFX_COMBAT_HEAL, 0.04f);
        }
    }

    private void HandleCharacterShieldGained(Character target, float shieldAmount)
    {
        if (shieldAmount > 0f)
        {
            AudioServiceLocator.PlaySFX(AudioCueKeys.SFX_COMBAT_SHIELD, 0.04f);
        }
    }

    private void HandleCharacterDied(Character deadCharacter)
    {
        AudioServiceLocator.PlaySFX(AudioCueKeys.SFX_COMBAT_DEATH, 0.05f);
    }
}
