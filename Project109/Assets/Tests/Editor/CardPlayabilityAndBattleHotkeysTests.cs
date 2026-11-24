using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CardTypes;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[TestFixture]
public class CardPlayabilityAndBattleHotkeysTests
{
    private bool _hadHotkeyPrefBackup;
    private int _hotkeyPrefBackupValue;
    private float _backupTimeScale;
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

    private sealed class DummyModalPopup : UIPanelBase
    {
        public override bool CanCloseByCancel => true;
        public override bool DestroyOnClose => false;
    }

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<Card> _createdCards = new List<Card>();
    private readonly List<PlayerBattleController> _createdControllers = new List<PlayerBattleController>();
    private readonly List<Player> _createdPlayers = new List<Player>();
    private readonly List<BattleManager> _createdBattleManagers = new List<BattleManager>();

    [SetUp]
    public void SetUp()
    {
        _backupTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        _hadHotkeyPrefBackup = PlayerPrefs.HasKey(HotkeyHintSettings.PREF_SHOW_HOTKEY_HINTS);
        _hotkeyPrefBackupValue = PlayerPrefs.GetInt(HotkeyHintSettings.PREF_SHOW_HOTKEY_HINTS, 1);
        HotkeyHintSettings.SetShowHotkeyHints(true, savePrefs: false);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = _backupTimeScale > 0f ? _backupTimeScale : 1f;

        if (_hadHotkeyPrefBackup)
        {
            PlayerPrefs.SetInt(HotkeyHintSettings.PREF_SHOW_HOTKEY_HINTS, _hotkeyPrefBackupValue);
        }
        else
        {
            PlayerPrefs.DeleteKey(HotkeyHintSettings.PREF_SHOW_HOTKEY_HINTS);
        }
        PlayerPrefs.Save();
        HotkeyHintSettings.SetShowHotkeyHints(true, savePrefs: false);
        for (int i = _createdBattleManagers.Count - 1; i >= 0; i--)
        {
            _createdBattleManagers[i]?.Dispose();
        }
        _createdBattleManagers.Clear();

        for (int i = _createdControllers.Count - 1; i >= 0; i--)
        {
            _createdControllers[i]?.Dispose();
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
            _createdPlayers[i]?.Dispose();
        }
        _createdPlayers.Clear();

        ActionQueueManager queueManager = UnityEngine.Object.FindFirstObjectByType<ActionQueueManager>();
        if (queueManager != null)
        {
            queueManager.ClearQueue();
            UnityEngine.Object.DestroyImmediate(queueManager.gameObject);
        }
        SetStaticField<ActionQueueManager>("_instance", null);

        PlayerInputController inputController = UnityEngine.Object.FindFirstObjectByType<PlayerInputController>();
        if (inputController != null)
        {
            UnityEngine.Object.DestroyImmediate(inputController.gameObject);
        }
        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);

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
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private Character CreateCharacter(string name, CharacterFaction faction, float maxStamina = 10f, float curStamina = 10f)
    {
        GameObject go = Track(new GameObject(name));
        Character character = go.AddComponent<Character>();
        InvokePrivateMethod(character, "Awake");
        character.faction = faction;
        character.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = maxStamina,
            staminaRegenPerSecond = 0f,
            moveRange = 3
        });
        character.curStamina = curStamina;
        character.currentState = CharacterState.Idle;
        return character;
    }

    private Tile CreateTile(string name, int x, int y)
    {
        GameObject tileGo = Track(new GameObject(name));
        tileGo.transform.position = new Vector3(x, 0f, y);
        Tile tile = tileGo.AddComponent<Tile>();
        tile.SetCoord(x, y);
        return tile;
    }

    private Card CreateCard(string name, int staminaCost, string targetType, int minDistance = 0, int maxDistance = 0, Character owner = null)
    {
        CardData data = new CardData
        {
            cardName = name,
            displayName = name,
            stamina = staminaCost,
            targetType = targetType,
            targetMinDistance = minDistance,
            targetMaxDistance = maxDistance
        };

        Card card = new Card(data, owner, null, -(_createdCards.Count + 1));
        _createdCards.Add(card);
        return card;
    }

    private (RunManager runManager, BattleManager battleManager) CreateRunWithBattle(PlayerBattleController playerController, Character enemyChar)
    {
        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        SetStaticAutoProperty<RunManager>("instance", runManager);

        BattleManager battleManager = new BattleManager();
        battleManager.Initialize();
        _createdBattleManagers.Add(battleManager);

        PropertyInfo bmProp = typeof(RunManager).GetProperty("battleManager", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(bmProp);
        bmProp.SetValue(runManager, battleManager);

        List<ICharacterController> players = new List<ICharacterController> { playerController };
        List<ICharacterController> enemies = enemyChar != null
            ? new List<ICharacterController> { new StubEnemyController(enemyChar) }
            : new List<ICharacterController>();

        SetPrivateField(battleManager, "playerTeam", players);
        SetPrivateField(battleManager, "enemyTeam", enemies);
        SetPrivateAutoProperty(battleManager, "battleState", BattleState.TurnInProgress);
        SetPrivateAutoProperty(battleManager, "currentTurnController", playerController);

        return (runManager, battleManager);
    }

    private (Canvas canvas, RectTransform contentRect) CreateTestCanvas(string name)
    {
        GameObject esGo = Track(new GameObject(name + "_EventSystem", typeof(EventSystem)));
        GameObject canvasGo = Track(new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        RectTransform rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1920f, 1080f);

        GameObject contentGo = Track(new GameObject("HandContent", typeof(RectTransform)));
        contentGo.transform.SetParent(canvasGo.transform, false);
        RectTransform contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(1000f, 220f);
        return (canvas, contentRect);
    }

    private CardUI CreateTestCardUI(Transform parent, string name)
    {
        GameObject cardGo = Track(new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image)));
        cardGo.transform.SetParent(parent, false);

        GameObject highlightGo = Track(new GameObject("SelectHighlight", typeof(RectTransform), typeof(Image)));
        highlightGo.transform.SetParent(cardGo.transform, false);

        GameObject staminaGo = Track(new GameObject("UseStamina", typeof(RectTransform), typeof(TextMeshProUGUI)));
        staminaGo.transform.SetParent(cardGo.transform, false);
        TextMeshProUGUI staminaText = staminaGo.GetComponent<TextMeshProUGUI>();
        staminaText.color = Color.white;

        CardUI cardUI = cardGo.AddComponent<CardUI>();
        cardUI.selectHighlightObject = highlightGo;
        cardUI.useStamina = staminaText;
        InvokePrivateMethod(cardUI, "Awake");
        return cardUI;
    }

    private void RegisterHandCardsInPanel(BattleHandPanel handPanel, Transform contentTransform, List<(Card card, CardUI cardUI)> items)
    {
        IList activeList = GetPrivateField<IList>(handPanel, "activeCardUIs");
        IDictionary entryMap = GetPrivateField<IDictionary>(handPanel, "_activeEntryMap");
        Type entryType = typeof(BattleHandPanel).GetNestedType("HandCardEntry", BindingFlags.NonPublic);
        Assert.IsNotNull(entryType);

        activeList.Clear();
        entryMap.Clear();

        List<Card> handCards = new List<Card>();
        for (int i = 0; i < items.Count; i++)
        {
            Card card = items[i].card;
            CardUI cardUI = items[i].cardUI;
            cardUI.transform.SetParent(contentTransform, false);
            cardUI.UpdateCardInstance(card);

            object entry = Activator.CreateInstance(entryType);
            entryType.GetField("card", BindingFlags.Instance | BindingFlags.Public).SetValue(entry, card);
            entryType.GetField("cardObj", BindingFlags.Instance | BindingFlags.Public).SetValue(entry, cardUI.gameObject);
            entryType.GetField("cardUI", BindingFlags.Instance | BindingFlags.Public).SetValue(entry, cardUI);
            entryType.GetField("rectTransform", BindingFlags.Instance | BindingFlags.Public).SetValue(entry, cardUI.CachedRectTransform);

            activeList.Add(entry);
            entryMap[card] = entry;
            handCards.Add(card);
        }

        handPanel.RefreshHand(handCards);
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

    private static void SetPrivateAutoProperty(object target, string propName, object value)
    {
        PropertyInfo prop = target.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(prop, $"프로퍼티 '{propName}'을(를) {target.GetType().Name}에서 찾을 수 없습니다.");
        prop.SetValue(target, value);
    }

    private static void SetStaticField<T>(string fieldName, object value)
    {
        FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        field?.SetValue(null, value);
    }

    private static void SetStaticAutoProperty<T>(string propName, object value)
    {
        PropertyInfo prop = typeof(T).GetProperty(propName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        prop?.SetValue(null, value);
    }

    [Test]
    public void CardPlayabilityVisual_TransitionsAcrossPlayableInsufficientStaminaAndNoTargets()
    {
        var (_, contentRect) = CreateTestCanvas("PlayabilityVisualCanvas");
        CardUI cardUI = CreateTestCardUI(contentRect, "TestCard");
        Color defaultCostColor = cardUI.useStamina.color;

        // 1. 사용 가능 상태 -> 글로우 활성, 딤 비활성, 기본 코스트 색상
        cardUI.SetPlayabilityVisualState(CardPlayabilityVisualState.Playable, isCostInsufficient: false);
        Assert.AreEqual(CardPlayabilityVisualState.Playable, cardUI.CurrentPlayabilityVisualState);
        Assert.IsTrue(cardUI.IsPlayableGlowActive);
        Assert.IsFalse(cardUI.IsDimOverlayActive);
        Assert.AreEqual(defaultCostColor, cardUI.useStamina.color);

        // 2. 스태미나 부족 상태 -> 글로우 비활성, 딤 활성, 빨간 코스트 색상
        cardUI.SetPlayabilityVisualState(CardPlayabilityVisualState.InsufficientStamina, isCostInsufficient: true);
        Assert.AreEqual(CardPlayabilityVisualState.InsufficientStamina, cardUI.CurrentPlayabilityVisualState);
        Assert.IsFalse(cardUI.IsPlayableGlowActive);
        Assert.IsTrue(cardUI.IsDimOverlayActive);
        Assert.AreNotEqual(defaultCostColor, cardUI.useStamina.color);

        // 3. 조건/대상 부족 상태 (코스트 충분) -> 글로우 비활성, 딤 활성, 기본 코스트 색상 유지
        cardUI.SetPlayabilityVisualState(CardPlayabilityVisualState.UnplayableCondition, isCostInsufficient: false);
        Assert.AreEqual(CardPlayabilityVisualState.UnplayableCondition, cardUI.CurrentPlayabilityVisualState);
        Assert.IsFalse(cardUI.IsPlayableGlowActive);
        Assert.IsTrue(cardUI.IsDimOverlayActive);
        Assert.AreEqual(defaultCostColor, cardUI.useStamina.color);

        // 4. 풀 반환 리셋 시 기본 상태 및 단축키 힌트 초기화
        cardUI.SetHotkeyHint("1");
        Assert.AreEqual("1", cardUI.CurrentHotkeyHintText);
        cardUI.ResetForPool();
        Assert.AreEqual(CardPlayabilityVisualState.Default, cardUI.CurrentPlayabilityVisualState);
        Assert.IsFalse(cardUI.IsPlayableGlowActive);
        Assert.IsFalse(cardUI.IsDimOverlayActive);
        Assert.AreEqual(string.Empty, cardUI.CurrentHotkeyHintText);
    }

    [Test]
    public void BattleHandPanel_EventDrivenPlayability_UpdatesOnStaminaMoveAndBattlefieldEvents()
    {
        var (canvas, contentRect) = CreateTestCanvas("EventPlayabilityCanvas");

        GameObject panelGo = Track(new GameObject("BattleHandPanel", typeof(RectTransform)));
        panelGo.transform.SetParent(canvas.transform, false);
        BattleHandPanel handPanel = panelGo.AddComponent<BattleHandPanel>();
        handPanel.contentTransform = contentRect;
        InvokePrivateMethod(handPanel, "Awake");

        Character playerChar = CreateCharacter("PlayerChar", CharacterFaction.Player, maxStamina: 10f, curStamina: 1f);
        Character enemyChar = CreateCharacter("EnemyChar", CharacterFaction.Enemy, maxStamina: 10f, curStamina: 0f);

        Tile playerTile = CreateTile("PlayerTile", 0, 0);
        Tile nearEnemyTile = CreateTile("NearEnemyTile", 1, 0);
        Tile farEnemyTile = CreateTile("FarEnemyTile", 4, 0);

        playerChar.characterMove.SetCurrentTile(playerTile);
        enemyChar.characterMove.SetCurrentTile(farEnemyTile);

        Player player = new Player(playerChar);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        PlayerBattleController controller = new PlayerBattleController(player);
        _createdControllers.Add(controller);

        var (_, battleManager) = CreateRunWithBattle(controller, enemyChar);

        Card strikeCard = CreateCard("검격", staminaCost: 2, targetType: "SingleEnemy", minDistance: 1, maxDistance: 1, owner: playerChar);
        Card defendCard = CreateCard("방어", staminaCost: 1, targetType: "Self", minDistance: 0, maxDistance: 0, owner: playerChar);

        CardUI strikeUI = CreateTestCardUI(contentRect, "StrikeUI");
        CardUI defendUI = CreateTestCardUI(contentRect, "DefendUI");

        handPanel.BindBattleDeck(player.battleDeck);
        handPanel.BindBattleController(controller);
        RegisterHandCardsInPanel(handPanel, contentRect, new List<(Card, CardUI)>
        {
            (strikeCard, strikeUI),
            (defendCard, defendUI)
        });

        // 초기 상태: 스태미나 1, 적 거리 4 -> 방어(코스트 1, Self)는 Playable, 검격(코스트 2, 거리 1)은 InsufficientStamina
        Assert.AreEqual(CardPlayabilityVisualState.InsufficientStamina, strikeUI.CurrentPlayabilityVisualState);
        Assert.AreEqual(CardPlayabilityVisualState.Playable, defendUI.CurrentPlayabilityVisualState);
        Assert.AreEqual("1", strikeUI.CurrentHotkeyHintText);
        Assert.AreEqual("2", defendUI.CurrentHotkeyHintText);

        // 1. 스태미나 상승(1 -> 3) 이벤트 발생: 검격은 여전히 적 사거리 밖이므로 UnplayableCondition(코스트는 흰색)
        playerChar.curStamina = 3f;
        Assert.AreEqual(CardPlayabilityVisualState.UnplayableCondition, strikeUI.CurrentPlayabilityVisualState);
        Assert.IsTrue(strikeUI.IsDimOverlayActive);

        // 2. 적 위치가 사거리 내(거리 1)로 이동 -> 전장 상태 변화 이벤트로 검격 즉시 Playable 전환
        enemyChar.characterMove.SetCurrentTile(nearEnemyTile);
        InvokePrivateMethod(battleManager, "HandleCombatantTileChanged", enemyChar, farEnemyTile, nearEnemyTile);
        Assert.AreEqual(CardPlayabilityVisualState.Playable, strikeUI.CurrentPlayabilityVisualState);
        Assert.IsTrue(strikeUI.IsPlayableGlowActive);
        Assert.IsFalse(strikeUI.IsDimOverlayActive);

        // 3. 플레이어 턴 종료 -> 턴 비활성 시 모든 손패 사용 불가 상태 반영
        SetPrivateAutoProperty(battleManager, "currentTurnController", new StubEnemyController(enemyChar));
        InvokePrivateMethod(battleManager, "HandleCombatantTileChanged", enemyChar, nearEnemyTile, nearEnemyTile);
        Assert.AreEqual(CardPlayabilityVisualState.UnplayableCondition, defendUI.CurrentPlayabilityVisualState);
    }

    [Test]
    public void BattleHotkeys_NumberKeys_SelectThenConfirmImmediateCard_AndTargetManualCard()
    {
        var (canvas, contentRect) = CreateTestCanvas("HotkeyCanvas");

        GameObject battleUIGo = Track(new GameObject("BattleUIPanel", typeof(RectTransform)));
        battleUIGo.transform.SetParent(canvas.transform, false);
        BattleUIPanel battleUI = battleUIGo.AddComponent<BattleUIPanel>();

        GameObject handGo = Track(new GameObject("BattleHandPanel", typeof(RectTransform)));
        handGo.transform.SetParent(battleUIGo.transform, false);
        BattleHandPanel handPanel = handGo.AddComponent<BattleHandPanel>();
        handPanel.contentTransform = contentRect;

        GameObject moveGo = Track(new GameObject("BattleMovePanel", typeof(RectTransform)));
        moveGo.transform.SetParent(battleUIGo.transform, false);
        BattleMovePanel movePanel = moveGo.AddComponent<BattleMovePanel>();

        GameObject endTurnGo = Track(new GameObject("EndTurnButton", typeof(RectTransform), typeof(Image), typeof(Button)));
        endTurnGo.transform.SetParent(battleUIGo.transform, false);
        GameObject endTurnTextGo = Track(new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)));
        endTurnTextGo.transform.SetParent(endTurnGo.transform, false);
        endTurnTextGo.GetComponent<TextMeshProUGUI>().text = "턴 종료";

        SetPrivateField(battleUI, "battleHandPanel", handPanel);
        SetPrivateField(battleUI, "battleMovePanel", movePanel);
        SetPrivateField(battleUI, "endTurnButton", endTurnGo.GetComponent<Button>());

        InvokePrivateMethod(handPanel, "Awake");
        InvokePrivateMethod(movePanel, "Awake");
        InvokePrivateMethod(battleUI, "Awake");
        InvokePrivateMethod(battleUI, "OnEnable");
        InvokePrivateMethod(handPanel, "OnEnable");

        Assert.AreEqual("턴 종료", endTurnTextGo.GetComponent<TextMeshProUGUI>().text);
        Assert.AreEqual("E", battleUI.CurrentEndTurnHotkeyHintText);

        Character playerChar = CreateCharacter("HotkeyPlayer", CharacterFaction.Player, maxStamina: 10f, curStamina: 5f);
        Character enemyChar = CreateCharacter("HotkeyEnemy", CharacterFaction.Enemy, maxStamina: 10f, curStamina: 0f);

        Tile playerTile = CreateTile("P_Tile", 0, 0);
        Tile enemyTile = CreateTile("E_Tile", 1, 0);
        playerChar.characterMove.SetCurrentTile(playerTile);
        enemyChar.characterMove.SetCurrentTile(enemyTile);

        Player player = new Player(playerChar);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        PlayerBattleController controller = new PlayerBattleController(player);
        _createdControllers.Add(controller);
        SetPrivateField(controller, "_battleUI", battleUI);

        var (_, battleManager) = CreateRunWithBattle(controller, enemyChar);
        battleUI.BindPlayer(player, controller);

        Card selfCard = CreateCard("방어", staminaCost: 1, targetType: "Self", minDistance: 0, maxDistance: 0, owner: playerChar);
        Card enemyCard = CreateCard("검격", staminaCost: 1, targetType: "SingleEnemy", minDistance: 1, maxDistance: 1, owner: playerChar);

        player.battleDeck.hand.Add(selfCard);
        player.battleDeck.hand.Add(enemyCard);

        CardUI selfUI = CreateTestCardUI(contentRect, "SelfCardUI");
        CardUI enemyUI = CreateTestCardUI(contentRect, "EnemyCardUI");
        RegisterHandCardsInPanel(handPanel, contentRect, new List<(Card, CardUI)>
        {
            (selfCard, selfUI),
            (enemyCard, enemyUI)
        });

        // 1. 즉시 시전형 카드(인덱스 0, 키 '1') 1차 입력 -> 선택만 되고 즉시 시전되지 않음
        controller.HandleCardHotkey(0);
        Assert.AreSame(selfCard, controller.KeyboardSelectedCard);
        Assert.IsTrue(selfUI.IsKeyboardSelected);
        Assert.IsTrue(controller.HasActiveBattleCancelTarget);
        Assert.IsFalse(ActionQueueManager.Instance.IsBusy);

        // ESC(취소)로 키보드 선택 해제 확인
        controller.CancelCurrentState();
        Assert.IsNull(controller.KeyboardSelectedCard);
        Assert.IsFalse(selfUI.IsKeyboardSelected);

        // 2. 다시 1차 입력 후 2차 재입력 -> 액션 큐에 카드 시전 등록
        controller.HandleCardHotkey(0);
        Assert.AreSame(selfCard, controller.KeyboardSelectedCard);
        controller.HandleCardHotkey(0);
        Assert.IsNull(controller.KeyboardSelectedCard);
        Assert.IsTrue(ActionQueueManager.Instance.IsBusy);

        // 액션 큐 정리 후 수동 지정형 카드(인덱스 1, 키 '2') 테스트 -> 1회 입력 시 즉시 타깃팅 모드 진입
        ActionQueueManager.Instance.ClearQueue(invokeCanceled: true);
        controller.HandleCardHotkey(1);
        Assert.AreEqual(PlayerControlState.TargetSelection, controller.controlState);
        Assert.AreSame(enemyCard, controller.ActiveTargetingCard);

        // 타깃팅 중 이동 모드 토글 및 턴 종료 단축키는 무시되어야 함
        controller.HandleToggleMoveModeHotkey();
        Assert.AreEqual(PlayerControlState.TargetSelection, controller.controlState);
        controller.HandleEndTurnHotkey();
        Assert.AreSame(controller, battleManager.currentTurnController);

        // 타깃팅 취소 후 이동 모드 토글 단축키 검증
        controller.CancelCurrentState();
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        controller.HandleToggleMoveModeHotkey();
        Assert.AreEqual(PlayerControlState.Move, controller.controlState);
        controller.HandleToggleMoveModeHotkey();
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);

        // 턴 종료 단축키 검증
        controller.HandleEndTurnHotkey();
        Assert.IsNull(battleManager.currentTurnController);
    }

    [Test]
    public void BattleHotkeys_BlockedWhenModalPopupOrWorldInputBlocked()
    {
        Character playerChar = CreateCharacter("BlockedPlayer", CharacterFaction.Player, maxStamina: 10f, curStamina: 5f);
        Player player = new Player(playerChar);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        PlayerBattleController controller = new PlayerBattleController(player);
        _createdControllers.Add(controller);
        CreateRunWithBattle(controller, null);

        Card selfCard = CreateCard("방어", staminaCost: 1, targetType: "Self", owner: playerChar);
        player.battleDeck.hand.Add(selfCard);

        // 1. PlayerInputController.IsWorldInputBlocked == true 시 단축키 전파 차단
        GameObject inputGo = Track(new GameObject("TestPlayerInputController"));
        PlayerInputController input = inputGo.AddComponent<PlayerInputController>();
        SetStaticField<PlayerInputController>("_instance", input);
        int receivedCardKey = 0;
        int receivedEndTurn = 0;
        int receivedMoveToggle = 0;
        input.OnCardHotkeyEvent += _ => receivedCardKey++;
        input.OnEndTurnHotkeyEvent += () => receivedEndTurn++;
        input.OnToggleMoveModeHotkeyEvent += () => receivedMoveToggle++;

        input.IsWorldInputBlocked = true;
        Assert.IsFalse(input.ProcessCardHotkey(0));
        Assert.IsFalse(input.ProcessEndTurnHotkey());
        Assert.IsFalse(input.ProcessToggleMoveModeHotkey());
        Assert.AreEqual(0, receivedCardKey);
        Assert.AreEqual(0, receivedEndTurn);
        Assert.AreEqual(0, receivedMoveToggle);

        // 2. UIManager 모달 팝업이 열려 있을 때 컨트롤러 단축키 핸들러 차단
        input.IsWorldInputBlocked = false;
        GameObject uiManagerGo = Track(new GameObject("TestUIManager"));
        UIManager uiManager = uiManagerGo.AddComponent<UIManager>();
        SetStaticAutoProperty<UIManager>("instance", uiManager);

        GameObject popupGo = Track(new GameObject("DummyModalPopup", typeof(RectTransform)));
        popupGo.transform.SetParent(uiManagerGo.transform, false);
        DummyModalPopup modal = popupGo.AddComponent<DummyModalPopup>();
        modal.uiLayerType = UILayerType.Popup;
        modal.blockWorldInput = true;
        uiManager.PushActiveUIPanel(modal);

        Assert.IsTrue(uiManager.IsWorldInputBlocked);
        Assert.IsTrue(uiManager.HasActivePopup);

        controller.HandleCardHotkey(0);
        controller.HandleToggleMoveModeHotkey();
        controller.HandleEndTurnHotkey();

        Assert.IsNull(controller.KeyboardSelectedCard);
        Assert.AreEqual(PlayerControlState.Normal, controller.controlState);
        Assert.IsNotNull(RunManager.instance.battleManager.currentTurnController);
    }

    [Test]
    public void HotkeyHintSettings_UnifiedBadgesAndPauseMenuToggle_SyncsRealtimeAndPersistsToPlayerPrefs()
    {
        var (canvas, contentRect) = CreateTestCanvas("UnifiedHotkeyBadgeCanvas");

        GameObject battleUIGo = Track(new GameObject("BattleUIPanel", typeof(RectTransform)));
        battleUIGo.transform.SetParent(canvas.transform, false);
        BattleUIPanel battleUI = battleUIGo.AddComponent<BattleUIPanel>();

        GameObject handGo = Track(new GameObject("BattleHandPanel", typeof(RectTransform)));
        handGo.transform.SetParent(battleUIGo.transform, false);
        BattleHandPanel handPanel = handGo.AddComponent<BattleHandPanel>();
        handPanel.contentTransform = contentRect;

        GameObject modeSwitchBtnGo = Track(new GameObject("SwitchToMoveButton", typeof(RectTransform), typeof(Image), typeof(Button)));
        modeSwitchBtnGo.transform.SetParent(handGo.transform, false);
        GameObject modeSwitchTextGo = Track(new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI)));
        modeSwitchTextGo.transform.SetParent(modeSwitchBtnGo.transform, false);
        TextMeshProUGUI modeSwitchText = modeSwitchTextGo.GetComponent<TextMeshProUGUI>();
        modeSwitchText.text = "이동 모드";

        SetPrivateField(handPanel, "switchToMoveModeButton", modeSwitchBtnGo.GetComponent<Button>());
        SetPrivateField(handPanel, "modeSwitchButtonText", modeSwitchText);

        GameObject moveGo = Track(new GameObject("BattleMovePanel", typeof(RectTransform)));
        moveGo.transform.SetParent(battleUIGo.transform, false);
        BattleMovePanel movePanel = moveGo.AddComponent<BattleMovePanel>();

        GameObject endTurnGo = Track(new GameObject("EndTurnButton", typeof(RectTransform), typeof(Image), typeof(Button)));
        endTurnGo.transform.SetParent(battleUIGo.transform, false);
        GameObject endTurnTextGo = Track(new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI)));
        endTurnTextGo.transform.SetParent(endTurnGo.transform, false);
        TextMeshProUGUI endTurnText = endTurnTextGo.GetComponent<TextMeshProUGUI>();
        endTurnText.text = "턴 종료";

        SetPrivateField(battleUI, "battleHandPanel", handPanel);
        SetPrivateField(battleUI, "battleMovePanel", movePanel);
        SetPrivateField(battleUI, "endTurnButton", endTurnGo.GetComponent<Button>());

        InvokePrivateMethod(handPanel, "Awake");
        InvokePrivateMethod(movePanel, "Awake");
        InvokePrivateMethod(battleUI, "Awake");
        InvokePrivateMethod(battleUI, "OnEnable");
        InvokePrivateMethod(handPanel, "OnEnable");

        Character playerChar = CreateCharacter("BadgePlayer", CharacterFaction.Player, maxStamina: 10f, curStamina: 5f);
        Character enemyChar = CreateCharacter("BadgeEnemy", CharacterFaction.Enemy, maxStamina: 10f, curStamina: 0f);
        Player player = new Player(playerChar);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        PlayerBattleController controller = new PlayerBattleController(player);
        _createdControllers.Add(controller);
        SetPrivateField(controller, "_battleUI", battleUI);
        CreateRunWithBattle(controller, enemyChar);
        battleUI.BindPlayer(player, controller);

        Card card1 = CreateCard("카드1", staminaCost: 1, targetType: "Self", owner: playerChar);
        Card card2 = CreateCard("카드2", staminaCost: 1, targetType: "Self", owner: playerChar);
        CardUI cardUI1 = CreateTestCardUI(contentRect, "BadgeCardUI1");
        CardUI cardUI2 = CreateTestCardUI(contentRect, "BadgeCardUI2");
        RegisterHandCardsInPanel(handPanel, contentRect, new List<(Card, CardUI)>
        {
            (card1, cardUI1),
            (card2, cardUI2)
        });

        // 1. 초기 상태: 텍스트에 [Q]/[E] 병기 없이 좌상단 배지로 통일 표시
        Assert.IsTrue(HotkeyHintSettings.ShowHotkeyHints);
        Assert.AreEqual("턴 종료", endTurnText.text);
        Assert.AreEqual("E", battleUI.CurrentEndTurnHotkeyHintText);
        Assert.AreEqual("이동 모드", modeSwitchText.text);
        Assert.AreEqual("Q", handPanel.CurrentModeSwitchHotkeyHintText);
        Assert.AreEqual("1", cardUI1.CurrentHotkeyHintText);
        Assert.AreEqual("2", cardUI2.CurrentHotkeyHintText);

        battleUI.SwitchToMoveMode();
        Assert.AreEqual("전투 모드", modeSwitchText.text);
        Assert.AreEqual("Q", handPanel.CurrentModeSwitchHotkeyHintText);
        battleUI.SwitchToHandMode();
        Assert.AreEqual("이동 모드", modeSwitchText.text);

        GameObject endTurnBadge = GetPrivateField<GameObject>(battleUI, "endTurnHotkeyBadgeObject");
        GameObject modeSwitchBadge = GetPrivateField<GameObject>(handPanel, "modeSwitchHotkeyBadgeObject");
        Assert.IsNotNull(endTurnBadge);
        Assert.IsNotNull(modeSwitchBadge);
        Assert.AreEqual(new Vector2(0f, 1f), endTurnBadge.GetComponent<RectTransform>().anchorMin);
        Assert.AreEqual(new Vector2(0f, 1f), endTurnBadge.GetComponent<RectTransform>().anchorMax);
        Assert.AreEqual(new Vector2(0f, 1f), modeSwitchBadge.GetComponent<RectTransform>().anchorMin);
        Assert.AreEqual(new Vector2(0f, 1f), modeSwitchBadge.GetComponent<RectTransform>().anchorMax);

        // 2. PauseMenuPanel 설정창 토글 연동 및 PlayerPrefs 저장/복원 검증
        GameObject pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/PauseMenuPanel.prefab");
        Assert.IsNotNull(pausePrefab, "PauseMenuPanel.prefab을 찾을 수 없습니다.");
        GameObject pauseGo = Track(UnityEngine.Object.Instantiate(pausePrefab, canvas.transform, false));
        PauseMenuPanel pausePanel = pauseGo.GetComponent<PauseMenuPanel>();
        Assert.IsNotNull(pausePanel);
        InvokePrivateMethod(pausePanel, "Awake");
        pausePanel.OnOpen();

        Toggle hotkeyHintToggle = GetPrivateField<Toggle>(pausePanel, "hotkeyHintToggle");
        TextMeshProUGUI hotkeyHintStatusText = GetPrivateField<TextMeshProUGUI>(pausePanel, "hotkeyHintStatusText");
        Assert.IsNotNull(hotkeyHintToggle);
        Assert.IsNotNull(hotkeyHintStatusText);
        Assert.IsTrue(hotkeyHintToggle.isOn);
        Assert.AreEqual("ON", hotkeyHintStatusText.text);

        // OFF 전환 -> 즉시 전투 UI/카드 배지 일괄 숨김 및 PlayerPrefs 저장
        hotkeyHintToggle.isOn = false;
        Assert.IsFalse(HotkeyHintSettings.ShowHotkeyHints);
        Assert.AreEqual(0, PlayerPrefs.GetInt(HotkeyHintSettings.PREF_SHOW_HOTKEY_HINTS, 1));
        Assert.AreEqual("OFF", hotkeyHintStatusText.text);
        Assert.AreEqual(string.Empty, battleUI.CurrentEndTurnHotkeyHintText);
        Assert.AreEqual(string.Empty, handPanel.CurrentModeSwitchHotkeyHintText);
        Assert.AreEqual(string.Empty, cardUI1.CurrentHotkeyHintText);
        Assert.AreEqual(string.Empty, cardUI2.CurrentHotkeyHintText);

        // PlayerPrefs 재로드 시에도 OFF 유지 확인
        HotkeyHintSettings.ReloadFromPrefs();
        Assert.IsFalse(HotkeyHintSettings.ShowHotkeyHints);

        // ON 복원 -> 즉시 전투 UI/카드 배지 일괄 복원
        hotkeyHintToggle.isOn = true;
        Assert.IsTrue(HotkeyHintSettings.ShowHotkeyHints);
        Assert.AreEqual(1, PlayerPrefs.GetInt(HotkeyHintSettings.PREF_SHOW_HOTKEY_HINTS, 0));
        Assert.AreEqual("ON", hotkeyHintStatusText.text);
        Assert.AreEqual("E", battleUI.CurrentEndTurnHotkeyHintText);
        Assert.AreEqual("Q", handPanel.CurrentModeSwitchHotkeyHintText);
        Assert.AreEqual("1", cardUI1.CurrentHotkeyHintText);
        Assert.AreEqual("2", cardUI2.CurrentHotkeyHintText);

        pausePanel.OnClose();
    }
}
