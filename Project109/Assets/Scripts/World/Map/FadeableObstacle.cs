using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라와 유닛 사이를 가릴 때 평상시에는 기본 Opaque 머티리얼(Early-Z/그림자/배칭 유지)을 사용하다가,
/// 가림 상태에서만 2-Pass URP 반투명 머티리얼로 전환하여 부드럽게 페이드 처리하는 장애물 컴포넌트입니다.
/// </summary>
public class FadeableObstacle : MonoBehaviour
{
    private const string DefaultFadeShaderName = "Custom/ObstacleSeeThroughFade";

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int FadeAlphaId = Shader.PropertyToID("_FadeAlpha");

    [Header("Renderer & Fade Settings")]
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private Shader fadeShader;
    [SerializeField, Range(0.05f, 0.95f)] private float fadedAlpha = 0.35f;
    [SerializeField] private float fadeSpeed = 8f;

    private Material[][] _originalMaterials;
    private Material[][] _fadeMaterials;
    private readonly List<Material> _createdFadeMaterials = new List<Material>();
    private MaterialPropertyBlock _propertyBlock;

    private bool _isInitialized;
    private bool _isOccluding;
    private bool _isFadeMaterialActive;
    private bool _loggedMissingShader;
    private float _currentAlpha = 1f;

    /// <summary>현재 카메라와 유닛 시선을 가리고 있는지 여부</summary>
    public bool IsOccluding => _isOccluding;

    /// <summary>현재 적용 중인 불투명도(0~1)</summary>
    public float CurrentAlpha => _currentAlpha;

    /// <summary>현재 상태 기준 목표 불투명도</summary>
    public float TargetAlpha => _isOccluding ? fadedAlpha : 1f;

    /// <summary>현재 반투명 페이드 머티리얼이 적용 중인지 여부</summary>
    public bool IsUsingFadeMaterial => _isFadeMaterialActive;

    /// <summary>가림 상태에서의 목표 알파값</summary>
    public float FadedAlpha => fadedAlpha;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDisable()
    {
        ResetImmediate();
    }

    private void OnDestroy()
    {
        ResetImmediate();
        DestroyCreatedFadeMaterials();
    }

    /// <summary>
    /// 가림 상태 진입/이탈을 설정합니다.
    /// </summary>
    public void SetOccluding(bool occluding)
    {
        EnsureInitialized();
        _isOccluding = occluding;
    }

    /// <summary>
    /// 현재 알파값을 목표 알파값으로 보간하고 머티리얼 상태를 갱신합니다.
    /// 추가 보간이 필요하면 true, 목표 상태에 도달해 대기가 필요 없으면 false를 반환합니다.
    /// </summary>
    public bool TickFade(float deltaTime)
    {
        EnsureInitialized();

        float target = TargetAlpha;
        if (deltaTime > 0f)
        {
            float speed = Mathf.Max(0.01f, fadeSpeed);
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, target, speed * deltaTime);
        }

        if (_currentAlpha < 0.999f)
        {
            ApplyFadeState(_currentAlpha);

            if (_isOccluding && Mathf.Abs(_currentAlpha - target) <= 0.0005f)
            {
                _currentAlpha = target;
                ApplyFadeState(_currentAlpha);
                return false;
            }

            return true;
        }

        if (!_isOccluding)
        {
            RestoreOpaqueImmediate();
            return false;
        }

        return true;
    }

    /// <summary>
    /// 즉시 불투명(원본 머티리얼) 상태로 복원합니다.
    /// </summary>
    public void ResetImmediate()
    {
        _isOccluding = false;
        _currentAlpha = 1f;
        RestoreOpaqueImmediate();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (targetRenderers == null)
        {
            targetRenderers = new Renderer[0];
        }

        _originalMaterials = new Material[targetRenderers.Length][];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer r = targetRenderers[i];
            if (r != null)
            {
                _originalMaterials[i] = r.sharedMaterials;
            }
        }

        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        _isInitialized = true;
    }

    private bool EnsureFadeMaterialsCreated()
    {
        if (_fadeMaterials != null)
        {
            return true;
        }

        Shader resolvedShader = fadeShader != null ? fadeShader : Shader.Find(DefaultFadeShaderName);
        if (resolvedShader == null)
        {
            if (!_loggedMissingShader)
            {
                _loggedMissingShader = true;
                Debug.LogError($"[FadeableObstacle] '{DefaultFadeShaderName}' 셰이더를 찾을 수 없어 반투명 전환을 수행할 수 없습니다.");
            }
            return false;
        }

        _fadeMaterials = new Material[targetRenderers.Length][];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Material[] originals = _originalMaterials[i];
            if (originals == null)
            {
                continue;
            }

            Material[] fades = new Material[originals.Length];
            for (int m = 0; m < originals.Length; m++)
            {
                Material srcMat = originals[m];
                Material fadeMat = new Material(resolvedShader);
                fadeMat.hideFlags = HideFlags.DontSave;

                if (srcMat != null)
                {
                    fadeMat.name = srcMat.name + "_SeeThroughFade";
                    if (srcMat.HasProperty(BaseMapId))
                    {
                        fadeMat.SetTexture(BaseMapId, srcMat.GetTexture(BaseMapId));
                        fadeMat.SetTextureScale(BaseMapId, srcMat.GetTextureScale(BaseMapId));
                        fadeMat.SetTextureOffset(BaseMapId, srcMat.GetTextureOffset(BaseMapId));
                    }

                    if (srcMat.HasProperty(BaseColorId))
                    {
                        fadeMat.SetColor(BaseColorId, srcMat.GetColor(BaseColorId));
                    }
                    else if (srcMat.HasProperty(LegacyColorId))
                    {
                        fadeMat.SetColor(BaseColorId, srcMat.GetColor(LegacyColorId));
                    }

                    if (srcMat.HasProperty(SmoothnessId))
                    {
                        fadeMat.SetFloat(SmoothnessId, srcMat.GetFloat(SmoothnessId));
                    }
                }
                else
                {
                    fadeMat.name = "Obstacle_SeeThroughFade";
                }

                fadeMat.SetFloat(FadeAlphaId, fadedAlpha);
                fades[m] = fadeMat;
                _createdFadeMaterials.Add(fadeMat);
            }

            _fadeMaterials[i] = fades;
        }

        return true;
    }

    private void ApplyFadeState(float alpha)
    {
        if (!EnsureFadeMaterialsCreated())
        {
            return;
        }

        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }
        _propertyBlock.SetFloat(FadeAlphaId, alpha);

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer r = targetRenderers[i];
            if (r == null)
            {
                continue;
            }

            if (!_isFadeMaterialActive && _fadeMaterials[i] != null)
            {
                r.sharedMaterials = _fadeMaterials[i];
            }

            r.SetPropertyBlock(_propertyBlock);
        }

        _isFadeMaterialActive = true;
    }

    private void RestoreOpaqueImmediate()
    {
        if (!_isInitialized || targetRenderers == null)
        {
            _isFadeMaterialActive = false;
            return;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer r = targetRenderers[i];
            if (r == null)
            {
                continue;
            }

            r.SetPropertyBlock(null);
            if (_isFadeMaterialActive && _originalMaterials != null && i < _originalMaterials.Length && _originalMaterials[i] != null)
            {
                r.sharedMaterials = _originalMaterials[i];
            }
        }

        _isFadeMaterialActive = false;
    }

    private void DestroyCreatedFadeMaterials()
    {
        for (int i = 0; i < _createdFadeMaterials.Count; i++)
        {
            Material mat = _createdFadeMaterials[i];
            if (mat != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(mat);
                }
                else
                {
                    DestroyImmediate(mat);
                }
            }
        }

        _createdFadeMaterials.Clear();
        _fadeMaterials = null;
    }
}
