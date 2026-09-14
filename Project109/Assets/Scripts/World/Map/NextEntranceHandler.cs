using UnityEngine;

public class NextEntranceHandler : MonoBehaviour, IInteractable
{
    private IncountNode currentNode;

    public void SetCurrentNode(IncountNode node)
    {
        currentNode = node;
    }

    public void ProcessNextEntrance()
    {
        if (currentNode == null)
            return;

        currentNode.LoadMapDataFromIncountNode();
    }

    public bool RequiresCameraFocus => false;

    public void OnInteract()
    {
        ProcessNextEntrance();
    }
}
