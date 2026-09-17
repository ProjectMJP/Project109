using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour
{
    private static PlayerInputController _instance;
    private static bool _isShuttingDown = false;

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
    public event Action OnCancelEvent; // 우클릭 및 ESC 취소 이벤트

    /// <summary>
    /// UI 팝업 등으로 인해 월드 상호작용 입력(터치/드래그/클릭)이 차단되어야 하는지 여부입니다.
    /// UIManager.IsWorldInputBlocked 상태와 동기화됩니다.
    /// </summary>
    public bool IsWorldInputBlocked { get; set; } = false;

    private Vector2 startTouchPos;
    private Vector2 lastTouchPos;
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

        // 마우스 우클릭 혹은 ESC(취소) 입력 감지 (월드 조작 취소)
        if (!IsWorldInputBlocked)
        {
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                OnCancelEvent?.Invoke();
            }
            else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                OnCancelEvent?.Invoke();
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
}
