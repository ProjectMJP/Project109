using UnityEngine;

/// <summary>
/// 은신처(Hideout)에 배치된 던전 진입 게이트 컴포넌트입니다.
/// 플레이어가 클릭하여 문 앞으로 이동한 뒤 상호작용(IInteractable)하면 
/// GameSceneManager를 통해 첫 던전 런으로 진입시킵니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HideoutDungeonGate : MonoBehaviour, IInteractable
{
    [Header("게이트 설정")]
    [SerializeField] private bool _requiresCameraFocus = false;

    public bool RequiresCameraFocus => _requiresCameraFocus;

    private void Awake()
    {
        // 클릭 레이캐스트 감지를 위해 오브젝트 레이어를 NPC로 보장
        int npcLayer = LayerMask.NameToLayer("NPC");
        if (npcLayer != -1)
        {
            gameObject.layer = npcLayer;
        }
    }

    /// <summary>
    /// 플레이어가 게이트 앞으로 이동을 마친 후 상호작용할 때 호출됩니다.
    /// </summary>
    public void OnInteract()
    {
        Debug.Log("[Hideout] 플레이어가 던전 진입 게이트와 상호작용했습니다. HideoutManager를 통해 출격을 요청합니다.");
        if (HideoutManager.instance != null)
        {
            HideoutManager.instance.EnterDungeon();
        }
        else if (GameSceneManager.instance != null)
        {
            GameSceneManager.instance.TransitionToDungeon();
        }
        else
        {
            Debug.LogWarning("[Hideout] GameSceneManager 및 HideoutManager 인스턴스가 존재하지 않아 진입할 수 없습니다.");
        }
    }
}
