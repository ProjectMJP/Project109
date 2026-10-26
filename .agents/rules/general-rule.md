---
trigger: always_on
---

---
trigger: always_on
---

# 통합 개발 및 오케스트레이션 규칙 (General & Ponytail Rules)

본 문서는 Project109 코드베이스에서 코드를 작성·수정할 때 에이전트가 반드시 준수해야 하는 행동 규칙과 작업 수명 주기를 정의한다.

> **참고**: 프로젝트의 구조, 계층 관계, 데이터 흐름 등 아키텍처 사실 정보는 [PROJECT_MAP.md](file:///c:/Users/siryu/fork/Project109/.agents/PROJECT_MAP.md)를 참조하라.

---

## 1. SSOT 참조 및 동기화 의무

### 1.1 PROJECT_MAP 참조 의무
- 코드를 **신규 작성**하거나 **구조적 수정**(새 클래스/디렉터리 추가, 참조 관계 변경 등)을 수행하기 전에 반드시 `.agents/PROJECT_MAP.md`를 읽고 다음을 확인하라:
  - 해당 코드가 속할 **계층(Layer)**과 그 계층의 책임 범위 (§2)
  - 허용된 **참조 방향** 및 금지된 역참조 (§3)
  - 관련 **데이터 원본(SSOT)**의 위치와 상태 변경 방식 (§5)
  - 관련 **이벤트 버스 인터페이스** 패턴 (§6)
- 단순 버그 수정, 주석 추가, 리터럴 변경 등 구조에 영향 없는 변경은 이 의무에서 제외된다.

### 1.2 사양 변경 시 GitLab Wiki 동기화 의무
- 코드 또는 YAML 데이터를 변경할 때 **게임 기획 사양(Specification)**(전투 공식, 시스템 메커니즘, 맵 규칙, 카드/유물/상태이상 사양, 스토리/NPC 스펙 등)이 변경되거나 GitLab Wiki의 내용과 연관이 있는 경우, 반드시 GitLab Wiki(`Project109.wiki.git`)의 관련 기획 문서를 함께 갱신해야 한다.
- 상세 절차 및 디렉터리 표준은 `.agents/rules/wiki-docs-rule.md`를 엄격히 준수한다.
- 단순 내부 리팩토링, 버그 수정, 성능 최적화 등 기획 사양에 영향이 없는 변경은 위키 갱신 대상에서 제외된다.

---

## 2. 작업 실행 수명 주기 (Ponytail 3-Phase Execution Lifecycle)

임의의 대규모 수정이나 아키텍처 침범을 엄격히 금지하며, 모든 코드 작성 및 리팩토링은 아래 3단계를 순차적으로 수행해야 한다:

<phase_1_plan>
- 작업 전 `.agents/PROJECT_MAP.md`를 참조하여 수정 대상 파일과 심볼(Model/View/Presenter/Event/Manager)의 의존 관계를 파악한다.
- 코드를 편집하기 전, 다음 항목을 간결한 불릿 포인트로 선언하라:
  * **작업 범위 및 아키텍처 레이어**: 수정할 대상 파일 목록 및 변경할 계층 (작업 단위는 최대 2~3개의 연관 파일 이내로 격리)
  * **이벤트 및 데이터 명세**: 신규/수정될 EventBus 이벤트 명세(`GenConfig.cs` 등록 필요 여부), YAML/XLua 연동 여부, 기획 사양 변경에 따른 GitLab Wiki 동기화 필요 여부
  * **GC 및 런타임 성능 영향도**: 핫패스 할당 여부 및 생명주기 해제 계획
</phase_1_plan>

<phase_2_execute>
- 한 번에 하나의 레이어(예: 데이터/Model -> 로직/Presenter -> View 순차 진행) 단위로 편집하라.
- 기존 직렬화 필드(`[SerializeField]`) 이름이나 public API 시그니처를 수정할 때는 유니티 인스펙터 연결 유실(Missing Reference) 및 에디터 셋업 스크립트 동기화 여부를 반드시 확인하라.
- 아래 **§3(아키텍처 행동 규칙)** 및 **§4(Unity C# 코딩 규칙)**를 엄격히 준수하여 구현하라.
</phase_2_execute>

<phase_3_verify>
- 코드 작성 또는 `.cs` 파일 추가/이동/삭제 직후 유니티 프로젝트 정적 컴파일 상태를 확인하라:
  1. 유니티 외부(IDE, AI 에이전트, 터미널)에서 `.cs` 파일을 생성/이동/삭제한 경우 `Assembly-CSharp.csproj`에 즉시 반영되지 않아 `CS0246`/`CS2001` 오류가 발생할 수 있으므로, 유니티 MCP 연결 시 `refresh_assets`를 실행하고, 미연결/백그라운드 시 `.agents/scripts/sync-csproj.ps1` 스크립트를 먼저 실행하여 컴파일 항목을 동기화하라.
  2. `dotnet build Assembly-CSharp.csproj --no-dependencies`를 실행하여 컴파일 오류가 없는지 최종 검증하라.
- **컴파일/런타임 에러 발생 시 대처**:
  1. 즉시 추가 파일 수정을 멈추고 롤백 지점을 확인한다.
  2. `grep_search` 또는 타깃 검색(`Select-String`)으로 해당 `CSXXXX` 에러 코드와 발생 파일/라인 주변 문맥만 국소 조회한다.
  3. 다른 아키텍처 레이어로 문제를 전파하지 말고, 실패한 해당 타입/이벤트 시그니처 불일치부터 핀포인트로 수정하라.
  4. 컴파일 에러를 억지로 통과시키기 위해 `#pragma warning disable`이나 불필요한 null-forgiving(`!`) 연산자로 덮는 행위를 금지한다.
</phase_3_verify>

---

## 3. 아키텍처 행동 규칙 (Core Architecture Guardrails)

### 3.1 데이터 구동형 설계 준수 (Data-Driven Design)
- 스킬, 버프, 효과 추가 시 하드코딩된 C# 메서드 생성을 **금지**한다.
  - 카드 속성, 스킬 계수, 아이템 효과 등은 반드시 `StreamingAssets`의 **YAML 데이터**로 구성하라.
  - 동적 효과나 커스텀 로직은 C# 클래스를 복잡하게 변경하지 말고, **XLua 루아 스크립트(.lua)**와 연동하여 구현하라.

### 3.2 MVP (Model-View-Presenter) 엄격 분리
- **View**: `MonoBehaviour`(`UIPanelBase` 등)를 상속하며 순수 UI 렌더링, 애니메이션 트리거, 유저 입력 수신만 담당한다. 비즈니스 로직 및 도메인 데이터 캐싱을 금지한다.
- **Presenter**: 인터페이스(`IView`)를 통해 View와 통신하며, EventBus 구독/발행 및 Model 갱신을 중계한다. 유니티 씬 직렬화 의존성을 최소화한다.
- **Model**: 순수 C# POCO 클래스/구조체로 유지하며 엔진 의존성(`UnityEngine`)을 가지지 않는다.

### 3.3 인터페이스 기반 생명주기 통일 (Lifecycle Rules)
- 신규 매니저 및 비-MonoBehaviour 컨트롤러 생성 시 반드시 `IInitializable`과 `IDisposable` 인터페이스를 구현하라.
  ~~~csharp
  public class CustomSystemManager : IInitializable, IDisposable
  {
      public void Initialize() { /* 초기화 및 이벤트 구독 */ }
      public void Dispose() { /* 리소스 해제 및 이벤트 구독 해제 */ }
  }
  ~~~
- 생성된 클래스는 상위 매니저(`RunManager`의 `Awake`/`OnDestroy` 등)의 생명주기 파이프라인에 동기적으로 등록하여 좀비 참조 및 메모리 누수를 방지하라.
- MonoBehaviour 기반 컴포넌트에서 이벤트를 구독할 때도 핸들러 수명 주기(`OnEnable`/`OnDisable` 또는 `OnDestroy`)를 명확히 맞춰 메모리 누수를 방지하라.

### 3.4 UI 스택 기반 입력 제어 및 닫기 규칙 (UI Stack & Closing Rules)
- 인게임 UI를 열거나 닫을 때, `PlayerInputController`의 입력 제어 메서드를 **직접 호출하는 것을 금지**한다.
- 반드시 `UIManager`의 UI 스택 Push/Pop 메커니즘을 경유하라.
- **모달/창 닫기 규칙**: 모든 `UIPanelBase` 파생 클래스 또는 하위 뷰에서 창을 닫을 때는 절대 `gameObject.SetActive(false)`나 `Destroy(gameObject)`를 직접 호출하지 마라.
  - 반드시 `Close()` 또는 `UIManager.instance.CloseUI()`를 호출하여 모달 스택(`_popupStack`)과 월드 입력 차단 카운트(`blockWorldInput`)가 정상적으로 감소·해제되도록 하라.

### 3.5 Addressable 비동기 로딩 레이스 컨디션 예방
- 어드레서블 리소스 로딩 완료(`isLoadComplete == true`) 이전에 동기 메서드가 UI 뷰나 컴포넌트를 조작하지 못하도록 보장하라.
- `await` 패턴 또는 완료 이벤트를 구독하여 데이터 선적재 및 바인딩이 안전한 순서로 일어나도록 코드를 구성하라.

### 3.6 XLua 샌드박스 제약 (XLua Sandbox Restrictions)
- XLua 환경에서는 보안 격리를 위해 `CS` 전역 테이블이 삭제(`CS = nil`)된 상태로 구동된다.
- 따라서 루아 스크립트 내부에서 C# 클래스나 네이티브 유니티 API를 직접 호출하는 설계는 **동작하지 않는다**.
- 반드시 `LuaEventBinder`에 정의된 동적 이벤트 바인딩 방식이나, 사전 정의되어 주입된 안전한 C# 데이터 구조체(`DamageInfo`, `ShieldInfo` 등)를 매개체로 사용하라.

### 3.7 신규 이벤트 인터페이스 추가 시 3단계 체크리스트
- 새로운 전투/플레이어 이벤트 인터페이스(`IOn...`)를 추가할 때는 반드시 다음 3가지를 동시에 수행하라:
  1. `CharacterInterfaces.cs` 또는 `PlayerInterfaces.cs`에 인터페이스 정의
  2. `GenConfig.cs`의 `CSharpCallLua` 리스트에 해당 타입 등록 (누락 시 XLua 런타임에 `InvalidCastException` 발생)
  3. `.agents/PROJECT_MAP.md` §6 업데이트

### 3.8 이벤트 구조체(struct) payload 설계 및 수정 규칙
- 전투/이동/이펙트 파이프라인에서 `DamageInfo`, `MoveInfo`, `EffectInfo` 등 struct 페이로드의 값을 리스너가 수정해야 하는 Before 계열 이벤트(`IOnBefore...`)는 반드시 인터페이스 메서드 시그니처에 `ref`를 붙이고, 호출 시 클로저 할당이 없는 `TryGetListeners` 역순 루프 패턴을 사용하라.
- 리스너가 값을 변경할 필요가 없는 단순 통지형(After 계열 또는 UI 알림) 이벤트 객체는 불변(`readonly struct`) 형태로 선언하여 상태 오염을 방지하라.

### 3.9 씬·에셋 정적 배치 우선 및 런타임 폴백 땜질 금지 (Asset-First & No Runtime Fallback Hack)
- 카메라, 캔버스 레이어, URP 카메라 스택, 고정 UI 인프라 등 씬이나 프리팹 에셋 차원에서 정적으로 구성 가능한 요소는 C# 런타임 코드에서 `new GameObject()`, `AddComponent()`, `FindObjectOfType()`으로 동적 생성하거나 임의의 폴백(`fallback`)을 두어 땜질하지 마라.
  - 반드시 유니티 에디터(씬/프리팹)에 사전 정적 배치하고, 직렬화 필드(`[SerializeField]`) 또는 URP 카메라 스택에 등록하여 관리하라.
- 런타임 C# 코드에서 직접 접근하거나 제어할 필요가 없는 참조(예: 단순 렌더링용 프로퍼티 등)를 억지로 코드로 주입/바인딩하는 불필요한 보일러플레이트를 작성하지 마라.
- 필수 에셋 또는 컴포넌트 바인딩 누락 시 임의의 기본값으로 조용히 폴백하여 비정상 상태를 유지하지 말고, 초기화 시점에 `Debug.LogError`를 출력하여 문제를 즉시 노출하는 **Fail-Fast 원칙**을 준수하라.

### 3.10 에디터 프리팹 빌더/셋업 스크립트 동기화 규칙 (Editor Prefab Setup Script Sync)
- 프로젝트 내에 프리팹을 프로그래밍 방식으로 생성하거나 인스펙터 프로퍼티를 일괄 세팅하는 에디터 스크립트(`Assets/Editor/...Setup...Prefab.cs` 등)가 존재하는 경우, UI 배치/좌표/크기/직렬화 필드를 변경할 때는 **프리팹 에셋(.prefab)과 에디터 셋업 스크립트 양쪽을 반드시 동시에 수정·동기화**하라.
- 어느 한쪽만 수정할 경우 에디터 재컴파일이나 셋업 메뉴 재실행 시 기존 작업물이 이전 값으로 덮어씌워지거나 런타임에 미반영된다.

### 3.11 재사용 인디케이터 및 뷰의 활성화(SetActive) 보장 규칙 (Indicator & View Lifecycle)
- `Clear()` 또는 `Hide()` 시 `SetActive(false)` 처리되는 공유 인디케이터(타일 범위/사거리 표시기), 팝업, 하이라이트 뷰 등은 데이터를 표시하는 진입점(`Show...`, `SetData...`, `UpdateView...`)에서 반드시 `gameObject.SetActive(true)`를 명시적으로 호출하여 시각적 활성 상태를 보장하라.
- 뷰 내부 자식 요소나 타일 컬렉션만 갱신하고 최상위 루트가 비활성화된 채 방치되어 화면에 렌더링되지 않는 침묵 실패(Silent Failure)를 철저히 방지하라.

### 3.12 YAML 데이터 문자열과 C# Enum 파싱 정규화 규칙 (Data Normalization Rule)
- YAML 등 외부 데이터 시트에서 기획자가 작성하는 문자열 필드(예: 타깃 타입, 속성, 상태이상 명칭 등)를 C# Enum으로 변환할 때 단순 `Enum.TryParse`에만 의존하지 마라.
- 기획 데이터에서 널리 쓰이는 동의어/별칭(예: `"Enemy"`/`"Single"` → `TargetType.Target`, `"AoE"`/`"All"` → `TargetType.Area`)을 매핑하는 정규화(Normalization) 함수를 데이터 계층에 반드시 구현하여, 기획 데이터 불일치로 인한 침묵 실패를 방지하라.

---

## 4. Unity C# 코딩 규칙 (Coding Conventions)

### 4.1 UnityEngine.Object Null 검사 규칙 (가장 중요)
- `MonoBehaviour`, `ScriptableObject` 등 `UnityEngine.Object`를 상속받은 모든 객체에 대해 C#의 Null 조건 연산자(`?.`) 또는 Null 병합 연산자(`??`)를 **절대 사용하지 마라**.
- 유니티의 네이티브(C++) 객체 소멸 검사(Fake Null Check)가 정상 작동하도록 반드시 명시적인 `== null` 또는 `!= null`을 사용하라.
  - ❌ *잘못된 예*: `myComponent?.DoSomething();`
  - ✅ *올바른 예*: `if (myComponent != null) { myComponent.DoSomething(); }`

### 4.2 Garbage Collection (GC) 최적화 규칙
- 핫패스(`Update`, `FixedUpdate`, 프레임 틱)나 자주 호출되는 이벤트 메서드 내부에서 박싱(Boxing), LINQ 호출, 빈번한 람다 할당, `new` 키워드를 사용한 임시 객체 생성을 엄격히 금지한다. 컬렉션 재사용 및 Object Pooling 원칙을 준수하라.
- 코루틴 내부 루프에서 `yield return new WaitForSeconds(delay)` 반복 생성을 **금지**한다. 대신 클래스 멤버 변수로 `WaitForSeconds` 객체를 캐싱하여 사용하라:
  ~~~csharp
  private readonly WaitForSeconds _checkInterval = new WaitForSeconds(0.1f);
  // ...
  yield return _checkInterval;
  ~~~

### 4.3 유니티 메타(.meta) 파일 동기화 규칙
- 유니티 프로젝트 내 모든 에셋(스크립트, 텍스처, 프리팹 등)은 대응되는 `.meta` 파일과 함께 생성, 이동, 삭제되어야 한다.
- C# 클래스를 생성하거나 삭제할 때 반드시 해당 `.meta` 파일이 Git에 동기화되었는지 확인하라.

### 4.4 유니티 C# 버전 호환성 및 패키지 가이드
- 유니티 컴파일러 빌드 안정성을 위해 파일 범위 네임스페이스(`namespace MySpace;`) 같은 최신 C# 문법은 지양하고, 기존 파일 구조와 동일하게 중괄호 블록 형태(`namespace MySpace { ... }`)를 유지하라.
- 입력 시스템은 신규 입력 시스템(`com.unity.inputsystem`)을 우선 사용하라.

### 4.5 파일 인코딩(Encoding) 강제 규칙
- 모든 `.cs`, `.yaml`, `.lua`, `.md` 파일을 생성하거나 수정할 때는 반드시 **UTF-8 인코딩**을 유지하라.
- CP949/EUC-KR 혼용으로 인한 한글 주석 깨짐(Mojibake) 현상을 절대 발생시키지 마라.

### 4.6 런타임 스크립트 빌드 금지 네임스페이스 규칙
- 런타임 스크립트(`Assets/Scripts/` 하위)에는 절대 `UnityEditor`, `NUnit.Framework`, `Newtonsoft.Json.Schema` 네임스페이스를 `#if UNITY_EDITOR` 전처리기 없이 `using`하지 마라. Standalone 플레이어 빌드 시 치명적인 컴파일 에러를 유발한다.

---

## 5. 도구 활용 및 로그 출력 규칙 (Tool Usage & Log Reporting)

- 대규모 빌드 에러 출력, `Assembly-CSharp.csproj` 분석, Unity Editor 로그(`.log`), 대규모 `git diff` 조회 시 전체 텍스트를 무분별하게 출력하여 컨텍스트를 오염시키지 말고, `grep_search`, `view_file`(범위 지정), 파워셸 필터링(`Select-String`, `-Tail`)을 활용해 필요한 문맥만 국소 조회하라.
- 최종 사용자에게는 긴 엔진 로그 원본을 복사해 붙여넣는 대신 **"발생한 CS 에러 번호"**, **"원인(인터페이스/이벤트 불일치 등)"**, **"수정 사항"**만 정제하여 보고하라.