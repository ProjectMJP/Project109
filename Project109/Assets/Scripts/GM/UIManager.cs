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
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    // UI 부모 레이어 관리 (각 레이어는 독립된 Canvas와 Sorting Order를 갖게 됨)
    [Header("UI Parent Layers")]
    public Canvas worldUILayer;
    public Canvas normalUILayer;
    public Canvas topUILayer;
    public Canvas popupUILayer;

    // 현재 활성화된 Normal UI 목록 (스택 관리 대상: 열기/닫기/ESC 뒤로가기)
    private readonly Stack<GameObject> activeNormalUIStack = new Stack<GameObject>();
    private readonly Stack<GameObject> tempDeactivatedNormalUIStack = new Stack<GameObject>();

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

    #region UIStack Check

    public GameObject OpenUI(string uiName, UILayerType layerType = UILayerType.Normal, bool startActive = true)
    {
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
        if (prefab.TryGetComponent<UIPanelBase>(out var panel))
        {
            targetLayerType = panel.uiLayerType;
        }

        RectTransform targetLayer = GetLayerTransform(targetLayerType);

        // 생성과 동시에 부모를 명사해 스케일 1,1,1 및 앵커가 프리팹 세팅 그대로 자연스럽게 자리 잡도록 합니다.
        GameObject uiInstance = Instantiate(prefab, targetLayer, false);
        uiInstance.name = uiName;

        if (startActive)
        {
            PushActiveUIPanel(uiInstance, targetLayerType);
        }
        else
        {
            uiInstance.SetActive(false);
        }

        return uiInstance;
    }

    private RectTransform GetLayerTransform(UILayerType layerType)
    {
        switch (layerType)
        {
            case UILayerType.World: return worldUILayer != null ? worldUILayer.transform as RectTransform : null;
            case UILayerType.Normal: return normalUILayer != null ? normalUILayer.transform as RectTransform : null;
            case UILayerType.Top: return topUILayer != null ? topUILayer.transform as RectTransform : null;
            case UILayerType.Popup: return popupUILayer != null ? popupUILayer.transform as RectTransform : null;
            default: return null;
        }
    }

    private readonly List<Component> activeWorldUIs = new List<Component>();

    #region World UI Factory & Lifecycle

    /// <summary>
    /// 캐릭터의 머리 위 상태바(CharacterStatusBarUI)를 World UI Layer 하위에 생성하여 반환합니다.
    /// </summary>
    public CharacterStatusBarUI CreateStatusBarUI(Transform target, Vector3 offset)
    {
        Transform parentTransform = (worldUILayer != null) ? worldUILayer.transform : transform;
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
        Transform parentTransform = (worldUILayer != null) ? worldUILayer.transform : transform;
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

    public void PushActiveUIPanel(GameObject newObject)
    {
        UILayerType layerType = UILayerType.Normal;
        if (newObject.TryGetComponent<UIPanelBase>(out var panel))
        {
            layerType = panel.uiLayerType;
        }
        PushActiveUIPanel(newObject, layerType);
    }

    public void PushActiveUIPanel(GameObject newObject, UILayerType layerType)
    {
        // 1. 해당 레이어 하위로 부모 재설정
        RectTransform targetLayer = GetLayerTransform(layerType);
        if (targetLayer != null)
        {
            if (newObject.transform.parent != targetLayer)
            {
                newObject.transform.SetParent(targetLayer, false);
            }
            newObject.transform.SetAsLastSibling();
        }

        // 2. Normal 타입만 스택으로 관리
        if (layerType == UILayerType.Normal)
        {
            activeNormalUIStack.Push(newObject);
            Debug.Log($"[UIManager] Pushed Normal UI: {newObject.name}. Current Normal Stack Count: {activeNormalUIStack.Count}");
        }
        else
        {
            Debug.Log($"[UIManager] Activated UI: {newObject.name} (Layer: {layerType})");
        }

        newObject.SetActive(true);

        //터치 활성화 여부 확인
        SetPlayerTouchSystemActiveInGame();
    }

    public void PopActiveUIPanel()
    {
        if (activeNormalUIStack.Count == 0)
            return;

        GameObject popObject = activeNormalUIStack.Pop();
        popObject.SetActive(false);
        Debug.Log($"[UIManager] Poped Normal UI: {popObject.name}. Current Normal Stack Count: {activeNormalUIStack.Count}");

        //터치 활성화 여부 확인
        SetPlayerTouchSystemActiveInGame();
    }

    //현재 활성화된 Normal UI들 임시 비활성화
    public void TempDeactivateCurrentActiveUIPanel()
    {
        if (activeNormalUIStack.Count == 0)
            return;

        //현재 활성화된 Normal UI들 비활성화 후 임시 스택에 저장
        while (activeNormalUIStack.Count > 0)
        {
            GameObject ui = activeNormalUIStack.Pop();
            ui.SetActive(false);
            tempDeactivatedNormalUIStack.Push(ui);
        }
        Debug.Log($"[UIManager] TempDeactivate Stack Count: {tempDeactivatedNormalUIStack.Count}.");

        //초기화 후 새로운 UI 활성화
        activeNormalUIStack.Clear();

        //터치 활성화 여부 확인
        SetPlayerTouchSystemActiveInGame();
    }

    //임시로 비활성화된 Normal UI들 다시 활성화
    public void ReactivateTempDeactiveUIPanel()
    {
        if (tempDeactivatedNormalUIStack.Count == 0)
            return;

        //현재 임시로 비활성화된 UI들 활성화 진행
        while (tempDeactivatedNormalUIStack.Count > 0)
        {
            GameObject ui = tempDeactivatedNormalUIStack.Pop();
            ui.SetActive(true);
            activeNormalUIStack.Push(ui);
        }
        tempDeactivatedNormalUIStack.Clear();
        Debug.Log($"[UIManager] TempDeactivate Normal UIs Reactivated. Current Normal Stack Count: {activeNormalUIStack.Count}");

        //터치 활성화 여부 확인
        SetPlayerTouchSystemActiveInGame();
    }

    public void RemoveActiveUIFromStack(GameObject targetObject)
    {
        UILayerType layerType = UILayerType.Normal;
        if (targetObject.TryGetComponent<UIPanelBase>(out var panel))
        {
            layerType = panel.uiLayerType;
        }
        RemoveActiveUIFromStack(targetObject, layerType);
    }

    public void RemoveActiveUIFromStack(GameObject targetObject, UILayerType layerType)
    {
        if (layerType != UILayerType.Normal)
        {
            return;
        }

        if (activeNormalUIStack.Count == 0)
            return;

        //최상위 UI가 제거된 UI와 일치하는지 확인
        if (activeNormalUIStack.Peek() == targetObject)
        {
            activeNormalUIStack.Pop();
            Debug.Log($"[UIManager] Removed top Normal UI: {targetObject.name}. Current Normal Stack Count: {activeNormalUIStack.Count}");
        }
        else
        {
            //최상위 UI가 아닌 경우 스택을 순회하여 강제로 제거
            Stack<GameObject> tempStack = new Stack<GameObject>();
            bool bfoundObject = false;
            while (activeNormalUIStack.Count > 0)
            {
                GameObject currentObject = activeNormalUIStack.Pop();
                if (currentObject == targetObject)
                {
                    bfoundObject = true;
                    Debug.Log($"[UIManager] Force Removed Normal UI: {targetObject.name}.");
                    break;
                }
                tempStack.Push(currentObject);
            }
            //상위에 있던 Object들 다시 채워넣기
            while (tempStack.Count > 0)
            {
                activeNormalUIStack.Push(tempStack.Pop());
            }
            if (!bfoundObject)
            {
                Debug.LogWarning($"[UIManager] Normal UI {targetObject.name} not found in stack to remove!");
            }
            else
            {
                Debug.Log($"Current Normal Stack Count: {activeNormalUIStack.Count}");
            }
        }

        //터치 활성화 여부 확인
        SetPlayerTouchSystemActiveInGame();
    }

    public void SetPlayerTouchSystemActiveInGame()
    {
        // 이제 InputManager가 Reference Counting을 통해 터치 잠금을 자동으로 관리하므로,
        // UIManager에서 직접 PlayerInputController의 입력을 활성화/비활성화할 필요가 없습니다.
    }

    public bool IsUIActiveInStack(GameObject checkObject)
    {
        return activeNormalUIStack.Contains(checkObject);
    }

    public GameObject GetCurrentTopActiveUI()
    {
        return activeNormalUIStack.Count > 0 ? activeNormalUIStack.Peek() : null;
    }

    #endregion
    #region 카드 범위확인 UI

    public void UpdateEffectAreaUI(CardData newCardData)
    {
        UIManager.instance.ClearEffectAreaTiles();

        //효과 범위 설정
        UIManager.instance.SetEffectAreaFromTargetDistance(newCardData.targetType,
                                                           newCardData.targetMinDistance,
                                                           newCardData.targetMaxDistance,
                                                           TileType.TargetTile);

        //추가 효과 범위 설정
        foreach (EffectArea additionalEffectArea in newCardData.additionalEffectAreaList)
        {
            UIManager.instance.SetEffectAreaFromShapeGenerator(additionalEffectArea.areaType,
                                                               Mathf.Abs(newCardData.targetMaxDistance - newCardData.targetMinDistance),
                                                               additionalEffectArea.distance,
                                                               TileType.AdditionalEffectTile);
        }
    }

    public void UpdateEffectAreaUI(Card card)
    {
        if (card != null && card.cardData != null)
        {
            UpdateEffectAreaUI(card.cardData);
        }
    }

    public void ClearEffectAreaTiles()
    {
        if (effectAreaTile == null)
            return;

        effectAreaTile.ClearAllTiles();
        AdditionalEffectAreaTile.ClearAllTiles();

    }

    public void SetEffectAreaFromTargetDistance(string cardTargetType, int minDistance, int maxDistance, TileType type)
    {
        if (effectAreaTile == null)
            return;

        if (Enum.TryParse(cardTargetType, out TargetType parsedTargetType))
        {
            effectAreaTile.SetTileFromTargetDistance(parsedTargetType, minDistance, maxDistance, type);
        }
        else
        {
            Debug.LogWarning($"Invalid TargetType string: {cardTargetType}");
        }
    }

    public void SetEffectAreaFromShapeGenerator(string shapeName, int shapeLength, int radius, TileType type)
    {
        if (effectAreaTile == null)
            return;

        AdditionalEffectAreaTile.SetTileFromShapeGenerator(shapeName, shapeLength, radius, type);
    }

    #endregion

    #region 상단 HUD 및 맵 위임 헬퍼 (하위 호환)
    /// <summary>
    /// 하위 호환성을 위한 플레이어 HUD 바인딩 위임 메서드입니다.
    /// </summary>
    public void BindPlayerToHUD(Player player)
    {
        if (RunManager.instance != null)
        {
            RunManager.instance.InitPlayerHUD();
        }
    }

    /// <summary>
    /// 스테이지 리셋 시 활성화된 ExploreMap을 정리합니다.
    /// </summary>
    public void DestroyExploreMap()
    {
        if (RunManager.instance != null && RunManager.instance.currentExploreUI != null)
        {
            RemoveActiveUIFromStack(RunManager.instance.currentExploreUI.gameObject);
            Destroy(RunManager.instance.currentExploreUI.gameObject);
            RunManager.instance.currentExploreUI = null;
            Debug.Log("[UIManager] Existing ExploreMap instance destroyed for stage reset.");
        }
    }
    #endregion

    #region Async UI & Dialog Support
    public System.Collections.IEnumerator OpenUIAsyncCoroutine<T>(string uiName, UILayerType layerType, bool blockWorldInput, Action<T> onComplete) where T : UIPanelBase
    {
        bool acquiredPreLock = false;

        // 1. 에셋 비동기 로딩을 시작하기 전에 '선제적'으로 터치 입력 차단
        if (blockWorldInput && UIInputManager.instance != null)
        {
            UIInputManager.instance.AcquireUILock();
            acquiredPreLock = true;
        }

        // 2. 비동기 에셋 캐시 획득 및 인스턴스화
        GameObject prefab = null;
        yield return StartCoroutine(AssetCacheManager.instance.GetUIAsyncCoroutine(uiName, (result) => prefab = result));

        if (prefab == null)
        {
            Debug.LogError($"[UIManager] Failed to load UI async: {uiName}");
            if (acquiredPreLock && UIInputManager.instance != null)
            {
                UIInputManager.instance.ReleaseUILock();
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
        if (acquiredPreLock && UIInputManager.instance != null)
        {
            UIInputManager.instance.ReleaseUILock();
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

    public BattleHandPanel GetOrSpawnBattleHand()
    {
        GameObject spawned = OpenUI(UIConstants.PANEL_BATTLE_HAND, UILayerType.Normal, true);
        return spawned != null ? spawned.GetComponent<BattleHandPanel>() : null;
    }
    #endregion
}
