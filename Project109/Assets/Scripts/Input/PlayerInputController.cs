using System;
using UnityEngine;
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
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    public PlayerInputAction playerInputAction;

    // 이벤트 정의
    public event Action<Vector2> OnTouchStartEvent;
    public event Action<Vector2> OnTouchDragEvent;
    public event Action<Vector2> OnTouchClickEvent;
    public event Action<Vector2> OnPointerHoverEvent; // 마우스/포인터 실시간 이동(호버) 이벤트
    public event Action OnCancelEvent; // 우클릭 및 ESC 취소 이벤트

    /// <summary>
    /// UI 팝업 등으로 인해 월드 상호작용 입력(터치/드래그/클릭)이 차단되어야 하는지 여부입니다.
    /// UIManager.IsWorldInputBlocked 상태와 동기화됩니다.
    /// </summary>
    public bool IsWorldInputBlocked { get; set; } = false;

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
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
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
            if (playerInputAction != null)
            {
                playerInputAction.Player.Touch.started -= OnTouchStart;
                playerInputAction.Player.Touch.performed -= OnDrag;
                playerInputAction.Player.Touch.canceled -= OnTouchClick;
                playerInputAction.Disable();
                playerInputAction.Dispose();
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
        playerInputAction?.Disable();
    }


    private void OnTouchStart(InputAction.CallbackContext context)
    {
        startTouchPos = context.ReadValue<Vector2>();

        if (IsWorldInputBlocked) return;

        OnTouchStartEvent?.Invoke(startTouchPos);
    }

    private void OnDrag(InputAction.CallbackContext context)
    {
        lastTouchPos = context.ReadValue<Vector2>();

        if (IsWorldInputBlocked) return;

        OnTouchDragEvent?.Invoke(lastTouchPos);
    }

    private void OnTouchClick(InputAction.CallbackContext context)
    {
        // 클릭과 드래그 판정 로직
        if (Vector2.Distance(startTouchPos, lastTouchPos) <= 20.0f)
        {
            if (IsWorldInputBlocked) return;

            isClickPending = true;
            pendingClickPos = lastTouchPos;
        }
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
