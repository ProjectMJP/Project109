using TMPro;
using UnityEngine;

/// <summary>
/// 은신처 시설 및 오브젝트 프리팹의 WorldspaceUI(Layer 11 TextMeshPro) 자식을 제어하고
/// 메인 카메라 방향을 향하도록 빌보드 회전을 처리하는 컴포넌트입니다.
/// </summary>
public class HideoutWorldLabel : MonoBehaviour
{
    private const string WorldUILayerName = "WorldUI";
    private const int FallbackWorldUILayer = 11;

    [Header("프리팹 바인딩")]
    [SerializeField] private TextMeshPro _tmpText;
    [SerializeField] private Transform _labelRoot;
    [SerializeField] private string _labelText = string.Empty;

    private Camera _cachedCamera;
    private int _worldUiLayer = FallbackWorldUILayer;
    private int _lastLabelChildCount = -1;

    private void Awake()
    {
        InitializeBindings();
    }

    private void Start()
    {
        if (_tmpText == null)
        {
            InitializeBindings();
        }
        FaceCamera();
    }

    private void LateUpdate()
    {
        if (_labelRoot == null) return;

        // TMP_SubMesh 등 하위 자식 오브젝트가 런타임에 생성될 경우 WorldUI 레이어를 유지
        if (_labelRoot.childCount != _lastLabelChildCount)
        {
            SetLayerRecursively(_labelRoot.gameObject, _worldUiLayer);
            _lastLabelChildCount = _labelRoot.childCount;
        }

        FaceCamera();
    }

    /// <summary>
    /// 라벨 문구를 갱신합니다.
    /// </summary>
    public void SetLabelText(string text)
    {
        _labelText = text ?? string.Empty;
        if (_tmpText != null && _tmpText.text != _labelText)
        {
            _tmpText.text = _labelText;
        }
    }

    private void InitializeBindings()
    {
        int resolvedLayer = LayerMask.NameToLayer(WorldUILayerName);
        _worldUiLayer = resolvedLayer != -1 ? resolvedLayer : FallbackWorldUILayer;

        if (_tmpText == null)
        {
            _tmpText = GetComponentInChildren<TextMeshPro>(true);
        }

        if (_tmpText == null)
        {
            Debug.LogError($"[HideoutWorldLabel] '{gameObject.name}' 프리팹에 WorldspaceUI(TextMeshPro) 자식이 연결되어 있지 않습니다.", this);
            enabled = false;
            return;
        }

        if (_labelRoot == null)
        {
            _labelRoot = _tmpText.transform;
        }

        if (!string.IsNullOrEmpty(_labelText))
        {
            _tmpText.text = _labelText;
        }

        SetLayerRecursively(_labelRoot.gameObject, _worldUiLayer);
        _lastLabelChildCount = _labelRoot.childCount;
        FaceCamera();
    }

    private void FaceCamera()
    {
        if (_labelRoot == null) return;

        if (_cachedCamera == null || !_cachedCamera.isActiveAndEnabled)
        {
            _cachedCamera = Camera.main;
        }

        if (_cachedCamera != null)
        {
            _labelRoot.rotation = _cachedCamera.transform.rotation;
        }
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null) return;

        target.layer = layer;
        Transform t = target.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform child = t.GetChild(i);
            if (child != null)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
