using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class EffectAreaManager : MonoBehaviour
{
    [SerializeField] private List<RawImage> activateBoxObject;
    [SerializeField] private Transform boxObjectsTransform;
    public Texture2D activeBoxTexture;
    public Texture2D deactiveBoxTexture;

    void Start()
    {
        //강제로 등록이 되어 있지 않다면 Transform의 자식 오브젝트들을 5개씩 5열로 리스트에 등록
        if (activateBoxObject.Count == 0)
        {
            for (int index = 0; index < boxObjectsTransform.childCount; index++)
            {
                activateBoxObject.Add(boxObjectsTransform.GetChild(index).GetComponent<RawImage>());
            }
        }

        EffectAreaDisable();
    }

    public void SetEffectArea(List<string> area)
    {
        ChangeEffectArea(area);
    }

    public void SetEffectArea(List<string> area, Vector3 newLocalScale)
    {
        gameObject.transform.localScale = newLocalScale;
        ChangeEffectArea(area);
    }

    private void ChangeEffectArea(List<string> area)
    {
        //Object가 등록이 되지 않았다면 강제로 등록
        if (activateBoxObject.Count == 0)
        {
            for (int i = 0; i < boxObjectsTransform.childCount; i++)
            {
                activateBoxObject.Add(boxObjectsTransform.GetChild(i).GetComponent<RawImage>());
            }

            EffectAreaDisable();
        }

        int index = 0;
        for (int i = 0; i < area.Count; i++)
        {
            for (int j = 0; j < area[i].Length; j++)
            {
                if (area[i][j] == '1')
                {
                    activateBoxObject[index].texture = activeBoxTexture;
                }
                else
                {
                    activateBoxObject[index].texture = deactiveBoxTexture;
                }

                index++;
            }
        }
    }

    public void EffectAreaEnable()
    {
        gameObject.SetActive(true);
    }

    public void EffectAreaDisable()
    {
        gameObject.SetActive(false);
    }
}
