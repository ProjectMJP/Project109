using System.Collections;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController instance { get; private set; }

    private Camera mainCamera;
    private Vector3 dragOrigin;

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
    }

    private void OnEnable()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchStartEvent -= HandleTouchStart;
            PlayerInputController.instance.OnTouchStartEvent += HandleTouchStart;

            PlayerInputController.instance.OnTouchDragEvent -= HandleTouchDrag;
            PlayerInputController.instance.OnTouchDragEvent += HandleTouchDrag;
        }
    }

    private void OnDisable()
    {
        if (PlayerInputController.instance != null)
        {
            PlayerInputController.instance.OnTouchStartEvent -= HandleTouchStart;
            PlayerInputController.instance.OnTouchDragEvent -= HandleTouchDrag;
        }
    }

    private void HandleTouchStart(Vector2 screenPos)
    {
        if (mainCamera != null)
        {
            dragOrigin = mainCamera.ScreenToWorldPoint(screenPos);
        }
    }

    private void HandleTouchDrag(Vector2 screenPos)
    {
        if (mainCamera == null) return;

        Vector3 dragDiff = dragOrigin - mainCamera.ScreenToWorldPoint(screenPos);
        mainCamera.transform.position = CameraMoveInClampArea(mainCamera.transform.position + dragDiff);

        // 부자연스러운 움직임을 막기 위해 마지막에 있던 마우스 위치값 업데이트
        dragOrigin = mainCamera.ScreenToWorldPoint(screenPos);
    }

    public void CameraFocusToTarget(Vector3 targetPos)
    {
        if (mainCamera == null) return;

        Vector3 worldToTarget = targetPos - mainCamera.transform.position;

        Vector3 planeOffset = mainCamera.transform.right * Vector3.Dot(worldToTarget, mainCamera.transform.right) +
                              mainCamera.transform.up * Vector3.Dot(worldToTarget, mainCamera.transform.up);

        StartCoroutine(SmoothFocus(mainCamera.transform.position + planeOffset, 0.5f));
    }

    private Vector3 CameraMoveInClampArea(Vector3 targetPos)
    {
        // 화면 중앙에서 나가는 Ray는 카메라의 위치(targetPos)에서 카메라가 바라보는 방향(forward)으로 나가는 선과 정확히 일치합니다.
        Ray ray = new Ray(targetPos, mainCamera.transform.forward);
        int layerMask = LayerMask.GetMask("Map", "Tile");

        // 1. 카메라가 targetPos로 이동했을 때 Map 또는 Tile 레이어의 오브젝트가 화면 중앙에 걸리는지 검사
        if (layerMask != 0 && Physics.Raycast(ray, out RaycastHit hit, 10000.0f, layerMask))
        {
            return targetPos; // Map이나 Tile이 있다면 해당 위치로 이동 허용
        }

        // 2. 콜라이더가 없는 경우에도 기본 지면 평면(Y=0)과 교차하는 경우 이동 허용 (드래그 먹통 방지)
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            return targetPos;
        }

        // 완전히 허공을 벗어난다면 원래 위치 유지
        return mainCamera.transform.position; 
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
    }
}
