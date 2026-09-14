using UnityEngine;

public class OpenExploreMap : MonoBehaviour
{
    public GameObject exploreMapObject;

    private void Start()
    {
        exploreMapObject.SetActive(false);
    }

    public void OnClick_ChangeExploreMapUIActive()
    {
        if (exploreMapObject != null)
        {
            if (exploreMapObject.activeSelf)
            {
                exploreMapObject.SetActive(false);
            }
            else
            {
                exploreMapObject.SetActive(true);
            }
        }
    }
}
