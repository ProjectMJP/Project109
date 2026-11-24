using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameItem.Types;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XLua;

/// <summary>
/// [39] 프로젝트 전체 아키텍처 점검 및 기술 부채 해소 항목에 대한 EditMode 회귀 테스트입니다.
/// 계층 역참조 분리(DI/ModObjectFactory/Input 허브), 세이브/로드 무결성, 게이지 UI 핫패스 무할당 캐싱을 검증합니다.
/// </summary>
[TestFixture]
public class ArchitectureAuditRegressionTests
{
    private const int TestSaveSlot = 99;

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private LuaManager _createdLuaManager;
    private int _originalSelectedSaveSlot;
    private string _backupSave99Json;
    private bool _hadExistingSave99;

    [SetUp]
    public void SetUp()
    {
        _originalSelectedSaveSlot = GameSceneManager.selectedSaveSlot;
        string savePath = GetTestSavePath();
        _hadExistingSave99 = File.Exists(savePath);
        _backupSave99Json = _hadExistingSave99 ? File.ReadAllText(savePath, System.Text.Encoding.UTF8) : null;
        if (_hadExistingSave99)
        {
            File.Delete(savePath);
        }
        SaveSystem.InvalidateAllCaches();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            ModObjectFactory.DisposeCachedDescriptions();
            if (_createdLuaManager != null)
            {
                _createdLuaManager.Dispose();
                _createdLuaManager = null;
            }
        }
        finally
        {
            GameSceneManager.selectedSaveSlot = _originalSelectedSaveSlot;

            string savePath = GetTestSavePath();
            if (_hadExistingSave99 && _backupSave99Json != null)
            {
                File.WriteAllText(savePath, _backupSave99Json, System.Text.Encoding.UTF8);
            }
            else if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
            SaveSystem.InvalidateAllCaches();

            InvokeStaticMethod<LuaManager>("ResetStaticState");
            InvokeStaticMethod<ModLoader>("ResetStaticState");
            InvokeStaticMethod(typeof(ModObjectFactory), "ResetStaticState");

            SetStaticAutoProperty<GameSceneManager>("instance", null);
            SetStaticAutoProperty<RunManager>("instance", null);
            SetStaticAutoProperty<GameItemRewardManager>("instance", null);
            SetStaticAutoProperty<CameraController>("instance", null);
            SetStaticAutoProperty<GameMap>("current", null);
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
        }
    }

    private static string GetTestSavePath()
    {
        return Path.Combine(Application.persistentDataPath, $"save_{TestSaveSlot}.json");
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private LuaManager CreateIsolatedLuaManager()
    {
        InvokeStaticMethod<LuaManager>("ResetStaticState");
        InvokeStaticMethod<ModLoader>("ResetStaticState");
        InvokeStaticMethod(typeof(ModObjectFactory), "ResetStaticState");

        GameObject luaGo = Track(new GameObject("TestLuaManager"));
        LuaManager luaManager = luaGo.AddComponent<LuaManager>();
        SetStaticField<LuaManager>("_instance", luaManager);
        SetStaticField<LuaManager>("_isShuttingDown", false);
        InvokePrivateMethod(luaManager, "InitLuaEnv");
        _createdLuaManager = luaManager;
        return luaManager;
    }

    // =========================================================================
    // 1. GameItemRewardManager: ModLoader 비의존 DB 초기화 주입 검증
    // =========================================================================
    [Test]
    public void GameItemRewardManager_InitializeDatabases_IsolatesPoolWithoutModLoader()
    {
        InvokeStaticMethod<ModLoader>("ResetStaticState");

        GameObject rewardGo = Track(new GameObject("TestRewardManager"));
        GameItemRewardManager rewardManager = rewardGo.AddComponent<GameItemRewardManager>();
        InvokePrivateMethod(rewardManager, "Awake");

        Assert.IsFalse(rewardManager.IsDatabasesInitialized);

        var cards = new Dictionary<string, CardData>
        {
            ["StdCommon"] = new CardData { cardName = "StdCommon", rarity = CardRarity.Common, obtainSource = ItemObtainSource.Standard, isUpgraded = false },
            ["StarterIgnored"] = new CardData { cardName = "StarterIgnored", rarity = CardRarity.Common, obtainSource = ItemObtainSource.Starter, isUpgraded = false },
            ["UpgradedIgnored"] = new CardData { cardName = "UpgradedIgnored", rarity = CardRarity.Common, obtainSource = ItemObtainSource.Standard, isUpgraded = true }
        };

        var relics = new Dictionary<string, RelicData>
        {
            ["StdRelic"] = new RelicData { relicName = "StdRelic", rarity = RelicRarity.Common, obtainSource = ItemObtainSource.Standard },
            ["BossRelic"] = new RelicData { relicName = "BossRelic", rarity = RelicRarity.Boss, obtainSource = ItemObtainSource.BossOnly },
            ["EventRelicIgnored"] = new RelicData { relicName = "EventRelicIgnored", rarity = RelicRarity.Rare, obtainSource = ItemObtainSource.EventOnly }
        };

        var dropTables = new Dictionary<string, DropTableData>
        {
            ["TestBossRelicPool"] = new DropTableData
            {
                commonWeight = 50,
                uncommonWeight = 50 // Relic 추첨 시 uncommonWeight가 Boss 가중치로 매핑됨
            }
        };

        rewardManager.InitializeDatabases(cards, relics, dropTables);
        Assert.IsTrue(rewardManager.IsDatabasesInitialized);

        List<CardData> pickedCards = rewardManager.GetUniqueRandomCards(null, 5);
        Assert.AreEqual(1, pickedCards.Count);
        Assert.AreEqual("StdCommon", pickedCards[0].cardName);

        // 기본 테이블(Boss 가중치 0)에서는 Standard 일반 유물만 추첨됨
        List<RelicData> defaultRelics = rewardManager.GetUniqueRandomRelics(null, 5);
        Assert.AreEqual(1, defaultRelics.Count);
        Assert.AreEqual("StdRelic", defaultRelics[0].relicName);

        // 보스 가중치가 부여된 테이블에서는 Standard + BossOnly(Boss)가 추첨되고 EventOnly는 배제됨
        List<RelicData> bossPoolRelics = rewardManager.GetUniqueRandomRelics("TestBossRelicPool", 5);
        Assert.AreEqual(2, bossPoolRelics.Count);
        Assert.IsFalse(bossPoolRelics.Exists(r => r.relicName == "EventRelicIgnored"));
    }

    // =========================================================================
    // 2. ModObjectFactory: 정적 카드 설명 미리보기 인스턴스 캐싱 및 Lua 종료 일괄 회수 검증
    // =========================================================================
    [Test]
    public void ModObjectFactory_GetCardDescription_CachesPreviewInstanceAndDisposesOnShutdown()
    {
        LuaManager luaManager = CreateIsolatedLuaManager();

        object[] protoRes = luaManager.luaEnv.DoString(@"
            _G.__auditInitCount = 0
            return {
                OnInit = function(self, card)
                    _G.__auditInitCount = _G.__auditInitCount + 1
                end,
                GetDescription = function(self, card, rawDesc)
                    return rawDesc .. ' [OK]'
                end
            }
        ");

        CardData cardData = new CardData
        {
            cardName = "FactoryPreviewCard",
            description = "피해 12",
            luaPrototype = protoRes[0] as LuaTable
        };

        string desc1 = ModObjectFactory.GetCardDescription(cardData);
        string desc2 = ModObjectFactory.GetCardDescription(cardData);

        Assert.AreEqual("피해 12 [OK]", desc1);
        Assert.AreEqual(desc1, desc2);

        int initCount = luaManager.luaEnv.Global.Get<int>("__auditInitCount");
        Assert.AreEqual(1, initCount, "동일 CardData 설명 조회 시 미리보기 Card 인스턴스는 1회만 생성되어 캐싱되어야 합니다.");

        cardData.DisposeLuaResources(true);
        Assert.DoesNotThrow(() => luaManager.Dispose());
        _createdLuaManager = null;
    }

    // =========================================================================
    // 3. PlayerInputController ↔ GameMap ↔ CameraController: 물리 레이어 없는 팬 경계 및 포커스 위임 검증
    // =========================================================================
    [Test]
    public void CameraPanValidatorAndFocus_RoutesThroughPlayerInputControllerWithoutPhysicsLayers()
    {
        GameObject inputGo = Track(new GameObject("TestInputController"));
        PlayerInputController input = inputGo.AddComponent<PlayerInputController>();
        InvokePrivateMethod(input, "Awake");

        GameObject mapGo = Track(new GameObject("TestGameMap"));
        GameMap map = mapGo.AddComponent<GameMap>();
        map.mapColumn = 6;
        map.mapRow = 6;
        InvokePrivateMethod(map, "Awake");

        input.SetCameraPanRayValidator(map.ValidateCameraPanRay);

        Assert.IsTrue(input.ValidateCameraPanRay(new Ray(new Vector3(2f, 10f, 2f), Vector3.down)));
        Assert.IsFalse(input.ValidateCameraPanRay(new Ray(new Vector3(500f, 10f, 500f), Vector3.down)), "맵 허용 마진 바깥의 Ray는 차단되어야 합니다.");
        Assert.IsFalse(input.ValidateCameraPanRay(new Ray(new Vector3(2f, 10f, 2f), Vector3.up)), "지면 반대 방향 Ray는 차단되어야 합니다.");

        GameObject camGo = Track(new GameObject("TestMainCamera"));
        camGo.tag = "MainCamera";
        camGo.AddComponent<Camera>();
        CameraController camCtrl = camGo.AddComponent<CameraController>();
        InvokePrivateMethod(camCtrl, "Awake");
        InvokePrivateMethod(camCtrl, "OnEnable");

        Assert.IsTrue(input.RequestCameraFocus(new Vector3(3f, 0f, 4f)));

        InvokePrivateMethod(camCtrl, "OnDisable");
        Assert.IsFalse(input.RequestCameraFocus(new Vector3(3f, 0f, 4f)), "구독 해제 후에는 RequestCameraFocus가 false를 반환해야 합니다.");
    }

    // =========================================================================
    // 4. SaveSystem: 1회성 유물 중복 차단, 유물 카운터·상점 슬롯 복원, 캐시 오염 차단 및 패배 리셋 검증
    // =========================================================================
    [Test]
    public void SaveSystem_RoundTrip_PreservesRelicCounter_BlocksDuplicateOneShotRelics_AndIsolatesCache()
    {
        CreateIsolatedLuaManager();
        ModLoader.Instance.LoadAllMods();

        GameObject rewardGo = Track(new GameObject("TestRewardManager"));
        GameItemRewardManager rewardManager = rewardGo.AddComponent<GameItemRewardManager>();
        InvokePrivateMethod(rewardManager, "Awake");
        rewardManager.InitializeDatabases(
            ModLoader.Instance.CardDatabase,
            ModLoader.Instance.RelicDatabase,
            ModLoader.Instance.DropTableDatabase);

        GameSceneManager.selectedSaveSlot = TestSaveSlot;
        GameObject gsmGo = Track(new GameObject("TestGameSceneManager"));
        GameSceneManager gsm = gsmGo.AddComponent<GameSceneManager>();
        InvokePrivateMethod(gsm, "Awake");
        SetAutoProperty(gsm, "isInDungeon", true);

        GameObject playerCharGo = Track(new GameObject("TestPlayerChar"));
        Character playerChar = playerCharGo.AddComponent<Character>();
        InvokePrivateMethod(playerChar, "Awake");
        playerChar.faction = CharacterFaction.Player;
        playerChar.InitializeStat(new CharacterStat { maxHealth = 100f, maxStamina = 100f, staminaRegenPerSecond = 20f, moveRange = 3 });
        playerChar.curHealth = 50f;

        Player player = new Player(playerChar);
        gsm.player = player;
        player.playerStat.InGameCurrencyGold = 100;
        player.playerStat.InGameCurrencyMemorySharp = 77;
        player.playerStat.ShopRerollCount = 2;
        player.UnlockStarterKit("unlocked_test_kit");

        // 1회성 즉시 발동 유물 장착: 금덩이(+400G -> 500G), 잘 구워진 스테이크(+20 HP -> 70 HP)
        Relic goldNugget = player.AddRelic("금덩이");
        Relic steak = player.AddRelic("잘 구워진 스테이크");
        Assert.IsNotNull(goldNugget);
        Assert.IsNotNull(steak);
        goldNugget.Counter = 4;

        Assert.AreEqual(500, player.playerStat.InGameCurrencyGold);
        Assert.AreEqual(70f, playerChar.curHealth, 0.01f);

        // 활성 상점 슬롯 구성
        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        InvokePrivateMethod(runManager, "Awake");

        ExploreMapData mapData = new ExploreMapData();
        ExploreFloorData floor0 = new ExploreFloorData();
        ExploreNode shopNode = new ExploreNode(0, 0);
        shopNode.encounterData.locationType = LocationType.Temple;
        shopNode.encounterData.encountType = EncountType.Store;
        floor0.nodes.Add(shopNode);
        mapData.floors.Add(floor0);
        runManager.exploreMapManager.RestoreMapData(mapData);
        runManager.currentExploreNode = shopNode;

        CardData anyCard = null;
        foreach (CardData cd in ModLoader.Instance.CardDatabase.Values)
        {
            anyCard = cd;
            break;
        }
        Assert.IsNotNull(anyCard);

        GameObject shopGo = Track(new GameObject("TestShopNPC"));
        ShopNPC shop = shopGo.AddComponent<ShopNPC>();
        shop.AddCardEntry(anyCard, basePriceOverride: 88, isOnSale: true, discountRate: 0.5f);
        ShopCardEntry soldEntry = shop.AddCardEntry(anyCard, basePriceOverride: 64, isOnSale: false, discountRate: 0f);
        soldEntry.isSold = true;
        runManager.SetActiveNodeShop(shop);

        // 저장 실행
        SaveSystem.SaveGame(runManager, TestSaveSlot);

        // JSON 캐시 인메모리 오염 차단 검증
        Assert.IsTrue(SaveSystem.TryLoadGameData(TestSaveSlot, out RunSaveData mutatedCopy));
        mutatedCopy.playerGold = 999999;
        mutatedCopy.playerRelicStates.Clear();

        Assert.IsTrue(SaveSystem.TryLoadGameData(TestSaveSlot, out RunSaveData cleanCopy));
        Assert.AreEqual(500, cleanCopy.playerGold, "캐시에서 반환된 RunSaveData 수정이 후속 로드 결과를 오염시키면 안 됩니다.");
        Assert.AreEqual(2, cleanCopy.playerRelicStates.Count);

        // 신규 Player 및 신규 ShopNPC에 복원 시 1회성 골드/힐 중복 미발생 및 카운터/상점 슬롯 일치 검증
        GameObject restoredCharGo = Track(new GameObject("RestoredPlayerChar"));
        Character restoredChar = restoredCharGo.AddComponent<Character>();
        InvokePrivateMethod(restoredChar, "Awake");
        restoredChar.faction = CharacterFaction.Player;
        restoredChar.InitializeStat(new CharacterStat { maxHealth = 100f, maxStamina = 100f, staminaRegenPerSecond = 20f, moveRange = 3 });
        Player restoredPlayer = new Player(restoredChar);

        SaveSystem.RestorePlayerData(cleanCopy, restoredPlayer);
        Assert.AreEqual(500, restoredPlayer.playerStat.InGameCurrencyGold, "세이브 복원 시 금덩이(+400G)가 중복 발동하면 안 됩니다.");
        Assert.AreEqual(70f, restoredChar.curHealth, 0.01f, "세이브 복원 시 잘 구워진 스테이크(+20 HP)가 중복 발동하면 안 됩니다.");
        Assert.AreEqual(2, restoredPlayer.playerStat.ShopRerollCount);
        Assert.AreEqual(4, restoredPlayer.GetSpecificRelic("금덩이").Counter);

        GameObject restoredShopGo = Track(new GameObject("RestoredShopNPC"));
        ShopNPC restoredShop = restoredShopGo.AddComponent<ShopNPC>();
        Assert.IsTrue(restoredShop.TryRestoreFromSaveState(cleanCopy.activeShopState));
        Assert.AreEqual(2, restoredShop.cardEntries.Count);
        Assert.IsTrue(restoredShop.IsCardSlotOnSale(0));
        Assert.AreEqual(44, restoredShop.GetCardSlotPrice(0, restoredPlayer));
        Assert.IsTrue(restoredShop.IsCardSlotPurchased(1));

        // 전투 패배 후 타이틀 복귀 정산 시 던전 세이브 즉시 덮어쓰기 및 영구 재화/해금 보존 검증
        // (참고: Unity JsonUtility는 [Serializable] 참조 필드를 빈 인스턴스로 생성하므로 isValid == false로 검증)
        gsm.FinalizeDefeatedRunState(saveImmediately: true);
        Assert.IsTrue(SaveSystem.TryLoadGameData(TestSaveSlot, out RunSaveData afterDefeat));
        Assert.IsFalse(afterDefeat.isInDungeon);
        Assert.IsTrue(afterDefeat.activeShopState == null || !afterDefeat.activeShopState.isValid);
        Assert.AreEqual(0, afterDefeat.playerShopRerollCount);
        Assert.AreEqual(77, afterDefeat.playerMemorySharp);
        CollectionAssert.Contains(afterDefeat.unlockedStarterKitIds, "unlocked_test_kit");

        restoredPlayer.Dispose();
    }

    // =========================================================================
    // 5. GaugeUI.RefreshNumeric: 동일 정수 구간 반복 호출 시 문자열 재포맷 생략 검증
    // =========================================================================
    [Test]
    public void GaugeUI_RefreshNumeric_SkipsRedundantStringFormattingWhenIntegersUnchanged()
    {
        GameObject gaugeGo = Track(new GameObject("TestGauge", typeof(RectTransform), typeof(Image)));
        GameObject textGo = Track(new GameObject("GaugeText", typeof(RectTransform), typeof(TextMeshProUGUI)));
        textGo.transform.SetParent(gaugeGo.transform, false);

        Image fillImg = gaugeGo.GetComponent<Image>();
        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        GaugeUI gauge = gaugeGo.AddComponent<GaugeUI>();
        InvokePrivateMethod(gauge, "Awake");

        gauge.RefreshNumeric(0.51f, 6, 10);
        Assert.AreEqual(1, gauge.NumericTextFormatCount);
        Assert.AreEqual("6 / 10", tmp.text);

        // 소수 비율만 바뀌고 표시 정수가 동일한 프레임에서는 문자열 포맷팅이 수행되지 않아야 함
        gauge.RefreshNumeric(0.55f, 6, 10);
        gauge.RefreshNumeric(0.59f, 6, 10);
        Assert.AreEqual(0.59f, fillImg.fillAmount, 0.001f);
        Assert.AreEqual(1, gauge.NumericTextFormatCount);

        // 실드 또는 수치 변경 시 즉시 갱신되어야 함
        gauge.RefreshNumeric(0.59f, 6, 10, shieldValue: 4);
        Assert.AreEqual(2, gauge.NumericTextFormatCount);
        Assert.AreEqual("(4) 6 / 10", tmp.text);

        gauge.RefreshNumeric(0.59f, 6, 10, shieldValue: 0);
        Assert.AreEqual(3, gauge.NumericTextFormatCount);
        Assert.AreEqual("6 / 10", tmp.text);
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} 메서드를 찾을 수 없습니다.");
        method.Invoke(target, args);
    }

    private static void InvokeStaticMethod<T>(string methodName)
    {
        InvokeStaticMethod(typeof(T), methodName);
    }

    private static void InvokeStaticMethod(Type targetType, string methodName)
    {
        MethodInfo method = targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"{targetType.Name}.{methodName} 정적 메서드를 찾을 수 없습니다.");
        method.Invoke(null, null);
    }

    private static void SetStaticField<T>(string fieldName, object value)
    {
        FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"{typeof(T).Name}.{fieldName} 정적 필드를 찾을 수 없습니다.");
        field.SetValue(null, value);
    }

    private static void SetStaticAutoProperty<T>(string propertyName, object value)
    {
        FieldInfo backingField = typeof(T).GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(backingField, $"{typeof(T).Name}.{propertyName} 정적 자동 프로퍼티 백킹 필드를 찾을 수 없습니다.");
        backingField.SetValue(null, value);
    }

    private static void SetAutoProperty(object target, string propertyName, object value)
    {
        FieldInfo backingField = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(backingField, $"{target.GetType().Name}.{propertyName} 자동 프로퍼티 백킹 필드를 찾을 수 없습니다.");
        backingField.SetValue(target, value);
    }
}
