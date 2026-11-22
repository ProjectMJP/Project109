using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[TestFixture]
public class BattleHandPeekAndHoverTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<Card> _createdCards = new List<Card>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdCards.Count - 1; i >= 0; i--)
        {
            if (_createdCards[i] != null && !_createdCards[i].IsDisposed)
            {
                _createdCards[i].Dispose();
            }
        }
        _createdCards.Clear();

        SetStaticAutoProperty<RunManager>("instance", null);
        SetStaticAutoProperty<UIManager>("instance", null);
        TooltipManager.SetTooltipEnabled(true);
        SetStaticField<TooltipManager>("instance", null);
        SetStaticAutoProperty<BattleTimelinePanel>("Instance", null);

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private (Canvas canvas, EventSystem eventSystem) CreateTestCanvas(string name)
    {
        GameObject esGo = Track(new GameObject(name + "_ES", typeof(EventSystem)));
        EventSystem eventSystem = esGo.GetComponent<EventSystem>();

        GameObject canvasGo = Track(new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
        CreateTestTooltipManager(canvas);
        return (canvas, eventSystem);
    }

    private TooltipManager CreateTestTooltipManager(Canvas canvas)
    {
        GameObject ttGo = Track(new GameObject("TooltipManager", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)));
        ttGo.transform.SetParent(canvas.transform, false);

        ContentSizeFitter fitter = ttGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/TooltipPanel.prefab");
        Assert.IsNotNull(templatePrefab, "TooltipPanel.prefab을 찾을 수 없습니다.");

        TooltipManager manager = ttGo.AddComponent<TooltipManager>();
        SetPrivateField(manager, "panelTemplatePrefab", templatePrefab.GetComponent<TooltipPanel>());
        InvokePrivateMethod(manager, "Awake");
        return manager;
    }

    private BattleUIPanel InstantiateBattleUIPanel(Canvas canvas)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleUIPanel.prefab");
        Assert.IsNotNull(prefab, "BattleUIPanel.prefab을 찾을 수 없습니다.");

        GameObject panelGo = Track(UnityEngine.Object.Instantiate(prefab, canvas.transform, false));
        BattleUIPanel battleUI = panelGo.GetComponent<BattleUIPanel>();
        InvokePrivateMethod(battleUI.HandPanel, "Awake");
        InvokePrivateMethod(battleUI.MovePanel, "Awake");
        InvokePrivateMethod(battleUI, "Awake");
        InvokePrivateMethod(battleUI, "OnEnable");
        InvokePrivateMethod(battleUI.HandPanel, "OnEnable");
        InvokePrivateMethod(battleUI.MovePanel, "OnEnable");
        return battleUI;
    }

    private Card CreateTestCard(string name, int cost = 1)
    {
        CardData data = new CardData
        {
            cardName = name,
            displayName = name,
            stamina = cost,
            targetType = "Self",
            targetMinDistance = 0,
            targetMaxDistance = 0
        };
        Card card = new Card(data, null, null, -(_createdCards.Count + 1));
        _createdCards.Add(card);
        return card;
    }

    private CardUI InstantiateActionCardUI(Transform parent, string name)
    {
        GameObject actionCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Card/ActionCard.prefab");
        Assert.IsNotNull(actionCardPrefab, "ActionCard.prefab을 찾을 수 없습니다.");

        GameObject cardGo = Track(UnityEngine.Object.Instantiate(actionCardPrefab, parent, false));
        cardGo.name = name;
        CardUI cardUI = cardGo.GetComponent<CardUI>();
        InvokePrivateMethod(cardUI, "Awake");
        return cardUI;
    }

    private static void RegisterCardsInHandPanel(BattleHandPanel handPanel, params (Card card, CardUI cardUI)[] entries)
    {
        IList activeList = GetPrivateField<IList>(handPanel, "activeCardUIs");
        IDictionary entryMap = GetPrivateField<IDictionary>(handPanel, "_activeEntryMap");
        Type entryType = typeof(BattleHandPanel).GetNestedType("HandCardEntry", BindingFlags.NonPublic);
        Assert.IsNotNull(entryType);

        List<Card> handCards = new List<Card>(entries.Length);
        MethodInfo startHandler = typeof(BattleHandPanel).GetMethod("HandleCardHoverZoomStarted", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo endHandler = typeof(BattleHandPanel).GetMethod("HandleCardHoverZoomEnded", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(startHandler);
        Assert.IsNotNull(endHandler);

        for (int i = 0; i < entries.Length; i++)
        {
            (Card card, CardUI cardUI) = entries[i];
            object entry = Activator.CreateInstance(entryType);
            entryType.GetField("card", BindingFlags.Instance | BindingFlags.Public).SetValue(entry, card);
            entryType.GetField("cardObj", BindingFlags.Instance | BindingFlags.Public).SetValue(entry, cardUI.gameObject);
            entryType.GetField("cardUI", BindingFlags.Instance | BindingFlags.Public).SetValue(entry, cardUI);
            entryType.GetField("rectTransform", BindingFlags.Instance | BindingFlags.Public).SetValue(entry, cardUI.CachedRectTransform);

            cardUI.UpdateCardInstance(card);
            cardUI.OnHoverZoomStarted += (Action<CardUI>)Delegate.CreateDelegate(typeof(Action<CardUI>), handPanel, startHandler);
            cardUI.OnHoverZoomEnded += (Action<CardUI>)Delegate.CreateDelegate(typeof(Action<CardUI>), handPanel, endHandler);

            activeList.Add(entry);
            entryMap[card] = entry;
            handCards.Add(card);
        }

        handPanel.RefreshHand(handCards);
    }

    private static Rect GetScreenSpaceRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            if (corners[i].x < minX) minX = corners[i].x;
            if (corners[i].y < minY) minY = corners[i].y;
            if (corners[i].x > maxX) maxX = corners[i].x;
            if (corners[i].y > maxY) maxY = corners[i].y;
        }
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"메서드 '{methodName}'를 {target.GetType().Name}에서 찾을 수 없습니다.");
        method.Invoke(target, args);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"필드 '{fieldName}'을(를) {target.GetType().Name}에서 찾을 수 없습니다.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"필드 '{fieldName}'을(를) {target.GetType().Name}에서 찾을 수 없습니다.");
        field.SetValue(target, value);
    }

    private static void SetStaticField<T>(string fieldName, object value)
    {
        FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(null, value);
        }
    }

    private static void SetStaticAutoProperty<T>(string propName, object value)
    {
        PropertyInfo prop = typeof(T).GetProperty(propName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null)
        {
            prop.SetValue(null, value);
        }
    }

    [Test]
    public void BattleHandPanel_RestingBottomPeekAndHoverReveal_UpdatesPositionScaleAndSiblingOrder()
    {
        var (canvas, eventSystem) = CreateTestCanvas("HandPeekHoverCanvas");
        BattleUIPanel battleUI = InstantiateBattleUIPanel(canvas);
        BattleHandPanel handPanel = battleUI.HandPanel;

        Card card1 = CreateTestCard("검격", 1);
        Card card2 = CreateTestCard("방어", 2);
        CardUI card1UI = InstantiateActionCardUI(handPanel.contentTransform, "Card1UI");
        CardUI card2UI = InstantiateActionCardUI(handPanel.contentTransform, "Card2UI");

        RegisterCardsInHandPanel(handPanel, (card1, card1UI), (card2, card2UI));

        // 1. 평상시 하단 걸침(Bottom Peek) 배치 및 상단 코스트/이름 노출 검증
        Assert.AreEqual(-105f, handPanel.RestingCardYOffset, 0.01f);
        Assert.AreEqual(140f, handPanel.HoverRevealYOffset, 0.01f);
        Assert.AreEqual(140f, card1UI.zoomYOffset, 0.01f);
        Assert.AreEqual(1.1f, card1UI.zoomScale, 0.001f);
        Assert.AreEqual(250f, card1UI.playDragThresholdY, 0.01f);

        Assert.AreEqual(handPanel.RestingCardYOffset, card1UI.CachedRectTransform.anchoredPosition.y, 0.01f);
        Assert.AreEqual(handPanel.RestingCardYOffset, card2UI.CachedRectTransform.anchoredPosition.y, 0.01f);

        Canvas.ForceUpdateCanvases();
        Rect restingCardRect = GetScreenSpaceRect(card1UI.CachedRectTransform);
        Rect restingNameRect = GetScreenSpaceRect(card1UI.cardName.rectTransform);
        Rect restingCostRect = GetScreenSpaceRect(card1UI.useStamina.rectTransform);
        Assert.Less(restingCardRect.yMin, 0f, "평상시에는 카드 하단이 화면 아래로 살짝 내려가 전장 시야를 확보해야 합니다.");
        Assert.Greater(restingNameRect.yMin, 0f, "하단 걸침 상태에서도 카드 이름 영역은 화면 안에 온전히 노출되어야 합니다.");
        Assert.Greater(restingCostRect.yMin, 0f, "하단 걸침 상태에서도 스태미나 코스트 영역은 화면 안에 온전히 노출되어야 합니다.");

        // 2. 호버 시 화면 안쪽으로 전체 노출 및 최상단 렌더링 순서 전환 검증
        PointerEventData pointerData = new PointerEventData(eventSystem);
        card1UI.OnPointerEnter(pointerData);

        Assert.IsTrue(card1UI.IsZoomed);
        Assert.AreEqual(handPanel.RestingCardYOffset + handPanel.HoverRevealYOffset, card1UI.CachedRectTransform.anchoredPosition.y, 0.01f);
        Assert.AreEqual(1.1f, card1UI.transform.localScale.x, 0.001f);
        Assert.AreEqual(handPanel.contentTransform.childCount - 1, card1UI.transform.GetSiblingIndex());

        Canvas.ForceUpdateCanvases();
        Rect hoveredCardRect = GetScreenSpaceRect(card1UI.CachedRectTransform);
        Assert.Greater(hoveredCardRect.yMin, 0f, "호버 시에는 카드 하단까지 화면 안쪽으로 올라와 전체가 노출되어야 합니다.");

        // 3. 포인터 이탈 시 원위치 및 형제 순서 복귀 검증
        card1UI.OnPointerExit(pointerData);

        Assert.IsFalse(card1UI.IsZoomed);
        Assert.AreEqual(handPanel.RestingCardYOffset, card1UI.CachedRectTransform.anchoredPosition.y, 0.01f);
        Assert.AreEqual(1f, card1UI.transform.localScale.x, 0.001f);
        Assert.AreEqual(0, card1UI.transform.GetSiblingIndex());
    }

    [Test]
    public void CardUI_HoverRaycastBridge_ExtendsBottomPaddingDuringHover_AndRestoresOnAllExitPaths()
    {
        var (canvas, eventSystem) = CreateTestCanvas("HoverBridgeCanvas");
        CardUI cardUI = InstantiateActionCardUI(canvas.transform, "BridgeCardUI");
        cardUI.enableZoom = true;
        cardUI.zoomScale = 1.1f;
        cardUI.zoomYOffset = 140f;
        cardUI.playDragThresholdY = 250f;
        cardUI.usePlayGesture = true;
        cardUI.SetHandSlot(new Vector2(0f, -105f), Vector3.one, 0);
        cardUI.CachedRectTransform.anchoredPosition = new Vector2(0f, -105f);

        Graphic bridgeGraphic = GetPrivateField<Graphic>(cardUI, "_hoverRaycastGraphic");
        Assert.IsNotNull(bridgeGraphic, "ActionCard.prefab의 주 레이캐스트 그래픽(Background)이 해석되어야 합니다.");
        Assert.AreEqual(Vector4.zero, bridgeGraphic.raycastPadding);

        PointerEventData pointerData = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };

        // 1. 호버 진입 시 하단 raycastPadding.y 음수 확장(Jitter 방지 브리지)
        cardUI.OnPointerEnter(pointerData);
        Assert.IsTrue(cardUI.IsZoomed);
        Assert.Less(bridgeGraphic.raycastPadding.y, -135f);

        // 2-a. OnPointerExit 시 원복
        cardUI.OnPointerExit(pointerData);
        Assert.AreEqual(Vector4.zero, bridgeGraphic.raycastPadding);

        // 2-b. 호버 후 드래그 시작(OnBeginDrag) 시 원복
        cardUI.OnPointerEnter(pointerData);
        Assert.Less(bridgeGraphic.raycastPadding.y, -135f);
        cardUI.OnBeginDrag(pointerData);
        Assert.IsFalse(cardUI.IsZoomed);
        Assert.AreEqual(Vector4.zero, bridgeGraphic.raycastPadding);
        cardUI.CancelDrag();

        // 2-c. 호버 중 SetInteractable(false) 시 원복 및 위치·스케일 복귀
        cardUI.OnPointerEnter(pointerData);
        Assert.IsTrue(cardUI.IsZoomed);
        cardUI.SetInteractable(false);
        Assert.IsFalse(cardUI.IsZoomed);
        Assert.AreEqual(Vector4.zero, bridgeGraphic.raycastPadding);
        Assert.AreEqual(-105f, cardUI.CachedRectTransform.anchoredPosition.y, 0.01f);
        Assert.AreEqual(1f, cardUI.transform.localScale.x, 0.001f);

        // 2-d. 호버 중 OnDisable() 호출 시 원복
        cardUI.SetInteractable(true);
        cardUI.OnPointerEnter(pointerData);
        Assert.Less(bridgeGraphic.raycastPadding.y, -135f);
        cardUI.gameObject.SetActive(false);
        InvokePrivateMethod(cardUI, "OnDisable");
        Assert.AreEqual(Vector4.zero, bridgeGraphic.raycastPadding);
        Assert.IsFalse(cardUI.IsZoomed);
    }

    [Test]
    public void BattleHandPanel_CrossHoverAndTargetingTransition_KeepsSingleTopSiblingAndResetsCleanly()
    {
        var (canvas, eventSystem) = CreateTestCanvas("CrossHoverCanvas");
        BattleUIPanel battleUI = InstantiateBattleUIPanel(canvas);
        BattleHandPanel handPanel = battleUI.HandPanel;

        Card card0 = CreateTestCard("카드0", 1);
        Card card1 = CreateTestCard("카드1", 1);
        Card card2 = CreateTestCard("카드2", 1);
        CardUI ui0 = InstantiateActionCardUI(handPanel.contentTransform, "Card0");
        CardUI ui1 = InstantiateActionCardUI(handPanel.contentTransform, "Card1");
        CardUI ui2 = InstantiateActionCardUI(handPanel.contentTransform, "Card2");

        RegisterCardsInHandPanel(handPanel, (card0, ui0), (card1, ui1), (card2, ui2));

        PointerEventData pointerData = new PointerEventData(eventSystem);

        // 1. card0 호버 중 Exit 전에 card1 호버 진입 -> card0 자동 해제 및 card1 최상단
        ui0.OnPointerEnter(pointerData);
        Assert.IsTrue(ui0.IsZoomed);
        Assert.AreEqual(2, ui0.transform.GetSiblingIndex());

        ui1.OnPointerEnter(pointerData);
        Assert.IsFalse(ui0.IsZoomed);
        Assert.IsTrue(ui1.IsZoomed);
        Assert.AreEqual(2, ui1.transform.GetSiblingIndex());

        // 2. 뒤늦게 card0 OnPointerExit가 호출되어도 현재 호버 중인 card1이 최상단 형제를 유지해야 함
        ui0.OnPointerExit(pointerData);
        Assert.IsTrue(ui1.IsZoomed);
        Assert.AreEqual(0, ui0.transform.GetSiblingIndex());
        Assert.AreEqual(1, ui2.transform.GetSiblingIndex());
        Assert.AreEqual(2, ui1.transform.GetSiblingIndex());

        // 3. 호버 중 타깃팅 모드 진입 시 호버·브리지·형제순서 정리 후 슬라이드 다운, 해제 시 원위치 복귀
        Graphic ui1Bridge = GetPrivateField<Graphic>(ui1, "_hoverRaycastGraphic");
        Assert.Less(ui1Bridge.raycastPadding.y, -135f);

        handPanel.SetTargetingState(card1);
        Assert.IsFalse(ui1.IsZoomed);
        Assert.IsFalse(ui1.IsInteractable);
        Assert.AreEqual(Vector4.zero, ui1Bridge.raycastPadding);
        Assert.AreEqual(0, ui0.transform.GetSiblingIndex());
        Assert.AreEqual(1, ui1.transform.GetSiblingIndex());
        Assert.AreEqual(2, ui2.transform.GetSiblingIndex());
        Assert.Less(ui1.CachedRectTransform.anchoredPosition.y, ui0.CachedRectTransform.anchoredPosition.y);

        handPanel.SetTargetingState(null);
        Assert.IsTrue(ui0.IsInteractable);
        Assert.IsTrue(ui1.IsInteractable);
        Assert.IsTrue(ui2.IsInteractable);
        Assert.AreEqual(handPanel.RestingCardYOffset, ui0.CachedRectTransform.anchoredPosition.y, 0.01f);
        Assert.AreEqual(handPanel.RestingCardYOffset, ui1.CachedRectTransform.anchoredPosition.y, 0.01f);
        Assert.AreEqual(handPanel.RestingCardYOffset, ui2.CachedRectTransform.anchoredPosition.y, 0.01f);
    }

    [Test]
    public void CardUI_ResetForPool_And_ObjectPoolManagerReturn_ClearsHoverBridgeTransformsAndFlags()
    {
        var (canvas, eventSystem) = CreateTestCanvas("PoolResetCanvas");
        CardUI cardUI = InstantiateActionCardUI(canvas.transform, "PooledCardUI");

        Card card = CreateTestCard("재사용카드", 2);
        cardUI.UpdateCardInstance(card);
        cardUI.enableZoom = true;
        cardUI.zoomScale = 1.25f;
        cardUI.zoomYOffset = 140f;
        cardUI.playDragThresholdY = 250f;
        cardUI.bIsCardHighlight = true;
        cardUI.bShowEffectAreaUI = true;
        cardUI.usePlayGesture = true;
        cardUI.SetHandSlot(new Vector2(40f, -105f), Vector3.one, 0);

        PointerEventData pointerData = new PointerEventData(eventSystem);
        cardUI.OnPointerEnter(pointerData);
        Graphic bridgeGraphic = GetPrivateField<Graphic>(cardUI, "_hoverRaycastGraphic");
        Assert.IsTrue(cardUI.IsZoomed);
        Assert.Less(bridgeGraphic.raycastPadding.y, -100f);

        // 1. ResetForPool() 직접 호출 검증
        cardUI.ResetForPool();
        Assert.IsFalse(cardUI.enableZoom);
        Assert.AreEqual(1.1f, cardUI.zoomScale, 0.001f);
        Assert.AreEqual(90f, cardUI.zoomYOffset, 0.01f);
        Assert.AreEqual(140f, cardUI.playDragThresholdY, 0.01f);
        Assert.IsFalse(cardUI.bIsCardHighlight);
        Assert.IsFalse(cardUI.bShowEffectAreaUI);
        Assert.IsFalse(cardUI.usePlayGesture);
        Assert.IsFalse(cardUI.IsZoomed);
        Assert.AreEqual(Vector4.zero, bridgeGraphic.raycastPadding);
        Assert.AreEqual(Vector2.zero, cardUI.CachedRectTransform.anchoredPosition);
        Assert.AreEqual(Vector3.one, cardUI.transform.localScale);
        Assert.AreEqual(Quaternion.identity, cardUI.transform.localRotation);

        // 2. ObjectPoolManager.OnReturnCardUIToPool() 반환 경로에서도 동일하게 초기화되는지 검증
        GameObject poolGo = Track(new GameObject("TestObjectPoolManager"));
        ObjectPoolManager poolManager = poolGo.AddComponent<ObjectPoolManager>();

        cardUI.UpdateCardInstance(card);
        cardUI.enableZoom = true;
        cardUI.zoomYOffset = 140f;
        cardUI.usePlayGesture = true;
        cardUI.OnPointerEnter(pointerData);
        Assert.IsTrue(cardUI.IsZoomed);
        Assert.Less(bridgeGraphic.raycastPadding.y, -100f);

        InvokePrivateMethod(poolManager, "OnReturnCardUIToPool", cardUI.gameObject);
        Assert.IsFalse(cardUI.gameObject.activeSelf);
        Assert.AreSame(poolManager.transform, cardUI.transform.parent);
        Assert.IsFalse(cardUI.IsZoomed);
        Assert.IsFalse(cardUI.enableZoom);
        Assert.IsFalse(cardUI.usePlayGesture);
        Assert.AreEqual(90f, cardUI.zoomYOffset, 0.01f);
        Assert.AreEqual(Vector4.zero, bridgeGraphic.raycastPadding);
    }
}
