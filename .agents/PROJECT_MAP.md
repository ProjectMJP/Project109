# PROJECT_MAP.md — Project109 아키텍처 SSOT

> **최종 갱신**: 2026-10-18  
> **목적**: 사람 개발자와 AI 에이전트가 동일한 기준으로 코드를 이해·수정할 수 있도록, 프로젝트의 계층 구조·참조 규칙·데이터 흐름을 단일 문서에 정의합니다.

---

## 1. 프로젝트 개요

**Project109**는 Unity 기반의 로그라이크 턴제 전투 + 맵 탐색 게임입니다.  
카드 기반 스킬 시스템, YAML/Lua 기반 모딩, 은신처–던전 서브씬 전환 구조를 갖추고 있습니다.

| 항목 | 설명 |
|------|------|
| 엔진 | Unity (C#) |
| 스크립팅 확장 | XLua (카드 로직), YamlDotNet (데이터 파싱) |
| 에셋 관리 | Addressables + StreamingAssets (모드 데이터) |
| 세이브 | JSON 기반 로컬 파일 (`SaveSystem`) |
| 빌드 도구 | EventBinderGenerator (Roslyn Source Generator) |

---

## 2. 레이어 아키텍처

프로젝트의 C# 스크립트(`Assets/Scripts/`)는 다음 **6개 논리 계층**으로 분류됩니다.

```
┌──────────────────────────────────────────────┐
│  [L0] Core  — 순수 유틸, 인터페이스, 이벤트  │  ← 어디서든 참조 가능 (최하위)
├──────────────────────────────────────────────┤
│  [L1] Data  — 스키마, 로더, SO, 맵 데이터    │  ← Core만 참조
├──────────────────────────────────────────────┤
│  [L2] World — 게임 오브젝트 로직            │  ← Core, Data 참조
├──────────────────────────────────────────────┤
│  [L3] Managers — 시스템 오케스트레이션       │  ← Core, Data, World 참조
├──────────────────────────────────────────────┤
│  [L4] UI — HUD, 패널, 툴팁                 │  ← Core, Data, (World는 읽기전용)
├──────────────────────────────────────────────┤
│  [L5] Input — 유저 입력 처리                │  ← Core, (이벤트로 상위에 전달)
└──────────────────────────────────────────────┘
  [외부] Lua  — XLua 기반 카드/이벤트 스크립트 (Data 스키마에 바인딩)
  [외부] Editor — 에디터 전용 도구 (빌드에 포함되지 않음)
```

### 2.1 계층별 책임

#### [L0] Core (`Scripts/Core/`)

| 서브디렉터리 | 책임 |
|---|---|
| `Algorithms/` | 경로 탐색(A*), 효과 범위 생성, 우선순위 큐 등 순수 알고리즘 |
| `Events/` | `EventBus<T>` 제네릭 이벤트 버스, `EventInfo` 전투 정보 구조체, `EventFlag` 비트 플래그 |
| `Interfaces/` | `IIdentifiable`, `IInitializable`, `IInteractable`, `IModAssetResolver` 등 공통 계약 |
| `Utils/` | `IDescribable`, `ImageLoader` 등 범용 유틸리티 |

- `Enums.cs` (Scripts 루트): 게임 아이템 타입 열거형, 카드 타입 열거형 등 전역 enum

#### [L1] Data (`Scripts/Data/`)

| 서브디렉터리 | 책임 |
|---|---|
| `Schemas/` | YAML 파싱 데이터 모델 (`CardData`, `RelicData`, `EffectData`, `NPCUnitData`, `DropTableData`, `DialogueData`, `InteractableData`, `StarterKitData` — 모두 `IIdentifiable` 구현) |
| `ExploreMap/` | 탐험 맵 데이터 모델 (`ExploreMapData`, `ExploreNode`, `EncounterData`, `LocationType`) |
| `Loaders/` | `AssetCacheManager` (Addressable 에셋 캐시), `BundleLoader`, `RandomItemPicker` |
| `Map/` | `MapDataSO` (ScriptableObject 타일맵 데이터), `CellData`, `EntityData` |

#### [L2] World (`Scripts/World/`)

| 서브디렉터리 | 책임 |
|---|---|
| `Combat/` | `Character` (MonoBehaviour, 전투 엔티티 핵심), `CharacterMove`, `CharacterAnimation`, `CharacterInterfaces` (40+ 이벤트 인터페이스), `Effects/` (Effect, EffectManager) |
| `Player/` | `Player` (영속 세션 데이터), `PlayerBattleController`, `PlayerExploreController`, `PlayerStat`, `PlayerInterfaces`, `ActionQueue/` (턴 액션 큐) |
| `Items/Card/` | `Card` (런타임 카드 인스턴스), `PlayerDeck`, `BattleDeck`, `CardMasteryManager`, `CardTag` |
| `Items/Relic/` | `RelicBase` (유물 베이스), `RelicManager` |
| `NPC/AI/` | `NPCUnitController` (적 AI 컨트롤러), `AIPositionPlanner` |
| `Interactables/` | `InteractableObject`, `RewardChest`, `RestoreBonfire`, `ShopNPC` |
| `Map/` | `GameMap`, `Tile`, `EffectAreaTile`, `DestructibleObject`, `MapMoveRangeIndicator` |

#### [L3] Managers (`Scripts/Managers/`)

| 서브디렉터리 | 책임 |
|---|---|
| `Scene/` | **`GameSceneManager`** (세션 루트 싱글턴), **`RunManager`** (던전 런 오케스트레이터), `HideoutManager` (은신처 서브씬), `SceneLoadManager` (씬 전환), `ScreenFader` |
| `Gameplay/` | **`BattleManager`** (전투 FSM), **`MapManager`** (맵 생성/상태), **`ExploreMapManager`** (탐험 맵 노드 생성/진행), **`UIManager`** (UI 레이어 스택), `GameItemRewardManager` (보상 풀/배분), `SaveSystem` (JSON 직렬화), `ObjectPoolManager` |
| `Modding/` | **`ModLoader`** (YAML 파싱, 데이터베이스 구축), `ModManager`, `ModObjectFactory` |

#### [L4] UI (`Scripts/UI/`)

| 서브디렉터리 | 책임 |
|---|---|
| `Framework/` | `UIPanelBase` (모달 베이스), `UIConstants`, `UIDialogPanel`, `UILayerType`, `UIBattleNotice` |
| `HUD/` | `TopHUDPanel`, `CharacterUIController`, `PlayerUIController`, `GaugeUI`, `DamageIndicator/` |
| `Panels/` | 기능별 패널: `Battle/` (`BattleUIPanel`, `BattleMovePanel`), `Cards/` (`BattleHandPanel`, `CardDeckViewPanel` 등), `Currency/`, `Dialogue/`, `ExploreMap/` (`ExploreMapUI`, `ExploreNodeUI`), `GameOver/` (`GameOverPanel`), `Relic/`, `Restore/`, `Reward/`, `Shop/`, `Title/` |
| `Tooltip/` | `TooltipManager`, `TooltipPanel`, `TooltipTrigger` |

#### [L5] Input (`Scripts/Input/`)

- `PlayerInputController` — Unity Input System 래퍼, 터치/드래그/클릭/취소 이벤트 발행
- `CameraController` — 카메라 팬/줌 처리

#### [외부] Lua (`Scripts/Lua/`)

- `LuaManager` — XLua 환경 초기화, 스크립트 검색 경로 관리
- `LuaEventBinder` — C# ↔ Lua 이벤트 바인딩
- `GenConfig.cs` — XLua 코드젠 설정
- `Choice/` — 이벤트 선택지 Lua 스크립트 (~20개)

#### [외부] Editor (`Assets/Editor/`)

- `YAMLImporter` — YAML 에셋 자동 임포트
- `MapPainterEditor` — 맵 타일 페인터 에디터 도구
- `AddressableAutoRegister` — Addressable 에셋 자동 등록

---

## 3. 참조 방향 규칙 (Dependency Rules)

### 3.1 허용된 참조 방향 (→ = "참조 가능")

```
L0 Core      ← 모든 계층에서 참조 가능
L1 Data      → L0
L2 World     → L0, L1
L3 Managers  → L0, L1, L2
L4 UI        → L0, L1, L3(UIManager)  // World는 이벤트를 통한 간접 참조만
L5 Input     → L0                     // 이벤트로 상위에 전달
Lua          → L2(Character API), L1(Data)  // C# API 바인딩을 통해
```

### 3.2 금지된 역참조 (Anti-patterns)

| ❌ 금지 | 이유 |
|---------|------|
| `Core` → 상위 어떤 계층이든 | 순수 유틸이므로 도메인 의존 불가 |
| `Data/Schemas` → `World` 또는 `Managers` | 데이터 모델이 런타임 로직에 의존하면 안 됨 |
| `World/Combat` → `UI` | 전투 로직이 UI에 직접 의존 금지 (이벤트로 통신) |
| `Input` → `Managers` 또는 `World` 직접 호출 | 반드시 이벤트를 통해 간접 전달 |
| `UI` → `World` 직접 메서드 호출 | UI는 Manager를 경유하거나 읽기전용 데이터만 참조 |

### 3.3 현재 허용된 예외 (Technical Debt)

> 아래 항목들은 현재 아키텍처에 존재하는 역참조이며, 점진적으로 개선 대상입니다.

| 위치 | 역참조 내용 | 개선 방향 |
|------|------------|----------|
| `GameItemRewardManager` | `ModLoader.Instance.CardDatabase` 직접 참조 | 초기화 시점에 주입 |

---

## 4. 핵심 싱글턴 & 생명주기

### 4.1 싱글턴 매니저 계보

```
[DontDestroyOnLoad — 앱 전체 수명]
  ├── AssetCacheManager     ← Addressable 에셋 캐시
  ├── GameItemRewardManager ← 보상 아이템 풀
  ├── LuaManager            ← XLua 환경
  └── PlayerInputController ← 입력 시스템

[GameScene 수명 — 씬 전환 시 재생성]
  ├── GameSceneManager      ← 세션 루트, Player 소유
  ├── SceneLoadManager      ← 서브씬 전환 관리
  └── UIManager             ← UI 레이어 스택

[SubScene 수명 — 서브씬 로드/언로드에 따라]
  ├── RunManager            ← 던전 런 (DungeonScene)
  │   ├── BattleManager     ← 전투 FSM (POCO, non-MonoBehaviour)
  │   └── MapManager        ← 맵 생성/상태 (POCO)
  └── HideoutManager        ← 은신처 (HideoutScene)
```

### 4.2 씬 전환 흐름

```
MainMenu (타이틀)
  └─ SceneManager.LoadScene("GameScene")
      └─ GameSceneManager.Start()
          ├─ 플레이어 Addressable 인스턴스화
          ├─ TopHUD 생성 & 바인딩
          └─ InitializeSubScene()
              ├─ [세이브 없음/던전 미진입] → HideoutScene 가산 로드
              └─ [세이브 있음 & 던전 진입 중] → DungeonScene 가산 로드

HideoutScene ──TransitionToDungeon()──→ DungeonScene
DungeonScene ──TransitionToHideout()──→ HideoutScene
  └─ 런 종료/패배 시: GameOverPanel → RunManager.ResetRunProgress() → SaveSystem.SaveGame(null) 은신처 상태 런 데이터 초기화
```

---

## 5. 데이터 흐름 & SSOT

### 5.1 정적 게임 데이터 (Static Data)

```
StreamingAssets/Mods/Core/
  ├── Cards/*.yaml       ─┐
  ├── Effects/*.yaml      │  ← ModLoader.LoadAllMods()
  ├── Relics/*.yaml       │     YAML → Dictionary<string, *Data>
  ├── Units/*.yaml        │     Data SSOT = ModLoader.*Database
  ├── StarterKits/*.yaml  │
  ├── Dialogues/*.yaml    │
  ├── Interactables/*.yaml│
  ├── DropTables/*.yaml  ─┘
  └── Scripts/Cards/*.lua ──→ LuaManager (LuaTable 프로토타입)
```

- **데이터 원본(SSOT)**: `ModLoader.Instance.*Database` (딕셔너리)
- **에셋 원본**: `AssetCacheManager.instance` (Addressable 캐시)
- **갱신 시점**: 앱 시작 시 1회 로드, 런타임 중 불변(Immutable)

### 5.2 런타임 플레이어 상태 (Session State)

```
GameSceneManager.instance.player (Player)
  ├── .character        ← Character (MonoBehaviour, 전투 스탯)
  ├── .playerStat       ← PlayerStat (재화, 기본 드로우 수 BaseDrawCount)
  ├── .deck             ← PlayerDeck (마스터 카드 풀)
  ├── .battleDeck       ← BattleDeck (전투 중 덱/손패/버린패/소멸패 런타임 상태)
  ├── .relicManager     ← RelicManager (유물 목록)
  └── .eventBus         ← EventBus<IPlayerEvent>
```

- **데이터 원본(SSOT)**: `GameSceneManager.instance.player`
- **상태 변경**: 각 도메인의 메서드 호출 (`AddCardToDeck()`, `AddRelic()`, `InitBattleDeck()` 등)
- **상태 관찰**: `EventBus<IPlayerEvent>` 및 `BattleDeck` 이벤트(`OnHandChanged`, `OnDrawPileChanged` 등)를 통해 UI 및 시스템이 구독

### 5.3 전투 상태 (Battle State)

```
RunManager.instance.battleManager (BattleManager)
  ├── .battleState      ← BattleState (None/Intro/Combat/TurnInProgress/BattleEnd)
  ├── playerTeam[]      ← List<ICharacterController>
  ├── enemyTeam[]       ← List<ICharacterController>
  └── 이벤트: OnBattleWon, OnBattleLost, OnBattleEnded
```

- **상태 원본(SSOT)**: `BattleManager.battleState`
- **상태 전이**: `InitBattle()` → `StartCombat()` → `RequestTurnStart()` → `EndTurn()` → `WinBattle()`/`LoseBattle()`
- **이벤트 전파**: `BattleManager` → `RunManager` → `MapManager` / `UIManager`

### 5.4 맵 상태 (Map State)

```
RunManager.instance.currentMap (MapManager)
  ├── .currentMapState   ← MapState (None/Battle/Secret)
  ├── .currentMapData    ← MapDataSO
  ├── .currentGameMap    ← GameMap (런타임 타일 오브젝트 루트)
  └── .currentEnemyControllers ← 현재 맵의 적 컨트롤러 목록
```

### 5.5 탐험 맵 상태 (ExploreMap State)

```
RunManager.instance.exploreMapManager (ExploreMapManager)
  ├── .currentMapData    ← ExploreMapData (현재 층의 결정론적 노드 그래프)
  ├── .currentNode       ← ExploreNode (현재 플레이어 위치 노드)
  └── EncounterData      ← 현재 노드의 인카운터 정보 (전투/이벤트/보상 ID)
```

- **상태 원본(SSOT)**: `ExploreMapManager.currentMapData`
- **저장/로드**: `SaveSystem.SaveGame()`을 통해 JSON 직렬화 및 복원
- **UI 동기화**: `ExploreMapUI`에서 노드 그래프 시각화 및 경로 선택 이벤트 전달

---

## 6. 이벤트 시스템 요약

### 6.1 Character EventBus (`EventBus<ICharacterEvent>`)

캐릭터별 전투 이벤트. 유물/이펙트/카드가 이 버스에 리스너로 등록됩니다.

| 카테고리 | 인터페이스 예시 | 용도 |
|---------|----------------|------|
| 데미지 | `IOnBeforeDealDamage`, `IOnAfterTakeDamage`, `IOnKill` | 데미지 파이프라인 훅 |
| 회복 | `IOnBeforeGiveHeal`, `IOnAfterTakeHeal` | 힐 파이프라인 훅 |
| 실드 | `IOnBeforeGiveShield`, `IOnAfterTakeShield` | 실드 파이프라인 훅 |
| 스태미나 | `IOnBeforeSpendStamina`, `IOnAfterTakeStamina` | 스태미나 소모/획득 훅 |
| 이동 | `IOnBeforeMove`, `IOnAfterForcedMove` | 이동 전후 훅 |
| 전투/턴 | `IOnBattleStart`, `IOnTurnStart`, `IOnDeath` | 전투 생명주기 훅 |
| 카드 | `IOnBeforeUseCard`, `IOnDrawCard`, `IOnExhaustCard` | 카드 사용 파이프라인 훅 |
| 이펙트 | `IOnBeforeGiveEffect`, `IOnAfterRemoveEffect` | 버프/디버프 훅 |

### 6.2 Player EventBus (`EventBus<IPlayerEvent>`)

플레이어 세션 수준 이벤트. 시스템 매니저와 UI가 구독합니다.

| 인터페이스 | 용도 |
|-----------|------|
| `IOnAddRelic` / `IOnRemoveRelic` | 유물 추가/제거 알림 |
| `IOnAddCard` / `IOnRemoveCard` | 카드 덱 변동 알림 |
| `IOnCardUpgrade` / `IOnCardMasteryUpgrade` | 카드 강화 알림 |
| `IOnAddGold` / `IOnRemoveGold` | 재화 변동 알림 |

### 6.3 C# 이벤트 (Action 델리게이트)

| 소유자 | 이벤트 | 구독자 |
|--------|-------|-------|
| `Character` | `OnCharacterDied` | `BattleManager` |
| `Character` | `OnCharacterHealthChanged` / `OnCharacterStaminaChanged` | HUD UI |
| `BattleManager` | `OnBattleWon` / `OnBattleLost` | `RunManager` |

---

## 7. 모딩 시스템 구조

```
StreamingAssets/Mods/
  └── Core/              ← 바닐라(기본) 게임 데이터
      ├── Cards/
      │   ├── *.yaml     ← CardData 정의
      │   └── ...
      ├── Effects/
      ├── Relics/
      ├── Units/
      ├── StarterKits/
      ├── Dialogues/
      ├── Interactables/
      ├── DropTables/
      ├── Icons/          ← 카드/유물 아이콘 이미지
      └── Scripts/Cards/  ← Lua 카드 로직 스크립트

유저 모드 경로: {persistentDataPath}/mods/{modName}/
  └── (Core와 동일한 구조, 동일 ID 시 오버라이드)
```

**데이터 로딩 파이프라인:**
1. `ModLoader.LoadAllMods()` → Core 디렉터리 파싱 → 유저 모드 디렉터리 파싱 (오버라이드)
2. 각 Schema의 `IModAssetResolver.ResolveAndValidate()` 호출 → 아이콘 로드 + Lua 컴파일
3. 결과를 `ModLoader.*Database` 딕셔너리에 적재

---

## 8. 전투 데이터 파이프라인 (Damage Pipeline 예시)

```
카드 사용 (CardPlayAction)
  │
  ├─ caster.eventBus.Invoke<IOnBeforeUseCard>(cardInfo)
  │
  ├─ [카드 Lua 스크립트 실행]
  │   └─ card:OnPlay(caster, targets, targetPos)
  │       └─ DealDamage / GiveHeal / GiveShield 등 호출
  │
  ├─ DealDamage 흐름:
  │   ├─ DamageInfo 생성
  │   ├─ caster.eventBus.Invoke<IOnBeforeDealDamage>(ref info)   ← 공격자 버프/유물 수정
  │   ├─ target.eventBus.Invoke<IOnBeforeTakeDamage>(ref info)   ← 피격자 방어/유물 수정
  │   ├─ 실제 HP/Shield 차감
  │   ├─ caster.eventBus.Invoke<IOnAfterDealDamage>(info)        ← 흡혈 등 후처리
  │   ├─ target.eventBus.Invoke<IOnAfterTakeDamage>(info)        ← 가시 등 후처리
  │   └─ [사망 시] target.OnCharacterDied → BattleManager.OnCharacterDied
  │
  └─ caster.eventBus.Invoke<IOnAfterUseCard>(cardInfo)
```

---

## 9. 에셋 디렉터리 요약

| 경로 (`Assets/`) | 용도 |
|---|---|
| `AddressableAssetsData/` | Addressable 빌드 설정 |
| `Animator/` | 캐릭터 애니메이터 컨트롤러 |
| `Data/` | ScriptableObject 에셋 (맵, 캐릭터 등) |
| `Editor/` | 에디터 전용 스크립트 (빌드 미포함) |
| `Font/` | 폰트 에셋 |
| `InputSystem/` | Input System 관련 에셋 |
| `Materials/` / `Shader/` | 머티리얼 / 셰이더 |
| `Plugins/` | 서드파티 플러그인 (XLua 등) |
| `Prefab/` | 프리팹 에셋 |
| `Resource/` | Resources 폴더 에셋 |
| `SO/` | ScriptableObject 인스턴스 |
| `Scenes/` | 유니티 씬 파일 |
| `StreamingAssets/` | 런타임 파일 I/O 대상 (모드 데이터) |
| `Texture/` | 텍스처/스프라이트 에셋 |
| `XLua/` | XLua 라이브러리 코어 |
| `YamlDotNet/` | YAML 파서 라이브러리 |

---

## 10. 코드 외부 주요 도구

| 도구 | 위치 | 용도 |
|------|------|------|
| `EventBinderGenerator/` | 프로젝트 루트 | Roslyn Source Generator — EventBus 바인딩 코드 자동 생성 |
| `.agents/` | 프로젝트 루트 | AI 에이전트 규칙, 스킬, 스크립트 |
| `.Docs/` | 프로젝트 루트 | 아키텍처 다이어그램, 리팩토링 계획 등 개발 문서 |
| `BackupFile/` | 프로젝트 루트 | 백업 파일 |

---

## 11. 유지보수 가이드

### 이 문서의 갱신 원칙

1. **커밋 시 자동 검토**: `git-commit-rule.md`에 의거, 코드 커밋 시 변경 사항이 아래 항목에 해당하면 이 문서를 함께 갱신합니다:
   - 새로운 디렉터리/계층 추가
   - 싱글턴 매니저 추가/제거
   - 계층 간 참조 방향 변경
   - 이벤트 버스 인터페이스 추가/삭제
   - 데이터 SSOT 위치 변경
   - 모딩 시스템 구조 변경
   - 씬 전환 흐름 변경

2. **갱신 범위**: 변경된 섹션만 업데이트하고, 최상단의 `최종 갱신` 날짜를 반드시 갱신합니다.

3. **AI 에이전트용 참조 규칙**: 코드를 수정하기 전에 이 문서의 §3 참조 방향 규칙을 확인하여 위반하지 않도록 합니다.
