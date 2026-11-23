using UnityEngine;

/// <summary>
/// L0 코어 계층에서 오디오 재생 기능을 추상화하는 서비스 계약입니다.
/// L2 World / L4 UI 계층이 L3 Managers(AudioManager)를 직접 역참조하지 않도록 분리합니다.
/// </summary>
public interface IAudioService
{
    float MasterVolume { get; }
    float BgmVolume { get; }
    float SfxVolume { get; }
    string CurrentBgmKey { get; }

    void PlayBGM(string key, float fadeDuration = 0.8f);
    void PlayBGM(AudioClip clip, float fadeDuration = 0.8f, string clipKey = null);
    void StopBGM(float fadeDuration = 0.5f);

    bool PlaySFX(string key, float pitchVariance = 0.05f, float volumeScale = 1f);
    bool PlaySFX(AudioClip clip, string throttleKey = null, float pitchVariance = 0.05f, float volumeScale = 1f);
}

/// <summary>
/// L0 코어 오디오 서비스 로케이터입니다.
/// AudioManager가 등록되지 않은 단위 테스트/헤드리스 환경에서도 부작용 없이 안전하게 무시됩니다.
/// </summary>
public static class AudioServiceLocator
{
    private static IAudioService _current;

    public static IAudioService Current => _current;

    public static void Register(IAudioService service)
    {
        _current = service;
    }

    public static void Unregister(IAudioService service)
    {
        if (ReferenceEquals(_current, service))
        {
            _current = null;
        }
    }

    public static void PlayBGM(string key, float fadeDuration = 0.8f)
    {
        _current?.PlayBGM(key, fadeDuration);
    }

    public static void StopBGM(float fadeDuration = 0.5f)
    {
        _current?.StopBGM(fadeDuration);
    }

    public static bool PlaySFX(string key, float pitchVariance = 0.05f, float volumeScale = 1f)
    {
        return _current != null && _current.PlaySFX(key, pitchVariance, volumeScale);
    }
}
