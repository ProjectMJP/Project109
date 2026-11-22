using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[TestFixture]
public class CardTargetingPreviewAndCancelTests
{
    private sealed class StubEnemyController : ICharacterController
    {
        public Character controlledCharacter { get; }

        public StubEnemyController(Character character)
        {
            controlledCharacter = character;
        }

        public void OnTurnStart() { }
        public void OnTurnEnd() { }
    }

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<Character> _createdCharacters = new List<Character>();
    private readonly List<PlayerBattleController> _createdControllers = new List<PlayerBattleController>();
    private readonly List<Player> _createdPlayers = new List<Player>();
    private readonly List<BattleManager> _createdBattleManagers = new List<BattleManager>();
    private readonly List<Card> _createdCards = new List<Card>();
    private readonly List<Camera> _disabledSceneMainCameras = new List<Camera>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < _createdCharacters.Count; i++)
        {
            Character c = _createdCharacters[i];
            if (c != null && c.characterMove != null)
            {
                c.characterMove.StopMoving();
            }
        }
        _createdCharacters.Clear();

        for (int i = _createdBattleManagers.Count - 1; i >= 0; i--)
        {
            if (_createdBattleManagers[i] != null)
            {
                _createdBattleManagers[i].Dispose();
            }
        }
        _createdBattleManagers.Clear();

        for (int i = _createdControllers.Count - 1; i >= 0; i--)
        {
            if (_createdControllers[i] != null)
            {
                _createdControllers[i].Dispose();
            }
        }
        _createdControllers.Clear();

        for (int i = _createdCards.Count - 1; i >= 0; i--)
        {
            if (_createdCards[i] != null && !_createdCards[i].IsDisposed)
            {
                _createdCards[i].Dispose();
            }
        }
        _createdCards.Clear();

        for (int i = _createdPlayers.Count - 1; i >= 0; i--)
        {
            if (_createdPlayers[i] != null)
            {
                _createdPlayers[i].Dispose();
            }
        }
        _createdPlayers.Clear();

        ActionQueueManager queueManager = UnityEngine.Object.FindFirstObjectByType<ActionQueueManager>();
        if (queueManager != null)
        {
            queueManager.ClearQueue();
            UnityEngine.Object.DestroyImmediate(queueManager.gameObject);
        }
        SetStaticField<ActionQueueManager>("_instance", null);

        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        SetStaticAutoProperty<BattleTimelinePanel>("Instance", null);
        SetStaticAutoProperty<BattleSideUnitHUDPanel>("Instance", null);
        SetStaticAutoProperty<RunManager>("instance", null);
        SetStaticAutoProperty<GameSceneManager>("instance", null);
        SetStaticAutoProperty<UIManager>("instance", null);
        SetStaticAutoProperty<GameMap>("current", null);

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
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private Canvas CreateTestCanvas(string name = "TestBattleCanvas")
    {
        GameObject esGo = Track(new GameObject(name + "_EventSystem", typeof(EventSystem)));
        _ = esGo;

        GameObject canvasGo = Track(new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1920f, 1080f);
        return canvas;
    }

    private GameMap CreateTestGameMap(int width = 5, int height = 5)
    {
        GameObject mapGo = Track(new GameObject("TestGameMap"));
        GameMap gameMap = mapGo.AddComponent<GameMap>();
        InvokePrivateMethod(gameMap, "Awake");

        GameObject tilePrefab = Track(new GameObject("TilePrefab", typeof(Tile)));
        gameMap.prefabTile = tilePrefab;

        MapDataSO mapData = Track(ScriptableObject.CreateInstance<MapDataSO>());
        mapData.width = width;
        mapData.height = height;
        mapData.cellSize = 1f;
        mapData.gridOffset = Vector3.zero;
        mapData.InitializeGrid();

        for (int i = 0; i < mapData.cells.Count; i++)
        {
            mapData.cells[i].terrainID = "Floor";
        }

        gameMap.TileCreateByMapData(mapData);
        return gameMap;
    }

    private Camera CreateTestMainCamera(Vector3 lookCenter)
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
        camGo.transform.position = lookCenter + new Vector3(0f, 10f, 0f);
        camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        Camera cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        return cam;
    }

    private Character CreateTestCharacter(string name, CharacterFaction faction, Tile startTile)
    {
        GameObject charGo = Track(new GameObject(name));
        Character character = charGo.AddComponent<Character>();
        InvokePrivateMethod(character, "Awake");
        character.faction = faction;
        character.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 10f,
            staminaRegenPerSecond = 0f,
            moveRange = 3
        });
        character.curStamina = 10f;
        character.currentState = CharacterState.Idle;
        if (startTile != null)
        {
            character.characterMove.SetCurrentTile(startTile);
            charGo.transform.position = startTile.transform.position;
        }
        _createdCharacters.Add(character);
        return character;
    }

    private Card CreateTestCard(string name, string targetType, int minDistance = 1, int maxDistance = 3)
    {
        CardData data = new CardData
        {
            cardName = name,
            displayName = name,
            stamina = 0,
            targetType = targetType,
            targetMinDistance = minDistance,
            targetMaxDistance = maxDistance
        };
        Card card = new Card(data, null, null, -(_createdCards.Count + 1));
        _createdCards.Add(card);
        return card;
    }

    private CardUI CreateTestCardUI(Transform parent, string name)
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

    private PlayerInputController CreateMockInputController()
    {
        GameObject inputGo = Track(new GameObject("TestPlayerInputController"));
        PlayerInputController inputCtrl = inputGo.AddComponent<PlayerInputController>();
        SetStaticField<PlayerInputController>("_instance", inputCtrl);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);
        return inputCtrl;
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
    public void BattleUIPanelPrefab_HasTargetingPreviewBindings_NoNestedCanvas_AndNonOverlappingBottomRightLayout()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleUIPanel.prefab");
        Assert.IsNotNull(prefab, "BattleUIPanel.prefab을 찾을 수 없습니다.");

        Canvas canvas = CreateTestCanvas("PrefabTargetingLayoutCanvas");
        GameObject panelGo = Track(UnityEngine.Object.Instantiate(prefab, canvas.transform, false));
        BattleUIPanel battleUI = panelGo.GetComponent<BattleUIPanel>();
        Assert.IsNotNull(battleUI);
        InvokePrivateMethod(battleUI.HandPanel, "Awake");
        InvokePrivateMethod(battleUI.MovePanel, "Awake");
        InvokePrivateMethod(battleUI, "Awake");

        Assert.AreEqual(0, panelGo.GetComponentsInChildren<Canvas>(true).Length, "BattleUIPanel 프리팹 내부에는 중첩 Canvas가 없어야 합니다.");
        Assert.AreEqual(0, panelGo.GetComponentsInChildren<CanvasScaler>(true).Length, "BattleUIPanel 프리팹 내부에는 CanvasScaler가 없어야 합니다.");

        Assert.IsNotNull(battleUI.TargetingPreviewContainer);
        Assert.IsNotNull(battleUI.TargetingPreviewCardUI);
        Assert.IsNotNull(battleUI.CancelTargetingButton);
        Assert.IsFalse(battleUI.TargetingPreviewContainer.activeSelf, "초기 상태에서 타깃팅 프리뷰 컨테이너는 비활성화되어야 합니다.");

        TextMeshProUGUI cancelText = battleUI.CancelTargetingButton.GetComponentInChildren<TextMeshProUGUI>(true);
        Assert.IsNotNull(cancelText);
        Assert.AreEqual("사용 취소", cancelText.text);

        RectTransform previewRectTransform = battleUI.TargetingPreviewContainer.GetComponent<RectTransform>();
        RectTransform endTurnRectTransform = battleUI.EndTurnButton.GetComponent<RectTransform>();
        Assert.AreEqual(new Vector2(1f, 0f), previewRectTransform.anchorMin);
        Assert.AreEqual(new Vector2(1f, 0f), previewRectTransform.anchorMax);

        battleUI.TargetingPreviewContainer.SetActive(true);
        Canvas.ForceUpdateCanvases();

        Rect previewScreenRect = GetScreenSpaceRect(previewRectTransform);
        Rect endTurnScreenRect = GetScreenSpaceRect(endTurnRectTransform);
        Assert.IsFalse(previewScreenRect.Overlaps(endTurnScreenRect), "타깃팅 프리뷰 영역과 턴 종료 버튼은 겹치지 않아야 합니다.");
        Assert.Greater(previewScreenRect.yMin, endTurnScreenRect.yMax, "타깃팅 프리뷰 영역은 턴 종료 버튼 위쪽에 배치되어야 합니다.");
    }

    [Test]
    public void TargetingMode_ShowsPreviewDisablesTurnButtons_AndCancelOrConfirmOrTurnEndRestoresAll()
    {
        CreateMockInputController();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleUIPanel.prefab");
        Assert.IsNotNull(prefab);

        GameMap map = CreateTestGameMap(5, 5);
        Camera cam = CreateTestMainCamera(new Vector3(2f, 0f, 2f));
        Tile playerTile = map.GetTile(2, 2);
        Tile enemyTile = map.GetTile(2, 3);

        Character playerChar = CreateTestCharacter("Player", CharacterFaction.Player, playerTile);
        Character enemyChar = CreateTestCharacter("Enemy", CharacterFaction.Enemy, enemyTile);

        Player player = new Player(playerChar);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        PlayerBattleController controller = new PlayerBattleController(player);
        _createdControllers.Add(controller);

        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        SetStaticAutoProperty<RunManager>("instance", runManager);
        runManager.playerBattleController = controller;

        BattleManager battleManager = new BattleManager();
        battleManager.Initialize();
        _createdBattleManagers.Add(battleManager);

        typeof(RunManager).GetProperty("battleManager", BindingFlags.Instance | BindingFlags.Public).SetValue(runManager, battleManager);
        typeof(BattleManager).GetProperty("battleState", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, BattleState.TurnInProgress);
        typeof(BattleManager).GetProperty("currentTurnController", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, controller);

        battleManager.InitBattle(
            new List<ICharacterController> { controller },
            new List<ICharacterController> { new StubEnemyController(enemyChar) });
        battleManager.StartCombat();
        typeof(BattleManager).GetProperty("battleState", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, BattleState.TurnInProgress);
        typeof(BattleManager).GetProperty("currentTurnController", BindingFlags.Instance | BindingFlags.Public).SetValue(battleManager, controller);

        Canvas canvas = CreateTestCanvas("TargetingFlowCanvas");
        GameObject panelGo = Track(UnityEngine.Object.Instantiate(prefab, canvas.transform, false));
        BattleUIPanel battleUI = panelGo.GetComponent<BattleUIPanel>();
        Button switchToMoveBtn = GetPrivateField<Button>(battleUI.HandPanel, "switchToMoveModeButton");

        InvokePrivateMethod(battleUI.HandPanel, "Awake");
        InvokePrivateMethod(battleUI.MovePanel, "Awake");
        InvokePrivateMethod(battleUI, "Awake");
        InvokePrivateMethod(battleUI, "OnEnable");
        InvokePrivateMethod(battleUI.HandPanel, "OnEnable");
        InvokePrivateMethod(battleUI.MovePanel, "OnEnable");

        SetPrivateField(controller, "_battleUI", battleUI);
        battleUI.BindPlayer(player, controller);
        battleUI.SetTurnActive(true);

        List<Card> targetingEvents = new List<Card>();
        controller.OnTargetingStateChanged += c => targetingEvents.Add(c);

        Card targetCard = CreateTestCard("강타", "SingleEnemy", minDistance: 1, maxDistance: 3);

        // 1. 타깃팅 진입 -> 프리뷰 표시, 프리뷰 카드 비대화형, 턴 종료·이동 전환 버튼 비활성화
        controller.TryUseCard(targetCard);

        Assert.AreEqual(PlayerControlState.TargetSelection, controller.controlState);
        Assert.AreSame(targetCard, controller.ActiveTargetingCard);
        Assert.AreEqual(1, targetingEvents.Count);
        Assert.AreSame(targetCard, targetingEvents[0]);

        Assert.IsTrue(battleUI.IsTargetingActive);
        Assert.IsTrue(battleUI.TargetingPreviewContainer.activeSelf);
        Assert.AreSame(targetCard, battleUI.TargetingPreviewCardUI.GetCardInstance());
        Assert.IsFalse(battleUI.TargetingPreviewCardUI.IsInteractable);
        Assert.IsFalse(battleUI.TargetingPreviewCardUI.enableZoom);
        Assert.IsFalse(battleUI.TargetingPreviewCardUI.usePlayGesture);
        Assert.IsFalse(battleUI.EndTurnButton.interactable);
        Assert.IsFalse(switchToMoveBtn.interactable);

        // 2. 사용 취소 버튼 클릭 -> CancelCurrentState와 동일하게 해제 및 버튼 복구
        battleUI.CancelTargetingButton.onClick.Invoke();

        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.IsNull(controller.ActiveTargetingCard);
        Assert.AreEqual(2, targetingEvents.Count);
        Assert.IsNull(targetingEvents[1]);

        Assert.IsFalse(battleUI.IsTargetingActive);
        Assert.IsFalse(battleUI.TargetingPreviewContainer.activeSelf);
        Assert.IsTrue(battleUI.EndTurnButton.interactable);
        Assert.IsTrue(switchToMoveBtn.interactable);

        // 3. 다시 타깃팅 진입 후 적 타일 클릭으로 시전 확정 -> 프리뷰 숨김 및 버튼 복구
        controller.TryUseCard(targetCard);
        Assert.IsTrue(battleUI.TargetingPreviewContainer.activeSelf);

        Vector2 enemyScreenPos = cam.WorldToScreenPoint(enemyTile.transform.position);
        InvokePrivateMethod(controller, "HandleGlobalClick", enemyScreenPos);

        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.IsNull(controller.ActiveTargetingCard);
        Assert.IsFalse(battleUI.TargetingPreviewContainer.activeSelf);
        Assert.IsTrue(battleUI.EndTurnButton.interactable);
        Assert.IsTrue(switchToMoveBtn.interactable);

        // 4. 타깃팅 도중 OnTurnEnd() 호출 시 프리뷰 및 타깃팅 상태 안전 정리
        ActionQueueManager.Instance.ClearQueue();
        playerChar.currentState = CharacterState.Idle;
        controller.TryUseCard(targetCard);
        Assert.IsTrue(battleUI.TargetingPreviewContainer.activeSelf);

        controller.OnTurnEnd();
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.IsNull(controller.ActiveTargetingCard);
        Assert.IsFalse(battleUI.TargetingPreviewContainer.activeSelf);
        Assert.IsFalse(battleUI.EndTurnButton.interactable);

        // 5. 타깃팅 도중 OnBattleEnd() 호출 시에도 프리뷰 및 타깃팅 상태 안전 정리
        controller.OnTurnStart();
        Assert.IsTrue(battleUI.EndTurnButton.interactable);
        controller.TryUseCard(targetCard);
        Assert.IsTrue(battleUI.TargetingPreviewContainer.activeSelf);

        controller.OnBattleEnd();
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.IsNull(controller.ActiveTargetingCard);
        Assert.IsFalse(battleUI.IsTargetingActive);
        Assert.IsFalse(battleUI.TargetingPreviewContainer.activeSelf);
    }

    [Test]
    public void BattleHandPanel_SetTargetingState_SlidesCardsDownAndDisablesInteraction_AndRestoresOnExit()
    {
        Canvas canvas = CreateTestCanvas("HandTargetingSlideCanvas");
        GameObject panelGo = Track(new GameObject("BattleHandPanel", typeof(RectTransform)));
        panelGo.transform.SetParent(canvas.transform, false);
        BattleHandPanel handPanel = panelGo.AddComponent<BattleHandPanel>();

        GameObject contentGo = Track(new GameObject("HandContent", typeof(RectTransform)));
        contentGo.transform.SetParent(panelGo.transform, false);
        handPanel.contentTransform = contentGo.transform;
        handPanel.defaultCardSpacing = 160f;
        InvokePrivateMethod(handPanel, "Awake");

        Card cardA = CreateTestCard("화염구", "AreaGround", 1, 3);
        Card cardB = CreateTestCard("방패막기", "Self", 0, 0);

        CardUI cardAUI = CreateTestCardUI(contentGo.transform, "CardAUI");
        CardUI cardBUI = CreateTestCardUI(contentGo.transform, "CardBUI");
        cardAUI.UpdateCardInstance(cardA);
        cardBUI.UpdateCardInstance(cardB);

        IList activeList = GetPrivateField<IList>(handPanel, "activeCardUIs");
        IDictionary entryMap = GetPrivateField<IDictionary>(handPanel, "_activeEntryMap");

        object entryA = CreateHandCardEntry(cardA, cardAUI.gameObject, cardAUI);
        object entryB = CreateHandCardEntry(cardB, cardBUI.gameObject, cardBUI);
        activeList.Add(entryA);
        activeList.Add(entryB);
        entryMap[cardA] = entryA;
        entryMap[cardB] = entryB;

        handPanel.RefreshHand(new List<Card> { cardA, cardB });

        float normalY = cardBUI.CachedRectTransform.anchoredPosition.y;
        Assert.AreEqual(normalY, cardAUI.CachedRectTransform.anchoredPosition.y, 0.01f);
        Assert.IsTrue(cardAUI.IsInteractable);
        Assert.IsTrue(cardBUI.IsInteractable);

        float slideDownOffset = GetPrivateField<float>(handPanel, "targetingSlideDownOffset");
        float selectedExtraOffset = GetPrivateField<float>(handPanel, "targetingSelectedCardExtraOffset");

        // 타깃팅 진입: 나머지 카드는 slideDownOffset만큼 내려가고, 선택된 카드는 extraOffset만큼 더 수납되며 상호작용 비활성화
        handPanel.SetTargetingState(cardA);

        Assert.IsTrue(handPanel.IsTargetingMode);
        Assert.IsFalse(cardAUI.IsInteractable);
        Assert.IsFalse(cardBUI.IsInteractable);
        Assert.AreEqual(normalY - slideDownOffset, cardBUI.CachedRectTransform.anchoredPosition.y, 0.01f);
        Assert.AreEqual(normalY - (slideDownOffset + selectedExtraOffset), cardAUI.CachedRectTransform.anchoredPosition.y, 0.01f);

        // 타깃팅 해제: 원래 Y 위치 및 상호작용 가능 상태로 복귀
        handPanel.SetTargetingState(null);

        Assert.IsFalse(handPanel.IsTargetingMode);
        Assert.IsTrue(cardAUI.IsInteractable);
        Assert.IsTrue(cardBUI.IsInteractable);
        Assert.AreEqual(normalY, cardAUI.CachedRectTransform.anchoredPosition.y, 0.01f);
        Assert.AreEqual(normalY, cardBUI.CachedRectTransform.anchoredPosition.y, 0.01f);
    }
}
