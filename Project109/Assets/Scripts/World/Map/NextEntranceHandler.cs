using UnityEngine;

public class NextEntranceHandler : MonoBehaviour, IInteractable
{
    private ExploreNodeUI currentNode;

    public void SetCurrentNode(ExploreNodeUI node)
    {
        currentNode = node;
    }

    public void ProcessNextEntrance()
    {
        if (currentNode == null)
            return;

        currentNode.LoadMapDataFromExploreNodeUI();
    }

    public bool RequiresCameraFocus => false;

    public void OnInteract()
    {
        ProcessNextEntrance();
    }
}
