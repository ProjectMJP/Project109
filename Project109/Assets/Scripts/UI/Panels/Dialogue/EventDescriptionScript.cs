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
        GameObject button = GameObject.Instantiate(eventButtonPrefab, eventButtonSpawnPoint);
        if (button != null)
        {
            var textMesh = button.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            textMesh.text = buttonDescription;
            textMesh.textWrappingMode = TextWrappingModes.Normal;
            var fitter = button.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = button.AddComponent<ContentSizeFitter>();
            }
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layoutGroup = button.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = button.AddComponent<VerticalLayoutGroup>();
                layoutGroup.padding = new RectOffset(15, 15, 10, 10);
                layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                layoutGroup.childControlWidth = true;
                layoutGroup.childControlHeight = true;
                layoutGroup.childForceExpandWidth = true;
                layoutGroup.childForceExpandHeight = false;
            }
            var parentLayoutGroup = eventButtonSpawnPoint.GetComponent<VerticalLayoutGroup>();
            if (parentLayoutGroup != null)
            {
                parentLayoutGroup.childControlWidth = true;
                parentLayoutGroup.childForceExpandWidth = true;
                parentLayoutGroup.childControlHeight = true;
                parentLayoutGroup.childForceExpandHeight = false;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(button.GetComponent<RectTransform>());
            if (parentLayoutGroup != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(eventButtonSpawnPoint.GetComponent<RectTransform>());
            }
        }
        return button.GetComponent<Button>();
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
