using System.Collections;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController instance { get; private set; }

    private Camera mainCamera;
    private Vector3 dragOrigin;
    private bool _isDraggingWorld = false;
    private Coroutine _focusCoroutine;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        SubscribeInputEvents();
    }

    private void OnEnable()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        SubscribeInputEvents();
    }

    private void OnDisable()
    {
        _isDraggingWorld = false;
        UnsubscribeInputEvents();
    }

    private void OnDestroy()
    {
        _isDraggingWorld = false;
        UnsubscribeInputEvents();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void SubscribeInputEvents()
    {
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchStartEvent -= HandleTouchStart;
            PlayerInputController.instance.OnTouchStartEvent += HandleTouchStart;

            PlayerInputController.instance.OnTouchDragEvent -= HandleTouchDrag;
            PlayerInputController.instance.OnTouchDragEvent += HandleTouchDrag;

            PlayerInputController.instance.OnTouchEndEvent -= HandleTouchEnd;
            PlayerInputController.instance.OnTouchEndEvent += HandleTouchEnd;

            PlayerInputController.instance.OnCameraFocusRequested -= CameraFocusToTarget;
            PlayerInputController.instance.OnCameraFocusRequested += CameraFocusToTarget;
        }
    }

    private void UnsubscribeInputEvents()
    {
        // 종료/해제 시점에는 HasInstance를 먼저 확인하여 불필요한 싱글턴 지연 생성을 방지합니다.
        if (PlayerInputController.HasInstance)
        {
            PlayerInputController.instance.OnTouchStartEvent -= HandleTouchStart;
            PlayerInputController.instance.OnTouchDragEvent -= HandleTouchDrag;
            PlayerInputController.instance.OnTouchEndEvent -= HandleTouchEnd;
            PlayerInputController.instance.OnCameraFocusRequested -= CameraFocusToTarget;
        }
    }

    private void HandleTouchStart(Vector2 screenPos)
    {
        if (mainCamera == null ||
            UIDragInputGuard.IsAnyUIDragging ||
            (PlayerInputController.HasInstance && PlayerInputController.instance.IsPointerDragBlockedByUI))
        {
            _isDraggingWorld = false;
            return;
        }

        _isDraggingWorld = true;
        dragOrigin = mainCamera.ScreenToWorldPoint(screenPos);
    }

    private void HandleTouchDrag(Vector2 screenPos)
    {
        if (!_isDraggingWorld || mainCamera == null)
        {
            return;
        }

        if (UIDragInputGuard.IsAnyUIDragging ||
            (PlayerInputController.HasInstance && PlayerInputController.instance.IsPointerDragBlockedByUI))
        {
            _isDraggingWorld = false;
            return;
        }

        Vector3 dragDiff = dragOrigin - mainCamera.ScreenToWorldPoint(screenPos);
        mainCamera.transform.position = CameraMoveInClampArea(mainCamera.transform.position + dragDiff);

        // 부자연스러운 움직임을 막기 위해 마지막에 있던 마우스 위치값 업데이트
        dragOrigin = mainCamera.ScreenToWorldPoint(screenPos);
    }

    private void HandleTouchEnd()
    {
        _isDraggingWorld = false;
    }

    public void CameraFocusToTarget(Vector3 targetPos)
    {
        if (mainCamera == null) return;

        Vector3 worldToTarget = targetPos - mainCamera.transform.position;

        Vector3 planeOffset = mainCamera.transform.right * Vector3.Dot(worldToTarget, mainCamera.transform.right) +
                              mainCamera.transform.up * Vector3.Dot(worldToTarget, mainCamera.transform.up);

        if (_focusCoroutine != null)
        {
            StopCoroutine(_focusCoroutine);
        }
        _focusCoroutine = StartCoroutine(SmoothFocus(mainCamera.transform.position + planeOffset, 0.5f));
    }

    private Vector3 CameraMoveInClampArea(Vector3 targetPos)
    {
        // 화면 중앙 시선 Ray를 Input 이벤트 허브의 검증 위임자로 전달하여 L2 Map/Tile 물리 레이어 직접 의존을 배제합니다.
        Ray ray = new Ray(targetPos, mainCamera.transform.forward);
        bool isWithinClampArea;

        if (PlayerInputController.HasInstance)
        {
            isWithinClampArea = PlayerInputController.instance.ValidateCameraPanRay(ray);
        }
        else
        {
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            isWithinClampArea = groundPlane.Raycast(ray, out float enter) && enter >= 0f;
        }

        return isWithinClampArea ? targetPos : mainCamera.transform.position;
    }

    private IEnumerator SmoothFocus(Vector3 targetPos, float duration = 0.5f)
    {
        Vector3 startPos = mainCamera.transform.position;
        float t = 0;
        
        while(t < 1f)
        {
            t += Time.deltaTime / duration;
            float easeOut = 1f - Mathf.Pow(1f - t, 2f);
            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, easeOut);
            yield return null;
        }

        mainCamera.transform.position = targetPos;
        _focusCoroutine = null;
    }
}
