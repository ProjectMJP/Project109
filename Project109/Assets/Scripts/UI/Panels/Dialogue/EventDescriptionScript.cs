using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EventDescriptionScript : UIPanelBase
{
    public TextMeshProUGUI description;

    public Transform eventButtonSpawnPoint;
    public GameObject eventButtonPrefab;

    void Start()
    {

    }

    public void SetDescription(string newDescription)
    {
        description.text = newDescription;
    }

    public Button CreateChoiceButton(string buttonDescription)
    {
        if (eventButtonPrefab == null || eventButtonSpawnPoint == null)
        {
            Debug.LogError("[EventDescriptionScript] eventButtonPrefab 또는 eventButtonSpawnPoint가 바인딩되지 않았습니다!", this);
            return null;
        }

        GameObject button = GameObject.Instantiate(eventButtonPrefab, eventButtonSpawnPoint);
        if (button != null)
        {
            var textMesh = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (textMesh != null)
            {
                textMesh.text = buttonDescription;
                textMesh.textWrappingMode = TextWrappingModes.Normal;
            }

            var rectTransform = button.transform as RectTransform;
            if (rectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }
            return button.GetComponent<Button>();
        }
        return null;
    }

    public void ClearChoiceButton()
    {
        if (eventButtonSpawnPoint.childCount > 0)
        {
            foreach (Transform t in eventButtonSpawnPoint)
            {
                Destroy(t.gameObject);
            }
        }
    }
}
