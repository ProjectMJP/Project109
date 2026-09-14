using System.Collections.Generic;
using UnityEngine;

public class BattleHandPanel : UIPanelBase
{
    [Header("Layout Settings")]
    public Transform contentTransform;
    public float maxPanelWidth = 900f;
    public float defaultCardSpacing = 160f;

    private List<GameObject> activeCardUIs = new List<GameObject>();

    /// <summary>
    /// 플레이어의 손패 카드를 UI에 반영하고 가로로 정렬합니다.
    /// </summary>
    public void RefreshHand(List<Card> handCards)
    {
        ClearHandUI();

        if (handCards == null || handCards.Count == 0) return;

        if (ObjectPoolManager.instance == null)
        {
            Debug.LogError("[BattleHandPanel] ObjectPoolManager.instance is null!");
            return;
        }

        int count = handCards.Count;
        for (int i = 0; i < count; i++)
        {
            Card card = handCards[i];
            GameObject cardObj = ObjectPoolManager.instance.GetCardUI(contentTransform);
            if (cardObj != null)
            {
                CardUI cardUI = cardObj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.UpdateCardInstance(card);
                    cardUI.enableZoom = true;
                    cardUI.zoomScale = 1.1f;
                    cardUI.zoomYOffset = 90f;
                    cardUI.bIsCardHighlight = true;
                    cardUI.bShowEffectAreaUI = true;

                    // 카드 클릭 시 플레이어 컨트롤러의 카드 시전 시도 연동
                    cardUI.OnCardClick.RemoveAllListeners();
                    cardUI.OnCardClick.AddListener(() =>
                    {
                        if (RunManager.instance != null && RunManager.instance.playerBattleController != null)
                        {
                            RunManager.instance.playerBattleController.TryUseCard(card);
                        }
                    });
                }
                activeCardUIs.Add(cardObj);
            }
        }

        UpdateCardLayout();
    }

    /// <summary>
    /// 카드 수량과 가로 제약 폭에 맞춰 카드 배치를 갱신합니다.
    /// </summary>
    public void UpdateCardLayout()
    {
        int count = activeCardUIs.Count;
        if (count == 0) return;

        float spacing = defaultCardSpacing;
        if (count > 1)
        {
            float requiredWidth = (count - 1) * defaultCardSpacing;
            if (requiredWidth > maxPanelWidth)
            {
                // 최대 가로 폭을 초과할 경우 겹치도록 간격을 축소
                spacing = maxPanelWidth / (count - 1);
            }
        }

        float startX = -((count - 1) * spacing) / 2f;

        for (int i = 0; i < count; i++)
        {
            GameObject cardObj = activeCardUIs[i];
            RectTransform rect = cardObj.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(startX + (i * spacing), 0f);
                rect.localScale = Vector3.one;
            }
            cardObj.transform.SetSiblingIndex(i); // SiblingIndex를 순서대로 맞춰 겹침 렌더링 순서 보장
        }
    }

    private void ClearHandUI()
    {
        foreach (GameObject cardObj in activeCardUIs)
        {
            if (cardObj != null)
            {
                if (ObjectPoolManager.instance != null)
                {
                    ObjectPoolManager.instance.ReturnCardUI(cardObj);
                }
                else
                {
                    Destroy(cardObj);
                }
            }
        }
        activeCardUIs.Clear();
    }

    private void OnDestroy()
    {
        ClearHandUI();
    }
}
