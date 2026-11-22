using System;
using System.Collections.Generic;
using System.Reflection;
using GameItem.Types;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

[TestFixture]
public class PauseMenuTimeFreezeTests
{
    private sealed class StubTurnCountingController : ICharacterController
    {
        public Character controlledCharacter { get; }
        public int TurnStartCount { get; private set; }
        public int TurnEndCount { get; private set; }

        public StubTurnCountingController(Character character)
        {
            controlledCharacter = character;
        }

        public void OnTurnStart()
        {
            TurnStartCount++;
        }

        public void OnTurnEnd()
        {
            TurnEndCount++;
        }
    }

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private Dictionary<string, CardData> _backupCards;
    private Dictionary<string, RelicData> _backupRelics;
    private float _backupTimeScale;

    [SetUp]
    public void SetUp()
    {
        _backupTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        TooltipManager.SetTooltipEnabled(true);

        SetStaticAutoProperty<UIManager>("instance", null);
        SetStaticField<TooltipManager>("instance", null);
        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);

        _backupCards = new Dictionary<string, CardData>(ModLoader.Instance.CardDatabase);
        _backupRelics = new Dictionary<string, RelicData>(ModLoader.Instance.RelicDatabase);
        ModLoader.Instance.CardDatabase.Clear();
        ModLoader.Instance.RelicDatabase.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = _backupTimeScale > 0f ? _backupTimeScale : 1f;
        TooltipManager.SetTooltipEnabled(true);

        SetStaticAutoProperty<UIManager>("instance", null);
        SetStaticField<TooltipManager>("instance", null);
        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();

        ModLoader.Instance.CardDatabase.Clear();
        foreach (var kvp in _backupCards)
        {
            ModLoader.Instance.CardDatabase[kvp.Key] = kvp.Value;
        }

        ModLoader.Instance.RelicDatabase.Clear();
        foreach (var kvp in _backupRelics)
        {
            ModLoader.Instance.RelicDatabase[kvp.Key] = kvp.Value;
        }
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private PauseMenuPanel CreatePauseMenuFromPrefab(Transform parent = null)
    {
        GameObject pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/PauseMenuPanel.prefab");
        Assert.IsNotNull(pausePrefab, "PauseMenuPanel.prefab을 찾을 수 없습니다.");

        GameObject pauseGo = Track(parent != null
            ? UnityEngine.Object.Instantiate(pausePrefab, parent, false)
            : UnityEngine.Object.Instantiate(pausePrefab));
        PauseMenuPanel pausePanel = pauseGo.GetComponent<PauseMenuPanel>();
        Assert.IsNotNull(pausePanel);
        InvokePrivateMethod(pausePanel, "Awake");
        return pausePanel;
    }

    private UIManager CreateTestUIManager()
    {
        GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/UIManager.prefab");
        Assert.IsNotNull(uiPrefab, "UIManager.prefab을 찾을 수 없습니다.");

        GameObject uiGo = Track(UnityEngine.Object.Instantiate(uiPrefab));
        UIManager uiManager = uiGo.GetComponent<UIManager>();
        Assert.IsNotNull(uiManager);
        InvokePrivateMethod(uiManager, "Awake");
        return uiManager;
    }

    private Character CreateCharacter(string name, CharacterFaction faction, float maxStamina, float regenPerSecond)
    {
        GameObject go = Track(new GameObject(name));
        Character character = go.AddComponent<Character>();
        InvokePrivateMethod(character, "Awake");
        character.faction = faction;
        character.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = maxStamina,
            staminaRegenPerSecond = regenPerSecond,
            moveRange = 3,
        });
        return character;
    }

    [Test]
    public void PauseMenuPanel_OnOpenAndOnClose_FreezesAndRestoresTimeScale_IncludingDuplicateOpenAndStandaloneMode()
    {
        SetStaticAutoProperty<UIManager>("instance", null);
        PauseMenuPanel pausePanel = CreatePauseMenuFromPrefab();

        Time.timeScale = 1.5f;
        pausePanel.OnOpen();
        Assert.AreEqual(0f, Time.timeScale, 0.0001f, "OnOpen 직후 Time.timeScale은 0f여야 합니다.");

        // 이미 일시정지된 상태에서 OnOpen이 중복 호출되어도 기존 배율(1.5f)이 0f로 덮어쓰이면 안 됩니다.
        pausePanel.OnOpen();
        Assert.AreEqual(0f, Time.timeScale, 0.0001f);

        pausePanel.OnClose();
        Assert.AreEqual(1.5f, Time.timeScale, 0.0001f, "OnClose 후 진입 전 타임스케일(1.5f)로 복원되어야 합니다.");

        // UIManager가 없는 단독 Open/Close 경로에서도 동일하게 일시정지 및 복원이 동작해야 합니다.
        Time.timeScale = 1.25f;
        pausePanel.Open();
        Assert.IsTrue(pausePanel.gameObject.activeSelf);
        Assert.AreEqual(0f, Time.timeScale, 0.0001f);

        pausePanel.Close();
        Assert.IsFalse(pausePanel.gameObject.activeSelf);
        Assert.AreEqual(1.25f, Time.timeScale, 0.0001f);
    }

    [Test]
    public void PauseMenuPanel_OnDisableAndOnDestroy_RestoreTimeScaleWithoutOnClose()
    {
        PauseMenuPanel pausePanel = CreatePauseMenuFromPrefab();

        // 1. OnClose 없이 OnDisable이 호출되는 비정상 비활성화 경로
        Time.timeScale = 1f;
        pausePanel.OnOpen();
        Assert.AreEqual(0f, Time.timeScale, 0.0001f);

        InvokePrivateMethod(pausePanel, "OnDisable");
        Assert.AreEqual(1f, Time.timeScale, 0.0001f, "OnDisable 경로에서도 타임스케일이 복원되어야 합니다.");

        // 2. OnClose/OnDisable 없이 OnDestroy가 호출되는 씬 전환/파괴 경로
        Time.timeScale = 1.75f;
        pausePanel.OnOpen();
        Assert.AreEqual(0f, Time.timeScale, 0.0001f);

        InvokePrivateMethod(pausePanel, "OnDestroy");
        Assert.AreEqual(1.75f, Time.timeScale, 0.0001f, "OnDestroy 경로에서도 진입 전 타임스케일로 복원되어야 합니다.");
    }

    [Test]
    public void PauseMenuPanel_TopHUDToggleAndEscCancel_SyncsPopupStackInputLockAndTimeScale()
    {
        UIManager uiManager = CreateTestUIManager();

        GameObject inputGo = Track(new GameObject("[TestPlayerInputController]"));
        PlayerInputController inputCtrl = inputGo.AddComponent<PlayerInputController>();
        SetStaticField<PlayerInputController>("_instance", inputCtrl);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);

        PauseMenuPanel pausePanel = CreatePauseMenuFromPrefab(uiManager.popupUILayer.transform);
        pausePanel.gameObject.SetActive(false);

        var cachedPanels = GetPrivateField<Dictionary<string, UIPanelBase>>(uiManager, "_cachedPanels");
        cachedPanels[UIConstants.PANEL_PAUSE_MENU] = pausePanel;

        GameObject topHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/TopHUDPanel.prefab");
        Assert.IsNotNull(topHudPrefab, "TopHUDPanel.prefab을 찾을 수 없습니다.");
        GameObject topHudGo = Track(UnityEngine.Object.Instantiate(topHudPrefab, uiManager.topUILayer.transform, false));
        TopHUDPanel topHud = topHudGo.GetComponent<TopHUDPanel>();
        Assert.IsNotNull(topHud);
        InvokePrivateMethod(topHud, "Awake");

        // 1. TopHUD 일시정지 버튼으로 열기 -> Time.timeScale == 0f & 월드 입력 차단
        topHud.OpenPauseMenu();
        Assert.IsTrue(pausePanel.gameObject.activeInHierarchy);
        Assert.AreEqual(0f, Time.timeScale, 0.0001f);
        Assert.IsTrue(uiManager.IsWorldInputBlocked);
        Assert.IsTrue(inputCtrl.IsWorldInputBlocked);

        // 2. TopHUD 일시정지 버튼 재클릭(토글 닫기) -> Time.timeScale == 1f & 입력 차단 해제
        topHud.OpenPauseMenu();
        Assert.IsFalse(pausePanel.gameObject.activeInHierarchy);
        Assert.AreEqual(1f, Time.timeScale, 0.0001f);
        Assert.IsFalse(uiManager.IsWorldInputBlocked);
        Assert.IsFalse(inputCtrl.IsWorldInputBlocked);

        // 3. 재오픈 후 ESC(CloseTopPopup)로 닫기 -> Time.timeScale == 1f 복원 확인
        topHud.OpenPauseMenu();
        Assert.IsTrue(pausePanel.gameObject.activeInHierarchy);
        Assert.AreEqual(0f, Time.timeScale, 0.0001f);

        uiManager.CloseTopPopup();
        Assert.IsFalse(pausePanel.gameObject.activeInHierarchy);
        Assert.AreEqual(1f, Time.timeScale, 0.0001f);
        Assert.IsFalse(uiManager.IsWorldInputBlocked);
        Assert.IsFalse(inputCtrl.IsWorldInputBlocked);
    }

    [Test]
    public void PauseMenuPanel_WithCompendiumDeckPanel_KeepsTimeFrozenUntilPauseMenuCloses_AndClosesChildOnDirectClose()
    {
        UIManager uiManager = CreateTestUIManager();

        CardData testCard = new CardData
        {
            cardName = "pause_test_card",
            displayName = "테스트 카드",
            rarity = CardRarity.Common,
            stamina = 20,
            cardType = "Attack",
            obtainSource = ItemObtainSource.Standard
        };
        ModLoader.Instance.CardDatabase[testCard.cardName] = testCard;

        PauseMenuPanel pausePanel = CreatePauseMenuFromPrefab(uiManager.popupUILayer.transform);
        pausePanel.gameObject.SetActive(false);

        GameObject deckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Card/CardDeckCanvas.prefab");
        Assert.IsNotNull(deckPrefab, "CardDeckCanvas.prefab을 찾을 수 없습니다.");
        GameObject deckGo = Track(UnityEngine.Object.Instantiate(deckPrefab, uiManager.normalUILayer.transform, false));
        CardDeckViewPanel deckPanel = deckGo.GetComponent<CardDeckViewPanel>();
        Assert.IsNotNull(deckPanel);
        InvokePrivateMethod(deckPanel, "Awake");

        var cachedPanels = GetPrivateField<Dictionary<string, UIPanelBase>>(uiManager, "_cachedPanels");
        cachedPanels[UIConstants.PANEL_PAUSE_MENU] = pausePanel;
        cachedPanels[UIConstants.PANEL_CARD_DECK] = deckPanel;

        // 1. 일시정지 메뉴 진입 후 도감 열람 버튼 클릭
        uiManager.OpenPauseMenu();
        Assert.AreEqual(0f, Time.timeScale, 0.0001f);

        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        InvokePrivateMethod(pausePanel, "OnCompendiumClicked");
        Assert.IsTrue(deckPanel.gameObject.activeInHierarchy);
        Assert.AreEqual(UILayerType.Popup, deckPanel.uiLayerType);
        Assert.AreEqual(0f, Time.timeScale, 0.0001f);

        // 2. ESC로 도감만 먼저 닫으면 일시정지 메뉴와 Time.timeScale == 0f는 유지되어야 함
        uiManager.CloseTopPopup();
        Assert.IsFalse(deckPanel.gameObject.activeInHierarchy);
        Assert.AreEqual(UILayerType.Popup, deckPanel.uiLayerType);
        Assert.IsTrue(pausePanel.gameObject.activeInHierarchy);
        Assert.AreEqual(0f, Time.timeScale, 0.0001f);

        // 3. 다시 도감을 연 상태에서 일시정지 메뉴를 직접 닫으면 도감도 함께 정리되고 타임스케일이 복원되어야 함
        LogAssert.Expect(LogType.Error, "[CardDeckViewPanel] ObjectPoolManager is not initialized.");
        InvokePrivateMethod(pausePanel, "OnCompendiumClicked");
        Assert.IsTrue(deckPanel.gameObject.activeInHierarchy);
        Assert.AreEqual(UILayerType.Popup, deckPanel.uiLayerType);

        pausePanel.Close();
        Assert.IsFalse(pausePanel.gameObject.activeInHierarchy);
        Assert.IsFalse(deckPanel.gameObject.activeInHierarchy);
        Assert.AreEqual(UILayerType.Popup, deckPanel.uiLayerType);
        Assert.AreEqual(1f, Time.timeScale, 0.0001f);
    }

    [Test]
    public void TopHUDPanel_ToggleDeckView_OpensCardDeckViewPanelOnPopupLayerAboveTopHUDRelicIcons()
    {
        UIManager uiManager = CreateTestUIManager();

        GameObject inputGo = Track(new GameObject("[TestPlayerInputController]"));
        PlayerInputController inputCtrl = inputGo.AddComponent<PlayerInputController>();
        SetStaticField<PlayerInputController>("_instance", inputCtrl);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);

        GameObject cacheGo = Track(new GameObject("[TestAssetCacheManager]"));
        AssetCacheManager cache = cacheGo.AddComponent<AssetCacheManager>();
        SetStaticAutoProperty<AssetCacheManager>("instance", cache);

        GameObject deckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Card/CardDeckCanvas.prefab");
        GameObject topHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/TopHUDPanel.prefab");
        Assert.IsNotNull(deckPrefab, "CardDeckCanvas.prefab을 찾을 수 없습니다.");
        Assert.IsNotNull(topHudPrefab, "TopHUDPanel.prefab을 찾을 수 없습니다.");

        SetPrivateField(cache, "uiDict", new Dictionary<string, GameObject>
        {
            { UIConstants.PANEL_CARD_DECK, deckPrefab },
            { UIConstants.PANEL_TOP_HUD, topHudPrefab }
        });

        TopHUDPanel topHud = uiManager.OpenUI<TopHUDPanel>(UIConstants.PANEL_TOP_HUD, UILayerType.Top, true);
        Assert.IsNotNull(topHud);
        Assert.AreSame(uiManager.topUILayer.transform, topHud.transform.parent);
        Assert.IsNotNull(topHud.PlayerRelicUI);

        topHud.ToggleDeckView();

        CardDeckViewPanel deckPanel = uiManager.GetUI<CardDeckViewPanel>(UIConstants.PANEL_CARD_DECK);
        Assert.IsNotNull(deckPanel);
        Assert.IsTrue(deckPanel.gameObject.activeInHierarchy);
        Assert.AreEqual(UILayerType.Popup, deckPanel.uiLayerType);
        Assert.AreSame(uiManager.popupUILayer.transform, deckPanel.transform.parent);
        Assert.Greater(uiManager.popupUILayer.sortingOrder, uiManager.topUILayer.sortingOrder,
            "CardDeckViewPanel이 배치된 PopupUILayer는 TopHUD/유물 아이콘(TopUILayer)보다 상위 SortingOrder여야 합니다.");
        Assert.IsTrue(uiManager.IsWorldInputBlocked);

        // ESC 닫기 및 재토글 정상 동작 확인
        uiManager.CloseTopPopup();
        Assert.IsFalse(deckPanel.gameObject.activeInHierarchy);
        Assert.IsFalse(uiManager.IsWorldInputBlocked);
    }

    [Test]
    public void BattleManager_Update_FreezesStaminaEffectsAndTurnsWhilePaused_AndResumesOnClose()
    {
        Character playerChar = CreateCharacter("Player", CharacterFaction.Player, maxStamina: 100f, regenPerSecond: 20f);
        Character enemyChar = CreateCharacter("Enemy", CharacterFaction.Enemy, maxStamina: 100f, regenPerSecond: 15f);

        var playerCtrl = new StubTurnCountingController(playerChar);
        var enemyCtrl = new StubTurnCountingController(enemyChar);

        EffectData timedDebuffData = new EffectData
        {
            effectName = "TestTimedDebuff",
            displayName = "테스트 디버프",
            description = "테스트용 시간형 효과",
            effectType = EffectType.Debuff,
            clockType = EffectClockType.Time,
            isPermanent = false,
            maxStack = 99
        };
        Effect timedEffect = new Effect(timedDebuffData, null);
        playerChar.effectManager.AddEffect(enemyChar, timedEffect, stack: 1, duration: 2.0f);
        playerChar.AddOrRefreshStatModifier(
            "TestSpeedBuff",
            StatModifierTarget.StaminaRegen,
            StatModifierOp.Flat,
            0f,
            StatModifierLifetime.DurationSeconds,
            duration: 2.0f,
            isBattleScoped: true);

        BattleManager battleManager = new BattleManager();
        PauseMenuPanel pausePanel = CreatePauseMenuFromPrefab();

        try
        {
            battleManager.InitBattle(
                new List<ICharacterController> { playerCtrl },
                new List<ICharacterController> { enemyCtrl });
            battleManager.StartCombat();
            Assert.AreEqual(BattleState.Combat, battleManager.battleState);

            // 일시정지 전: 0.5초 경과 시 스태미나·이펙트·모디파이어 정상 진행 확인
            battleManager.Update(0.5f);
            Assert.AreEqual(10f, playerChar.curStamina, 0.001f);
            Assert.AreEqual(7.5f, enemyChar.curStamina, 0.001f);
            Assert.AreEqual(1.5f, timedEffect.currentDuration, 0.001f);
            Assert.AreEqual(1.5f, playerChar.StatModifiers[0].remainingDuration, 0.001f);

            // 일시정지 진입: 외부에서 양수 dt를 전달해도 BattleManager.Update가 완전히 정지해야 함
            pausePanel.OnOpen();
            Assert.AreEqual(0f, Time.timeScale, 0.0001f);

            battleManager.Update(0.5f);
            battleManager.Update(2.0f);
            Assert.AreEqual(10f, playerChar.curStamina, 0.001f);
            Assert.AreEqual(7.5f, enemyChar.curStamina, 0.001f);
            Assert.AreEqual(0f, playerChar.overflowStamina, 0.001f);
            Assert.AreEqual(1.5f, timedEffect.currentDuration, 0.001f);
            Assert.AreEqual(1.5f, playerChar.StatModifiers[0].remainingDuration, 0.001f);
            Assert.AreEqual(0, playerCtrl.TurnStartCount);
            Assert.AreEqual(0, enemyCtrl.TurnStartCount);

            // 완충 상태 유닛이 있더라도 Time.timeScale == 0f 중에는 턴 개시가 발생하면 안 됨
            playerChar.curStamina = 100f;
            battleManager.Update(0.1f);
            Assert.AreEqual(0, playerCtrl.TurnStartCount);
            Assert.AreEqual(BattleState.Combat, battleManager.battleState);

            // 일시정지 해제 후: 틱 및 턴 획득 정상 재개 확인
            pausePanel.OnClose();
            Assert.AreEqual(1f, Time.timeScale, 0.0001f);

            playerChar.curStamina = 95f;
            battleManager.Update(0.5f); // +10 -> 100 도달 + 턴 개시
            Assert.AreEqual(1, playerCtrl.TurnStartCount);
            Assert.AreEqual(BattleState.TurnInProgress, battleManager.battleState);
            Assert.AreSame(playerCtrl, battleManager.currentTurnController);
            Assert.AreEqual(1.0f, timedEffect.currentDuration, 0.001f);
            Assert.AreEqual(1.0f, playerChar.StatModifiers[0].remainingDuration, 0.001f);
        }
        finally
        {
            battleManager.Dispose();
        }
    }

    [Test]
    public void PauseMenuPanel_WhileTimeScaleZero_SettingsTooltipAndRelicCompendiumWorkNormally()
    {
        GameObject canvasGo = Track(new GameObject("PauseUiZeroScaleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject ttGo = Track(new GameObject("TooltipManager", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)));
        ttGo.transform.SetParent(canvas.transform, false);
        GameObject templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/TooltipPanel.prefab");
        Assert.IsNotNull(templatePrefab);
        TooltipManager tooltipManager = ttGo.AddComponent<TooltipManager>();
        SetPrivateField(tooltipManager, "panelTemplatePrefab", templatePrefab.GetComponent<TooltipPanel>());
        InvokePrivateMethod(tooltipManager, "Awake");

        RelicData testRelic = new RelicData
        {
            relicName = "pause_test_relic",
            displayName = "일시정지 유물",
            rarity = RelicRarity.Common,
            obtainSource = ItemObtainSource.Standard,
            description = "테스트 설명",
            classTypes = new List<string> { "All" }
        };
        ModLoader.Instance.RelicDatabase[testRelic.relicName] = testRelic;

        PauseMenuPanel pausePanel = CreatePauseMenuFromPrefab(canvas.transform);
        pausePanel.OnOpen();
        Assert.AreEqual(0f, Time.timeScale, 0.0001f);

        // 1. Time.timeScale == 0f 상태에서 툴팁 토글 및 TooltipManager 표시/숨김 동작 확인
        Toggle tooltipToggle = GetPrivateField<Toggle>(pausePanel, "tooltipToggle");
        Assert.IsNotNull(tooltipToggle);
        tooltipToggle.isOn = false;
        Assert.IsFalse(TooltipManager.IsTooltipEnabled);
        tooltipToggle.isOn = true;
        Assert.IsTrue(TooltipManager.IsTooltipEnabled);

        tooltipManager.ShowTooltip("일시정지 툴팁", "타임스케일 0에서도 표시");
        Assert.IsTrue(tooltipManager.gameObject.activeSelf);

        // 2. Time.timeScale == 0f 상태에서 유물 도감 열람 및 필터 동작 확인
        Button relicCompendiumBtn = GetPrivateField<Button>(pausePanel, "relicCompendiumButton");
        relicCompendiumBtn.onClick.Invoke();
        Assert.AreEqual(1, pausePanel.DisplayedRelicCount);

        pausePanel.OnClose();
        Assert.IsFalse(tooltipManager.gameObject.activeSelf);
        Assert.AreEqual(1f, Time.timeScale, 0.0001f);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    private static void SetStaticField<T>(string fieldName, object value)
    {
        FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"Static field '{fieldName}' not found on {typeof(T).Name}");
        field.SetValue(null, value);
    }

    private static void SetStaticAutoProperty<T>(string propertyName, object value)
    {
        PropertyInfo prop = typeof(T).GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(prop, $"Static property '{propertyName}' not found on {typeof(T).Name}");
        prop.SetValue(null, value);
    }

    private static object InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}");
        return method.Invoke(target, args);
    }
}
