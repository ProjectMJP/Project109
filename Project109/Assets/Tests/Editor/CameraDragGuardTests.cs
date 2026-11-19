using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[TestFixture]
public class CameraDragGuardTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<Camera> _disabledSceneMainCameras = new List<Camera>();
    private readonly List<PlayerInputController> _createdInputControllers = new List<PlayerInputController>();
    private readonly List<CameraController> _createdCameraControllers = new List<CameraController>();

    [SetUp]
    public void SetUp()
    {
        UIDragInputGuard.ClearAll();
        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        SetStaticAutoProperty<CameraController>("instance", null);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdCameraControllers.Count - 1; i >= 0; i--)
        {
            CameraController camCtrl = _createdCameraControllers[i];
            if (camCtrl != null)
            {
                InvokePrivateMethod(camCtrl, "OnDisable");
                InvokePrivateMethod(camCtrl, "OnDestroy");
            }
        }
        _createdCameraControllers.Clear();

        for (int i = _createdInputControllers.Count - 1; i >= 0; i--)
        {
            PlayerInputController inputCtrl = _createdInputControllers[i];
            if (inputCtrl != null)
            {
                inputCtrl.PointerOverUIOverrideForTest = null;
                InvokePrivateMethod(inputCtrl, "OnDisable");
                InvokePrivateMethod(inputCtrl, "OnDestroy");
            }
        }
        _createdInputControllers.Clear();

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();

        for (int i = 0; i < _disabledSceneMainCameras.Count; i++)
        {
            Camera cam = _disabledSceneMainCameras[i];
            if (cam != null)
            {
                cam.enabled = true;
            }
        }
        _disabledSceneMainCameras.Clear();

        UIDragInputGuard.ClearAll();
        // 파괴된 CardUI 정적 드래그 목록 잔여분까지 정리 보장
        _ = CardUI.IsAnyCardDragging;

        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        SetStaticAutoProperty<CameraController>("instance", null);
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private Camera CreateTestMainCamera(Vector3 position, Quaternion rotation)
    {
        Camera[] existingCameras = Camera.allCameras;
        for (int i = 0; i < existingCameras.Length; i++)
        {
            Camera existing = existingCameras[i];
            if (existing != null && existing.enabled && existing.CompareTag("MainCamera"))
            {
                existing.enabled = false;
                _disabledSceneMainCameras.Add(existing);
            }
        }

        GameObject camGo = Track(new GameObject("TestMainCamera", typeof(Camera)));
        camGo.tag = "MainCamera";
        camGo.transform.position = position;
        camGo.transform.rotation = rotation;

        Camera cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 10f;
        return cam;
    }

    private CameraController CreateTestCameraController(Camera cam)
    {
        GameObject ctrlGo = Track(new GameObject("TestCameraController"));
        CameraController camCtrl = ctrlGo.AddComponent<CameraController>();
        SetPrivateField(camCtrl, "mainCamera", cam);
        InvokePrivateMethod(camCtrl, "Awake");
        InvokePrivateMethod(camCtrl, "Start");
        InvokePrivateMethod(camCtrl, "OnEnable");
        _createdCameraControllers.Add(camCtrl);
        return camCtrl;
    }

    private PlayerInputController CreateTestInputController()
    {
        GameObject inputGo = Track(new GameObject("TestPlayerInputController"));
        PlayerInputController input = inputGo.AddComponent<PlayerInputController>();
        InvokePrivateMethod(input, "Awake");
        InvokePrivateMethod(input, "OnEnable");
        _createdInputControllers.Add(input);
        return input;
    }

    private (EventSystem eventSystem, RectTransform parentRect) CreateTestCardCanvas()
    {
        GameObject esGo = Track(new GameObject("TestEventSystem", typeof(EventSystem)));
        EventSystem eventSystem = esGo.GetComponent<EventSystem>();

        GameObject canvasGo = Track(new GameObject("TestCardCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject parentGo = Track(new GameObject("HandContent", typeof(RectTransform)));
        parentGo.transform.SetParent(canvasGo.transform, false);
        RectTransform parentRect = parentGo.GetComponent<RectTransform>();
        parentRect.anchorMin = new Vector2(0.5f, 0.5f);
        parentRect.anchorMax = new Vector2(0.5f, 0.5f);
        parentRect.pivot = new Vector2(0.5f, 0.5f);
        parentRect.sizeDelta = new Vector2(1000f, 220f);

        return (eventSystem, parentRect);
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
        cardUI.usePlayGesture = true;
        cardUI.enableZoom = true;
        cardUI.playDragThresholdY = 140f;
        InvokePrivateMethod(cardUI, "Awake");
        InvokePrivateMethod(cardUI, "OnEnable");
        return cardUI;
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"메서드 '{methodName}'를 {target.GetType().Name}에서 찾을 수 없습니다.");
        method.Invoke(target, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"필드 '{fieldName}'을(를) {target.GetType().Name}에서 찾을 수 없습니다.");
        field.SetValue(target, value);
    }

    private static T GetStaticPrivateField<T>(Type type, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"정적 필드 '{fieldName}'을(를) {type.Name}에서 찾을 수 없습니다.");
        return (T)field.GetValue(null);
    }

    private static void SetStaticField<T>(string fieldName, object value)
    {
        FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"정적 필드 '{fieldName}'을(를) {typeof(T).Name}에서 찾을 수 없습니다.");
        field.SetValue(null, value);
    }

    private static void SetStaticAutoProperty<T>(string propertyName, object value)
    {
        FieldInfo backingField = typeof(T).GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(backingField, $"정적 백킹 필드 '<{propertyName}>k__BackingField'을(를) {typeof(T).Name}에서 찾을 수 없습니다.");
        backingField.SetValue(null, value);
    }

    [Test]
    public void UIDragInputGuard_IgnoresNull_TracksMultipleOwners_AndNeverUnderflows()
    {
        Assert.IsFalse(UIDragInputGuard.IsAnyUIDragging);

        UIDragInputGuard.BeginDrag(null);
        UIDragInputGuard.EndDrag(null);
        Assert.IsFalse(UIDragInputGuard.IsAnyUIDragging);

        object ownerA = new object();
        object ownerB = new object();

        UIDragInputGuard.BeginDrag(ownerA);
        UIDragInputGuard.BeginDrag(ownerA);
        UIDragInputGuard.BeginDrag(ownerB);
        Assert.IsTrue(UIDragInputGuard.IsAnyUIDragging);

        // 동일 소유자 반복 해제 시에도 다른 소유자는 유지되어야 함
        UIDragInputGuard.EndDrag(ownerA);
        UIDragInputGuard.EndDrag(ownerA);
        Assert.IsTrue(UIDragInputGuard.IsAnyUIDragging);

        UIDragInputGuard.EndDrag(ownerB);
        UIDragInputGuard.EndDrag(ownerB);
        Assert.IsFalse(UIDragInputGuard.IsAnyUIDragging);

        UIDragInputGuard.BeginDrag(ownerA);
        UIDragInputGuard.BeginDrag(ownerB);
        Assert.IsTrue(UIDragInputGuard.IsAnyUIDragging);
        UIDragInputGuard.ClearAll();
        Assert.IsFalse(UIDragInputGuard.IsAnyUIDragging);
    }

    [Test]
    public void TouchStartOverUI_BlocksStartDragAndClickAndCameraPan_EvenAfterCursorLeavesUI()
    {
        Camera cam = CreateTestMainCamera(new Vector3(0f, 10f, -10f), Quaternion.Euler(45f, 0f, 0f));
        PlayerInputController input = CreateTestInputController();
        CreateTestCameraController(cam);

        bool pointerOverUI = true;
        input.PointerOverUIOverrideForTest = () => pointerOverUI;

        int startCount = 0;
        int dragCount = 0;
        int endCount = 0;
        int clickCount = 0;

        input.OnTouchStartEvent += _ => startCount++;
        input.OnTouchDragEvent += _ => dragCount++;
        input.OnTouchEndEvent += () => endCount++;
        input.OnTouchClickEvent += _ => clickCount++;

        Vector3 initialCamPos = cam.transform.position;
        Vector2 startScreen = cam.WorldToScreenPoint(initialCamPos);
        Vector2 dragOffUIScreen = cam.WorldToScreenPoint(initialCamPos + cam.transform.right * 4f + cam.transform.up * 2f);

        // 1. UI 위에서 터치 시작 -> Start 이벤트 미발행 & 차단 플래그 활성화
        input.ProcessTouchStart(startScreen);
        Assert.IsTrue(input.IsPointerDragBlockedByUI);
        Assert.AreEqual(0, startCount);

        // 2. 드래그 도중 커서가 UI 영역 밖(월드)으로 이동해도 카메라 팬 및 Drag 이벤트 차단 유지
        pointerOverUI = false;
        input.ProcessTouchDrag(dragOffUIScreen);
        Assert.IsTrue(input.IsPointerDragBlockedByUI);
        Assert.AreEqual(0, startCount);
        Assert.AreEqual(0, dragCount);
        Assert.AreEqual(initialCamPos, cam.transform.position);

        // 3. 터치 종료 시 OnTouchEndEvent는 발행되고 _startedOverUI 플래그가 초기화됨
        input.ProcessTouchEnd();
        Assert.AreEqual(1, endCount);
        Assert.IsFalse(input.IsPointerDragBlockedByUI);

        // 4. UI 위에서 시작 후 짧은 거리(<= 20px)로 떼어도 월드 클릭이 발생하면 안 됨
        pointerOverUI = true;
        input.ProcessTouchStart(startScreen);
        pointerOverUI = false;
        input.ProcessTouchDrag(startScreen + new Vector2(5f, 5f));
        input.ProcessTouchEnd();
        InvokePrivateMethod(input, "Update");
        Assert.AreEqual(0, clickCount);
    }

    [Test]
    public void AfterTouchEndResetsStartedOverUI_PureWorldDragPansCameraAndRespectsClampArea()
    {
        Camera cam = CreateTestMainCamera(new Vector3(0f, 10f, -10f), Quaternion.Euler(45f, 0f, 0f));
        PlayerInputController input = CreateTestInputController();
        CreateTestCameraController(cam);

        bool pointerOverUI = true;
        input.PointerOverUIOverrideForTest = () => pointerOverUI;

        int startCount = 0;
        int dragCount = 0;
        int endCount = 0;
        int clickCount = 0;

        input.OnTouchStartEvent += _ => startCount++;
        input.OnTouchDragEvent += _ => dragCount++;
        input.OnTouchEndEvent += () => endCount++;
        input.OnTouchClickEvent += _ => clickCount++;

        Vector3 initialCamPos = cam.transform.position;
        Vector2 screenOrigin = cam.WorldToScreenPoint(initialCamPos);

        // 1. 먼저 UI 위 드래그 시도 (차단 후 종료)
        input.ProcessTouchStart(screenOrigin);
        pointerOverUI = false;
        input.ProcessTouchDrag(cam.WorldToScreenPoint(initialCamPos + cam.transform.right * 3f));
        input.ProcessTouchEnd();
        Assert.AreEqual(initialCamPos, cam.transform.position);
        Assert.AreEqual(0, startCount);
        Assert.AreEqual(0, dragCount);
        Assert.AreEqual(1, endCount);

        // 2. 순수 월드에서 새 터치 시작 -> 유효 지면 평면(Y=0) 교차 범위 내 카메라 팬 정상 동작
        Vector2 panTargetScreen = cam.WorldToScreenPoint(initialCamPos + cam.transform.right * 3f + cam.transform.up * 2f);
        Vector3 expectedDiff = cam.ScreenToWorldPoint(screenOrigin) - cam.ScreenToWorldPoint(panTargetScreen);

        input.ProcessTouchStart(screenOrigin);
        Assert.IsFalse(input.IsPointerDragBlockedByUI);
        Assert.AreEqual(1, startCount);

        input.ProcessTouchDrag(panTargetScreen);
        Assert.AreEqual(1, dragCount);
        Vector3 pannedPos = cam.transform.position;
        Assert.Greater(Vector3.Distance(initialCamPos, pannedPos), 0.1f, "순수 월드 드래그 시 카메라가 이동해야 합니다.");
        Assert.AreEqual((initialCamPos + expectedDiff).x, pannedPos.x, 0.01f);
        Assert.AreEqual((initialCamPos + expectedDiff).y, pannedPos.y, 0.01f);
        Assert.AreEqual((initialCamPos + expectedDiff).z, pannedPos.z, 0.01f);

        // 3. CameraMoveInClampArea 경계 밖(Y < 0, 지면 평면/콜라이더 미교차 영역) 드래그 시 클램핑 유지 확인
        Vector2 outOfBoundsScreen = cam.WorldToScreenPoint(pannedPos + cam.transform.up * 40f);
        input.ProcessTouchDrag(outOfBoundsScreen);
        Assert.AreEqual(pannedPos.x, cam.transform.position.x, 0.001f);
        Assert.AreEqual(pannedPos.y, cam.transform.position.y, 0.001f);
        Assert.AreEqual(pannedPos.z, cam.transform.position.z, 0.001f);

        input.ProcessTouchEnd();
        Assert.AreEqual(2, endCount);

        // 4. 순수 월드 짧은 클릭은 정상적으로 OnTouchClickEvent 발행
        input.ProcessTouchStart(screenOrigin);
        input.ProcessTouchEnd();
        InvokePrivateMethod(input, "Update");
        Assert.AreEqual(1, clickCount);
    }

    [Test]
    public void CardUI_BeginDrag_BlocksInputAndCameraEvenWhenPointerOverUIIsFalse()
    {
        Camera cam = CreateTestMainCamera(new Vector3(0f, 10f, -10f), Quaternion.Euler(45f, 0f, 0f));
        PlayerInputController input = CreateTestInputController();
        CreateTestCameraController(cam);
        var (eventSystem, parentRect) = CreateTestCardCanvas();
        CardUI cardUI = CreateTestCardUI(parentRect, "DraggedCard");

        // 포인터 오버 UI 판정이 false인 상태에서도 카드 드래그 가드가 차단해야 함
        input.PointerOverUIOverrideForTest = () => false;

        int startCount = 0;
        int dragCount = 0;
        input.OnTouchStartEvent += _ => startCount++;
        input.OnTouchDragEvent += _ => dragCount++;

        PointerEventData beginData = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = Vector2.zero
        };
        cardUI.OnBeginDrag(beginData);

        Assert.IsTrue(cardUI.IsDragging);
        Assert.IsTrue(CardUI.IsAnyCardDragging);
        Assert.IsTrue(UIDragInputGuard.IsAnyUIDragging);
        Assert.IsTrue(input.IsPointerDragBlockedByUI);

        Vector3 initialCamPos = cam.transform.position;
        Vector2 screenStart = cam.WorldToScreenPoint(initialCamPos);
        Vector2 screenDrag = cam.WorldToScreenPoint(initialCamPos + cam.transform.right * 4f);

        input.ProcessTouchStart(screenStart);
        input.ProcessTouchDrag(screenDrag);
        Assert.AreEqual(0, startCount);
        Assert.AreEqual(0, dragCount);
        Assert.AreEqual(initialCamPos, cam.transform.position);
        input.ProcessTouchEnd();

        // 이미 월드 드래그가 시작된 직후 카드 드래그가 발동하더라도 즉시 _isDraggingWorld가 해제되어야 함
        cardUI.OnEndDrag(beginData);
        Assert.IsFalse(CardUI.IsAnyCardDragging);
        Assert.IsFalse(UIDragInputGuard.IsAnyUIDragging);

        input.ProcessTouchStart(screenStart);
        Assert.AreEqual(1, startCount);

        cardUI.OnBeginDrag(beginData);
        input.ProcessTouchDrag(screenDrag);
        Assert.AreEqual(0, dragCount);
        Assert.AreEqual(initialCamPos, cam.transform.position);

        // 같은 터치 세션 내에서는 카드 드래그가 끝나도 새 TouchStart 전까지 카메라가 갑자기 튀지 않아야 함
        cardUI.OnEndDrag(beginData);
        input.ProcessTouchDrag(screenDrag);
        Assert.AreEqual(initialCamPos, cam.transform.position);

        input.ProcessTouchEnd();
    }

    [Test]
    public void CardUI_AllClearResetAndDestroyPaths_ClearDragGuardWithoutCounterLeaks()
    {
        var (eventSystem, parentRect) = CreateTestCardCanvas();
        CardUI cardUI = CreateTestCardUI(parentRect, "LifecycleCard");
        PointerEventData dragData = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = Vector2.zero
        };

        void AssertCleared()
        {
            Assert.IsFalse(cardUI.IsDragging);
            Assert.IsFalse(CardUI.IsAnyCardDragging);
            Assert.IsFalse(UIDragInputGuard.IsAnyUIDragging);
            Assert.AreEqual(0, GetStaticPrivateField<int>(typeof(CardUI), "_activeCardDragCount"));
        }

        // 1. OnEndDrag (+ 중복 호출 언더플로 방지)
        cardUI.OnBeginDrag(dragData);
        Assert.IsTrue(CardUI.IsAnyCardDragging);
        Assert.IsTrue(UIDragInputGuard.IsAnyUIDragging);
        cardUI.OnEndDrag(dragData);
        cardUI.OnEndDrag(dragData);
        AssertCleared();

        // 2. CancelDrag (+ 중복 호출)
        cardUI.OnBeginDrag(dragData);
        cardUI.CancelDrag();
        cardUI.CancelDrag();
        AssertCleared();

        // 3. ResetForPool
        cardUI.usePlayGesture = true;
        cardUI.OnBeginDrag(dragData);
        cardUI.ResetForPool();
        AssertCleared();

        // 4. SetDiscarding(true)
        cardUI.usePlayGesture = true;
        cardUI.SetInteractable(true);
        cardUI.OnBeginDrag(dragData);
        cardUI.SetDiscarding(true);
        AssertCleared();

        // 5. SetInteractable(false)
        cardUI.SetDiscarding(false);
        cardUI.SetInteractable(true);
        cardUI.OnBeginDrag(dragData);
        cardUI.SetInteractable(false);
        AssertCleared();

        // 6. SetEntering(true)
        cardUI.SetInteractable(true);
        cardUI.OnBeginDrag(dragData);
        cardUI.SetEntering(true);
        AssertCleared();

        // 7. UpdateCardData / UpdateCardInstance
        cardUI.SetEntering(false);
        cardUI.OnBeginDrag(dragData);
        cardUI.UpdateCardData(new CardData { cardName = "TestStrike", displayName = "TestStrike", stamina = 1 });
        AssertCleared();

        // 8. OnDisable
        cardUI.OnBeginDrag(dragData);
        cardUI.gameObject.SetActive(false);
        InvokePrivateMethod(cardUI, "OnDisable");
        AssertCleared();
        cardUI.gameObject.SetActive(true);
        InvokePrivateMethod(cardUI, "OnEnable");

        // 9. OnDestroy
        cardUI.OnBeginDrag(dragData);
        InvokePrivateMethod(cardUI, "OnDestroy");
        AssertCleared();

        // 10. 다중 카드 드래그 후 개별 종료 및 OnDestroy 미호출 DestroyImmediate 정합성
        CardUI secondCard = CreateTestCardUI(parentRect, "SecondCard");
        cardUI.OnBeginDrag(dragData);
        secondCard.OnBeginDrag(dragData);
        Assert.AreEqual(2, GetStaticPrivateField<int>(typeof(CardUI), "_activeCardDragCount"));
        Assert.IsTrue(CardUI.IsAnyCardDragging);
        Assert.IsTrue(UIDragInputGuard.IsAnyUIDragging);

        cardUI.OnEndDrag(dragData);
        Assert.AreEqual(1, GetStaticPrivateField<int>(typeof(CardUI), "_activeCardDragCount"));
        Assert.IsTrue(CardUI.IsAnyCardDragging);
        Assert.IsTrue(UIDragInputGuard.IsAnyUIDragging);

        UnityEngine.Object.DestroyImmediate(secondCard.gameObject);
        Assert.IsFalse(UIDragInputGuard.IsAnyUIDragging);
        Assert.IsFalse(CardUI.IsAnyCardDragging);
        Assert.AreEqual(0, GetStaticPrivateField<int>(typeof(CardUI), "_activeCardDragCount"));
    }

    [Test]
    public void CameraController_OnDisable_DoesNotLazyInstantiatePlayerInputController_AndInputDisableResetsFlag()
    {
        Assert.IsFalse(PlayerInputController.HasInstance);
        Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<PlayerInputController>());

        GameObject camCtrlGo = Track(new GameObject("StandaloneCameraController"));
        CameraController camCtrl = camCtrlGo.AddComponent<CameraController>();
        InvokePrivateMethod(camCtrl, "OnDisable");

        Assert.IsFalse(PlayerInputController.HasInstance, "OnDisable 시 PlayerInputController를 지연 생성하면 안 됩니다.");
        Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<PlayerInputController>());

        // PlayerInputController.OnDisable 시 _startedOverUI 플래그 초기화 검증
        PlayerInputController input = CreateTestInputController();
        input.PointerOverUIOverrideForTest = () => true;
        input.ProcessTouchStart(new Vector2(100f, 100f));
        Assert.IsTrue(input.IsPointerDragBlockedByUI);

        InvokePrivateMethod(input, "OnDisable");
        Assert.IsFalse(input.IsPointerDragBlockedByUI);
    }
}
