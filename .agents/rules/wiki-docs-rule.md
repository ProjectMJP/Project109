# 기획 문서 및 GitLab Wiki 관리 규칙 (Planning Documentation & GitLab Wiki Rule)

본 문서는 Project109 프로젝트에서 게임 기획, 세계관, 스토리, 캐릭터 설정, 시스템 스펙 문서를 참조하거나 작성·수정할 때 에이전트가 반드시 준수해야 하는 행동 규칙을 정의한다.

---

## 1. 기획/스토리 단일 진실 공급원 (SSOT)

- 프로젝트의 **모든 게임 기획서, 스토리/시나리오, 세계관(Lore), 캐릭터 설정, 시스템 상세 스펙**의 SSOT(Single Source of Truth)는 **GitLab Wiki**이다.
  - **위키 Web 주소**: `https://gitlab-siryu.com/SIRYU/Project109/-/wikis/home`
  - **위키 Git 저장소**: `https://gitlab-siryu.com/SIRYU/Project109.wiki.git`
- **로컬 코드베이스 내 임의 기획 문서 생성 금지**:
  - `Assets/` 또는 프로젝트 루트 아래에 임의의 기획/스토리 텍스트 파일(`.txt`, `.doc`, 비공식 `.md`)을 생성하거나 방치하지 마라.
  - 프로젝트 내부 문서는 기술 아키텍처 명세인 `.agents/PROJECT_MAP.md` 및 개발 규칙 파일(`.agents/rules/`)에 한정한다.

---

## 2. 데이터(YAML), 코드(C#)와 기획(Wiki)의 역할 분담 및 사양 동기화 의무

- **게임 런타임 데이터 (`StreamingAssets/ModData/`)**:
  - 게임 엔진이 직접 읽어 파싱하는 순수 데이터(수치, 카드 ID, 태그, 프리팹 경로 등 YAML 및 XLua 스크립트).
- **게임 런타임 코드 (`Assets/Scripts/`)**:
  - 코어 전투 메커니즘, 상태이상 파이프라인, 탐험 맵 생성 알고리즘, 세이브/로드 흐름 등을 구현하는 로직.
- **GitLab Wiki (`Project109.wiki.git`)**:
  - 해당 수치와 시스템이 설계된 **기획 의도, 배경 스토리, 시나리오 콘텍스트, 시스템 메커니즘 설명, 밸런스 공식, 상세 스펙**.

### 2.1 사양 변경 시 Wiki 동기화 의무 (Specification Change Rule)
- 코드나 YAML 데이터를 변경할 때 **게임의 사양(Specification)**이 변경되거나 기존 GitLab Wiki 문서의 내용에 영향을 주는 경우, **반드시 관련된 GitLab Wiki 문서도 함께 갱신**해야 한다.
  - **대상 예시**:
    - 전투 메커니즘 공식(스태미나 회복/이월, 실드 감쇄, 데미지 계산 파이프라인 등) 변경
    - 카드/유물/상태이상의 동작 사양 및 판정 규칙 변경
    - 탐험 맵 층수, 노드 배치 규칙, 세이브/로드 생명주기 사양 변경
    - 새로운 캐릭터, 적, 보스, NPC의 기획 스펙 및 대화/선택지 사양 변경
  - **제외 대상**:
    - 기획 스펙에 영향을 주지 않는 단순 버그 수정, 내부 성능 최적화, 변수명/클래스 리팩토링, 코드 정리
- 사양 변경 시 Wiki를 갱신하지 않고 코드만 변경하여 Wiki(SSOT)와의 불일치를 유발하는 것을 엄격히 금지한다.

---

## 3. 기획/스토리 요청 시 에이전트 행동 절차 (Trigger & Workflow)

사용자가 다음과 유사한 프롬프트를 요청할 경우, 에이전트는 반드시 본 절차를 따른다:
> *"기획 문서 확인해줘"*, *"스토리 추가/수정해줘"*, *"캐릭터 설정 갱신해줘"*, *"전투 시스템 기획서 정리해줘"*

### 3.1 단계별 파이프라인
1. **최신 상태 동기화 (Sync)**:
   - GitLab Wiki Git 저장소(`https://gitlab-siryu.com/SIRYU/Project109.wiki.git`)에서 최신 상태를 `pull` 또는 `fetch`하여 기존 문서의 작성 현황과 맥락을 파악한다.
2. **문서 작성 및 갱신 (Edit)**:
   - 아래 §4의 [Wiki 디렉터리 및 네비게이션 표준]에 맞춰 적절한 경로에 마크다운 문서를 작성/수정한다.
   - 새 문서를 생성한 경우 반드시 최상위 [`Home.md`](file:///c:/Users/siryu/fork/Project109/README.md)의 목차(Index)에 링크를 추가하여 고립된 페이지(Orphan Page)가 발생하지 않도록 한다.
3. **커밋 및 원격 푸시 (Commit & Push)**:
   - Wiki 저장소에 명확한 커밋 메시지(예: `[docs] 전투 시스템 기획서 추가`, `[story] 챕터 1 인카운터 대사 수정`)로 커밋 후 `master` 브랜치에 푸시한다.
   - 원격 푸시 즉시 GitLab 웹 Wiki 인터페이스에 반영되었음을 사용자에게 보고한다.

---

## 4. Wiki 디렉터리 및 네비게이션 표준

GitLab Wiki의 기본 알파벳 정렬로 인한 순서 꼬임을 방지하고, 직관적인 계층 탐색을 보장하기 위해 **커스텀 사이드바(`_sidebar.md`)**와 **넘버링 접두사**를 의무 적용한다:

```text
Project109.wiki/
├── Home.md                     # 위키 대문 및 전체 목차/네비게이션 인덱스
├── _sidebar.md                 # GitLab 웹 사이드바 커스텀 네비게이션 (Home 최상단 고정)
├── 01-story-and-lore/          # 세계관 및 시나리오
│   ├── world-setting.md        # 왜곡된 발할라, 신들의 정체, 라그나로크의 진실
│   └── main-scenario.md        # 메인 시나리오 흐름 및 엔딩 분기 (순응 vs 저항)
├── 02-characters/              # 캐릭터 및 엔티티 설정
│   ├── playable-characters.md  # '기억하는 자' 및 선대 전사들의 무기 서사
│   ├── hideout-npcs.md         # 은신처 NPC (인도자, 마녀, 고행자)
│   └── enemies-and-bosses.md   # 기억을 잃은 영혼, 오염된 괴이 및 보스
├── 03-game-design/             # 코어 게임 시스템 기획
│   ├── combat-system.md        # 그리드 전술, ATB 스태미나 턴, 사거리/AoE
│   ├── card-and-mastery.md     # 카드 플레이, 투트랙 강화/마스터리 시스템
│   └── exploration-map.md      # 15개 층 탐험 맵, 노드 유형 및 세이브 규칙
└── 04-balance-and-data/        # 밸런스 설계 및 데이터 명세
    ├── status-effects.md       # 상태이상(버프/디버프) 기획 정의
    └── relics-and-items.md     # 유물 및 소모품 컨셉 정의
```


---

## 5. 문서 작성 가이드라인 (Formatting Standards)

1. **인코딩**: 모든 위키 문서는 반드시 **UTF-8 인코딩**으로 작성한다.
2. **언어**: 프로젝트 기본 기획 및 설정 문서는 **한국어** 작성을 원칙으로 한다.
3. **상호 링크(Inter-wiki Links)**:
   - 위키 내 다른 페이지로 연결할 때는 상대 경로 마크다운 링크 또는 GitLab 위키 링크 문법을 사용한다.
   - 예: `[전투 시스템](game-design/combat-system.md)`
4. **다이어그램 표기**:
   - 구조나 흐름을 도식화할 때는 `.agents/rules/mermaid-diagram-rule.md`를 엄격히 준수하여 Mermaid 구문 오류가 발생하지 않도록 주의한다.
