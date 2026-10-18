---
name: unity-ui-layout-tester
description: 유니티 내 UI Hierarchy 요소를 탐색하고, UI 입력 시뮬레이션 및 캡처 이미지를 활용해 UI 상태와 레이아웃을 검증하는 스킬입니다.
---

# Unity UI Layout Tester 가이드

본 스킬이 활성화되면 에이전트는 유니티 Hierarchy의 UI 요소를 분석하고, 시뮬레이션 입력을 주어 레이아웃이 정상적으로 출력되고 클릭이 동작하는지 검증한다.

## [프로세스 단계]

### 1단계: UI 게임오브젝트 탐색
- `find_ui_elements` 또는 `get_hierarchy` 도구를 호출하여 타겟 UI(예: Canvas, Button, Panel)가 올바른 계층 구조를 이루고 있는지 확인한다.
- **[Project109 전용 런타임 UI 계층 힌트]**: Project109의 인게임 화면 UI(패널, 팝업, 카드 핸드 등)는 정적 씬 계층에 미리 배치되어 있지 않고, 런타임에 `UIManager` 하위의 `MainUICanvas`(`HUDLayer`, `PopupLayer`) 및 `WorldUICanvas`에 동적으로 생성된다. 따라서 정적 씬 계층에서 타겟 UI를 찾을 수 없다면 플레이모드 진입 후 `UIManager` 하위 경로를 탐색하라.
- `get_component_values` 도구를 사용하여 UI 요소들의 `RectTransform` 값이나 `Image`, `Button` 컴포넌트 설정이 정확한지 확인한다.


### 2단계: UI 클릭 및 입력 시뮬레이션
- 유니티를 플레이모드로 띄운 후(`play_game`), `click_ui_element` 또는 `simulate_ui_input` 도구를 호출하여 버튼 클릭, 텍스트 입력 등의 이벤트를 시뮬레이션한다.
- **[상호작용 딜레이 적용]**: UI 트랜지션 애니메이션(예: Fade, Slide)이 완료된 후에 클릭 이벤트가 접수되도록 상호작용 지시 전후에 최소 1초 이상의 대기 시간을 적용한다.
- 이로 인해 이벤트 헨들러가 트리거되고 정상적으로 씬 로드 또는 상태 전이가 일어나는지 검증한다.
- 키보드/마우스 물리 입력의 테스트가 필요할 경우, 설치된 신규 입력 시스템(`com.unity.inputsystem`) 도구인 `input_keyboard` 또는 `input_mouse`를 활용한다.

### 3단계: 시각적 레이아웃 검증 (스크린샷)
- 시뮬레이션 동작 후 `capture_screenshot` 도구를 사용하여 유니티 화면을 캡처한다.
- **[캡처 이미지 확인]**: 캡처된 파일은 유니티 캡처 전용 폴더(`C:\Users\siryu\AppData\Local\Programs\Antigravity IDE\.unity\captures\`)에 저장되므로, 에이전트는 해당 경로의 캡처 파일을 직접 조회하여 UI 겹침, 해상도 깨짐, 레이아웃 꼬임 등의 문제가 발생하지 않았는지 시각적으로 최종 검증을 완료하고 결과를 사용자에게 안내한다.
