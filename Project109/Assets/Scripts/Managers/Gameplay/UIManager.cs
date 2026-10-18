using CardTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

    /// <summary>
    /// 하위 호환성을 위한 래퍼 메서드입니다.
    /// </summary>
    public void EnsureCanvases() => InitializeLayers();

    // 1. 인스턴스 캐시 풀 (동일 UI 재사용, 무할당 최적화)
    private readonly Dictionary<string, UIPanelBase> _cachedPanels = new Dictionary<string, UIPanelBase>();

    // 2. 현재 화면에 떠 있는 모달 팝업 스택 (LIFO)
    private readonly Stack<UIPanelBase> _popupStack = new Stack<UIPanelBase>();

    private int _asyncLoadLockCount = 0;

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

    // 카드 범위 확인 관련 변수 (하위 호환 필드)
    [Header("Effect Area Tiles (Optional)")]
    public EffectAreaManager effectAreaManager;
    public EffectAreaTile effectAreaTile;
    public EffectAreaTile AdditionalEffectAreaTile;

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        // ESC(취소) 키 입력 감지 시 최상위 팝업에 닫기 요청 전달
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseTopPopup();
        }
    }

    /// <summary>
    /// 최상위 모달 팝업에게 ESC 취소 입력을 전달합니다.
    /// </summary>
    public void CloseTopPopup()
    {
        if (_popupStack.Count > 0)
        {
            UIPanelBase topPanel = _popupStack.Peek();
            topPanel.OnCancelInput();
        }
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
        if (prefab.TryGetComponent<UIPanelBase>(out var panelPrefab))
        {
            targetLayerType = panelPrefab.uiLayerType;
        }

        RectTransform targetLayer = GetLayerTransform(targetLayerType);

        // 생성과 동시에 부모를 명시해 스케일 1,1,1 및 앵커가 프리팹 세팅 그대로 자연스럽게 자리 잡도록 합니다.
        GameObject uiInstance = Instantiate(prefab, targetLayer, false);
        uiInstance.name = uiName;

        if (uiInstance.TryGetComponent<UIPanelBase>(out var panel))
        {
            panel.uiLayerType = targetLayerType;
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
        EnsureCanvases();

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

    private readonly List<Component> activeWorldUIs = new List<Component>();

    #region World UI Factory & Lifecycle

    /// <summary>
    /// 캐릭터의 머리 위 상태바(CharacterStatusBarUI)를 World UI Layer 하위에 생성하여 반환합니다.
    /// </summary>
    public CharacterStatusBarUI CreateStatusBarUI(Transform target, Vector3 offset)
    {
        EnsureCanvases();

        Transform parentTransform = (_worldLayerRect != null) ? _worldLayerRect : ((worldUILayer != null) ? worldUILayer.transform : transform);
        GameObject prefab = null;

        if (AssetCacheManager.instance != null)
        {
            AssetCacheManager.instance.TryGetUI(UIConstants.PREFAB_CHARACTER_STATUS_BAR, out prefab);
        }

        CharacterStatusBarUI statusBarUI = null;
        if (prefab != null)
        {
            GameObject go = Instantiate(prefab, parentTransform);
            go.name = $"CharacterStatusBar_{target?.name ?? "Target"}";
            statusBarUI = go.GetComponent<CharacterStatusBarUI>();
            if (statusBarUI == null) statusBarUI = go.AddComponent<CharacterStatusBarUI>();
        }
        else
        {
            GameObject go = new GameObject($"CharacterStatusBar_{target?.name ?? "Target"}");
            go.transform.SetParent(parentTransform, false);
            statusBarUI = go.AddComponent<CharacterStatusBarUI>();
        }

        if (statusBarUI != null)
        {
            // World Space 캔버스의 scale이 0.01f이므로 자식 UI의 기본 scale을 (1, 1, 1)로 보장합니다.
            statusBarUI.transform.localScale = Vector3.one;
            statusBarUI.SetTarget(target, offset);
            activeWorldUIs.Add(statusBarUI);
        }

        return statusBarUI;
    }

    /// <summary>
    /// 캐릭터의 머리 위 이펙트 목록(CharacterEffectListUI)을 World UI Layer 하위에 생성하여 반환합니다.
    /// </summary>
    public CharacterEffectListUI CreateEffectListUI(Transform target, Vector3 offset)
    {
        EnsureCanvases();

        Transform parentTransform = (_worldLayerRect != null) ? _worldLayerRect : ((worldUILayer != null) ? worldUILayer.transform : transform);
        GameObject prefab = null;

        if (AssetCacheManager.instance != null)
        {
            AssetCacheManager.instance.TryGetUI(UIConstants.PREFAB_CHARACTER_EFFECT_LIST, out prefab);
        }

        CharacterEffectListUI effectListUI = null;
        if (prefab != null)
        {
            GameObject go = Instantiate(prefab, parentTransform);
            go.name = $"CharacterEffectList_{target?.name ?? "Target"}";
            effectListUI = go.GetComponent<CharacterEffectListUI>();
            if (effectListUI == null) effectListUI = go.AddComponent<CharacterEffectListUI>();
        }
        else
        {
            GameObject go = new GameObject($"CharacterEffectList_{target?.name ?? "Target"}");
            go.transform.SetParent(parentTransform, false);
            effectListUI = go.AddComponent<CharacterEffectListUI>();
        }

        if (effectListUI != null)
        {
            effectListUI.transform.localScale = Vector3.one;
            effectListUI.SetTarget(target, offset);
            activeWorldUIs.Add(effectListUI);
        }

        return effectListUI;
    }

    /// <summary>
    /// 지정된 월드 UI 컴포넌트를 관리 목록에서 제거하고 안전하게 파괴합니다.
    /// </summary>
    public void ReleaseWorldUI(Component uiComponent)
    {
        if (uiComponent == null) return;

        activeWorldUIs.Remove(uiComponent);
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

        if (worldUILayer != null)
        {
            Transform parent = worldUILayer.transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }
    }

    /// <summary>
    /// 하위 호환성을 위한 World UI 정리 별칭입니다.
    /// </summary>
    public void ClearAllCharacterStatusBars() => ClearWorldUILayer();

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

        // Normal 또는 Popup 레이어인 경우 모달 스택으로 관리
        if (panel.uiLayerType == UILayerType.Normal || panel.uiLayerType == UILayerType.Popup)
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

    public void PushActiveUIPanel(GameObject newObject, UILayerType layerType)
    {
        PushActiveUIPanel(newObject);
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

    public void RemoveActiveUIFromStack(GameObject targetObject)
    {
        if (targetObject != null && targetObject.TryGetComponent<UIPanelBase>(out var panel))
        {
            RemovePanelFromStack(panel);
            EvaluateWorldInputLock();
        }
    }

    public void RemoveActiveUIFromStack(GameObject targetObject, UILayerType layerType)
    {
        RemoveActiveUIFromStack(targetObject);
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

    public bool IsUIActiveInStack(GameObject checkObject)
    {
        if (checkObject != null && checkObject.TryGetComponent<UIPanelBase>(out var panel))
        {
            return _popupStack.Contains(panel);
        }
        return false;
    }

    public GameObject GetCurrentTopActiveUI()
    {
        return _popupStack.Count > 0 ? _popupStack.Peek().gameObject : null;
    }

    #endregion
    #region 카드 범위확인 UI (GameMap으로 위임)

    /// <summary>
    /// 카드 데이터의 효과 범위를 표시합니다. 실제 로직은 월드 계층인 GameMap.current가 전담합니다.
    /// </summary>
    public void UpdateEffectAreaUI(CardData newCardData)
    {
        if (GameMap.current != null)
        {
            GameMap.current.UpdateEffectArea(newCardData);
            return;
        }

        // GameMap이 없는 씬을 위한 레거시 폴백
        ClearEffectAreaTiles();
        SetEffectAreaFromTargetDistance(newCardData.targetType,
                                           newCardData.targetMinDistance,
                                           newCardData.targetMaxDistance,
                                           TileType.TargetTile);

        if (newCardData.additionalEffectAreaList != null)
        {
            foreach (EffectArea additionalEffectArea in newCardData.additionalEffectAreaList)
            {
                SetEffectAreaFromShapeGenerator(additionalEffectArea.areaType,
                                                   Mathf.Abs(newCardData.targetMaxDistance - newCardData.targetMinDistance),
                                                   additionalEffectArea.distance,
                                                   TileType.AdditionalEffectTile);
            }
        }
    }

    public void UpdateEffectAreaUI(Card card)
    {
        if (GameMap.current != null)
        {
            GameMap.current.UpdateEffectArea(card);
            return;
        }

        if (card != null && card.cardData != null)
        {
            UpdateEffectAreaUI(card.cardData);
        }
    }

    public void ClearEffectAreaTiles()
    {
        if (GameMap.current != null)
        {
            GameMap.current.ClearEffectAreaTiles();
            return;
        }

        if (effectAreaTile != null)
            effectAreaTile.ClearAllTiles();
        if (AdditionalEffectAreaTile != null)
            AdditionalEffectAreaTile.ClearAllTiles();
    }

    public void SetEffectAreaFromTargetDistance(string cardTargetType, int minDistance, int maxDistance, TileType type)
    {
        if (GameMap.current != null)
        {
            GameMap.current.SetEffectAreaFromTargetDistance(cardTargetType, minDistance, maxDistance, type);
            return;
        }

        if (effectAreaTile == null)
            return;

        if (Enum.TryParse(cardTargetType, out TargetType parsedTargetType))
        {
            effectAreaTile.SetTileFromTargetDistance(parsedTargetType, minDistance, maxDistance, type);
        }
        else
        {
            Debug.LogWarning($"[UIManager] Invalid TargetType string: {cardTargetType}");
        }
    }

    public void SetEffectAreaFromShapeGenerator(string shapeName, int shapeLength, int radius, TileType type)
    {
        if (GameMap.current != null)
        {
            GameMap.current.SetEffectAreaFromShapeGenerator(shapeName, shapeLength, radius, type);
            return;
        }

        if (AdditionalEffectAreaTile == null)
            return;

        AdditionalEffectAreaTile.SetTileFromShapeGenerator(shapeName, shapeLength, radius, type);
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
            PushActiveUIPanel(uiInstance, layerType);
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
            Debug.LogWarning("[UIManager] ConfirmDialog prefab not found in cache. Executing confirm callback as fallback.");
            onConfirm?.Invoke();
        }
    }

    public BattleUIPanel GetOrSpawnBattleUI()
    {
        GameObject spawned = OpenUI(UIConstants.PANEL_BATTLE_UI, UILayerType.Top, true);
        return spawned != null ? spawned.GetComponent<BattleUIPanel>() : null;
    }

    public BattleHandPanel GetOrSpawnBattleHand()
    {
        BattleUIPanel battleUI = GetOrSpawnBattleUI();
        if (battleUI != null && battleUI.HandPanel != null)
        {
            return battleUI.HandPanel;
        }

        GameObject spawned = OpenUI(UIConstants.PANEL_BATTLE_HAND, UILayerType.Top, true);
        return spawned != null ? spawned.GetComponentInChildren<BattleHandPanel>(true) : null;
    }
    #endregion
}
