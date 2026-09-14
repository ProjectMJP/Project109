using UnityEngine;

/// <summary>
/// 로비에 배치되어 플레이어가 상호작용(F키 또는 마우스 클릭)할 때 
/// 시작 키트(로드아웃)를 임시 선택할 수 있도록 해주는 컴포넌트입니다.
/// </summary>
public class LobbyInteractionStand : MonoBehaviour
{
    [Header("선택대 설정")]
    [SerializeField] private string _targetStarterKitId; // 선택 시 부여될 StarterKit ID
    [SerializeField] private string _standDisplayName;  // UI 표시 이름 (예: "전투광의 대검")

    /// <summary>
    /// 플레이어가 상호작용 범위에 들어와서 버튼을 누르거나 클릭했을 때 호출됩니다.
    /// </summary>
    public void Interact()
    {
        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            GameSceneManager.instance.player.selectedStarterKitId = _targetStarterKitId;
            Debug.Log($"[Lobby] 시작 무기/로드아웃이 '{_standDisplayName}'({_targetStarterKitId})으로 지정되었습니다.");
        }
        else
        {
            Debug.LogWarning("[Lobby] GameSceneManager 또는 Player 인스턴스가 존재하지 않습니다.");
        }
    }
}
