using UnityEngine;

/// <summary>
/// 로비에 배치된 던전 진입 게이트 컴포넌트입니다.
/// 플레이어 캐릭터가 게이트 영역(Trigger Collider)에 닿으면 
/// RunManager를 통해 임시 로드아웃을 확정하고 첫 던전 런으로 진입시킵니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LobbyDungeonGate : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 충돌한 오브젝트가 플레이어 캐릭터인지 확인
        Character character = other.GetComponent<Character>();
        if (character == null)
        {
            var controller = other.GetComponent<ICharacterController>();
            if (controller != null)
            {
                character = controller.controlledCharacter;
            }
        }

        if (character != null)
        {
            // 플레이어 캐릭터가 게이트에 접촉했으므로 최상위 GameSceneManager를 통해 던전 출격!
            Debug.Log("[Lobby] 플레이어가 던전 진입 게이트에 진입했습니다. TransitionToDungeon을 실행합니다.");
            if (GameSceneManager.instance != null)
            {
                GameSceneManager.instance.TransitionToDungeon();
            }
            else
            {
                Debug.LogWarning("[Lobby] GameSceneManager instance가 존재하지 않아 진입할 수 없습니다.");
            }
        }
    }
}
