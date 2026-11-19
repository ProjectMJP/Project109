using System;
using System.Collections.Generic;
using System.Reflection;
using EventStructs;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NUnit.Framework;

[TestFixture]
public class BattleSideHUDAndTooltipTests
{
    private sealed class StubCharacterController : ICharacterController
    {
        public Character controlledCharacter { get; }

        public StubCharacterController(Character character)
        {
            controlledCharacter = character;
        }

        public void OnTurnStart() { }
        public void OnTurnEnd() { }
    }

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        TooltipManager.SetTooltipEnabled(true);
        BattleManager.SetGlobalBattleSpeedIndex(0, savePrefs: false);
    }

    [TearDown]
    public void TearDown()
    {
        TooltipManager.SetTooltipEnabled(true);
        BattleManager.SetGlobalBattleSpeedIndex(0, savePrefs: false);

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

    private Canvas CreateTestCanvas(string name, float width = 1920f, float height = 1080f)
    {
        GameObject canvasGo = Track(new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        RectTransform rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        rt.position = new Vector3(width * 0.5f, height * 0.5f, 0f);
        return canvas;
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

    private Character CreateCharacter(string name, CharacterFaction faction, float maxHp = 100f, float maxStamina = 100f)
    {
        GameObject go = Track(new GameObject(name));
        Character c = go.AddComponent<Character>();
        InvokePrivateMethod(c, "Awake");
        c.faction = faction;
        c.InitializeStat(new CharacterStat
        {
            maxHealth = maxHp,
            maxStamina = maxStamina,
            staminaRegenPerSecond = 20f,
            moveRange = 3,
        });
        return c;
    }

    private static Effect CreateTestEffect(string id, string displayName, EffectType type, int stack = 1, float duration = 5f)
    {
        EffectData data = new EffectData
        {
            effectName = id,
            displayName = displayName,
            description = $"{displayName} 설명",
            effectType = type,
            clockType = EffectClockType.Time,
            isPermanent = false,
            maxStack = 99
        };
        Effect effect = new Effect(data, null);
        effect.OnAdded(null, null, stack, duration);
        return effect;
    }

    [Test]
    public void TooltipManager_GlobalToggle_SwitchesBetweenFullTooltipAndCompactHintInPlace_AndRespectsHide()
    {
        Canvas canvas = CreateTestCanvas("TooltipToggleCanvas");
        TooltipManager manager = CreateTestTooltipManager(canvas);

        List<bool> notifiedStates = new List<bool>();
        void OnChanged(bool state) => notifiedStates.Add(state);
        TooltipManager.OnTooltipEnabledChanged += OnChanged;

        try
        {
            Assert.IsTrue(TooltipManager.IsTooltipEnabled);

            // 1. ON 상태: 2개 툴팁 박스 동시 표시 시 최하단 박스에만 [T] 툴팁 숨기기 캡션 표시 검증
            var tooltips = new List<TooltipData>
            {
                new TooltipData("첫 번째", "내용 1"),
                new TooltipData("두 번째", "내용 2")
            };
            manager.ShowTooltips(tooltips);
            Assert.IsTrue(manager.gameObject.activeSelf);

            var activePanels = GetPrivateField<List<TooltipPanel>>(manager, "activePanels");
            Assert.AreEqual(2, activePanels.Count);

            TextMeshProUGUI firstHint = GetPrivateField<TextMeshProUGUI>(activePanels[0], "_hintCaptionText");
            TextMeshProUGUI lastHint = GetPrivateField<TextMeshProUGUI>(activePanels[1], "_hintCaptionText");
            Assert.IsNotNull(firstHint);
            Assert.IsNotNull(lastHint);
            Assert.IsFalse(firstHint.gameObject.activeSelf, "상단 툴팁 박스에는 힌트 캡션이 숨겨져야 합니다.");
            Assert.IsTrue(lastHint.gameObject.activeSelf, "최하단 툴팁 박스에는 [T] 힌트 캡션이 표시되어야 합니다.");
            Assert.AreEqual("[T] 툴팁 숨기기", lastHint.text);

            // 2. 호버 중 OFF 전환 시 즉시 1줄 컴팩트 힌트([T] 툴팁 보기)로 전환 검증
            bool afterOff = TooltipManager.ToggleGlobalTooltipEnabled();
            Assert.IsFalse(afterOff);
            Assert.IsFalse(TooltipManager.IsTooltipEnabled);
            Assert.IsTrue(manager.gameObject.activeSelf, "호버 중 OFF 전환 시 1줄 힌트가 즉시 표시되어야 합니다.");
            Assert.AreEqual(1, activePanels.Count);
            AssertCompactHintPanelState(activePanels[0], "[T] 툴팁 보기");

            // 3. OFF 상태에서 ShowTooltip / ShowTooltips 호출 시 1줄 힌트만 표시 + 키워드 없는 ShowKeywordTooltips는 숨김 유지
            manager.ShowKeywordTooltips("키워드가 전혀 없는 일반 설명");
            Assert.IsFalse(manager.gameObject.activeSelf, "키워드가 없는 카드 호버 시에는 [T] 툴팁 보기 힌트가 뜨지 않아야 합니다.");

            manager.ShowTooltip("단일 제목", "단일 내용");
            Assert.IsTrue(manager.gameObject.activeSelf);
            Assert.AreEqual(1, activePanels.Count);
            AssertCompactHintPanelState(activePanels[0], "[T] 툴팁 보기");

            manager.ShowTooltips(tooltips);
            Assert.IsTrue(manager.gameObject.activeSelf);
            Assert.AreEqual(1, activePanels.Count);
            AssertCompactHintPanelState(activePanels[0], "[T] 툴팁 보기");

            // 4. 호버 중 ON 복원 시 즉시 풀 툴팁 복구 검증
            bool afterOn = TooltipManager.ToggleGlobalTooltipEnabled();
            Assert.IsTrue(afterOn);
            Assert.IsTrue(TooltipManager.IsTooltipEnabled);
            Assert.IsTrue(manager.gameObject.activeSelf);
            Assert.AreEqual(2, activePanels.Count);

            TextMeshProUGUI restoredHeader = GetPrivateField<TextMeshProUGUI>(activePanels[0], "_headerText");
            TextMeshProUGUI restoredBody = GetPrivateField<TextMeshProUGUI>(activePanels[0], "_bodyText");
            LayoutElement restoredLayout = GetPrivateField<LayoutElement>(activePanels[0], "_layoutElement");
            TextMeshProUGUI restoredLastHint = GetPrivateField<TextMeshProUGUI>(activePanels[1], "_hintCaptionText");
            Assert.IsTrue(restoredHeader.gameObject.activeSelf);
            Assert.IsTrue(restoredBody.gameObject.activeSelf);
            Assert.IsTrue(restoredLayout.enabled);
            Assert.IsTrue(restoredLastHint.gameObject.activeSelf);
            Assert.AreEqual("[T] 툴팁 숨기기", restoredLastHint.text);

            // 5. HideTooltip() 호출 후에는 OFF/ON 토글해도 툴팁이 재활성화되지 않아야 함
            manager.HideTooltip();
            Assert.IsFalse(manager.gameObject.activeSelf);

            TooltipManager.SetTooltipEnabled(false);
            Assert.IsFalse(manager.gameObject.activeSelf);

            TooltipManager.SetTooltipEnabled(true);
            Assert.IsFalse(manager.gameObject.activeSelf);

            Assert.AreEqual(new List<bool> { false, true, false, true }, notifiedStates);
        }
        finally
        {
            TooltipManager.OnTooltipEnabledChanged -= OnChanged;
        }
    }

    private static void AssertCompactHintPanelState(TooltipPanel panel, string expectedCaption)
    {
        TextMeshProUGUI header = GetPrivateField<TextMeshProUGUI>(panel, "_headerText");
        TextMeshProUGUI body = GetPrivateField<TextMeshProUGUI>(panel, "_bodyText");
        TextMeshProUGUI hint = GetPrivateField<TextMeshProUGUI>(panel, "_hintCaptionText");
        LayoutElement layoutElem = GetPrivateField<LayoutElement>(panel, "_layoutElement");

        Assert.IsFalse(header.gameObject.activeSelf, "컴팩트 힌트 모드에서는 _headerText가 비활성화되어야 합니다.");
        Assert.IsFalse(body.gameObject.activeSelf, "컴팩트 힌트 모드에서는 _bodyText가 비활성화되어야 합니다.");
        Assert.IsFalse(layoutElem.enabled, "컴팩트 힌트 모드에서는 고정 폭 LayoutElement가 비활성화되어야 합니다.");
        Assert.IsTrue(hint.gameObject.activeSelf, "컴팩트 힌트 모드에서는 _hintCaptionText가 활성화되어야 합니다.");
        Assert.AreEqual(expectedCaption, hint.text);
        Assert.AreEqual(TextAlignmentOptions.Center, hint.alignment);
    }

    [Test]
    public void TopHUDPanel_SpeedToggleButton_CyclesBattleSpeedAndUpdatesLabel()
    {
        const string prefKey = "Settings_BattleSpeedIndex";
        bool hadPref = PlayerPrefs.HasKey(prefKey);
        int previousPref = PlayerPrefs.GetInt(prefKey, 0);

        GameObject topHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/TopHUDPanel.prefab");
        Assert.IsNotNull(topHudPrefab, "TopHUDPanel.prefab을 찾을 수 없습니다.");

        Canvas canvas = CreateTestCanvas("TopHudTestCanvas");
        GameObject topHudGo = Track(UnityEngine.Object.Instantiate(topHudPrefab, canvas.transform, false));
        TopHUDPanel topHud = topHudGo.GetComponent<TopHUDPanel>();
        Assert.IsNotNull(topHud);

        try
        {
            InvokePrivateMethod(topHud, "Awake");
            InvokePrivateMethod(topHud, "OnEnable");

            Transform hudPanel = topHudGo.transform.Find("HUDPanel");
            Assert.IsNotNull(hudPanel);
            Assert.IsNotNull(hudPanel.Find("SpeedToggleButton"), "TopHUDPanel.prefab의 버튼 오브젝트 이름이 SpeedToggleButton이어야 합니다.");
            Assert.IsNull(hudPanel.Find("TooltipToggleButton"), "레거시 TooltipToggleButton 오브젝트는 남아있지 않아야 합니다.");

            Button speedButton = GetPrivateField<Button>(topHud, "speedToggleButton");
            TextMeshProUGUI speedText = GetPrivateField<TextMeshProUGUI>(topHud, "speedToggleText");
            Image speedBg = GetPrivateField<Image>(topHud, "speedToggleBgImage");
            Assert.IsNotNull(speedButton);
            Assert.IsNotNull(speedText);
            Assert.IsNotNull(speedBg);

            Color color1xBg = GetStaticPrivateField<Color>(typeof(TopHUDPanel), "Speed1xBgColor");
            Color color1xText = GetStaticPrivateField<Color>(typeof(TopHUDPanel), "Speed1xTextColor");
            Color color1_5xBg = GetStaticPrivateField<Color>(typeof(TopHUDPanel), "Speed1_5xBgColor");
            Color color1_5xText = GetStaticPrivateField<Color>(typeof(TopHUDPanel), "Speed1_5xTextColor");
            Color color2xBg = GetStaticPrivateField<Color>(typeof(TopHUDPanel), "Speed2xBgColor");
            Color color2xText = GetStaticPrivateField<Color>(typeof(TopHUDPanel), "Speed2xTextColor");

            Assert.AreEqual(1.0f, BattleManager.GlobalBattleSpeed, 0.001f);
            Assert.AreEqual("배속: 1x", speedText.text);
            Assert.AreEqual(color1xBg, speedBg.color);
            Assert.AreEqual(color1xText, speedText.color);

            speedButton.onClick.Invoke();
            Assert.AreEqual(1.5f, BattleManager.GlobalBattleSpeed, 0.001f);
            Assert.AreEqual("배속: 1.5x", speedText.text);
            Assert.AreEqual(color1_5xBg, speedBg.color);
            Assert.AreEqual(color1_5xText, speedText.color);

            speedButton.onClick.Invoke();
            Assert.AreEqual(2.0f, BattleManager.GlobalBattleSpeed, 0.001f);
            Assert.AreEqual("배속: 2x", speedText.text);
            Assert.AreEqual(color2xBg, speedBg.color);
            Assert.AreEqual(color2xText, speedText.color);

            speedButton.onClick.Invoke();
            Assert.AreEqual(1.0f, BattleManager.GlobalBattleSpeed, 0.001f);
            Assert.AreEqual("배속: 1x", speedText.text);
            Assert.AreEqual(color1xBg, speedBg.color);
            Assert.AreEqual(color1xText, speedText.color);
        }
        finally
        {
            InvokePrivateMethod(topHud, "OnDisable");
            RestoreIntPref(prefKey, hadPref, previousPref);
        }
    }

    [Test]
    public void BattleManager_SpeedToggle_SyncsBattleTimeScaleAndPersistsPref()
    {
        const string prefKey = "Settings_BattleSpeedIndex";
        bool hadPref = PlayerPrefs.HasKey(prefKey);
        int previousPref = PlayerPrefs.GetInt(prefKey, 0);

        GameObject runGo = Track(new GameObject("TestRunManager_Speed"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        InvokePrivateMethod(runManager, "Awake");

        int callbackCount = 0;
        float lastNotifiedSpeed = 0f;
        void OnSpeedChanged(float speed)
        {
            callbackCount++;
            lastNotifiedSpeed = speed;
        }
        BattleManager.OnBattleSpeedChanged += OnSpeedChanged;

        try
        {
            BattleManager bm = runManager.battleManager;
            Assert.IsNotNull(bm);
            Assert.AreEqual(0, BattleManager.CurrentSpeedIndex);
            Assert.AreEqual(1.0f, BattleManager.GlobalBattleSpeed, 0.001f);
            Assert.AreEqual(1.0f, bm.battleTimeScale, 0.001f);

            // 1x -> 1.5x
            BattleManager.CycleGlobalBattleSpeed();
            Assert.AreEqual(1, BattleManager.CurrentSpeedIndex);
            Assert.AreEqual(1.5f, BattleManager.GlobalBattleSpeed, 0.001f);
            Assert.AreEqual(1.5f, bm.battleTimeScale, 0.001f);
            Assert.AreEqual(1, PlayerPrefs.GetInt(prefKey, -1));
            Assert.AreEqual(1.5f, lastNotifiedSpeed, 0.001f);

            // 1.5x -> 2x
            BattleManager.CycleGlobalBattleSpeed();
            Assert.AreEqual(2, BattleManager.CurrentSpeedIndex);
            Assert.AreEqual(2.0f, BattleManager.GlobalBattleSpeed, 0.001f);
            Assert.AreEqual(2.0f, bm.battleTimeScale, 0.001f);
            Assert.AreEqual(2, PlayerPrefs.GetInt(prefKey, -1));
            Assert.AreEqual(2.0f, lastNotifiedSpeed, 0.001f);

            // 신규 BattleManager 생성 및 Dispose 시 정적 구독자 유지 확인
            BattleManager extraBm = new BattleManager();
            Assert.AreEqual(2.0f, extraBm.battleTimeScale, 0.001f);
            extraBm.Dispose();

            int countBeforeCycle = callbackCount;
            BattleManager.CycleGlobalBattleSpeed(); // 2x -> 1x
            Assert.AreEqual(countBeforeCycle + 1, callbackCount, "BattleManager.Dispose 이후에도 정적 OnBattleSpeedChanged 구독자는 유지되어야 합니다.");
            Assert.AreEqual(0, BattleManager.CurrentSpeedIndex);
            Assert.AreEqual(1.0f, BattleManager.GlobalBattleSpeed, 0.001f);
            Assert.AreEqual(1.0f, bm.battleTimeScale, 0.001f);
            Assert.AreEqual(0, PlayerPrefs.GetInt(prefKey, -1));

            // PlayerPrefs 복원 및 범위 클램프 검증
            PlayerPrefs.SetInt(prefKey, 2);
            PlayerPrefs.Save();
            BattleManager.SetGlobalBattleSpeedIndex(0, savePrefs: false);
            InvokeStaticPrivateMethod(typeof(BattleManager), "LoadSavedBattleSpeedOnStartup");
            Assert.AreEqual(2, BattleManager.CurrentSpeedIndex);
            Assert.AreEqual(2.0f, bm.battleTimeScale, 0.001f);

            PlayerPrefs.SetInt(prefKey, 99);
            PlayerPrefs.Save();
            InvokeStaticPrivateMethod(typeof(BattleManager), "LoadSavedBattleSpeedOnStartup");
            Assert.AreEqual(BattleManager.SpeedSteps.Length - 1, BattleManager.CurrentSpeedIndex);

            // ActionQueue 정적 WaitForSeconds 캐시 배열 길이 검증
            WaitForSeconds[] playDelays = GetStaticPrivateField<WaitForSeconds[]>(typeof(CardPlayAction), "_playDelays");
            WaitForSeconds[] postPlayDelays = GetStaticPrivateField<WaitForSeconds[]>(typeof(CardPlayAction), "_postPlayDelays");
            WaitForSeconds[] postMoveDelays = GetStaticPrivateField<WaitForSeconds[]>(typeof(EnemyMoveAction), "_postMoveDelays");
            Assert.AreEqual(BattleManager.SpeedSteps.Length, playDelays.Length);
            Assert.AreEqual(BattleManager.SpeedSteps.Length, postPlayDelays.Length);
            Assert.AreEqual(BattleManager.SpeedSteps.Length, postMoveDelays.Length);
            for (int i = 0; i < BattleManager.SpeedSteps.Length; i++)
            {
                Assert.IsNotNull(playDelays[i]);
                Assert.IsNotNull(postPlayDelays[i]);
                Assert.IsNotNull(postMoveDelays[i]);
            }
        }
        finally
        {
            BattleManager.OnBattleSpeedChanged -= OnSpeedChanged;
            InvokePrivateMethod(runManager, "OnDestroy");
            RestoreIntPref(prefKey, hadPref, previousPref);
        }
    }

    [Test]
    public void PauseMenuPanel_SettingsTooltipToggle_SyncsWithGlobalTooltipManager()
    {
        GameObject pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/PauseMenuPanel.prefab");
        Assert.IsNotNull(pausePrefab, "PauseMenuPanel.prefab을 찾을 수 없습니다.");

        Canvas canvas = CreateTestCanvas("PauseMenuTooltipCanvas");
        GameObject pauseGo = Track(UnityEngine.Object.Instantiate(pausePrefab, canvas.transform, false));
        PauseMenuPanel pausePanel = pauseGo.GetComponent<PauseMenuPanel>();
        Assert.IsNotNull(pausePanel);

        try
        {
            InvokePrivateMethod(pausePanel, "Awake");
            InvokePrivateMethod(pausePanel, "OnEnable");
            pausePanel.OnOpen();

            GameObject settingsGroup = GetPrivateField<GameObject>(pausePanel, "settingsGroup");
            Button settingsBackButton = GetPrivateField<Button>(pausePanel, "settingsBackButton");
            Toggle tooltipToggle = GetPrivateField<Toggle>(pausePanel, "tooltipToggle");
            TextMeshProUGUI tooltipStatusText = GetPrivateField<TextMeshProUGUI>(pausePanel, "tooltipStatusText");

            Assert.IsNotNull(settingsGroup);
            Assert.IsNotNull(settingsBackButton);
            Assert.IsNotNull(tooltipToggle);
            Assert.IsNotNull(tooltipStatusText);

            Transform tooltipRow = settingsGroup.transform.Find("TooltipRow");
            Assert.IsNotNull(tooltipRow, "SettingsGroup 내에 TooltipRow가 자동 생성되어야 합니다.");
            Assert.Less(tooltipRow.GetSiblingIndex(), settingsBackButton.transform.GetSiblingIndex(), "TooltipRow는 SettingsBackButton 위에 배치되어야 합니다.");

            Assert.IsTrue(TooltipManager.IsTooltipEnabled);
            Assert.IsTrue(tooltipToggle.isOn);
            Assert.AreEqual("ON", tooltipStatusText.text);

            // UI 토글 조작 -> TooltipManager 전역 상태 반영 확인
            tooltipToggle.isOn = false;
            Assert.IsFalse(TooltipManager.IsTooltipEnabled);
            Assert.AreEqual("OFF", tooltipStatusText.text);

            // 외부([T] 단축키 등)에서 상태 변경 -> 설정 UI 자동 동기화 확인
            TooltipManager.SetTooltipEnabled(true);
            Assert.IsTrue(tooltipToggle.isOn);
            Assert.AreEqual("ON", tooltipStatusText.text);
        }
        finally
        {
            InvokePrivateMethod(pausePanel, "OnDisable");
            InvokePrivateMethod(pausePanel, "OnDestroy");
        }
    }

    [Test]
    public void TooltipManager_HorizontalInward_And_TriggerOverride_PositionsInwardWithoutCoveringBelowCards()
    {
        Canvas canvas = CreateTestCanvas("HorizontalInwardCanvas", 1920f, 1080f);
        TooltipManager manager = CreateTestTooltipManager(canvas);
        RectTransform tooltipRect = manager.GetComponent<RectTransform>();

        GameObject leftCardGo = Track(new GameObject("LeftCard", typeof(RectTransform)));
        leftCardGo.transform.SetParent(canvas.transform, false);
        RectTransform leftCardRect = leftCardGo.GetComponent<RectTransform>();
        leftCardRect.sizeDelta = new Vector2(270f, 96f);
        leftCardRect.anchoredPosition = new Vector2(-809f, 200f);

        GameObject rightCardGo = Track(new GameObject("RightCard", typeof(RectTransform)));
        rightCardGo.transform.SetParent(canvas.transform, false);
        RectTransform rightCardRect = rightCardGo.GetComponent<RectTransform>();
        rightCardRect.sizeDelta = new Vector2(270f, 96f);
        rightCardRect.anchoredPosition = new Vector2(809f, 200f);

        // 좌측 카드 아이콘 -> TooltipTrigger가 cardRect를 anchorOverride로 사용하여 오른쪽으로 전개
        GameObject iconGo = Track(new GameObject("LeftEffectIcon", typeof(RectTransform), typeof(TooltipTrigger)));
        iconGo.transform.SetParent(leftCardRect, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(25f, 25f);

        TooltipTrigger trigger = iconGo.GetComponent<TooltipTrigger>();
        trigger.ConfigureAnchor(leftCardRect, TooltipPlacementMode.HorizontalInward);
        trigger.SetStaticContent("독", "매 턴 피해");
        trigger.OnPointerEnter(null);

        Assert.IsTrue(manager.gameObject.activeSelf);
        Assert.AreEqual(0f, tooltipRect.pivot.x, 0.001f, "좌측 사이드 카드 툴팁은 오른쪽(pivot.x = 0)으로 전개되어야 합니다.");
        Assert.AreEqual(1f, tooltipRect.pivot.y, 0.001f);
        Assert.Greater(tooltipRect.anchoredPosition.x, -809f + 135f, "좌측 카드의 오른쪽 가장자리 바깥에 배치되어야 합니다.");
        Assert.AreSame(leftCardRect, GetPrivateField<RectTransform>(manager, "currentTargetRect"));

        // 우측 카드 -> 왼쪽(화면 중앙 방향)으로 전개
        manager.ShowTooltip("힘", "피해 증가", rightCardRect, TooltipPlacementMode.HorizontalInward);
        Assert.AreEqual(1f, tooltipRect.pivot.x, 0.001f, "우측 사이드 카드 툴팁은 왼쪽(pivot.x = 1)으로 전개되어야 합니다.");
        Assert.AreEqual(1f, tooltipRect.pivot.y, 0.001f);
        Assert.Less(tooltipRect.anchoredPosition.x, 809f - 135f, "우측 카드의 왼쪽 가장자리 바깥에 배치되어야 합니다.");
    }

    [Test]
    public void CharacterStatusBarUI_CompactMode_HidesTextAndUpdatesFillAndShieldColor()
    {
        GameObject statusBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterStatusBarUI.prefab");
        Assert.IsNotNull(statusBarPrefab, "CharacterStatusBarUI.prefab을 찾을 수 없습니다.");

        Canvas canvas = CreateTestCanvas("WorldStatusBarTestCanvas");
        GameObject go = Track(UnityEngine.Object.Instantiate(statusBarPrefab, canvas.transform, false));
        CharacterStatusBarUI statusBar = go.GetComponent<CharacterStatusBarUI>();
        Assert.IsNotNull(statusBar);
        InvokePrivateMethod(statusBar, "Awake");

        RectTransform rt = go.GetComponent<RectTransform>();
        Assert.AreEqual(64f, rt.sizeDelta.x, 0.01f);
        Assert.AreEqual(14f, rt.sizeDelta.y, 0.01f);
        Assert.IsFalse(statusBar.ShowValueText);

        GaugeUI hpGauge = GetPrivateField<GaugeUI>(statusBar, "healthGaugeUI");
        GaugeUI stamGauge = GetPrivateField<GaugeUI>(statusBar, "staminaGaugeUI");
        Assert.IsNotNull(hpGauge);
        Assert.IsNotNull(stamGauge);

        TextMeshProUGUI hpText = GetPrivateField<TextMeshProUGUI>(hpGauge, "gaugeText");
        TextMeshProUGUI stamText = GetPrivateField<TextMeshProUGUI>(stamGauge, "gaugeText");
        Assert.IsNotNull(hpText);
        Assert.IsNotNull(stamText);
        Assert.IsFalse(hpText.gameObject.activeSelf);
        Assert.IsFalse(stamText.gameObject.activeSelf);

        statusBar.UpdateHealth(50f, 100f, 0f);
        Image hpFill = GetPrivateField<Image>(hpGauge, "gaugeImageFilled");
        Assert.AreEqual(0.5f, hpFill.fillAmount, 0.001f);
        Assert.AreEqual(UIConstants.COLOR_HEALTH, hpFill.color);
        Assert.IsFalse(hpText.gameObject.activeSelf);

        statusBar.UpdateHealth(50f, 100f, 20f);
        Assert.AreEqual(UIConstants.COLOR_SHIELD, hpFill.color);

        statusBar.UpdateStamina(25f, 100f);
        Image stamFill = GetPrivateField<Image>(stamGauge, "gaugeImageFilled");
        Assert.AreEqual(0.25f, stamFill.fillAmount, 0.001f);
        Assert.IsFalse(stamText.gameObject.activeSelf);
    }

    [Test]
    public void CharacterEffectListUI_DotMode_AddsColoredDotsWithoutTooltipTriggerAndClearsCleanly()
    {
        GameObject effectListPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterEffectListUI.prefab");
        Assert.IsNotNull(effectListPrefab, "CharacterEffectListUI.prefab을 찾을 수 없습니다.");

        Canvas canvas = CreateTestCanvas("WorldEffectDotTestCanvas");
        GameObject go = Track(UnityEngine.Object.Instantiate(effectListPrefab, canvas.transform, false));
        CharacterEffectListUI effectList = go.GetComponent<CharacterEffectListUI>();
        Assert.IsNotNull(effectList);
        InvokePrivateMethod(effectList, "Awake");

        Assert.IsTrue(effectList.UseDotMode);
        Assert.AreEqual(0, effectList.ActiveEffectCount);
        Assert.IsNull(go.GetComponent<ContentSizeFitter>(), "폭 64 고정을 위해 ContentSizeFitter가 없어야 합니다.");

        Effect buff = CreateTestEffect("BuffTest", "힘", EffectType.Buff);
        Effect debuff = CreateTestEffect("DebuffTest", "화상", EffectType.Debuff);

        effectList.AddEffect(buff);
        effectList.AddEffect(debuff);
        Assert.AreEqual(2, effectList.ActiveEffectCount);

        Image[] dotImages = go.GetComponentsInChildren<Image>(false);
        Assert.AreEqual(2, dotImages.Length);
        Assert.AreEqual(0, go.GetComponentsInChildren<TooltipTrigger>(true).Length, "월드 도트에는 TooltipTrigger가 없어야 합니다.");
        foreach (Image img in dotImages)
        {
            Assert.IsFalse(img.raycastTarget, "월드 도트는 raycastTarget=false여야 합니다.");
        }

        Color expectedBuffColor = GetPrivateField<Color>(effectList, "buffDotColor");
        Color expectedDebuffColor = GetPrivateField<Color>(effectList, "debuffDotColor");
        Assert.AreEqual(expectedBuffColor, dotImages[0].color);
        Assert.AreEqual(expectedDebuffColor, dotImages[1].color);

        effectList.RemoveEffect(buff);
        Assert.AreEqual(1, effectList.ActiveEffectCount);

        effectList.ClearAll();
        Assert.AreEqual(0, effectList.ActiveEffectCount);
        Assert.AreEqual(0, go.GetComponentsInChildren<Image>(false).Length);
    }

    [Test]
    public void BattleSideUnitHUDPanel_RosterBinding_PlacesPlayerLeftAndLivingEnemiesRight_RemovesOnDeath()
    {
        GameObject sideHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleSideUnitHUDPanel.prefab");
        Assert.IsNotNull(sideHudPrefab, "BattleSideUnitHUDPanel.prefab을 찾을 수 없습니다.");

        Canvas canvas = CreateTestCanvas("SideHudRosterCanvas");
        GameObject panelGo = Track(UnityEngine.Object.Instantiate(sideHudPrefab, canvas.transform, false));
        BattleSideUnitHUDPanel sidePanel = panelGo.GetComponent<BattleSideUnitHUDPanel>();
        Assert.IsNotNull(sidePanel);
        InvokePrivateMethod(sidePanel, "Awake");
        Assert.IsNull(panelGo.GetComponent<Graphic>(), "루트는 월드 클릭 비차단을 위해 Graphic이 없어야 합니다.");
        Assert.IsFalse(sidePanel.blockWorldInput);

        Character playerChar = CreateCharacter("Player", CharacterFaction.Player, 100f, 120f);
        Character enemyA = CreateCharacter("SkeletonWarrior", CharacterFaction.Enemy, 60f, 100f);
        Character enemyB = CreateCharacter("SkeletonGuard", CharacterFaction.Enemy, 80f, 100f);

        var playerCtrl = new StubCharacterController(playerChar);
        var enemyACtrl = new StubCharacterController(enemyA);
        var enemyBCtrl = new StubCharacterController(enemyB);

        var battleManager = new BattleManager();
        try
        {
            battleManager.InitBattle(
                new List<ICharacterController> { playerCtrl },
                new List<ICharacterController> { enemyACtrl, enemyBCtrl });
            battleManager.StartCombat();

            Effect poison = CreateTestEffect("Poison", "중독", EffectType.Debuff, stack: 3, duration: 4f);
            enemyA.effectManager.AddEffect(playerChar, poison, 3, 4f);

            sidePanel.BindBattle(battleManager);

            Assert.AreEqual(1, sidePanel.ActiveLeftCards.Count);
            Assert.AreEqual(2, sidePanel.ActiveRightCards.Count);
            Assert.AreSame(playerChar, sidePanel.ActiveLeftCards[0].BoundCharacter);
            Assert.AreSame(enemyA, sidePanel.ActiveRightCards[0].BoundCharacter);
            Assert.AreSame(enemyB, sidePanel.ActiveRightCards[1].BoundCharacter);

            // 우측 카드 이펙트 아이콘의 레이어(UI=5) 및 HorizontalInward 카드 앵커 확인
            BattleSideUnitCardUI enemyACard = sidePanel.ActiveRightCards[0];
            Assert.AreEqual(1, enemyACard.ActiveEffectItems.Count);
            CharacterEffectItemUI itemUI = enemyACard.ActiveEffectItems[0];
            Assert.AreEqual(panelGo.layer, itemUI.gameObject.layer);

            TooltipTrigger itemTrigger = itemUI.GetComponent<TooltipTrigger>();
            Assert.IsNotNull(itemTrigger);
            Assert.AreSame(enemyACard.transform as RectTransform, GetPrivateField<RectTransform>(itemTrigger, "anchorRectOverride"));
            Assert.AreEqual(TooltipPlacementMode.HorizontalInward, GetPrivateField<TooltipPlacementMode>(itemTrigger, "placementMode"));

            // enemyA 사망 시 OnTimelineDirty -> LateUpdate를 거쳐 우측 카드에서 제거 확인
            enemyA.TakeDamage(new DamageInfo { baseDamageAmount = 999f, damageMultiplier = 1f });
            InvokePrivateMethod(sidePanel, "LateUpdate");

            Assert.AreEqual(1, sidePanel.ActiveRightCards.Count);
            Assert.AreSame(enemyB, sidePanel.ActiveRightCards[0].BoundCharacter);
        }
        finally
        {
            battleManager.Dispose();
        }
    }

    [Test]
    public void ThreeWayHighlight_SyncsTimelineSideCardAndWorldSiblingOrder()
    {
        GameObject timelinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleTimelinePanel.prefab");
        GameObject sideHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/BattleSideUnitHUDPanel.prefab");
        GameObject statusBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterStatusBarUI.prefab");
        GameObject effectListPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterEffectListUI.prefab");
        GameObject intentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Battle/CharacterIntentUI.prefab");

        Assert.IsNotNull(timelinePrefab);
        Assert.IsNotNull(sideHudPrefab);
        Assert.IsNotNull(statusBarPrefab);
        Assert.IsNotNull(effectListPrefab);
        Assert.IsNotNull(intentPrefab);

        Canvas mainCanvas = CreateTestCanvas("MainTopCanvas");
        Canvas worldCanvas = CreateTestCanvas("WorldUICanvas");

        GameObject timelineGo = Track(UnityEngine.Object.Instantiate(timelinePrefab, mainCanvas.transform, false));
        BattleTimelinePanel timelinePanel = timelineGo.GetComponent<BattleTimelinePanel>();
        InvokePrivateMethod(timelinePanel, "Awake");

        GameObject sideHudGo = Track(UnityEngine.Object.Instantiate(sideHudPrefab, mainCanvas.transform, false));
        BattleSideUnitHUDPanel sidePanel = sideHudGo.GetComponent<BattleSideUnitHUDPanel>();
        InvokePrivateMethod(sidePanel, "Awake");

        Character enemyChar = CreateCharacter("Enemy", CharacterFaction.Enemy, 80f, 100f);
        CharacterUIController enemyUiCtrl = enemyChar.gameObject.AddComponent<CharacterUIController>();
        InvokePrivateMethod(enemyUiCtrl, "Awake");
        Character playerChar = CreateCharacter("Player", CharacterFaction.Player, 100f, 120f);
        CharacterUIController playerUiCtrl = playerChar.gameObject.AddComponent<CharacterUIController>();
        InvokePrivateMethod(playerUiCtrl, "Awake");

        // 월드 UI를 enemy 먼저, player 나중에 배치해 초기 형제 순서가 player가 뒤(앞선 렌더)가 되도록 구성
        CharacterEffectListUI enemyEffects = Track(UnityEngine.Object.Instantiate(effectListPrefab, worldCanvas.transform, false)).GetComponent<CharacterEffectListUI>();
        CharacterStatusBarUI enemyStatus = Track(UnityEngine.Object.Instantiate(statusBarPrefab, worldCanvas.transform, false)).GetComponent<CharacterStatusBarUI>();
        CharacterIntentUI enemyIntent = Track(UnityEngine.Object.Instantiate(intentPrefab, worldCanvas.transform, false)).GetComponent<CharacterIntentUI>();

        CharacterEffectListUI playerEffects = Track(UnityEngine.Object.Instantiate(effectListPrefab, worldCanvas.transform, false)).GetComponent<CharacterEffectListUI>();
        CharacterStatusBarUI playerStatus = Track(UnityEngine.Object.Instantiate(statusBarPrefab, worldCanvas.transform, false)).GetComponent<CharacterStatusBarUI>();

        BindCharacterWorldViews(enemyUiCtrl, enemyStatus, enemyEffects, enemyIntent);
        BindCharacterWorldViews(playerUiCtrl, playerStatus, playerEffects, null);

        Vector3 enemyInitialPos = enemyStatus.transform.position;

        var battleManager = new BattleManager();
        try
        {
            battleManager.InitBattle(
                new List<ICharacterController> { new StubCharacterController(playerChar) },
                new List<ICharacterController> { new StubCharacterController(enemyChar) });
            battleManager.StartCombat();

            timelinePanel.BindBattle(battleManager);
            InvokePrivateMethod(timelinePanel, "LateUpdate");
            sidePanel.BindBattle(battleManager);

            // 초기 상태에서는 playerStatus가 enemyStatus보다 뒤에 있음
            Assert.Less(enemyStatus.transform.GetSiblingIndex(), playerStatus.transform.GetSiblingIndex());

            BattleSideUnitCardUI enemyCard = sidePanel.ActiveRightCards[0];
            BattleSideUnitCardUI playerCard = sidePanel.ActiveLeftCards[0];

            // 사이드 적 카드 호버 -> 타임라인 슬롯 + 월드 UI SetAsLastSibling() 최상단 포커스 확인
            sidePanel.NotifyCardPointerEnter(enemyCard);

            Assert.IsTrue(enemyCard.IsHighlighted);
            Assert.IsFalse(playerCard.IsHighlighted);

            var activeSlots = GetPrivateField<System.Collections.IList>(timelinePanel, "_activeSlots");
            bool enemySlotHighlighted = false;
            for (int i = 0; i < activeSlots.Count; i++)
            {
                object slotInst = activeSlots[i];
                Character boundChar = slotInst.GetType().GetField("boundCharacter").GetValue(slotInst) as Character;
                BattleTimelineSlotUI slotUi = slotInst.GetType().GetField("ui").GetValue(slotInst) as BattleTimelineSlotUI;
                bool slotHl = GetPrivateField<bool>(slotUi, "_isHighlighted");
                if (boundChar == enemyChar)
                {
                    Assert.IsTrue(slotHl);
                    enemySlotHighlighted = true;
                }
                else
                {
                    Assert.IsFalse(slotHl);
                }
            }
            Assert.IsTrue(enemySlotHighlighted);

            Assert.Greater(enemyStatus.transform.GetSiblingIndex(), playerStatus.transform.GetSiblingIndex(), "호버 시 적 상태바가 SetAsLastSibling으로 최상단에 올라와야 합니다.");
            Assert.Greater(enemyStatus.transform.GetSiblingIndex(), enemyEffects.transform.GetSiblingIndex());
            Assert.Greater(enemyIntent.transform.GetSiblingIndex(), enemyStatus.transform.GetSiblingIndex());
            Assert.AreEqual(new Vector3(1.12f, 1.12f, 1.12f), enemyStatus.transform.localScale);
            Assert.AreEqual(enemyInitialPos, enemyStatus.transform.position, "호버 포커스 시 월드 Y좌표 이동(Y-Repulsion)은 없어야 합니다.");

            // 호버 해제 시 강조 및 스케일 원복
            sidePanel.NotifyCardPointerExit(enemyCard);
            Assert.IsFalse(enemyCard.IsHighlighted);
            Assert.AreEqual(Vector3.one, enemyStatus.transform.localScale);
        }
        finally
        {
            battleManager.Dispose();
        }
    }

    private static void BindCharacterWorldViews(
        CharacterUIController ctrl,
        CharacterStatusBarUI statusBar,
        CharacterEffectListUI effectList,
        CharacterIntentUI intentUI)
    {
        SetPrivateField(ctrl, "statusBarUI", statusBar);
        SetPrivateField(ctrl, "effectListUI", effectList);
        SetPrivateField(ctrl, "intentUI", intentUI);
        SetPrivateField(ctrl, "_isBattleActive", true);
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

    private static object InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}");
        return method.Invoke(target, args);
    }

    private static T GetStaticPrivateField<T>(Type targetType, string fieldName)
    {
        FieldInfo field = targetType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"Static field '{fieldName}' not found on {targetType.Name}");
        return (T)field.GetValue(null);
    }

    private static object InvokeStaticPrivateMethod(Type targetType, string methodName, params object[] args)
    {
        MethodInfo method = targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"Static method '{methodName}' not found on {targetType.Name}");
        return method.Invoke(null, args);
    }

    private static void RestoreIntPref(string key, bool hadKey, int previousValue)
    {
        if (hadKey)
        {
            PlayerPrefs.SetInt(key, previousValue);
        }
        else
        {
            PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
    }
}
