using CardTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 타깃 트랜스폼을 추적하는 월드 UI 컴포넌트 공통 인터페이스입니다.
/// </summary>
public interface IWorldFollowUI
{
    Transform TargetTransform { get; }
    void SetTarget(Transform target, Vector3? offset = null);
}

public class UIManager : MonoBehaviour
{
    public static UIManager instance { get; private set; }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;

            // DontDestroyOnLoad(this.gameObject);
            InitializeLayers();
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    // UI 부모 레이어 관리 (UIManager 프리팹에서 사전에 바인딩됨)
    [Header("UI Parent Layers (Pre-bound in Prefab)")]
    public Canvas worldUILayer;
    public Canvas normalUILayer;
    public Canvas topUILayer;
    public Canvas popupUILayer;

    // 슬더스식 단일 메인 캔버스 기반 하위 레이어 캐시
    private RectTransform _hudLayerRect;
    private RectTransform _topLayerRect;
    private RectTransform _popupLayerRect;
    private RectTransform _worldLayerRect;

    /// <summary>
    /// 프리팹에서 사전 바인딩된 각 캔버스 레이어의 RectTransform을 캐싱하고 무결성을 검증합니다.
    /// 동적 생성/폴백 땜질 없이, 프리팹 에셋 설정에 기반하여 Fail-Fast 원칙을 준수합니다.
    /// </summary>
    public void InitializeLayers()
    {
        if (normalUILayer != null) _hudLayerRect = normalUILayer.transform as RectTransform;
        else Debug.LogError("[UIManager] normalUILayer가 바인딩되지 않았습니다! UIManager 프리팹 설정을 확인하세요.");

        if (topUILayer != null) _topLayerRect = topUILayer.transform as RectTransform;
        else Debug.LogError("[UIManager] topUILayer가 바인딩되지 않았습니다! UIManager 프리팹 설정을 확인하세요.");

        if (popupUILayer != null) _popupLayerRect = popupUILayer.transform as RectTransform;
        else Debug.LogError("[UIManager] popupUILayer가 바인딩되지 않았습니다! UIManager 프리팹 설정을 확인하세요.");

        if (worldUILayer != null) _worldLayerRect = worldUILayer.transform as RectTransform;
        else Debug.LogError("[UIManager] worldUILayer가 바인딩되지 않았습니다! UIManager 프리팹 설정을 확인하세요.");
    }


    // 1. 인스턴스 캐시 풀 (동일 UI 재사용, 무할당 최적화)
    private readonly Dictionary<string, UIPanelBase> _cachedPanels = new Dictionary<string, UIPanelBase>();

    // 2. 현재 화면에 떠 있는 모달 팝업 스택 (LIFO)
    private readonly Stack<UIPanelBase> _popupStack = new Stack<UIPanelBase>();

    private int _asyncLoadLockCount = 0;
    private int _lastCancelConsumedFrame = -1;

    /// <summary>
    /// 현재 화면에 활성화된 모달 팝업이 하나라도 열려 있는지 여부입니다.
    /// </summary>
    public bool HasActivePopup
    {
        get
        {
            if (_popupStack.Count > 0)
            {
                foreach (var panel in _popupStack)
                {
                    if (panel != null && panel.gameObject.activeInHierarchy)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 이번 프레임에 팝업이 취소(ESC) 입력을 소비하여 닫기 동작을 수행했는지 여부입니다.
    /// </summary>
    public bool IsCancelConsumedThisFrame => _lastCancelConsumedFrame == Time.frameCount;

    /// <summary>
    /// 모달 팝업 스택에 입력을 차단하는 창(blockWorldInput == true)이 떠 있거나 비동기 UI 로딩 중이어서 배경 월드 입력이 차단되어야 하는지 여부입니다.
    /// </summary>
    public bool IsWorldInputBlocked
    {
        get
        {
            if (_asyncLoadLockCount > 0) return true;

            if (_popupStack.Count > 0)
            {
                foreach (var panel in _popupStack)
                {
                    if (panel != null && panel.blockWorldInput && panel.gameObject.activeInHierarchy)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    private void Start()
    {
        SubscribeCancelEvent();
    }

    private void OnEnable()
    {
        SubscribeCancelEvent();
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        UnsubscribeCancelEvent();
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnDestroy()
    {
        UnsubscribeCancelEvent();
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        if (_obstacleDepthClearPass != null)
        {
            _obstacleDepthClearPass.Dispose();
            _obstacleDepthClearPass = null;
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        // 전역 [T] 단축키로 툴팁 표시 On/Off 토글 (입력 필드 포커스 중에는 무시)
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            if (EventSystem.current != null)
            {
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null && selected.GetComponent<TMP_InputField>() != null)
                {
                    return;
                }
            }

            TooltipManager.ToggleGlobalTooltipEnabled();
        }
    }

    private void LateUpdate()
    {
        if (activeWorldUIs.Count > 1)
        {
            SortWorldUILayersByRotatedDepth(Camera.main);
        }
    }

    private void SubscribeCancelEvent()
    {
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnCancelEvent -= HandleCancelEvent;
            PlayerInputController.instance.OnCancelEvent += HandleCancelEvent;
        }
    }

    private void UnsubscribeCancelEvent()
    {
        if (PlayerInputController.HasInstance)
        {
            PlayerInputController.instance.OnCancelEvent -= HandleCancelEvent;
        }
    }

    private void HandleCancelEvent()
    {
        // 우클릭(월드 조작 취소)이 아닌 ESC 키 입력일 때만 UI 취소/일시정지 메뉴 처리를 수행
        if (PlayerInputController.HasInstance && !PlayerInputController.instance.IsEscapeCancel)
        {
            return;
        }

        // 1순위: 현재 열린 팝업/모달 스택 최상위 패널 닫기
        if (TryHandleTopPopupCancel())
        {
            return;
        }

        // 2순위: 전투 중 이동/타깃 선택 취소 대상이 있거나 동일 프레임에 이미 취소된 경우 일시정지 메뉴를 열지 않음
        if (IsBattleCancelActiveOrConsumedThisFrame())
        {
            return;
        }

        // 3순위: 활성 대화 중이거나 비동기 로딩/월드 입력 차단 상태에서는 일시정지 메뉴를 겹쳐 열지 않음
        if ((DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) ||
            _asyncLoadLockCount > 0 ||
            IsWorldInputBlocked ||
            GameSceneManager.instance == null)
        {
            return;
        }

        // 4순위: 유휴 상태(은신처 / 탐험 / 전투 유휴)에서 ESC 입력 시 일시정지 메뉴 열기
        OpenPauseMenu();
    }

    /// <summary>
    /// 최상위 모달 팝업에게 ESC 취소 입력을 전달합니다.
    /// </summary>
    public void CloseTopPopup()
    {
        TryHandleTopPopupCancel();
    }

    private bool TryHandleTopPopupCancel()
    {
        while (_popupStack.Count > 0)
        {
            UIPanelBase topPanel = _popupStack.Peek();
            if (topPanel == null || !topPanel.gameObject.activeInHierarchy)
            {
                _popupStack.Pop();
                continue;
            }

            if (!topPanel.CanCloseByCancel)
            {
                _popupStack.Pop();
                continue;
            }

            _lastCancelConsumedFrame = Time.frameCount;

            // 활성 대화 진행 중에는 ESC로 DialogueUI만 단독 종료되어 대화가 고착되지 않도록 보호합니다.
            if (topPanel is DialogueUI && DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
            {
                return true;
            }

            topPanel.OnCancelInput();
            return true;
        }

        return false;
    }

    private bool IsBattleCancelActiveOrConsumedThisFrame()
    {
        if (RunManager.instance == null || RunManager.instance.activePlayerController == null)
        {
            return false;
        }

        PlayerBattleController battleController = RunManager.instance.activePlayerController as PlayerBattleController;
        if (battleController == null)
        {
            return false;
        }

        return battleController.HasActiveBattleCancelTarget || battleController.IsBattleCancelConsumedThisFrame;
    }

    /// <summary>
    /// 일시정지/설정 메뉴(PauseMenuPanel)를 Popup 레이어에 엽니다.
    /// </summary>
    public void OpenPauseMenu()
    {
        OpenUI(UIConstants.PANEL_PAUSE_MENU, UILayerType.Popup, true);
    }



    #region UI Stack & Caching Lifecycle

    /// <summary>
    /// UI를 열거나 인스턴스를 가져옵니다. 캐시에 존재하면 재사용하고, 없으면 최초 1회 생성합니다.
    /// </summary>
    public GameObject OpenUI(string uiName, UILayerType layerType = UILayerType.Normal, bool startActive = true)
    {
        // 1. 캐시 풀에서 기존 인스턴스 확인 (무할당 재사용)
        if (_cachedPanels.TryGetValue(uiName, out UIPanelBase cachedPanel) && cachedPanel != null)
        {
            if (layerType != UILayerType.Normal)
            {
                cachedPanel.uiLayerType = layerType;
            }

            if (startActive)
            {
                PushActiveUIPanel(cachedPanel);
            }
            return cachedPanel.gameObject;
        }

        // 2. 캐시에 없으면 에셋 캐시에서 프리팹 탐색
        if (AssetCacheManager.instance == null)
        {
            Debug.LogError("[UIManager] AssetCacheManager is null!");
            return null;
        }

        if (!AssetCacheManager.instance.TryGetUI(uiName, out GameObject prefab))
        {
            Debug.LogError($"[UIManager] Failed to find UI prefab in AssetCacheManager cache: {uiName}");
            return null;
        }

        // 프리팹 단계에서 UIPanelBase의 레이어 타입을 먼저 체크합니다.
        UILayerType targetLayerType = layerType;
        if (layerType == UILayerType.Normal && prefab.TryGetComponent<UIPanelBase>(out var panelPrefab))
        {
            targetLayerType = panelPrefab.uiLayerType;
        }

        RectTransform targetLayer = GetLayerTransform(targetLayerType);

        // 생성과 동시에 부모를 명시해 스케일 1,1,1 및 앵커가 프리팹 세팅 그대로 자연스럽게 자리 잡도록 합니다.
        GameObject uiInstance = Instantiate(prefab, targetLayer, false);
        uiInstance.name = uiName;

        if (uiInstance.TryGetComponent<UIPanelBase>(out var panel))
        {
            // 호출자가 명시적으로 Non-Normal 레이어를 요청한 경우 이를 우선 적용하고,
            // 그렇지 않으면 Awake()에서 설정한 레이어(프리팹 기본값보다 우선)를 존중합니다.
            UILayerType resolvedLayerType = layerType != UILayerType.Normal ? layerType : panel.uiLayerType;
            panel.uiLayerType = resolvedLayerType;

            RectTransform resolvedLayerRect = GetLayerTransform(resolvedLayerType);
            if (resolvedLayerRect != null && panel.transform.parent != resolvedLayerRect)
            {
                panel.transform.SetParent(resolvedLayerRect, false);
            }

            _cachedPanels[uiName] = panel;

            if (startActive)
            {
                PushActiveUIPanel(panel);
            }
            else
            {
                uiInstance.SetActive(false);
            }
        }
        else
        {
            if (!startActive)
            {
                uiInstance.SetActive(false);
            }
        }

        return uiInstance;
    }

    public T OpenUI<T>(string uiName, UILayerType layerType = UILayerType.Normal, bool startActive = true) where T : UIPanelBase
    {
        GameObject obj = OpenUI(uiName, layerType, startActive);
        return obj != null ? obj.GetComponent<T>() : null;
    }

    /// <summary>
    /// 지정된 UI 패널이 열려있으면 닫고, 닫혀있거나 생성되지 않았으면 엽니다.
    /// </summary>
    public T ToggleUI<T>(string uiName, UILayerType layerType = UILayerType.Normal, Action<T> onFirstCreated = null) where T : UIPanelBase
    {
        // 1. 이미 캐시된 인스턴스가 있는지 확인
        if (_cachedPanels.TryGetValue(uiName, out UIPanelBase cachedPanel) && cachedPanel != null)
        {
            if (cachedPanel.gameObject.activeSelf)
            {
                CloseUI(cachedPanel);
                return cachedPanel as T;
            }
            else
            {
                PushActiveUIPanel(cachedPanel);
                return cachedPanel as T;
            }
        }

        // 2. 캐시에 없으면 새로 생성하여 열기
        T newPanel = OpenUI<T>(uiName, layerType, true);
        if (newPanel != null && onFirstCreated != null)
        {
            onFirstCreated.Invoke(newPanel);
        }
        return newPanel;
    }

    /// <summary>
    /// 현재 캐시되어 있는 지정된 UI 패널을 가져옵니다.
    /// </summary>
    public T GetUI<T>(string uiName) where T : UIPanelBase
    {
        if (_cachedPanels.TryGetValue(uiName, out UIPanelBase panel) && panel != null)
        {
            return panel as T;
        }
        return null;
    }

    private RectTransform GetLayerTransform(UILayerType layerType)
    {

        switch (layerType)
        {
            case UILayerType.World:
                return _worldLayerRect != null ? _worldLayerRect : (worldUILayer != null ? worldUILayer.transform as RectTransform : null);
            case UILayerType.Normal:
                return _hudLayerRect != null ? _hudLayerRect : (normalUILayer != null ? normalUILayer.transform as RectTransform : null);
            case UILayerType.Top:
                return _topLayerRect != null ? _topLayerRect : (topUILayer != null ? topUILayer.transform as RectTransform : null);
            case UILayerType.Popup:
                return _popupLayerRect != null ? _popupLayerRect : (popupUILayer != null ? popupUILayer.transform as RectTransform : null);
            default:
                return transform as RectTransform;
        }
    }

    private struct WorldUISortMeta
    {
        public Component component;
        public IWorldFollowUI followUI;
        public int typeOrder;
    }

    private struct WorldUISortEntry
    {
        public Transform child;
        public float rotatedDepth;
        public int typeOrder;
        public bool isHighlighted;
        public int originalIndex;
    }

    private static readonly Comparison<WorldUISortEntry> WorldUISortComparison = CompareWorldUISortEntries;

    private readonly List<Component> activeWorldUIs = new List<Component>();
    private readonly Dictionary<Transform, WorldUISortMeta> _worldUIMetaByTransform = new Dictionary<Transform, WorldUISortMeta>();
    private readonly HashSet<Component> _highlightedWorldUIs = new HashSet<Component>();
    private readonly List<WorldUISortEntry> _worldUISortBuffer = new List<WorldUISortEntry>(32);
    private Canvas _worldDepthUILayer;
    private ObstacleDepthClearRenderPass _obstacleDepthClearPass;
    private int _cachedWorldUiLayer = -1;
    private int _cachedWorldDepthLayer = -1;

    #region World UI Factory & Lifecycle

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam == null || (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView))
        {
            return;
        }

        if (!HasActiveDepthWorldUI())
        {
            return;
        }

        UniversalAdditionalCameraData cameraData = cam.GetUniversalAdditionalCameraData();
        if (cameraData == null || cameraData.renderType != CameraRenderType.Base || cameraData.scriptableRenderer == null)
        {
            return;
        }

        if (_obstacleDepthClearPass == null)
        {
            _obstacleDepthClearPass = new ObstacleDepthClearRenderPass();
        }

        if (_obstacleDepthClearPass.IsReady)
        {
            cameraData.scriptableRenderer.EnqueuePass(_obstacleDepthClearPass);
        }
    }

    private bool HasActiveDepthWorldUI()
    {
        if (_worldDepthUILayer == null)
        {
            return false;
        }

        Transform depthParent = _worldDepthUILayer.transform;
        int childCount = depthParent.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = depthParent.GetChild(i);
            if (child != null && child.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 월드 UI의 호버·타깃팅 강조 상태를 등록합니다. 강조된 UI는 회전 Y-Order 정렬 시 최상단에 배치됩니다.
    /// </summary>
    public void SetWorldUIHighlighted(Component uiComponent, bool highlighted)
    {
        if (uiComponent == null)
        {
            return;
        }

        if (highlighted)
        {
            _highlightedWorldUIs.Add(uiComponent);
        }
        else
        {
            _highlightedWorldUIs.Remove(uiComponent);
        }
    }

    /// <summary>
    /// 카메라 회전에 맞춘 수평 전방 벡터(camForwardXZ) 내적 기준으로
    /// WorldUILayer 및 WorldDepthUILayer의 활성 월드 UI를 회전 Y-Order(멀리 있는 유닛 → 가까운 유닛 순)로 정렬합니다.
    /// 실제 순서 변경이 발생한 경우에만 SetSiblingIndex를 호출하여 정지 상태의 캔버스 리빌드를 방지합니다.
    /// </summary>
    public void SortWorldUILayersByRotatedDepth(Camera cam)
    {
        if (cam == null)
        {
            return;
        }

        Vector3 camForward = cam.transform.forward;
        Vector3 sortAxis = new Vector3(camForward.x, 0f, camForward.z);
        if (sortAxis.sqrMagnitude < 0.0001f)
        {
            sortAxis = camForward;
        }
        else
        {
            sortAxis.Normalize();
        }

        if (worldUILayer != null)
        {
            SortLayerChildrenByRotatedDepth(worldUILayer.transform, sortAxis);
        }

        if (_worldDepthUILayer != null)
        {
            SortLayerChildrenByRotatedDepth(_worldDepthUILayer.transform, sortAxis);
        }
    }

    private void SortLayerChildrenByRotatedDepth(Transform layerTransform, Vector3 sortAxis)
    {
        if (layerTransform == null)
        {
            return;
        }

        int childCount = layerTransform.childCount;
        if (childCount <= 1)
        {
            return;
        }

        _worldUISortBuffer.Clear();
        for (int i = 0; i < childCount; i++)
        {
            Transform child = layerTransform.GetChild(i);
            if (child == null)
            {
                continue;
            }

            Vector3 refPos = child.position;
            int typeOrder = 1;
            bool isHighlighted = false;

            if (_worldUIMetaByTransform.TryGetValue(child, out WorldUISortMeta meta))
            {
                if (meta.followUI != null && meta.followUI.TargetTransform != null)
                {
                    refPos = meta.followUI.TargetTransform.position;
                }
                typeOrder = meta.typeOrder;
                isHighlighted = meta.component != null && _highlightedWorldUIs.Contains(meta.component);
            }

            _worldUISortBuffer.Add(new WorldUISortEntry
            {
                child = child,
                rotatedDepth = Vector3.Dot(refPos, sortAxis),
                typeOrder = typeOrder,
                isHighlighted = isHighlighted,
                originalIndex = i
            });
        }

        if (_worldUISortBuffer.Count <= 1)
        {
            return;
        }

        _worldUISortBuffer.Sort(WorldUISortComparison);

        for (int i = 0; i < _worldUISortBuffer.Count; i++)
        {
            Transform child = _worldUISortBuffer[i].child;
            if (child != null && child.GetSiblingIndex() != i)
            {
                child.SetSiblingIndex(i);
            }
        }
    }

    private static int CompareWorldUISortEntries(WorldUISortEntry a, WorldUISortEntry b)
    {
        if (a.isHighlighted != b.isHighlighted)
        {
            return a.isHighlighted ? 1 : -1;
        }

        // 내림차순: 카메라 수평 전방 벡터 방향(화면 위쪽/먼 유닛)이 먼저, 가까운 유닛이 나중에 그려짐
        const float depthEpsilon = 0.0005f;
        float depthDiff = b.rotatedDepth - a.rotatedDepth;
        if (depthDiff > depthEpsilon) return 1;
        if (depthDiff < -depthEpsilon) return -1;

        int typeCmp = a.typeOrder.CompareTo(b.typeOrder);
        if (typeCmp != 0)
        {
            return typeCmp;
        }

        return a.originalIndex.CompareTo(b.originalIndex);
    }

    private static int ResolveWorldUITypeOrder(Component uiComponent)
    {
        if (uiComponent is CharacterEffectListUI) return 0;
        if (uiComponent is CharacterStatusBarUI) return 1;
        if (uiComponent is CharacterIntentUI) return 2;
        return 1;
    }

    private int GetWorldUIOverlayLayer()
    {
        if (_cachedWorldUiLayer < 0)
        {
            _cachedWorldUiLayer = LayerMask.NameToLayer("WorldUI");
        }
        return _cachedWorldUiLayer;
    }

    private int GetWorldUIDepthLayer()
    {
        if (_cachedWorldDepthLayer < 0)
        {
            _cachedWorldDepthLayer = LayerMask.NameToLayer(ObstacleDepthClearRenderPass.WorldDepthUILayerName);
            if (_cachedWorldDepthLayer < 0)
            {
                _cachedWorldDepthLayer = LayerMask.NameToLayer("UI");
            }
            if (_cachedWorldDepthLayer < 0)
            {
                _cachedWorldDepthLayer = 0;
            }
        }
        return _cachedWorldDepthLayer;
    }

    /// <summary>
    /// 3D 깊이 테스트(Main Camera)를 적용받는 월드 공간 UI 전용 캔버스를 지연 확보합니다.
    /// UGUI는 가장 가까운 상위 Canvas의 레이어로 배치를 렌더링하므로, Overlay용 worldUILayer와 분리해 관리합니다.
    /// </summary>
    private Canvas EnsureWorldDepthUILayer()
    {
        if (_worldDepthUILayer != null)
        {
            return _worldDepthUILayer;
        }

        if (worldUILayer == null)
        {
            return null;
        }

        Transform parent = worldUILayer.transform.parent;
        if (parent != null)
        {
            Transform existing = parent.Find("WorldDepthUILayer");
            if (existing != null)
            {
                _worldDepthUILayer = existing.GetComponent<Canvas>();
                if (_worldDepthUILayer != null)
                {
                    return _worldDepthUILayer;
                }
            }
        }

        GameObject depthLayerObj = new GameObject("WorldDepthUILayer", typeof(RectTransform), typeof(Canvas));
        depthLayerObj.layer = GetWorldUIDepthLayer();
        depthLayerObj.transform.SetParent(parent, false);

        RectTransform srcRect = worldUILayer.transform as RectTransform;
        RectTransform dstRect = depthLayerObj.GetComponent<RectTransform>();
        if (srcRect != null && dstRect != null)
        {
            dstRect.anchorMin = srcRect.anchorMin;
            dstRect.anchorMax = srcRect.anchorMax;
            dstRect.pivot = srcRect.pivot;
            dstRect.anchoredPosition3D = srcRect.anchoredPosition3D;
            dstRect.sizeDelta = srcRect.sizeDelta;
            dstRect.localRotation = srcRect.localRotation;
            dstRect.localScale = srcRect.localScale;
        }

        Canvas depthCanvas = depthLayerObj.GetComponent<Canvas>();
        depthCanvas.renderMode = RenderMode.WorldSpace;
        depthCanvas.worldCamera = null;
        depthCanvas.overrideSorting = false;

        _worldDepthUILayer = depthCanvas;
        return _worldDepthUILayer;
    }

    /// <summary>
    /// 개별 월드 UI의 렌더링 모드를 Overlay(WorldUICamera, 3D 통과) 또는 DepthTest(Main Camera, 3D 깊이 가림)로 전환합니다.
    /// </summary>
    public void SetWorldUIOverlay(Component uiComponent, bool useOverlay)
    {
        if (uiComponent == null || uiComponent.gameObject == null)
        {
            return;
        }

        Transform targetParent = useOverlay
            ? (worldUILayer != null ? worldUILayer.transform : null)
            : (EnsureWorldDepthUILayer() != null ? _worldDepthUILayer.transform : null);

        int targetLayer = useOverlay ? GetWorldUIOverlayLayer() : GetWorldUIDepthLayer();

        if (targetParent != null && uiComponent.transform.parent != targetParent)
        {
            uiComponent.transform.SetParent(targetParent, worldPositionStays: true);
            uiComponent.transform.localScale = Vector3.one;
        }

        if (targetLayer >= 0 && uiComponent.gameObject.layer != targetLayer)
        {
            SetLayerRecursively(uiComponent.gameObject, targetLayer);
        }

        if (useOverlay)
        {
            uiComponent.transform.SetAsLastSibling();
        }
    }

    public static void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null || newLayer < 0) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child != null)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }
    }

    /// <summary>
    /// 캐릭터의 머리 위 상태바(CharacterStatusBarUI)를 World UI Layer 하위에 생성하여 반환합니다.
    /// </summary>
    /// <summary>
    /// 월드 UI 레이어에 지정한 프리팹을 생성하고 공통 설정(레이어, 스케일, 추적 타깃, 활성 목록 등록)을 적용합니다.
    /// </summary>
    public T CreateWorldUI<T>(string prefabKey, Transform target, Vector3 offset) where T : Component
    {
        if (string.IsNullOrEmpty(prefabKey))
        {
            Debug.LogError("[UIManager] CreateWorldUI에 전달된 prefabKey가 비어 있습니다.");
            return null;
        }

        Transform parentTransform = (_worldLayerRect != null) ? _worldLayerRect : ((worldUILayer != null) ? worldUILayer.transform : transform);
        GameObject prefab = null;

        if (AssetCacheManager.instance != null)
        {
            AssetCacheManager.instance.TryGetUI(prefabKey, out prefab);
        }

        if (prefab == null)
        {
            Debug.LogError($"[UIManager] 필수 프리팹({prefabKey})이 AssetCacheManager에 로드되지 않았습니다.");
            return null;
        }

        string targetName = target != null ? target.name : "Target";
        GameObject go = Instantiate(prefab, parentTransform);
        go.name = $"{typeof(T).Name}_{targetName}";
        SetLayerRecursively(go, LayerMask.NameToLayer("WorldUI"));

        T uiComponent = go.GetComponent<T>();
        if (uiComponent == null)
        {
            Debug.LogError($"[UIManager] 프리팹({prefabKey})에 {typeof(T).Name} 컴포넌트가 누락되어 있습니다!");
            Destroy(go);
            return null;
        }

        // World Space 캔버스의 scale이 0.01f이므로 자식 UI의 기본 scale을 (1, 1, 1)로 보장합니다.
        uiComponent.transform.localScale = Vector3.one;
        IWorldFollowUI followUI = uiComponent as IWorldFollowUI;
        if (followUI != null)
        {
            followUI.SetTarget(target, offset);
        }

        _worldUIMetaByTransform[go.transform] = new WorldUISortMeta
        {
            component = uiComponent,
            followUI = followUI,
            typeOrder = ResolveWorldUITypeOrder(uiComponent)
        };

        activeWorldUIs.Add(uiComponent);
        return uiComponent;
    }

    public CharacterStatusBarUI CreateStatusBarUI(Transform target, Vector3 offset)
    {
        return CreateWorldUI<CharacterStatusBarUI>(UIConstants.PREFAB_CHARACTER_STATUS_BAR, target, offset);
    }

    /// <summary>
    /// 캐릭터의 머리 위 이펙트 목록(CharacterEffectListUI)을 World UI Layer 하위에 생성하여 반환합니다.
    /// </summary>
    public CharacterEffectListUI CreateEffectListUI(Transform target, Vector3 offset)
    {
        return CreateWorldUI<CharacterEffectListUI>(UIConstants.PREFAB_CHARACTER_EFFECT_LIST, target, offset);
    }

    /// <summary>
    /// 적 캐릭터의 머리 위 행동 예고 UI(CharacterIntentUI)를 World UI Layer 하위에 생성하여 반환합니다.
    /// </summary>
    public CharacterIntentUI CreateIntentUI(Transform target, Vector3 offset)
    {
        return CreateWorldUI<CharacterIntentUI>(UIConstants.PREFAB_CHARACTER_INTENT, target, offset);
    }

    /// <summary>
    /// 지정된 월드 UI 컴포넌트를 관리 목록에서 제거하고 안전하게 파괴합니다.
    /// </summary>
    public void ReleaseWorldUI(Component uiComponent)
    {
        if (uiComponent == null) return;

        activeWorldUIs.Remove(uiComponent);
        _highlightedWorldUIs.Remove(uiComponent);
        if (uiComponent.transform != null)
        {
            _worldUIMetaByTransform.Remove(uiComponent.transform);
        }
        if (uiComponent.gameObject != null)
        {
            Destroy(uiComponent.gameObject);
        }
    }

    /// <summary>
    /// World UI Layer에 생성된 모든 월드 공간 UI 객체를 제거합니다.
    /// </summary>
    public void ClearWorldUILayer()
    {
        for (int i = activeWorldUIs.Count - 1; i >= 0; i--)
        {
            if (activeWorldUIs[i] != null && activeWorldUIs[i].gameObject != null)
            {
                Destroy(activeWorldUIs[i].gameObject);
            }
        }
        activeWorldUIs.Clear();
        _worldUIMetaByTransform.Clear();
        _highlightedWorldUIs.Clear();

        if (worldUILayer != null)
        {
            Transform parent = worldUILayer.transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        if (_worldDepthUILayer != null)
        {
            Transform depthParent = _worldDepthUILayer.transform;
            for (int i = depthParent.childCount - 1; i >= 0; i--)
            {
                Destroy(depthParent.GetChild(i).gameObject);
            }
        }
    }

    #endregion

    /// <summary>
    /// UI 패널을 활성화하고 모달 스택에 등록합니다.
    /// </summary>
    public void PushActiveUIPanel(UIPanelBase panel)
    {
        if (panel == null) return;

        RectTransform targetLayer = GetLayerTransform(panel.uiLayerType);
        if (targetLayer != null && panel.transform.parent != targetLayer)
        {
            panel.transform.SetParent(targetLayer, false);
        }

        panel.transform.SetAsLastSibling();

        // Normal 또는 Popup 레이어이면서 닫기 가능(또는 활성 대화창)인 경우에만 모달 스택으로 관리
        if ((panel.uiLayerType == UILayerType.Normal || panel.uiLayerType == UILayerType.Popup) &&
            (panel.CanCloseByCancel || panel is DialogueUI))
        {
            if (!_popupStack.Contains(panel))
            {
                _popupStack.Push(panel);
            }
            Debug.Log($"[UIManager] Pushed Modal Popup: {panel.name}. Stack Count: {_popupStack.Count}");
        }

        panel.gameObject.SetActive(true);
        panel.OnOpen();

        EvaluateWorldInputLock();
    }

    public void PushActiveUIPanel(GameObject newObject)
    {
        if (newObject != null && newObject.TryGetComponent<UIPanelBase>(out var panel))
        {
            PushActiveUIPanel(panel);
        }
        else if (newObject != null)
        {
            newObject.SetActive(true);
            newObject.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// 지정된 패널을 닫고 모달 스택에서 제거하며, 정책에 따라 비활성화하거나 파괴합니다.
    /// </summary>
    public void CloseUI(UIPanelBase panel)
    {
        if (panel == null) return;

        RemovePanelFromStack(panel);
        panel.OnClose();

        if (panel.DestroyOnClose)
        {
            _cachedPanels.Remove(panel.name);
            Destroy(panel.gameObject);
            Debug.Log($"[UIManager] Destroyed 1-time panel: {panel.name}. Stack Count: {_popupStack.Count}");
        }
        else
        {
            panel.gameObject.SetActive(false);
            Debug.Log($"[UIManager] Deactivated reusable panel: {panel.name}. Stack Count: {_popupStack.Count}");
        }

        EvaluateWorldInputLock();
    }

    public void CloseUI(GameObject targetObject)
    {
        if (targetObject != null && targetObject.TryGetComponent<UIPanelBase>(out var panel))
        {
            CloseUI(panel);
        }
        else if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
    }

    /// <summary>
    /// UI 이름으로 패널을 찾아 닫습니다.
    /// </summary>
    public void CloseUI(string uiName)
    {
        if (_cachedPanels.TryGetValue(uiName, out UIPanelBase panel) && panel != null)
        {
            CloseUI(panel);
        }
    }

    /// <summary>
    /// 특정 패널을 모달 스택 및 캐시 풀에서 완전히 제거하고 파괴합니다.
    /// </summary>
    public void DestroyUI(string uiName)
    {
        if (_cachedPanels.TryGetValue(uiName, out UIPanelBase panel))
        {
            _cachedPanels.Remove(uiName);
            if (panel != null)
            {
                RemovePanelFromStack(panel);
                if (panel.gameObject != null)
                {
                    Destroy(panel.gameObject);
                }
            }
        }
    }

    private void RemovePanelFromStack(UIPanelBase targetPanel)
    {
        if (_popupStack.Count == 0 || targetPanel == null) return;

        if (_popupStack.Peek() == targetPanel)
        {
            _popupStack.Pop();
        }
        else
        {
            Stack<UIPanelBase> tempStack = new Stack<UIPanelBase>();
            while (_popupStack.Count > 0)
            {
                UIPanelBase current = _popupStack.Pop();
                if (current == targetPanel) break;
                tempStack.Push(current);
            }
            while (tempStack.Count > 0)
            {
                _popupStack.Push(tempStack.Pop());
            }
        }
    }

    /// <summary>
    /// 모달 팝업 스택 상태에 따라 배경 월드 입력을 100% 안전하게 동기화합니다.
    /// </summary>
    private void EvaluateWorldInputLock()
    {
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.IsWorldInputBlocked = IsWorldInputBlocked;
        }
    }

    #endregion
    #region 카드 범위확인 UI (GameMap으로 위임)

    private Vector2Int GetPlayerOriginCoord()
    {
        if (GameMap.current != null && GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            return GameMap.current.GetCharacterCoordinate(GameSceneManager.instance.player.character);
        }
        return Vector2Int.zero;
    }

    /// <summary>
    /// 카드 데이터의 효과 범위를 표시합니다. 실제 타일맵 제어는 World 계층인 GameMap.current가 전담합니다.
    /// </summary>
    public void UpdateEffectAreaUI(CardData newCardData)
    {
        UpdateEffectAreaUI(GetPlayerOriginCoord(), newCardData);
    }

    public void UpdateEffectAreaUI(Vector2Int originCoord, CardData newCardData)
    {
        if (GameMap.current != null)
        {
            GameMap.current.UpdateEffectArea(originCoord, newCardData);
        }
    }

    public void UpdateEffectAreaUI(Card card)
    {
        UpdateEffectAreaUI(GetPlayerOriginCoord(), card);
    }

    public void UpdateEffectAreaUI(Vector2Int originCoord, Card card)
    {
        if (GameMap.current != null)
        {
            GameMap.current.UpdateEffectArea(originCoord, card);
        }
    }

    public void ClearEffectAreaTiles()
    {
        if (GameMap.current != null)
        {
            GameMap.current.ClearEffectAreaTiles();
        }
    }

    #endregion


    /// <summary>
    /// 스테이지 리셋 시 활성화된 ExploreMap을 정리합니다.
    /// </summary>
    public void DestroyExploreMap()
    {
        DestroyUI(UIConstants.PANEL_EXPLORE_MAP);
        Debug.Log("[UIManager] Existing ExploreMap instance destroyed for stage reset.");
    }

    #region Async UI & Dialog Support
    public System.Collections.IEnumerator OpenUIAsyncCoroutine<T>(string uiName, UILayerType layerType, bool blockWorldInput, Action<T> onComplete) where T : UIPanelBase
    {
        bool acquiredPreLock = false;

        // 1. 에셋 비동기 로딩을 시작하기 전에 '선제적'으로 터치 입력 차단
        if (blockWorldInput)
        {
            _asyncLoadLockCount++;
            acquiredPreLock = true;
            EvaluateWorldInputLock();
        }

        // 2. 비동기 에셋 캐시 획득 및 인스턴스화
        GameObject prefab = null;
        yield return StartCoroutine(AssetCacheManager.instance.GetUIAsyncCoroutine(uiName, (result) => prefab = result));

        if (prefab == null)
        {
            Debug.LogError($"[UIManager] Failed to load UI async: {uiName}");
            if (acquiredPreLock)
            {
                _asyncLoadLockCount = Mathf.Max(0, _asyncLoadLockCount - 1);
                EvaluateWorldInputLock();
            }
            onComplete?.Invoke(null);
            yield break;
        }

        RectTransform targetLayer = GetLayerTransform(layerType);
        GameObject uiInstance = Instantiate(prefab, targetLayer, false);
        uiInstance.name = uiName;

        T panel = uiInstance.GetComponent<T>();
        if (panel != null)
        {
            panel.blockWorldInput = blockWorldInput;
            panel.uiLayerType = layerType;
        }

        if (uiInstance != null)
        {
            PushActiveUIPanel(uiInstance);
        }

        // 선제 락을 해제합니다.
        if (acquiredPreLock)
        {
            _asyncLoadLockCount = Mathf.Max(0, _asyncLoadLockCount - 1);
            EvaluateWorldInputLock();
        }

        onComplete?.Invoke(panel);
    }

    public void ShowConfirmDialog(string title, string message, Action onConfirm, Action onCancel)
    {
        // 팝업 레이어(popupUILayer)에 띄우도록 설정
        GameObject dialogObj = OpenUI(UIConstants.PANEL_CONFIRM_DIALOG, UILayerType.Popup, true);
        if (dialogObj != null)
        {
            UIDialogPanel dialogPanel = dialogObj.GetComponent<UIDialogPanel>();
            if (dialogPanel != null)
            {
                dialogPanel.Setup(title, message, onConfirm, onCancel);
            }
            else
            {
                Debug.LogError("[UIManager] ConfirmDialog prefab does not have UIDialogPanel component!");
            }
        }
        else
        {
            Debug.LogError($"[UIManager] ConfirmDialog 프리팹('{UIConstants.PANEL_CONFIRM_DIALOG}')을 캐시에서 찾을 수 없어 다이얼로그를 표시하지 못했습니다.");
        }
    }

    public BattleUIPanel GetOrSpawnBattleUI()
    {
        GameObject spawned = OpenUI(UIConstants.PANEL_BATTLE_UI, UILayerType.Top, true);
        return spawned != null ? spawned.GetComponent<BattleUIPanel>() : null;
    }

    public BattleTimelinePanel GetOrSpawnBattleTimelineUI()
    {
        return OpenUI<BattleTimelinePanel>(UIConstants.PANEL_BATTLE_TIMELINE, UILayerType.Top, false);
    }

    public BattleSideUnitHUDPanel GetOrSpawnBattleSideUnitHUD()
    {
        return OpenUI<BattleSideUnitHUDPanel>(UIConstants.PANEL_BATTLE_SIDE_UNIT_HUD, UILayerType.Top, false);
    }
    #endregion
}
