using UnityEngine;

public class MouseSeeThroughController : MonoBehaviour
{
    [Header("See-Through Settings (Screen Space)")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float maskRadius = 0.15f; // 화면 높이 대비 구멍의 반지름 (0.15 = 15% 크기)
    [Range(0.0f, 0.2f)]
    [SerializeField] private float fadeSoftness = 0.03f; // 테두리의 부드러운 감쇄 정도

    private Camera mainCamera;
    private int mousePosID;
    private int radiusID;
    private int softnessID;

    private Vector2 lastMousePos;
    private float aspect;
    private float lastAspectCheckTime;

    private void Awake()
    {
        mainCamera = Camera.main;
        mousePosID = Shader.PropertyToID("_SeeThroughMousePos");
        radiusID = Shader.PropertyToID("_SeeThroughRadius");
        softnessID = Shader.PropertyToID("_SeeThroughSoftness");

        // 초기 해상도 종횡비 계산
        UpdateAspectRatio();
    }

    private void UpdateAspectRatio()
    {
        float width = Screen.width;
        float height = Screen.height;
        aspect = height > 0f ? width / height : 1f;
    }

    private void Update()
    {
        // 셰이더로 스크린 단위 투과 반경 및 부드러움 데이터 지속 주입
        Shader.SetGlobalFloat(radiusID, maskRadius);
        Shader.SetGlobalFloat(softnessID, fadeSoftness);

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        Vector2 currentMousePos = Vector2.zero;
        if (UnityEngine.InputSystem.Pointer.current != null)
        {
            currentMousePos = UnityEngine.InputSystem.Pointer.current.position.ReadValue();
        }
        else
        {
            return;
        }

        // 최적화 1: 마우스 커서의 누적 미세 이동량이 매우 작을 시(0.5 픽셀 이하), 연산 및 전역 셰이더 데이터 주입 생략
        if (Vector2.SqrMagnitude(currentMousePos - lastMousePos) < 0.25f)
        {
            return;
        }
        lastMousePos = currentMousePos;

        // 최적화 2: 매 프레임 스크린 너비/높이 나눗셈 방지를 위해 0.5초마다 한번 종횡비 갱신
        if (Time.unscaledTime - lastAspectCheckTime > 0.5f)
        {
            UpdateAspectRatio();
            lastAspectCheckTime = Time.unscaledTime;
        }

        // 스크린 픽셀 좌표를 0~1 범위의 뷰포트 좌표로 변환
        Vector2 mouseViewport = mainCamera.ScreenToViewportPoint(currentMousePos);
        
        // x: 마우스 X뷰포트 * 종횡비, y: 마우스 Y뷰포트, z: 종횡비
        Vector4 mouseData = new Vector4(mouseViewport.x * aspect, mouseViewport.y, aspect, 0f);
        
        Shader.SetGlobalVector(mousePosID, mouseData);
    }
}
