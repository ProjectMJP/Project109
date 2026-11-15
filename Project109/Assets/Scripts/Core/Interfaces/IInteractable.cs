using UnityEngine;

public interface IInteractable
{
    // 카메라 포커스 이동이 필요한지 여부
    bool RequiresCameraFocus { get; }

    // 터치(클릭) 시 실행될 로직
    void OnInteract();
}
