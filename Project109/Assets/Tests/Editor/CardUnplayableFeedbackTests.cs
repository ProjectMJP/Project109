using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using CardTypes;
using EventStructs;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XLua;

[TestFixture]
public class CardUnplayableFeedbackTests
{
    private sealed class TryUseCardBlocker : IOnTryUseCard
    {
        public bool ShouldCancel { get; set; }

        public void OnTryUseCard(CardInfo info)
        {
            if (ShouldCancel)
            {
                info.AddFlag(CardFlag.Cancel);
            }
        }
    }

    private sealed class StubTurnController : ICharacterController
    {
        public Character controlledCharacter { get; }

        public StubTurnController(Character character)
        {
            controlledCharacter = character;
        }

        public void OnTurnStart() { }
        public void OnTurnEnd() { }
    }

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<Card> _createdCards = new List<Card>();
    private readonly List<PlayerBattleController> _createdControllers = new List<PlayerBattleController>();
    private readonly List<Player> _createdPlayers = new List<Player>();
    private readonly List<BattleManager> _createdBattleManagers = new List<BattleManager>();

    [TearDown]
    public void TearDown()
    {
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

        // ActionQueueManager 싱글턴 잔여 인스턴스 정리
        ActionQueueManager queueManager = UnityEngine.Object.FindFirstObjectByType<ActionQueueManager>();
        if (queueManager != null)
        {
            queueManager.ClearQueue();
            UnityEngine.Object.DestroyImmediate(queueManager.gameObject);
        }
        SetStaticField<ActionQueueManager>("_instance", null);

        SetStaticAutoProperty<RunManager>("instance", null);
        SetStaticAutoProperty<GameSceneManager>("instance", null);
        SetStaticAutoProperty<UIManager>("instance", null);

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

    private Character CreateCharacter(string name, float maxStamina = 10f, float curStamina = 10f)
    {
        GameObject go = Track(new GameObject(name));
        Character character = go.AddComponent<Character>();
        InvokePrivateMethod(character, "Awake");
        character.faction = CharacterFaction.Player;
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

    private Card CreateCard(string name, int staminaCost, string targetType = "Self", Character owner = null)
    {
        CardData data = new CardData
        {
            cardName = name,
            displayName = name,
            stamina = staminaCost,
            targetType = targetType,
            targetMinDistance = targetType == "Self" ? 0 : 1,
            targetMaxDistance = targetType == "Self" ? 0 : 1
        };

        Card card = new Card(data, owner, null, -(_createdCards.Count + 1));
        _createdCards.Add(card);
        return card;
    }

    private (RunManager runManager, BattleManager battleManager) CreateTestRunWithBattleManager()
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

        return (runManager, battleManager);
    }

    private Canvas CreateTestCanvas(string name)
    {
        GameObject canvasGo = Track(new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        RectTransform rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1920f, 1080f);
        return canvas;
    }

    private BattleHandPanel CreateTestHandPanel(Transform parent)
    {
        GameObject panelGo = Track(new GameObject("BattleHandPanel", typeof(RectTransform)));
        panelGo.transform.SetParent(parent, false);
        RectTransform rect = panelGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return panelGo.AddComponent<BattleHandPanel>();
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"메서드 '{methodName}'를 {target.GetType().Name}에서 찾을 수 없습니다.");
        method.Invoke(target, null);
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ScrubMonoStack()
    {
        Span<byte> scrub = stackalloc byte[256];
        scrub.Clear();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RunLuaConditionNotMetScenario(LuaEnv luaEnv, Character caster)
    {
        LuaTable luaTable = null;
        Card luaCard = null;
        try
        {
            object[] results = luaEnv.DoString(@"
                return {
                    allowPlay = false,
                    CanPlay = function(self, card, caster)
                        return self.allowPlay
                    end
                }
            ");

            luaTable = results[0] as LuaTable;
            Assert.IsNotNull(luaTable);

            CardData data = new CardData
            {
                cardName = "LuaConditionCard",
                stamina = 1,
                targetType = "Self"
            };
            luaCard = new Card(data, caster, luaTable, -999);

            // 1. Lua CanPlay == false -> ConditionNotMet
            bool playable = luaCard.TryCheckPlayable(caster, out CardPlayFailReason reason, out string msg);
            Assert.IsFalse(playable);
            Assert.AreEqual(CardPlayFailReason.ConditionNotMet, reason);
            Assert.AreEqual("카드 사용 조건을 충족하지 못했습니다.", msg);
            Assert.IsFalse(luaCard.CanPlay(caster));

            // 2. Lua CanPlay == true -> None
            luaTable.Set("allowPlay", true);
            playable = luaCard.TryCheckPlayable(caster, out reason, out msg);
            Assert.IsTrue(playable);
            Assert.AreEqual(CardPlayFailReason.None, reason);
            Assert.AreEqual(string.Empty, msg);
            Assert.IsTrue(luaCard.CanPlay(caster));
        }
        finally
        {
            if (luaCard != null && !luaCard.IsDisposed)
            {
                luaCard.Dispose();
            }
            if (luaTable != null)
            {
                luaTable.Dispose();
            }
        }
    }

    [Test]
    public void Card_TryCheckPlayable_ReturnsExpectedFailReasonsAndMessages()
    {
        Character caster = CreateCharacter("Caster", maxStamina: 10f, curStamina: 1f);
        Card costThreeCard = CreateCard("강타", staminaCost: 3, targetType: "Self", owner: caster);

        // 1. 시전자 null -> Unknown
        Assert.IsFalse(costThreeCard.TryCheckPlayable(null, out CardPlayFailReason reason, out string msg));
        Assert.AreEqual(CardPlayFailReason.Unknown, reason);
        Assert.AreEqual("카드를 사용할 수 없습니다.", msg);

        // 2. 스태미나 부족 -> InsufficientStamina
        Assert.IsFalse(costThreeCard.TryCheckPlayable(caster, out reason, out msg));
        Assert.AreEqual(CardPlayFailReason.InsufficientStamina, reason);
        Assert.AreEqual("스태미나가 부족합니다.", msg);
        Assert.IsFalse(costThreeCard.CanPlay(caster));
        Assert.IsFalse(costThreeCard.CanPlayWithStamina(caster, 2f));

        // 3. 스태미나 충분 + IOnTryUseCard 차단 -> BlockedByEffect
        caster.curStamina = 5f;
        TryUseCardBlocker blocker = new TryUseCardBlocker { ShouldCancel = true };
        caster.eventBus.Add<IOnTryUseCard>(blocker);
        try
        {
            Assert.IsFalse(costThreeCard.TryCheckPlayable(caster, out reason, out msg));
            Assert.AreEqual(CardPlayFailReason.BlockedByEffect, reason);
            Assert.AreEqual("상태이상 효과로 카드를 사용할 수 없습니다.", msg);
            Assert.IsFalse(costThreeCard.CanPlay(caster));
        }
        finally
        {
            caster.eventBus.Remove<IOnTryUseCard>(blocker);
        }

        // 4. 정상 통과 -> None
        Assert.IsTrue(costThreeCard.TryCheckPlayable(caster, out reason, out msg));
        Assert.AreEqual(CardPlayFailReason.None, reason);
        Assert.AreEqual(string.Empty, msg);
        Assert.IsTrue(costThreeCard.CanPlay(caster));
        Assert.IsTrue(costThreeCard.CanPlayWithStamina(caster, 3f));

        // 5. 폐기된 카드 -> Unknown
        costThreeCard.Dispose();
        Assert.IsFalse(costThreeCard.TryCheckPlayable(caster, out reason, out msg));
        Assert.AreEqual(CardPlayFailReason.Unknown, reason);
        Assert.AreEqual("카드를 사용할 수 없습니다.", msg);
    }

    [Test]
    public void Card_TryCheckPlayable_WhenLuaCanPlayReturnsFalse_ReturnsConditionNotMet()
    {
        Character caster = CreateCharacter("LuaCaster", maxStamina: 10f, curStamina: 10f);
        LuaEnv luaEnv = new LuaEnv();
        try
        {
            RunLuaConditionNotMetScenario(luaEnv, caster);
        }
        finally
        {
            if (caster != null)
            {
                UnityEngine.Object.DestroyImmediate(caster.gameObject);
                caster = null;
            }
            ScrubMonoStack();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            luaEnv.Tick();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            luaEnv.Tick();
            luaEnv.Dispose();
        }
    }

    [Test]
    public void PlayerBattleController_TryUseCard_EmitsOnCardPlayRejectedForEachFailureReason()
    {
        Character playerChar = CreateCharacter("PlayerChar", maxStamina: 10f, curStamina: 5f);
        Character enemyChar = CreateCharacter("EnemyChar", maxStamina: 10f, curStamina: 5f);
        enemyChar.faction = CharacterFaction.Enemy;

        Player player = new Player(playerChar);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        PlayerBattleController controller = new PlayerBattleController(player);
        _createdControllers.Add(controller);

        Card selfCard = CreateCard("방어", staminaCost: 2, targetType: "Self", owner: playerChar);
        Card enemyCard = CreateCard("검격", staminaCost: 1, targetType: "SingleEnemy", owner: playerChar);

        List<(Card card, CardPlayFailReason reason, string message)> rejections = new List<(Card, CardPlayFailReason, string)>();
        controller.OnCardPlayRejected += (c, r, m) => rejections.Add((c, r, m));

        var (_, battleManager) = CreateTestRunWithBattleManager();
        StubTurnController enemyTurnController = new StubTurnController(enemyChar);

        // 1. 플레이어 턴이 아님 -> NotPlayerTurn
        PropertyInfo turnProp = typeof(BattleManager).GetProperty("currentTurnController", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(turnProp);
        turnProp.SetValue(battleManager, enemyTurnController);

        controller.TryUseCard(selfCard);
        Assert.AreEqual(1, rejections.Count);
        Assert.AreSame(selfCard, rejections[0].card);
        Assert.AreEqual(CardPlayFailReason.NotPlayerTurn, rejections[0].reason);
        Assert.AreEqual("현재 플레이어의 턴이 아닙니다.", rejections[0].message);

        // 현재 턴을 플레이어 컨트롤러로 전환
        turnProp.SetValue(battleManager, controller);

        // 2-a. 캐릭터 행동 중 (currentState != Idle) -> CharacterBusy
        playerChar.currentState = CharacterState.Attack;
        controller.TryUseCard(selfCard);
        Assert.AreEqual(2, rejections.Count);
        Assert.AreEqual(CardPlayFailReason.CharacterBusy, rejections[1].reason);
        Assert.AreEqual("현재 행동 중입니다.", rejections[1].message);
        playerChar.currentState = CharacterState.Idle;

        // 2-b. 액션 큐 실행 중 (ActionQueueManager.Instance.IsBusy) -> CharacterBusy
        SetPrivateField(ActionQueueManager.Instance, "_isProcessing", true);
        controller.TryUseCard(selfCard);
        Assert.AreEqual(3, rejections.Count);
        Assert.AreEqual(CardPlayFailReason.CharacterBusy, rejections[2].reason);
        Assert.AreEqual("현재 행동 중입니다.", rejections[2].message);
        SetPrivateField(ActionQueueManager.Instance, "_isProcessing", false);

        // 3. 스태미나 부족 -> InsufficientStamina
        playerChar.curStamina = 0f;
        controller.TryUseCard(selfCard);
        Assert.AreEqual(4, rejections.Count);
        Assert.AreEqual(CardPlayFailReason.InsufficientStamina, rejections[3].reason);
        Assert.AreEqual("스태미나가 부족합니다.", rejections[3].message);

        // 4. 사거리 내 유효 대상 없음 -> NoValidTargets
        playerChar.curStamina = 10f;
        controller.TryUseCard(enemyCard);
        Assert.AreEqual(5, rejections.Count);
        Assert.AreSame(enemyCard, rejections[4].card);
        Assert.AreEqual(CardPlayFailReason.NoValidTargets, rejections[4].reason);
        Assert.AreEqual("사거리 내에 유효한 대상이 없습니다.", rejections[4].message);

        // 5. Dispose 이후 이벤트 구독자 정리 검증
        controller.Dispose();
        FieldInfo rejectedEventField = typeof(PlayerBattleController).GetField("OnCardPlayRejected", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(rejectedEventField);
        Assert.IsNull(rejectedEventField.GetValue(controller));
    }

    [Test]
    public void BattleHandPanel_BindBattleControllerAndShowUnplayableFeedback_UpdatesReusesAndUnbindsSafely()
    {
        Canvas canvas = CreateTestCanvas("FeedbackCanvas");
        BattleHandPanel handPanel = CreateTestHandPanel(canvas.transform);

        Character playerChar = CreateCharacter("FeedbackPlayerChar", maxStamina: 10f, curStamina: 0f);
        Player player = new Player(playerChar);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        PlayerBattleController controller = new PlayerBattleController(player);
        _createdControllers.Add(controller);

        Card expensiveCard = CreateCard("대지진", staminaCost: 3, targetType: "Self", owner: playerChar);

        handPanel.BindBattleController(controller);

        // 1. 이벤트 수신 시 플로팅 피드백 UI 자동 생성·활성화·텍스트 갱신 검증
        controller.TryUseCard(expensiveCard);

        RectTransform feedbackRect = GetPrivateField<RectTransform>(handPanel, "unplayableFeedbackRect");
        TextMeshProUGUI feedbackText = GetPrivateField<TextMeshProUGUI>(handPanel, "unplayableFeedbackText");
        CanvasGroup feedbackGroup = GetPrivateField<CanvasGroup>(handPanel, "unplayableFeedbackCanvasGroup");

        Assert.IsNotNull(feedbackRect);
        Assert.IsNotNull(feedbackText);
        Assert.IsNotNull(feedbackGroup);
        Assert.IsTrue(feedbackRect.gameObject.activeSelf);
        Assert.AreEqual("스태미나가 부족합니다.", feedbackText.text);
        Assert.AreEqual(1f, feedbackGroup.alpha);
        Assert.IsFalse(feedbackText.raycastTarget);
        Assert.IsFalse(feedbackGroup.blocksRaycasts);

        int childCountAfterFirst = handPanel.transform.childCount;

        // 2. 연속 호출 시 새 UI를 중복 생성하지 않고 동일 배지를 재사용하며 텍스트를 즉시 교체해야 함
        Card enemyCard = CreateCard("검격", staminaCost: 1, targetType: "SingleEnemy", owner: playerChar);
        playerChar.curStamina = 10f;
        controller.TryUseCard(enemyCard);

        Assert.AreEqual(childCountAfterFirst, handPanel.transform.childCount);
        Assert.AreSame(feedbackRect, GetPrivateField<RectTransform>(handPanel, "unplayableFeedbackRect"));
        Assert.IsTrue(feedbackRect.gameObject.activeSelf);
        Assert.AreEqual("사거리 내에 유효한 대상이 없습니다.", feedbackText.text);

        handPanel.ShowUnplayableFeedback(enemyCard, CardPlayFailReason.CharacterBusy, "현재 행동 중입니다.");
        Assert.AreEqual(childCountAfterFirst, handPanel.transform.childCount);
        Assert.IsTrue(feedbackRect.gameObject.activeSelf);
        Assert.AreEqual("현재 행동 중입니다.", feedbackText.text);

        // 3. UnbindBattleController 호출 시 이벤트 수신 중단
        handPanel.UnbindBattleController();
        playerChar.curStamina = 0f;
        controller.TryUseCard(expensiveCard);
        Assert.AreEqual("현재 행동 중입니다.", feedbackText.text);
    }

    [Test]
    public void BattleUIPanel_BindPlayerAndOnEnable_SyncsBattleControllerBindingOnHandPanel()
    {
        Canvas canvas = CreateTestCanvas("BattleUIBindCanvas");

        GameObject battleUIGo = Track(new GameObject("BattleUIPanel", typeof(RectTransform)));
        battleUIGo.transform.SetParent(canvas.transform, false);
        battleUIGo.SetActive(false);

        BattleUIPanel battleUI = battleUIGo.AddComponent<BattleUIPanel>();
        BattleHandPanel handPanel = CreateTestHandPanel(battleUIGo.transform);

        GameObject moveGo = Track(new GameObject("BattleMovePanel", typeof(RectTransform)));
        moveGo.transform.SetParent(battleUIGo.transform, false);
        BattleMovePanel movePanel = moveGo.AddComponent<BattleMovePanel>();

        GameObject btnGo = Track(new GameObject("EndTurnButton", typeof(RectTransform), typeof(Image), typeof(Button)));
        btnGo.transform.SetParent(battleUIGo.transform, false);
        Button endTurnButton = btnGo.GetComponent<Button>();

        SetPrivateField(battleUI, "battleHandPanel", handPanel);
        SetPrivateField(battleUI, "battleMovePanel", movePanel);
        SetPrivateField(battleUI, "endTurnButton", endTurnButton);

        battleUIGo.SetActive(true);
        InvokePrivateMethod(handPanel, "Awake");
        InvokePrivateMethod(movePanel, "Awake");
        InvokePrivateMethod(battleUI, "Awake");
        InvokePrivateMethod(battleUI, "OnEnable");
        InvokePrivateMethod(handPanel, "OnEnable");

        Character playerChar = CreateCharacter("UIBindPlayerChar", maxStamina: 10f, curStamina: 0f);
        Player player = new Player(playerChar);
        player.InitBattleDeck();
        _createdPlayers.Add(player);

        PlayerBattleController controller = new PlayerBattleController(player);
        _createdControllers.Add(controller);

        Card costCard = CreateCard("방어", staminaCost: 2, targetType: "Self", owner: playerChar);

        // 1. BindPlayer로 BattleHandPanel에 컨트롤러가 바인딩되어야 함
        battleUI.BindPlayer(player, controller);
        controller.TryUseCard(costCard);

        TextMeshProUGUI feedbackText = GetPrivateField<TextMeshProUGUI>(handPanel, "unplayableFeedbackText");
        RectTransform feedbackRect = GetPrivateField<RectTransform>(handPanel, "unplayableFeedbackRect");
        Assert.IsNotNull(feedbackText);
        Assert.IsNotNull(feedbackRect);
        Assert.IsTrue(feedbackRect.gameObject.activeSelf);
        Assert.AreEqual("스태미나가 부족합니다.", feedbackText.text);

        // 2. 패널 비활성화 시 구독 해제 및 피드백 숨김, 재활성화 시 OnEnable에서 재바인딩되어야 함
        battleUIGo.SetActive(false);
        InvokePrivateMethod(handPanel, "OnDisable");
        InvokePrivateMethod(battleUI, "OnDisable");
        Assert.IsFalse(feedbackRect.gameObject.activeSelf);

        handPanel.ShowUnplayableFeedback(costCard, CardPlayFailReason.Unknown, "비활성 테스트");
        Assert.AreEqual("비활성 테스트", feedbackText.text);
        feedbackRect.gameObject.SetActive(false);

        // 비활성 상태에서는 컨트롤러 거부 이벤트가 수신되지 않음
        controller.TryUseCard(costCard);
        Assert.IsFalse(feedbackRect.gameObject.activeSelf);

        // 재활성화 후 OnEnable에서 _controller 재바인딩 확인
        battleUIGo.SetActive(true);
        InvokePrivateMethod(battleUI, "OnEnable");
        InvokePrivateMethod(handPanel, "OnEnable");
        controller.TryUseCard(costCard);
        Assert.IsTrue(feedbackRect.gameObject.activeSelf);
        Assert.AreEqual("스태미나가 부족합니다.", feedbackText.text);
    }
}
