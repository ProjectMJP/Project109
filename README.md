# Project109 (프로젝트 109)

> **그리드 전술(Grid Tactics)**과 **반실시간 ATB 스태미나 턴제**가 결합된 2D 로그라이크 덱빌딩(Roguelike Deckbuilder) 게임

---

## 1. 프로젝트 개요 (Overview)

**Project109**는 고정된 자리에서 카드만 내는 전통적인 덱빌더와 달리, 2D 격자 타일맵 위에서의 위치 선정, 사거리, 광역 범위(AoE), 돌진 및 밀치기(넉백) 등 **공간 전술**을 카드 플레이와 밀접하게 결합한 독창적인 전투 메커니즘을 제공합니다.

### 핵심 게임 기획 기둥 (Core Pillars)
1. **그리드 공간 전술과 덱빌딩의 융합**: 이동력(`curMoveCount`)을 소모해 위치를 잡고, 사거리(`Range`), 방사 범위(`AoE`), 시야 및 장애물(벽/유닛 충돌)을 계산하여 카드를 시전합니다.
2. **반실시간 스태미나(ATB) 턴 시스템**: 유닛별 스태미나가 실시간으로 누적되어 최대치에 도달한 유닛이 즉시 턴을 획득하고 시간을 정지한 뒤 행동합니다. 미사용 스태미나는 다음 턴으로 일부 이월됩니다.
3. **카드 투트랙(Two-Track) 성장**: 모닥불/상점에서의 1회성 카드 수치 강화(`Upgrade`)뿐 아니라, 전투 중 사용 방식에 따라 경험치가 쌓여 분기형 특성/태그(`CardTag`)를 영구 개조하는 마스터리(`Mastery`) 시스템을 지원합니다.
4. **완전 데이터 구동형 모딩(Data-Driven Modding)**: 카드 속성, 스킬 효과, 유물, 상태이상, 적 AI, 대화, 드롭 테이블이 모두 `StreamingAssets`의 YAML 데이터와 XLua 스크립트로 분리되어 있어 소스 코드 수정 없이 확장이 가능합니다.

---

## 2. 게임 루프 구조 (Game Loop)

- **거시적 루프 (Macro Loop — 세션 & 은신처)**  
  `GameScene`(상주 인프라)을 기반으로 `HideoutScene`(은신처 정비)과 `DungeonScene`(던전 출격)을 가산(Additive) 방식으로 전환합니다.
- **중간 루프 (Meso Loop — 절차적 탐험)**  
  `ExploreMap`을 통해 15개 층의 연결 노드(전투, 엘리트, 보물, 상점, 모닥불, 보스)를 선택하며 진행합니다.
- **미시적 루프 (Micro Loop — 전술 전투 & 액션 큐)**  
  ATB 스태미나 기반 턴 진입 → 드로우 및 이동 → 카드 타깃팅/시전 → `ActionQueueManager`를 통한 비동기 액션 순차 실행 → 이벤트 버스 파이프라인 연계.

---

## 3. 기술 스택 (Tech Stack)

| 항목 | 사용 기술 / 규격 | 설명 |
|---|---|---|
| **엔진 (Engine)** | Unity 6 (`6000.3.8f1`) | Universal Render Pipeline (URP), New Input System |
| **스크립팅 (Scripting)** | XLua (Lua 5.3), YamlDotNet | 런타임 카드 로직, 이벤트 핸들러, YAML 스키마 파싱 |
| **에셋 관리 (Assets)** | Addressables + StreamingAssets | 3D/2D 에셋 비동기 로딩 및 데이터 구동형 모딩 시스템 |
| **빌드 도구 (Build Tools)** | Roslyn Source Generator (`EventBinderGenerator`) | 리액티브 이벤트 버스 자동 코드 바인딩 |
| **저장 시스템 (Save)** | JSON 기반 로컬 파일 (`SaveSystem`) | 다중 세이브 슬롯, 인카운터별 자동 저장 지원 |

---

## 4. 아키텍처 및 계층 구조 (Architecture)

프로젝트의 C# 스크립트(`Assets/Scripts/`)는 다음 6개 논리 계층으로 엄격히 분리되어 관리됩니다:

```text
[L0] Core     — 순수 알고리즘(A*), 이벤트 버스(EventBus<T>), 공통 인터페이스, 유틸리티
  ↑
[L1] Data     — YAML 데이터 스키마, 탐험 맵 모델, 어드레서블/번들 로더
  ↑
[L2] World    — 전투 캐릭터, 플레이어, 카드 인스턴스, 타일맵(GameMap), 적 AI
  ↑
[L3] Managers — 게임 루프 매니저(GameSceneManager, RunManager, BattleManager, StageManager 등)
  ↑
[L4] UI       — HUD, 덱/패널 뷰, 툴팁, UIManager 스택 기반 모달 시스템
  ↑
[L5] Input    — New Input System 기반 플레이어 입력 제어
```

> 상세한 계층별 책임, 데이터 SSOT, 이벤트 버스 인터페이스 명세는 [.agents/PROJECT_MAP.md](.agents/PROJECT_MAP.md)를 참조하십시오.

---

## 5. 프로젝트 문서 및 기획 위키 (Documentation & Wiki)

- **개발 아키텍처 명세 (SSOT)**: [.agents/PROJECT_MAP.md](.agents/PROJECT_MAP.md)
- **개발 행동 규칙**: [.agents/rules/](.agents/rules/)
- **게임 기획 / 시나리오 / 세계관 / 밸런스 위키**:  
  👉 **[GitLab Project109 Wiki](https://gitlab-siryu.com/SIRYU/Project109/-/wikis/home)**  
  *(모든 시스템 상세 기획, 몬스터/카드 스펙 기획 의도 및 배경 스토리는 GitLab Wiki를 단일 진실 공급원(SSOT)으로 관리합니다.)*

---

## 6. 시작하기 (Getting Started)

### 요구 사양
- **Unity Editor**: `6000.3.8f1` 이상
- **Git LFS**: 대용량 에셋 관리를 위해 Git LFS 활성화 필수

### 클론 및 프로젝트 열기
```bash
# Git LFS가 포함된 저장소 클론
git clone https://github.com/ProjectMJP/Project109.git
cd Project109
git lfs pull
```
- Unity Hub에서 `Project109` 프로젝트 폴더를 열고 `Assets/Scenes/GameScene.unity`를 기본 실행 씬으로 시작합니다.
