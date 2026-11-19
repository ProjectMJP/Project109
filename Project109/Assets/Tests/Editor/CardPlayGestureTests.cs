using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[TestFixture]
public class CardPlayGestureTests
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

    private (Canvas canvas, EventSystem eventSystem, RectTransform parentRect) CreateTestCanvasHierarchy(
        string name = "TestCanvas",
        Vector2? parentPivot = null,
        Vector2? parentSize = null)
    {
        GameObject esGo = Track(new GameObject(name + "_EventSystem", typeof(EventSystem)));
        EventSystem eventSystem = esGo.GetComponent<EventSystem>();

        GameObject canvasGo = Track(new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
        canvasRect.position = Vector3.zero;

        GameObject parentGo = Track(new GameObject("HandContent", typeof(RectTransform)));
        parentGo.transform.SetParent(canvasGo.transform, false);
        RectTransform parentRect = parentGo.GetComponent<RectTransform>();
        parentRect.anchorMin = new Vector2(0.5f, 0.5f);
        parentRect.anchorMax = new Vector2(0.5f, 0.5f);
        parentRect.pivot = parentPivot ?? new Vector2(0.5f, 0.5f);
        parentRect.sizeDelta = parentSize ?? new Vector2(1000f, 220f);
        parentRect.anchoredPosition = Vector2.zero;

        return (canvas, eventSystem, parentRect);
    }

    private CardUI CreateTestCardUI(Transform parent, string name = "TestCardUI")
    {
        GameObject cardGo = Track(new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image)));
        cardGo.transform.SetParent(parent, false);

        RectTransform rect = cardGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(185f, 325f);
        rect.anchoredPosition = Vector2.zero;

        GameObject highlightGo = Track(new GameObject("SelectHighlight", typeof(RectTransform), typeof(Image)));
        highlightGo.transform.SetParent(cardGo.transform, false);

        CardUI cardUI = cardGo.AddComponent<CardUI>();
        cardUI.selectHighlightObject = highlightGo;
        InvokePrivateMethod(cardUI, "Awake");
        return cardUI;
    }

    private Card CreateTestCard(string name, int staminaCost = 1)
    {
        CardData data = new CardData
        {
            cardName = name,
            displayName = name,
            stamina = staminaCost,
            targetType = "Self",
            targetMinDistance = 0,
            targetMaxDistance = 0
        };
        Card card = new Card(data, null, null, -(_createdCards.Count + 1));
        _createdCards.Add(card);
        return card;
    }

    private static Vector2 LocalAnchoredPosToScreenPos(RectTransform parentRect, RectTransform cardRect, Vector2 anchoredPos)
    {
        Rect parentBounds = parentRect.rect;
        float anchorCenterX = Mathf.Lerp(parentBounds.xMin, parentBounds.xMax, (cardRect.anchorMin.x + cardRect.anchorMax.x) * 0.5f);
        float anchorCenterY = Mathf.Lerp(parentBounds.yMin, parentBounds.yMax, (cardRect.anchorMin.y + cardRect.anchorMax.y) * 0.5f);
        Vector3 parentLocalPos = new Vector3(anchoredPos.x + anchorCenterX, anchoredPos.y + anchorCenterY, 0f);
        Vector3 worldPos = parentRect.TransformPoint(parentLocalPos);
        return RectTransformUtility.WorldToScreenPoint(null, worldPos);
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"메서드 '{methodName}'를 {target.GetType().Name}에서 찾을 수 없습니다.");
        method.Invoke(target, args);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"필드 '{fieldName}'을(를) {target.GetType().Name}에서 찾을 수 없습니다.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"필드 '{fieldName}'을(를) {target.GetType().Name}에서 찾을 수 없습니다.");
        field.SetValue(target, value);
    }

    private static object CreateHandCardEntry(Card card, GameObject cardObj, CardUI cardUI)
    {
        Type entryType = typeof(BattleHandPanel).GetNestedType("HandCardEntry", BindingFlags.NonPublic);
        Assert.IsNotNull(entryType, "BattleHandPanel.HandCardEntry 타입을 찾을 수 없습니다.");
        object entry = Activator.CreateInstance(entryType);
        entryType.GetField("card", BindingFlags.Instance | BindingFlags.Public).SetValue(entry, card);
        entryType.GetField("cardObj", BindingFlags.Instance | BindingFlags.Public).SetValue(entry, cardObj);
        entryType.GetField("cardUI", BindingFlags.Instance | BindingFlags.Public).SetValue(entry, cardUI);
        entryType.GetField("rectTransform", BindingFlags.Instance | BindingFlags.Public).SetValue(entry, cardUI.CachedRectTransform);
        return entry;
    }

    [Test]
    public void CardUI_WhenUsePlayGestureIsFalse_SingleClickInvokesOnCardClickImmediately()
    {
        var (_, eventSystem, parentRect) = CreateTestCanvasHierarchy("SingleClickCanvas");
        CardUI cardUI = CreateTestCardUI(parentRect);

        Assert.IsFalse(cardUI.usePlayGesture, "기본값 usePlayGesture는 상점/도감/보상 호환을 위해 false여야 합니다.");

        int clickCount = 0;
        cardUI.OnCardClick.AddListener(() => clickCount++);

        // 우클릭은 무시되어야 함
        PointerEventData rightClick = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Right,
            clickCount = 1
        };
        cardUI.OnPointerClick(rightClick);
        Assert.AreEqual(0, clickCount);

        // 좌클릭 1회 시 즉시 시전/선택 콜백 발화
        PointerEventData leftClick = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            clickCount = 1
        };
        cardUI.OnPointerClick(leftClick);
        Assert.AreEqual(1, clickCount);

        // usePlayGesture == false일 때는 드래그를 시도해도 시작되지 않아야 함
        cardUI.OnBeginDrag(leftClick);
        Assert.IsFalse(cardUI.IsDragging);
    }

    [Test]
    public void CardUI_WhenUsePlayGestureIsTrue_RequiresDoubleClickOrFastReclickToInvokeOnCardClick()
    {
        var (_, eventSystem, parentRect) = CreateTestCanvasHierarchy("DoubleClickCanvas");
        CardUI cardUI = CreateTestCardUI(parentRect);
        cardUI.usePlayGesture = true;
        cardUI.doubleClickInterval = 0.32f;

        int invokeCount = 0;
        cardUI.OnCardClick.AddListener(() => invokeCount++);

        // 1. 첫 단일 클릭 -> 발화하지 않고 선택 하이라이트만 켜짐
        PointerEventData singleClick = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            clickCount = 1
        };
        cardUI.OnPointerClick(singleClick);
        Assert.AreEqual(0, invokeCount);
        Assert.IsTrue(cardUI.selectHighlightObject.activeSelf);

        // 2. 짧은 간격(doubleClickInterval 이내) 재클릭 -> 더블 클릭으로 판정되어 1회 발화
        cardUI.OnPointerClick(singleClick);
        Assert.AreEqual(1, invokeCount);

        // 3. 더블 클릭 직후 타임스탬프가 초기화되므로 바로 이어지는 단일 클릭은 즉시 발화하지 않아야 함
        cardUI.OnPointerClick(singleClick);
        Assert.AreEqual(1, invokeCount);

        // 4. 시간 간격이 doubleClickInterval을 초과한 경우 재클릭해도 발화하지 않음
        SetPrivateField(cardUI, "_lastClickUnscaledTime", Time.unscaledTime - (cardUI.doubleClickInterval + 0.1f));
        cardUI.OnPointerClick(singleClick);
        Assert.AreEqual(1, invokeCount);

        // 5. EventSystem clickCount >= 2 전달 시 즉시 더블 클릭 발화
        PointerEventData explicitDoubleClick = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            clickCount = 2
        };
        cardUI.OnPointerClick(explicitDoubleClick);
        Assert.AreEqual(2, invokeCount);
    }

    [Test]
    public void CardUI_WhenUsePlayGestureIsTrue_DragThresholdDeterminesPlayOrSlotReturn()
    {
        // BattleUIPanel.prefab의 contentTransform과 동일하게 pivot (0.5, 0) 환경에서도 정확히 동작하는지 검증
        var (_, eventSystem, parentRect) = CreateTestCanvasHierarchy(
            "DragGestureCanvas",
            parentPivot: new Vector2(0.5f, 0f),
            parentSize: new Vector2(1000f, 220f));

        // 형제 순서 복원 검증용 더미 형제 카드 2장 배치 (대상 카드는 인덱스 0)
        CardUI cardUI = CreateTestCardUI(parentRect, "DraggedCard");
        CreateTestCardUI(parentRect, "OtherCardA");
        CreateTestCardUI(parentRect, "OtherCardB");

        cardUI.usePlayGesture = true;
        cardUI.enableZoom = true;
        cardUI.zoomScale = 1.1f;
        cardUI.playDragThresholdY = 140f;

        Vector2 slotPos = new Vector2(-120f, 0f);
        cardUI.SetHandSlot(slotPos, Vector3.one, 0);
        RectTransform rect = cardUI.CachedRectTransform;
        rect.anchoredPosition = slotPos;

        int castCount = 0;
        cardUI.OnCardClick.AddListener(() => castCount++);

        // 1. 임계치 미만(100f < 140f) 위로 드래그 후 놓으면 시전되지 않고 원래 슬롯으로 복귀
        PointerEventData beginData = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = LocalAnchoredPosToScreenPos(parentRect, rect, slotPos)
        };
        cardUI.OnBeginDrag(beginData);
        Assert.IsTrue(cardUI.IsDragging);
        Assert.AreEqual(parentRect.childCount - 1, cardUI.transform.GetSiblingIndex(), "드래그 시작 시 최상위 형제로 올라와야 합니다.");

        PointerEventData belowDragData = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = LocalAnchoredPosToScreenPos(parentRect, rect, new Vector2(-100f, 100f))
        };
        cardUI.OnDrag(belowDragData);
        Assert.AreEqual(-100f, rect.anchoredPosition.x, 0.01f);
        Assert.AreEqual(100f, rect.anchoredPosition.y, 0.01f);
        Assert.AreEqual(1.1f, cardUI.transform.localScale.x, 0.001f);

        // 드래그 중 OnPointerEnter / OnPointerExit가 호출되어도 드래그 상태가 깨지지 않아야 함
        cardUI.OnPointerEnter(belowDragData);
        cardUI.OnPointerExit(belowDragData);
        Assert.IsTrue(cardUI.IsDragging);
        Assert.AreEqual(100f, rect.anchoredPosition.y, 0.01f);

        cardUI.OnEndDrag(belowDragData);
        Assert.IsFalse(cardUI.IsDragging);
        Assert.AreEqual(0, castCount);
        Assert.AreEqual(slotPos.x, rect.anchoredPosition.x, 0.01f);
        Assert.AreEqual(slotPos.y, rect.anchoredPosition.y, 0.01f);
        Assert.AreEqual(1f, cardUI.transform.localScale.x, 0.001f);
        Assert.AreEqual(0, cardUI.transform.GetSiblingIndex());

        // 2. 임계치 이상(160f >= 140f) 위로 드래그 후 놓으면 OnCardClick 발화 및 슬롯 복귀 시작
        cardUI.OnBeginDrag(beginData);
        PointerEventData aboveDragData = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = LocalAnchoredPosToScreenPos(parentRect, rect, new Vector2(-80f, 160f))
        };
        cardUI.OnDrag(aboveDragData);
        Assert.AreEqual(160f, rect.anchoredPosition.y, 0.01f);
        Assert.IsTrue(cardUI.selectHighlightObject.activeSelf);
        Assert.AreEqual(1.1f * 1.05f, cardUI.transform.localScale.x, 0.001f);

        cardUI.OnEndDrag(aboveDragData);
        Assert.IsFalse(cardUI.IsDragging);
        Assert.AreEqual(1, castCount);
        Assert.AreEqual(slotPos.x, rect.anchoredPosition.x, 0.01f);
        Assert.AreEqual(slotPos.y, rect.anchoredPosition.y, 0.01f);
        Assert.AreEqual(1f, cardUI.transform.localScale.x, 0.001f);
    }

    [Test]
    public void CardUI_CancelDragAndSafetyResets_ClearDragStateAndRestoreSlotSafely()
    {
        var (_, eventSystem, parentRect) = CreateTestCanvasHierarchy("CancelAndResetCanvas");
        CardUI cardUI = CreateTestCardUI(parentRect);
        cardUI.usePlayGesture = true;
        cardUI.enableZoom = true;
        cardUI.playDragThresholdY = 140f;

        Vector2 slotPos = new Vector2(80f, 0f);
        cardUI.SetHandSlot(slotPos, Vector3.one, 0);
        RectTransform rect = cardUI.CachedRectTransform;
        rect.anchoredPosition = slotPos;

        int castCount = 0;
        cardUI.OnCardClick.AddListener(() => castCount++);

        // 1. 임계치 이상 드래그 후 CancelDrag() 호출 -> 시전되지 않고 슬롯 복귀, 후속 OnEndDrag도 무시
        PointerEventData dragOverThreshold = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = LocalAnchoredPosToScreenPos(parentRect, rect, new Vector2(80f, 220f))
        };
        cardUI.OnBeginDrag(dragOverThreshold);
        cardUI.OnDrag(dragOverThreshold);
        Assert.IsTrue(cardUI.IsDragging);
        Assert.AreEqual(220f, rect.anchoredPosition.y, 0.01f);

        cardUI.CancelDrag();
        Assert.IsFalse(cardUI.IsDragging);
        Assert.IsFalse(cardUI.selectHighlightObject.activeSelf);
        Assert.AreEqual(slotPos.x, rect.anchoredPosition.x, 0.01f);
        Assert.AreEqual(slotPos.y, rect.anchoredPosition.y, 0.01f);
        Assert.AreEqual(0, castCount);

        cardUI.OnEndDrag(dragOverThreshold);
        Assert.AreEqual(0, castCount);

        // 2. SetInteractable(false) 시 드래그 중단 및 슬롯 복귀
        cardUI.OnBeginDrag(dragOverThreshold);
        cardUI.OnDrag(dragOverThreshold);
        Assert.IsTrue(cardUI.IsDragging);
        cardUI.SetInteractable(false);
        Assert.IsFalse(cardUI.IsDragging);
        Assert.AreEqual(slotPos.y, rect.anchoredPosition.y, 0.01f);

        // 3. SetDiscarding(true) 시 드래그 중단 및 클릭 차단
        cardUI.SetInteractable(true);
        cardUI.OnBeginDrag(dragOverThreshold);
        Assert.IsTrue(cardUI.IsDragging);
        cardUI.SetDiscarding(true);
        Assert.IsFalse(cardUI.IsDragging);
        Assert.IsTrue(cardUI.IsDiscarding);

        // 4. OnDisable() 시 드래그 상태 해제 및 슬롯 위치 복구 검증
        cardUI.SetDiscarding(false);
        cardUI.SetInteractable(true);
        cardUI.OnBeginDrag(dragOverThreshold);
        cardUI.OnDrag(dragOverThreshold);
        Assert.IsTrue(cardUI.IsDragging);
        Assert.AreEqual(220f, rect.anchoredPosition.y, 0.01f);

        cardUI.gameObject.SetActive(false);
        InvokePrivateMethod(cardUI, "OnDisable");
        Assert.IsFalse(cardUI.IsDragging);
        Assert.AreEqual(slotPos.x, rect.anchoredPosition.x, 0.01f);
        Assert.AreEqual(slotPos.y, rect.anchoredPosition.y, 0.01f);
        cardUI.gameObject.SetActive(true);
        InvokePrivateMethod(cardUI, "OnEnable");

        // 5. ResetForPool() 시 usePlayGesture와 IsDragging 모두 초기화
        cardUI.usePlayGesture = true;
        cardUI.OnBeginDrag(dragOverThreshold);
        Assert.IsTrue(cardUI.IsDragging);

        cardUI.ResetForPool();
        Assert.IsFalse(cardUI.usePlayGesture);
        Assert.IsFalse(cardUI.IsDragging);
        Assert.AreEqual(Vector2.zero, rect.anchoredPosition);
    }

    [Test]
    public void BattleHandPanel_RefreshHand_PreservesDraggingCardPositionAndTopSiblingOrder()
    {
        var (_, eventSystem, contentRect) = CreateTestCanvasHierarchy("HandPanelDragCanvas");

        GameObject panelGo = Track(new GameObject("BattleHandPanel", typeof(RectTransform)));
        panelGo.transform.SetParent(contentRect.parent, false);
        BattleHandPanel handPanel = panelGo.AddComponent<BattleHandPanel>();
        handPanel.contentTransform = contentRect;
        handPanel.defaultCardSpacing = 160f;
        InvokePrivateMethod(handPanel, "Awake");

        Card card1 = CreateTestCard("검격", 1);
        Card card2 = CreateTestCard("방어", 1);

        CardUI card1UI = CreateTestCardUI(contentRect, "Card1UI");
        CardUI card2UI = CreateTestCardUI(contentRect, "Card2UI");
        card1UI.UpdateCardInstance(card1);
        card2UI.UpdateCardInstance(card2);

        IList activeList = GetPrivateField<IList>(handPanel, "activeCardUIs");
        IDictionary entryMap = GetPrivateField<IDictionary>(handPanel, "_activeEntryMap");

        object entry1 = CreateHandCardEntry(card1, card1UI.gameObject, card1UI);
        object entry2 = CreateHandCardEntry(card2, card2UI.gameObject, card2UI);
        activeList.Add(entry1);
        activeList.Add(entry2);
        entryMap[card1] = entry1;
        entryMap[card2] = entry2;

        // 1. 초기 RefreshHand 호출로 ConfigureHandCardVisuals 및 슬롯 배치 적용
        List<Card> initialHand = new List<Card> { card1, card2 };
        handPanel.RefreshHand(initialHand);

        Assert.IsTrue(card1UI.usePlayGesture, "BattleHandPanel은 손패 카드에 usePlayGesture = true를 설정해야 합니다.");
        Assert.IsTrue(card2UI.usePlayGesture);
        Assert.AreEqual(-80f, card1UI.CachedRectTransform.anchoredPosition.x, 0.01f);
        Assert.AreEqual(80f, card2UI.CachedRectTransform.anchoredPosition.x, 0.01f);

        // 2. card1 드래그 시작 후 임의 위치로 이동
        PointerEventData dragData = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = LocalAnchoredPosToScreenPos(contentRect, card1UI.CachedRectTransform, new Vector2(45f, 200f))
        };
        card1UI.OnBeginDrag(dragData);
        card1UI.OnDrag(dragData);
        Assert.IsTrue(card1UI.IsDragging);
        Assert.AreEqual(new Vector2(45f, 200f), card1UI.CachedRectTransform.anchoredPosition);

        // 3. 드래그 도중 손패 갱신(card2 제거로 card1 슬롯이 -80 -> 0으로 변경됨)이 일어나도
        //    드래그 중인 card1의 현재 위치(45, 200)는 덮어쓰이지 않고 최상위 형제를 유지해야 함
        handPanel.RefreshHand(new List<Card> { card1 });

        Assert.IsTrue(card1UI.usePlayGesture);
        Assert.IsTrue(card1UI.IsDragging);
        Assert.AreEqual(45f, card1UI.CachedRectTransform.anchoredPosition.x, 0.01f);
        Assert.AreEqual(200f, card1UI.CachedRectTransform.anchoredPosition.y, 0.01f);
        Assert.AreEqual(contentRect.childCount - 1, card1UI.transform.GetSiblingIndex());

        // 4. 갱신된 기준 슬롯 좌표(0, 0)로 CancelDrag 복귀 확인
        card1UI.CancelDrag();
        Assert.IsFalse(card1UI.IsDragging);
        Assert.AreEqual(0f, card1UI.CachedRectTransform.anchoredPosition.x, 0.01f);
        Assert.AreEqual(0f, card1UI.CachedRectTransform.anchoredPosition.y, 0.01f);
    }
}
