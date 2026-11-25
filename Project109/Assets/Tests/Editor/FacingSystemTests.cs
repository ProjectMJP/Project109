using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CardTypes;
using EventStructs;
using NUnit.Framework;
using UnityEngine;
using XLua;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

[TestFixture]
public class FacingSystemTests
{
    private sealed class BackArcConditionalBonusListener : IOnBeforeDealDamage, IOnAfterDealDamage
    {
        public int BeforeBackArcHits;
        public int AfterDirectBackHits;
        public float BonusDamageOnBackArc;

        public void OnBeforeDealDamage(ref DamageInfo info)
        {
            if (info.IsBackArc())
            {
                BeforeBackArcHits++;
                info.baseDamageAmount += BonusDamageOnBackArc;
            }
        }

        public void OnAfterDealDamage(DamageInfo info)
        {
            if (info.IsDirectBack())
            {
                AfterDirectBackHits++;
            }
        }
    }

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<LuaTable> _createdLuaTables = new List<LuaTable>();
    private readonly List<Card> _createdCards = new List<Card>();
    private LuaEnv _luaEnv;
    private IDeserializer _yamlDeserializer;

    [SetUp]
    public void SetUp()
    {
        CombatFacing.ClearActiveCastSession();
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _luaEnv = new LuaEnv();
        _luaEnv.DoString(@"
            EventStructs = {
                DamageInfo = CS.EventStructs.DamageInfo,
                DamageFlag = CS.EventStructs.DamageFlag,
                FacingZone = CS.FacingZone,
                FacingHitRelation = CS.FacingHitRelation,
                FacingOriginMode = CS.FacingOriginMode,
            }
            UnityEngine = {
                Vector2Int = CS.UnityEngine.Vector2Int,
            }
            LookDirection = CS.LookDirection
            HexMath = CS.HexMath
            CombatFacing = CS.CombatFacing
            CS = nil
        ");
    }

    [TearDown]
    public void TearDown()
    {
        CombatFacing.ClearActiveCastSession();

        for (int i = _createdCards.Count - 1; i >= 0; i--)
        {
            if (_createdCards[i] != null && !_createdCards[i].IsDisposed)
            {
                _createdCards[i].Dispose();
            }
        }
        _createdCards.Clear();

        for (int i = _createdLuaTables.Count - 1; i >= 0; i--)
        {
            if (_createdLuaTables[i] != null)
            {
                _createdLuaTables[i].Dispose();
            }
        }
        _createdLuaTables.Clear();

        if (_luaEnv != null)
        {
            _luaEnv.FullGc();
            _luaEnv.Dispose();
            _luaEnv = null;
        }

        SetStaticAutoProperty<RunManager>("instance", null);
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

    private GameMap CreateTestMap(int width = 8, int height = 8)
    {
        GameObject mapGo = Track(new GameObject("FacingTestGameMap"));
        GameMap gameMap = mapGo.AddComponent<GameMap>();
        InvokePrivateMethod(gameMap, "Awake");

        GameObject tilePrefab = Track(new GameObject("FacingTilePrefab", typeof(Tile)));
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

    private Character CreateTestCharacter(string name, CharacterFaction faction, Tile tile, LookDirection facing)
    {
        GameObject go = Track(new GameObject(name));
        Character character = go.AddComponent<Character>();
        InvokePrivateMethod(character, "Awake");
        character.faction = faction;
        character.InitializeStat(new CharacterStat
        {
            maxHealth = 100f,
            maxStamina = 100f,
            staminaRegenPerSecond = 10f,
            moveRange = 3
        });
        if (tile != null)
        {
            go.transform.position = tile.transform.position;
            character.characterMove.SetCurrentTile(tile, triggerTileHazards: false);
        }
        character.characterMove.SetFacingDirection(facing, true);
        return character;
    }

    private Effect AttachCoreEffect(Character target, string effectId, int stack, float duration)
    {
        string yamlPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "YAML", "Effects", effectId + ".yaml");
        string luaPath = Path.Combine(Application.streamingAssetsPath, "Mods", "Core", "Scripts", "Effects", effectId + ".lua");

        EffectData data = _yamlDeserializer.Deserialize<EffectData>(File.ReadAllText(yamlPath));
        object[] res = _luaEnv.DoString(File.ReadAllText(luaPath), effectId);
        LuaTable table = res[0] as LuaTable;
        _createdLuaTables.Add(table);

        Effect effect = new Effect(data, table);
        target.effectManager.AddEffect(target, effect, stack, duration);
        return effect;
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"메서드 '{methodName}'를 {target.GetType().Name}에서 찾을 수 없습니다.");
        method.Invoke(target, null);
    }

    private static void SetStaticAutoProperty<T>(string propName, object value)
    {
        PropertyInfo prop = typeof(T).GetProperty(propName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        prop?.SetValue(null, value);
    }

    [Test]
    public void HexMathAndXLua_FourFacingZonesAndTravelVector_EvaluateAccurately()
    {
        // 피격자가 East를 바라볼 때:
        // East(정전면0), NorthEast/SouthEast(전측1,5), NorthWest/SouthWest(후측2,4), West(정후면3)
        Assert.IsTrue(HexMath.IsInDirectFront(LookDirection.East, LookDirection.East));
        Assert.IsFalse(HexMath.IsInDirectFront(LookDirection.East, LookDirection.NorthEast));

        Assert.IsTrue(HexMath.IsInFrontArc(LookDirection.East, LookDirection.East));
        Assert.IsTrue(HexMath.IsInFrontArc(LookDirection.East, LookDirection.NorthEast));
        Assert.IsTrue(HexMath.IsInFrontArc(LookDirection.East, LookDirection.SouthEast));
        Assert.IsFalse(HexMath.IsInFrontArc(LookDirection.East, LookDirection.NorthWest));

        Assert.IsTrue(HexMath.IsInDirectBack(LookDirection.East, LookDirection.West));
        Assert.IsFalse(HexMath.IsInDirectBack(LookDirection.East, LookDirection.SouthWest));

        Assert.IsTrue(HexMath.IsInBackArc(LookDirection.East, LookDirection.West));
        Assert.IsTrue(HexMath.IsInBackArc(LookDirection.East, LookDirection.NorthWest));
        Assert.IsTrue(HexMath.IsInBackArc(LookDirection.East, LookDirection.SouthWest));
        Assert.IsFalse(HexMath.IsInBackArc(LookDirection.East, LookDirection.East));

        // 투사체가 East로 진행(입사) -> 공격 원점은 West -> East를 보는 피격자는 정후면 피격
        Assert.IsTrue(HexMath.IsTravelDirectBack(LookDirection.East, LookDirection.East));
        Assert.IsTrue(HexMath.IsTravelBackArc(LookDirection.East, LookDirection.East));
        Assert.IsFalse(HexMath.IsTravelFrontArc(LookDirection.East, LookDirection.East));

        // 투사체가 West로 진행(입사) -> 공격 원점은 East -> East를 보는 피격자는 정전면 피격
        Assert.IsTrue(HexMath.IsTravelDirectFront(LookDirection.East, LookDirection.West));
        Assert.IsTrue(HexMath.IsTravelFrontArc(LookDirection.East, LookDirection.West));

        // XLua 샌드박스에서 4종 판정 API, Enum, DamageInfo 헬퍼 호출 검증
        object[] luaResult = _luaEnv.DoString(@"
            local backMatch = HexMath.IsInDirectBack(LookDirection.East, LookDirection.West)
            local travelBack = HexMath.IsTravelDirectBack(LookDirection.East, LookDirection.East)
            local rel = HexMath.EvaluateFacingRelation(LookDirection.East, LookDirection.NorthWest)
            local inBackArc = HexMath.IsFacingZoneMatch(rel, EventStructs.FacingZone.BackArc)
            local mult = CombatFacing.DirectBackDamageMultiplier
            return backMatch, travelBack, inBackArc, mult
        ");

        Assert.IsTrue((bool)luaResult[0]);
        Assert.IsTrue((bool)luaResult[1]);
        Assert.IsTrue((bool)luaResult[2]);
        Assert.AreEqual(CombatFacing.DirectBackDamageMultiplier, Convert.ToSingle(luaResult[3]), 0.001f);
    }

    [Test]
    public void MeleeAttack_DirectBackDealsOnePointFiveMultiplier_ForBothPlayerAndEnemy()
    {
        GameMap gameMap = CreateTestMap(6, 6);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        // 1. 플레이어가 적의 정후면(West)에서 East를 보는 적을 공격 -> 10 * 1.5 = 15 피해
        Character player = CreateTestCharacter("Player", CharacterFaction.Player, tileMap[1][2], LookDirection.East);
        Character enemy = CreateTestCharacter("Enemy", CharacterFaction.Enemy, tileMap[2][2], LookDirection.East);

        player.TakeDamage(new DamageInfo(null, player, 0f, DamageFlag.Normal)); // 무해 호출 확인
        enemy.TakeDamage(new DamageInfo(player, enemy, 10f, DamageFlag.Normal));
        Assert.AreEqual(85f, enemy.curHealth, 0.001f, "정후면 공격은 1.5배 피해를 입혀야 합니다.");
        Assert.AreEqual(LookDirection.West, enemy.characterMove.facingDirection, "단독 피격 직후 적은 공격자 방향(West)으로 회전해야 합니다.");

        // 2. 이제 적이 West(플레이어 방향)를 바라보는 상태에서 플레이어가 다시 공격 -> 정전면 1.0배 = 10 피해
        enemy.TakeDamage(new DamageInfo(player, enemy, 10f, DamageFlag.Normal));
        Assert.AreEqual(75f, enemy.curHealth, 0.001f, "정전면 공격은 1.0배 피해를 입혀야 합니다.");

        // 3. 측면(FrontSide / BackSide)은 1.0배 피해 확인
        Character sideEnemy = CreateTestCharacter("SideEnemy", CharacterFaction.Enemy, tileMap[3][3], LookDirection.East);
        Vector2Int nwNeighbor = HexMath.GetOddRNeighbor(new Vector2Int(3, 3), LookDirection.NorthWest);
        Character flanker = CreateTestCharacter("Flanker", CharacterFaction.Player, tileMap[nwNeighbor.x][nwNeighbor.y], LookDirection.SouthEast);
        sideEnemy.TakeDamage(new DamageInfo(flanker, sideEnemy, 10f, DamageFlag.Normal));
        Assert.AreEqual(90f, sideEnemy.curHealth, 0.001f, "후측면(±120°)은 전역 1.5배 보정 대상이 아닙니다.");

        // 4. 적이 플레이어의 정후면(West)에서 East를 보는 플레이어를 공격 -> 10 * 1.5 = 15 피해 (플레이어·적 공통)
        Character backstabberEnemy = CreateTestCharacter("Backstabber", CharacterFaction.Enemy, tileMap[0][2], LookDirection.East);
        player.characterMove.SetFacingDirection(LookDirection.East, true);
        player.TakeDamage(new DamageInfo(backstabberEnemy, player, 10f, DamageFlag.Normal));
        Assert.AreEqual(85f, player.curHealth, 0.001f, "적의 정후면 공격도 플레이어에게 1.5배 피해를 입혀야 합니다.");
        Assert.AreEqual(LookDirection.West, player.characterMove.facingDirection, "플레이어도 피격 후 공격자 방향으로 돌아봐야 합니다.");
    }

    [Test]
    public void ProjectileTravelDirection_EvaluatesByTravelVectorRatherThanCasterCoord()
    {
        GameMap gameMap = CreateTestMap(8, 8);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        // 시전자는 (1,2), 폭 3 투사체는 East 방향으로 직진.
        // 피격자는 (3,3)에서 East를 바라보고 있어 좌표 벡터 기준으로는 SouthWest(후측)에 가깝더라도,
        // 투사체 진행(입사) 방향이 East이므로 피격자 입장에서 정후면(West) 입사로 1.5배 판정되어야 함.
        Character caster = CreateTestCharacter("Caster", CharacterFaction.Player, tileMap[1][2], LookDirection.East);
        Character target = CreateTestCharacter("Target", CharacterFaction.Enemy, tileMap[3][3], LookDirection.East);

        DamageInfo projInfo = new DamageInfo(caster, target, 10f, DamageFlag.Normal);
        projInfo.SetIncomingTravelDirection(LookDirection.East);

        target.TakeDamage(projInfo);
        Assert.AreEqual(85f, target.curHealth, 0.001f, "원거리/직선 투사체는 진행(입사) 방향 기준으로 정후면 1.5배가 판정되어야 합니다.");
        Assert.AreEqual(LookDirection.West, target.characterMove.facingDirection, "투사체 피격 후 진행 방향의 반대쪽(West)을 바라봐야 합니다.");
    }

    [Test]
    public void AreaGroundOriginCoord_EvaluatesFromAoECenterAndTurnsTargetTowardCenter()
    {
        GameMap gameMap = CreateTestMap(8, 8);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        // 공격자는 적의 앞쪽(East, (5,2))에 서 있지만, 광역(AoE) 중심점을 적의 등 뒤(West, (1,2))에 지정!
        Character caster = CreateTestCharacter("Caster", CharacterFaction.Player, tileMap[5][2], LookDirection.West);
        Character target = CreateTestCharacter("Target", CharacterFaction.Enemy, tileMap[2][2], LookDirection.East);

        DamageInfo aoeInfo = new DamageInfo(caster, target, 10f, DamageFlag.Normal);
        aoeInfo.SetOriginCoord(new Vector2Int(1, 2));

        target.TakeDamage(aoeInfo);
        Assert.AreEqual(85f, target.curHealth, 0.001f, "광역 공격은 시전자 위치가 아니라 AoE 중심점((1,2)) 기준으로 정후면 1.5배를 판정해야 합니다.");
        Assert.AreEqual(LookDirection.West, target.characterMove.facingDirection, "AoE 피격 후 대상은 AoE 중심점 방향(West)으로 회전해야 합니다.");

        // AoE 중심점과 동일 타일에 있는 대상은 방향 보정 없음(1.0배)
        Character centerTarget = CreateTestCharacter("CenterTarget", CharacterFaction.Enemy, tileMap[1][4], LookDirection.East);
        DamageInfo centerInfo = new DamageInfo(caster, centerTarget, 10f, DamageFlag.Normal);
        centerInfo.SetOriginCoord(new Vector2Int(1, 4));
        centerTarget.TakeDamage(centerInfo);
        Assert.AreEqual(90f, centerTarget.curHealth, 0.001f);
    }

    [Test]
    public void MultiHitCastSession_PreservesInitialFacingDuringHitsAndRotatesOnlyOnCastCompletion()
    {
        GameMap gameMap = CreateTestMap(6, 6);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        Character caster = CreateTestCharacter("Caster", CharacterFaction.Player, tileMap[1][2], LookDirection.East);
        Character target = CreateTestCharacter("Target", CharacterFaction.Enemy, tileMap[2][2], LookDirection.East);

        var listener = new BackArcConditionalBonusListener { BonusDamageOnBackArc = 2f };
        caster.eventBus.Add<IOnBeforeDealDamage>(listener);
        caster.eventBus.Add<IOnAfterDealDamage>(listener);

        CardData multiHitData = new CardData
        {
            cardName = "연속 정후면 베기",
            cardType = "Attack",
            targetType = "Directional",
            targetMinDistance = 1,
            targetMaxDistance = 1,
            stamina = 0,
            baseValues = new Dictionary<string, float> { { "damage", 10f } }
        };

        LuaTable luaProto = _luaEnv.DoString(@"
            local card = {}
            function card:Execute(cardInfo)
                local caster = cardInfo.caster
                local target = cardInfo.targets[0]
                local d1 = EventStructs.DamageInfo(caster, target, 10, EventStructs.DamageFlag.Normal)
                local d2 = EventStructs.DamageInfo(caster, target, 10, EventStructs.DamageFlag.Normal)
                target:TakeDamage(d1)
                target:TakeDamage(d2)
            end
            return card
        ")[0] as LuaTable;
        _createdLuaTables.Add(luaProto);
        Card multiHitCard = new Card(multiHitData, caster, luaProto, -1);
        _createdCards.Add(multiHitCard);

        GameObject runGo = Track(new GameObject("TestRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        SetStaticAutoProperty<RunManager>("instance", runManager);
        BattleManager bm = new BattleManager();
        bm.Initialize();
        typeof(RunManager).GetProperty("battleManager", BindingFlags.Instance | BindingFlags.Public).SetValue(runManager, bm);
        typeof(BattleManager).GetProperty("battleState", BindingFlags.Instance | BindingFlags.Public).SetValue(bm, BattleState.Combat);

        try
        {
            CardPlayAction action = new CardPlayAction(caster, multiHitCard, new List<Character> { target }, new Vector2Int(2, 2));
            IEnumerator routine = action.ExecuteRoutine();

            // 첫 yield(_playDelays) 직후에는 아직 카드 Execute 전
            Assert.IsTrue(routine.MoveNext());
            Assert.AreEqual(LookDirection.East, target.characterMove.facingDirection);

            // 두 번째 yield(_postPlayDelays): card.Execute() 완료 직후, 아직 시전 완료 전!
            Assert.IsTrue(routine.MoveNext());

            // 2타 모두 최초 방향(정후면) 유지 + BackArc 조건부 +2 보너스 -> 각 (10 + 2) * 1.5 = 18씩 총 36 피해
            Assert.AreEqual(64f, target.curHealth, 0.001f, "다단히트 2타 모두 정후면 1.5배 및 조건부 후방 보너스가 유지되어야 합니다.");
            Assert.AreEqual(2, listener.BeforeBackArcHits);
            Assert.AreEqual(2, listener.AfterDirectBackHits);
            Assert.AreEqual(LookDirection.East, target.characterMove.facingDirection, "시전 완료 전에는 타깃이 돌아보지 않아야 합니다.");

            // 루틴 끝까지 진행 (시전 종료 시점)
            while (routine.MoveNext()) { }

            Assert.AreEqual(LookDirection.West, target.characterMove.facingDirection,
                "카드 1회 시전 완료 시점에는 피격 대상이 공격 원점(West) 방향으로 회전해야 합니다.");
            Assert.IsFalse(CombatFacing.HasActiveCastSession);
        }
        finally
        {
            bm.Dispose();
        }
    }

    [Test]
    public void IncapacitatedStunnedUnit_TakesBackstabBonusButDoesNotRotateOnHit()
    {
        GameMap gameMap = CreateTestMap(6, 6);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        Character caster = CreateTestCharacter("Caster", CharacterFaction.Player, tileMap[1][2], LookDirection.East);
        Character stunnedTarget = CreateTestCharacter("StunnedTarget", CharacterFaction.Enemy, tileMap[2][2], LookDirection.East);
        AttachCoreEffect(stunnedTarget, "Stun", 1, 0f);

        Assert.IsTrue(stunnedTarget.IsIncapacitated());

        int sessionId = CombatFacing.BeginCastSession(caster, null, new Vector2Int(1, 2), LookDirection.East, new Vector2Int(2, 2));
        stunnedTarget.TakeDamage(new DamageInfo(caster, stunnedTarget, 10f, DamageFlag.Normal));
        CombatFacing.CompleteCastSession(sessionId, applyRotations: true);

        Assert.AreEqual(85f, stunnedTarget.curHealth, 0.001f, "기절 상태여도 정후면 1.5배 피해는 정상 적용되어야 합니다.");
        Assert.AreEqual(LookDirection.East, stunnedTarget.characterMove.facingDirection,
            "기절 등 행동 불가 상태의 유닛은 피격 시 돌아보지 않아야 합니다.");
    }

    [Test]
    public void HPLossAndReflectedDamage_IgnoreFacingBonusAndDoNotRotate()
    {
        GameMap gameMap = CreateTestMap(6, 6);
        List<List<Tile>> tileMap = gameMap.GetTileMap();

        Character caster = CreateTestCharacter("Caster", CharacterFaction.Player, tileMap[1][2], LookDirection.East);
        Character target = CreateTestCharacter("Target", CharacterFaction.Enemy, tileMap[2][2], LookDirection.East);

        target.TakeDamage(new DamageInfo(caster, target, 10f, DamageFlag.HPLoss));
        Assert.AreEqual(90f, target.curHealth, 0.001f);
        Assert.AreEqual(LookDirection.East, target.characterMove.facingDirection);

        target.TakeDamage(new DamageInfo(caster, target, 10f, DamageFlag.Reflected));
        Assert.AreEqual(80f, target.curHealth, 0.001f);
        Assert.AreEqual(LookDirection.East, target.characterMove.facingDirection);
    }
}
