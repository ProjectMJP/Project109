using UnityEngine;

/// <summary>
/// MaterialPropertyBlock 기반으로 대상 Renderer 배열에 GC 무할당 점멸(펄스) 하이라이트를 적용하는 재사용 헬퍼입니다.
/// 캐릭터 3D 모델뿐 아니라 바닥 타일, 범위 표시 메시 등에도 동일하게 사용할 수 있으며 URP Lit/Unlit 셰이더를 호환 지원합니다.
/// </summary>
public sealed class RendererPulseHighlighter
{
    public readonly struct PulseColorProfile
    {
        public readonly Color minTintColor;
        public readonly Color maxTintColor;
        public readonly Color minEmissionColor;
        public readonly Color maxEmissionColor;
        public readonly float pulseSpeed;

        public PulseColorProfile(
            Color minTintColor,
            Color maxTintColor,
            Color minEmissionColor,
            Color maxEmissionColor,
            float pulseSpeed)
        {
            this.minTintColor = minTintColor;
            this.maxTintColor = maxTintColor;
            this.minEmissionColor = minEmissionColor;
            this.maxEmissionColor = maxEmissionColor;
            this.pulseSpeed = pulseSpeed;
        }

        public static readonly PulseColorProfile HostileTarget = new PulseColorProfile(
            minTintColor: new Color(1f, 0.78f, 0.74f, 1f),
            maxTintColor: new Color(1f, 0.18f, 0.15f, 1f),
            minEmissionColor: new Color(0.10f, 0.01f, 0.01f, 1f),
            maxEmissionColor: new Color(0.90f, 0.08f, 0.05f, 1f),
            pulseSpeed: 7.0f);

        public static readonly PulseColorProfile FriendlyTarget = new PulseColorProfile(
            minTintColor: new Color(0.75f, 1f, 0.94f, 1f),
            maxTintColor: new Color(0.18f, 0.95f, 0.80f, 1f),
            minEmissionColor: new Color(0.02f, 0.12f, 0.09f, 1f),
            maxEmissionColor: new Color(0.08f, 0.55f, 0.45f, 1f),
            pulseSpeed: 6.0f);
    }

    private static readonly int BaseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorShaderId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorShaderId = Shader.PropertyToID("_EmissionColor");

    private Renderer[] _renderers;
    private MaterialPropertyBlock _propertyBlock;
    private PulseColorProfile _activeProfile;
    private bool _isActive;
    private bool _hasAppliedBlock;
    private float _elapsed;
    private float _phaseOffset;
    private float _currentPulseFactor;
    private Color _lastAppliedTint;
    private Color _lastAppliedEmission;

    public bool IsActive => _isActive;
    public bool HasAppliedBlock => _hasAppliedBlock;
    public float CurrentPulseFactor => _currentPulseFactor;
    public Color LastAppliedTint => _lastAppliedTint;
    public Color LastAppliedEmission => _lastAppliedEmission;

    public void SetRenderers(Renderer[] renderers)
    {
        _renderers = renderers;
    }

    public void BeginPulse(in PulseColorProfile profile, float initialPhase = 0.75f)
    {
        _activeProfile = profile;
        _isActive = true;
        _elapsed = 0f;

        float clampedPhase = Mathf.Clamp01(initialPhase);
        _phaseOffset = Mathf.Asin(Mathf.Clamp(2f * clampedPhase - 1f, -1f, 1f));
        ApplyPulseFactor(clampedPhase);
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (!_isActive)
        {
            return;
        }

        _elapsed += Mathf.Max(0f, unscaledDeltaTime);
        float pulse = 0.5f + 0.5f * Mathf.Sin(_phaseOffset + _elapsed * _activeProfile.pulseSpeed);
        ApplyPulseFactor(pulse);
    }

    public void Clear()
    {
        if (_hasAppliedBlock && _renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r != null)
                {
                    r.SetPropertyBlock(null);
                }
            }
        }

        _isActive = false;
        _hasAppliedBlock = false;
        _elapsed = 0f;
        _phaseOffset = 0f;
        _currentPulseFactor = 0f;
        _lastAppliedTint = default;
        _lastAppliedEmission = default;
    }

    private void ApplyPulseFactor(float pulse)
    {
        _currentPulseFactor = pulse;
        _lastAppliedTint = Color.Lerp(_activeProfile.minTintColor, _activeProfile.maxTintColor, pulse);
        _lastAppliedEmission = Color.Lerp(_activeProfile.minEmissionColor, _activeProfile.maxEmissionColor, pulse);

        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        _propertyBlock.Clear();
        _propertyBlock.SetColor(BaseColorShaderId, _lastAppliedTint);
        _propertyBlock.SetColor(ColorShaderId, _lastAppliedTint);
        _propertyBlock.SetColor(EmissionColorShaderId, _lastAppliedEmission);

        if (_renderers == null)
        {
            return;
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r != null)
            {
                r.SetPropertyBlock(_propertyBlock);
                _hasAppliedBlock = true;
            }
        }
    }
}
