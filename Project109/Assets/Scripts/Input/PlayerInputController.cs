using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour
{
    private static PlayerInputController _instance;
    private static bool _isShuttingDown = false;

    /// <summary>
    /// 지연 생성(Lazy Instantiation)을 유발하지 않고 현재 활성 인스턴스가 존재하는지 확인합니다.
    /// OnDisable / OnDestroy / Dispose 등 해제 시점에 안전하게 구독 해제할 때 사용합니다.
    /// </summary>
    public static bool HasInstance => _instance != null && !_isShuttingDown;

    public static PlayerInputController instance
    {
        get
        {
            if (_isShuttingDown) return null;

            if (_instance == null)
            {
                _instance = FindFirstObjectByType<PlayerInputController>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[PlayerInputController]");
                    _instance = go.AddComponent<PlayerInputController>();
                    if (Application.isPlaying)
                    {
                        DontDestroyOnLoad(go);
                    }
                }
            }
            return _instance;
        }
    }

    public PlayerInputAction playerInputAction;

    // 이벤트 정의
    public event Action<Vector2> OnTouchStartEvent;
    public event Action<Vector2> OnTouchDragEvent;
    public event Action OnTouchEndEvent;
    public event Action<Vector2> OnTouchClickEvent;
    public event Action<Vector2> OnPointerHoverEvent; // 마우스/포인터 실시간 이동(호버) 이벤트
    public event Action OnCancelEvent; // 우클릭 및 ESC 취소 이벤트
    public event Action<int> OnCardHotkeyEvent; // 숫자 단축키(0-based 손패 인덱스)
    public event Action OnEndTurnHotkeyEvent; // 턴 종료 단축키(E / Space)
    public event Action OnToggleMoveModeHotkeyEvent; // 이동 모드 토글 단축키(Tab / Q / M)
    public event Action<Vector3> OnCameraFocusRequested; // 월드 목표 지점 카메라 포커스 요청 이벤트

    private Func<Ray, bool> _cameraPanRayValidator;

    /// <summary>
    /// UI 팝업 등으로 인해 월드 상호작용 입력(터치/드래그/클릭)이 차단되어야 하는지 여부입니다.
    /// UIManager.IsWorldInputBlocked 상태와 동기화됩니다.
    /// </summary>
    public bool IsWorldInputBlocked { get; set; } = false;

    /// <summary>
    /// 현재 포인터 드래그가 UI 위에서 시작되었거나 활성 UI 드래그 가드에 의해 차단되는지 여부입니다.
    /// </summary>
    public bool IsPointerDragBlockedByUI => _startedOverUI || UIDragInputGuard.IsAnyUIDragging;

    /// <summary>
    /// 에디터 테스트에서 포인터의 UI 위 여부를 결정론적으로 제어하기 위한 오버라이드 델리게이트입니다.
    /// </summary>
    public Func<bool> PointerOverUIOverrideForTest;

    /// <summary>
    /// OnCancelEvent가 발행되는 시점에 월드 입력이 차단 상태였는지 여부입니다.
    /// ESC 1회 입력으로 최상위 팝업이 닫히면서 IsWorldInputBlocked가 해제되더라도,
    /// 동일 이벤트 호출 체인 내의 월드 조작(타깃팅/이동) 취소가 중복 실행되지 않도록 방어합니다.
    /// </summary>
    public bool WasWorldInputBlockedOnCancel { get; private set; } = false;

    /// <summary>
    /// 현재 발행 중인 OnCancelEvent가 ESC 키 입력에 의한 것인지 여부입니다. (우클릭에 의해 UI 팝업이 닫히는 현상 방어)
    /// </summary>
    public bool IsEscapeCancel { get; private set; } = false;

    private Vector2 startTouchPos;
    private Vector2 lastTouchPos;
    private Vector2 lastHoverPos;
    private bool isClickPending;
    private Vector2 pendingClickPos;
    private bool _startedOverUI = false;
    private bool _isWorldTouchActive = false;
    private bool _isInitialized = false;

    private void Update()
    {
        if (isClickPending)
        {
            isClickPending = false;
            if (!IsWorldInputBlocked)
            {
                OnTouchClickEvent?.Invoke(pendingClickPos);
            }
        }

        // ESC(전역 취소) 또는 마우스 우클릭(월드 비차단 시 조작 취소) 입력 감지
        bool isEscapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool isRightClickPressed = !IsWorldInputBlocked && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

        if (isEscapePressed || isRightClickPressed)
        {
            WasWorldInputBlockedOnCancel = IsWorldInputBlocked;
            IsEscapeCancel = isEscapePressed;
            OnCancelEvent?.Invoke();
            WasWorldInputBlockedOnCancel = false;
            IsEscapeCancel = false;
        }

        if (!IsWorldInputBlocked)
        {
            // 실시간 포인터 이동 감지 (호버 타깃팅용)
            Vector2 currentPointerPos = GetCurrentPointerPosition();
            if (currentPointerPos != lastHoverPos)
            {
                lastHoverPos = currentPointerPos;
                OnPointerHoverEvent?.Invoke(currentPointerPos);
            }

            // 전투 단축키 입력 감지 (텍스트 입력 필드 포커스 및 드래그 가드 중 차단)
            PollBattleHotkeys();
        }
    }

    private bool IsBattleHotkeyInputBlocked()
    {
        if (IsWorldInputBlocked || UIDragInputGuard.IsAnyUIDragging)
        {
            return true;
        }

        if (EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null &&
                (selected.GetComponent<TMPro.TMP_InputField>() != null ||
                 selected.GetComponent<UnityEngine.UI.InputField>() != null))
            {
                return true;
            }
        }

        return false;
    }

    private void PollBattleHotkeys()
    {
        if (Keyboard.current == null || IsBattleHotkeyInputBlocked())
        {
            return;
        }

        Keyboard kb = Keyboard.current;
        int pressedCardIndex = GetPressedCardHotkeyIndex(kb);
        if (pressedCardIndex >= 0)
        {
            ProcessCardHotkey(pressedCardIndex);
        }

        if (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
        {
            ProcessEndTurnHotkey();
        }

        if (kb.tabKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame)
        {
            ProcessToggleMoveModeHotkey();
        }
    }

    private static int GetPressedCardHotkeyIndex(Keyboard kb)
    {
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
        if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 3;
        if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) return 4;
        if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) return 5;
        if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) return 6;
        if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) return 7;
        if (kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame) return 8;
        return -1;
    }

    /// <summary>
    /// 손패 카드 단축키(1~9, 0-based 인덱스 0~8) 입력을 검증 후 이벤트로 발행합니다.
    /// </summary>
    public bool ProcessCardHotkey(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex > 8 || IsBattleHotkeyInputBlocked())
        {
            return false;
        }

        OnCardHotkeyEvent?.Invoke(cardIndex);
        return true;
    }

    /// <summary>
    /// 턴 종료 단축키(E / Space) 입력을 검증 후 이벤트로 발행합니다.
    /// </summary>
    public bool ProcessEndTurnHotkey()
    {
        if (IsBattleHotkeyInputBlocked())
        {
            return false;
        }

        OnEndTurnHotkeyEvent?.Invoke();
        return true;
    }

    /// <summary>
    /// 이동 모드 토글 단축키(Tab / Q / M) 입력을 검증 후 이벤트로 발행합니다.
    /// </summary>
    public bool ProcessToggleMoveModeHotkey()
    {
        if (IsBattleHotkeyInputBlocked())
        {
            return false;
        }

        OnToggleMoveModeHotkeyEvent?.Invoke();
        return true;
    }

    /// <summary>
    /// 카메라 포커스 이동 요청을 Input 이벤트 버스로 발행합니다.
    /// 활성 구독자(CameraController 등)가 존재하면 true를 반환합니다.
    /// </summary>
    public bool RequestCameraFocus(Vector3 targetWorldPos)
    {
        Action<Vector3> handler = OnCameraFocusRequested;
        if (handler == null)
        {
            return false;
        }

        handler.Invoke(targetWorldPos);
        return true;
    }

    /// <summary>
    /// 현재 활성 스테이지/맵의 카메라 팬 허용 경계 검증기를 등록합니다.
    /// </summary>
    public void SetCameraPanRayValidator(Func<Ray, bool> validator)
    {
        _cameraPanRayValidator = validator;
    }

    /// <summary>
    /// 등록된 카메라 팬 경계 검증기를 해제합니다.
    /// </summary>
    public void ClearCameraPanRayValidator(Func<Ray, bool> validator = null)
    {
        if (validator == null || _cameraPanRayValidator == validator)
        {
            _cameraPanRayValidator = null;
        }
    }

    /// <summary>
    /// 카메라 중앙 시선 Ray가 유효한 이동 허용 영역 내에 있는지 검증합니다.
    /// 외부 검증기가 없으면 기본 지면 평면(Y=0)과의 전방 교차 여부를 반환합니다.
    /// </summary>
    public bool ValidateCameraPanRay(Ray centerRay)
    {
        if (_cameraPanRayValidator != null)
        {
            if (_cameraPanRayValidator.Target is UnityEngine.Object unityTarget && unityTarget == null)
            {
                _cameraPanRayValidator = null;
            }
            else
            {
                return _cameraPanRayValidator.Invoke(centerRay);
            }
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        return groundPlane.Raycast(centerRay, out float enter) && enter >= 0f;
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
            InitializeInputActions();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void InitializeInputActions()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        if (playerInputAction == null)
        {
            playerInputAction = new PlayerInputAction();
        }

        playerInputAction.Player.Touch.started += OnTouchStart;
        playerInputAction.Player.Touch.performed += OnDrag;
        playerInputAction.Player.Touch.canceled += OnTouchClick;
        playerInputAction.Enable();
    }

    private void OnApplicationQuit()
    {
        _isShuttingDown = true;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _cameraPanRayValidator = null;
            OnCameraFocusRequested = null;
            if (playerInputAction != null)
            {
                playerInputAction.Player.Touch.started -= OnTouchStart;
                playerInputAction.Player.Touch.performed -= OnDrag;
                playerInputAction.Player.Touch.canceled -= OnTouchClick;
                playerInputAction.Disable();
                if (Application.isPlaying)
                {
                    playerInputAction.Dispose();
                }
                else if (playerInputAction.asset != null)
                {
                    DestroyImmediate(playerInputAction.asset);
                }
                playerInputAction = null;
            }
            _instance = null;
        }
    }

    public void OnEnable()
    {
        playerInputAction?.Enable();
    }

    public void OnDisable()
    {
        _startedOverUI = false;
        _isWorldTouchActive = false;
        playerInputAction?.Disable();
    }

    private bool IsPointerCurrentlyOverUI()
    {
        if (PointerOverUIOverrideForTest != null)
        {
            return PointerOverUIOverrideForTest();
        }

        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>
    /// 터치/드래그 시작 좌표를 기록하고 UI 위에서 시작되지 않았을 때만 월드 터치 시작 이벤트를 발행합니다.
    /// </summary>
    public void ProcessTouchStart(Vector2 pos)
    {
        startTouchPos = pos;
        lastTouchPos = pos;
        _startedOverUI = IsPointerCurrentlyOverUI();

        if (IsWorldInputBlocked || IsPointerDragBlockedByUI)
        {
            _isWorldTouchActive = false;
            return;
        }

        _isWorldTouchActive = true;
        OnTouchStartEvent?.Invoke(startTouchPos);
    }

    /// <summary>
    /// 터치/드래그 이동 좌표를 갱신하고 UI 드래그 차단 중이 아닐 때만 월드 드래그 이벤트를 발행합니다.
    /// 진행 중인 월드 터치 도중 UI 드래그 가드가 개입하면 즉시 월드 드래그 세션을 종료하여 재튀기를 방지합니다.
    /// </summary>
    public void ProcessTouchDrag(Vector2 pos)
    {
        lastTouchPos = pos;

        if (IsWorldInputBlocked || IsPointerDragBlockedByUI)
        {
            if (_isWorldTouchActive)
            {
                _isWorldTouchActive = false;
                OnTouchEndEvent?.Invoke();
            }
            return;
        }

        if (!_isWorldTouchActive)
        {
            return;
        }

        OnTouchDragEvent?.Invoke(lastTouchPos);
    }

    /// <summary>
    /// 터치/드래그 종료를 처리하여 UI 시작 플래그를 해제하고 종료 이벤트 및 클릭 판정을 수행합니다.
    /// </summary>
    public void ProcessTouchEnd()
    {
        bool wasWorldActive = _isWorldTouchActive;
        bool wasBlocked = _startedOverUI || UIDragInputGuard.IsAnyUIDragging || !wasWorldActive;
        _startedOverUI = false;
        _isWorldTouchActive = false;

        OnTouchEndEvent?.Invoke();

        // 클릭과 드래그 판정 로직
        if (!wasBlocked && !IsWorldInputBlocked && Vector2.Distance(startTouchPos, lastTouchPos) <= 20.0f)
        {
            isClickPending = true;
            pendingClickPos = lastTouchPos;
        }
    }

    private void OnTouchStart(InputAction.CallbackContext context)
    {
        ProcessTouchStart(context.ReadValue<Vector2>());
    }

    private void OnDrag(InputAction.CallbackContext context)
    {
        ProcessTouchDrag(context.ReadValue<Vector2>());
    }

    private void OnTouchClick(InputAction.CallbackContext context)
    {
        ProcessTouchEnd();
    }

    /// <summary>
    /// 현재 마우스 또는 포인터의 화면 스크린 좌표를 반환합니다.
    /// </summary>
    public Vector2 GetCurrentPointerPosition()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
        else if (Pointer.current != null)
        {
            return Pointer.current.position.ReadValue();
        }
        return lastTouchPos;
    }
}
