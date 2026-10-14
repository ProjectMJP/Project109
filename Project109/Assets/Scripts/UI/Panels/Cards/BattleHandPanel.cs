using System.Collections.Generic;
using UnityEngine;

public class BattleHandPanel : UIPanelBase
{
    [Header("Layout Settings")]
    public Transform contentTransform;
    public float maxPanelWidth = 900f;
    public float defaultCardSpacing = 160f;

    private readonly List<GameObject> activeCardUIs = new List<GameObject>();
    private BattleDeck _boundDeck;

    private void Awake()
    {
        uiLayerType = UILayerType.Top;
        blockWorldInput = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        // 바인딩된 덱이 없으면 현재 플레이어의 전투 덱 자동 바인딩 시도
        if (_boundDeck == null && GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            BindBattleDeck(GameSceneManager.instance.player.battleDeck);
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnbindBattleDeck();
    }

    /// <summary>
    /// 플레이어의 전투 덱을 바인딩하여 손패 변경(OnHandChanged) 이벤트를 수신합니다.
    /// </summary>
    public void BindPlayer(Player player)
    {
        BindBattleDeck(player != null ? player.battleDeck : null);
    }

    /// <summary>
    /// 전투 덱을 바인딩하고 손패 UI를 즉시 갱신합니다.
    /// </summary>
    public void BindBattleDeck(BattleDeck deck)
    {
        if (_boundDeck == deck) return;

        UnbindBattleDeck();
        _boundDeck = deck;

        if (_boundDeck != null)
        {
            _boundDeck.OnHandChanged += HandleHandChanged;
            RefreshHand(_boundDeck.hand);
        }
    }

    /// <summary>
    /// 등록된 전투 덱 이벤트 구독을 해제합니다.
    /// </summary>
    public void UnbindBattleDeck()
    {
        if (_boundDeck != null)
        {
            _boundDeck.OnHandChanged -= HandleHandChanged;
            _boundDeck = null;
        }
    }

    private void HandleHandChanged()
    {
        if (_boundDeck != null)
        {
            RefreshHand(_boundDeck.hand);
        }
    }

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
            if (card == null) continue;

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
            if (cardObj == null) continue;

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
        for (int i = 0; i < activeCardUIs.Count; i++)
        {
            GameObject cardObj = activeCardUIs[i];
            if (cardObj != null)
            {
                CardUI cardUI = cardObj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.OffSelectHighlight();
                    cardUI.bIsCardHighlight = false;
                }

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
        UnbindBattleDeck();
        ClearHandUI();
    }
}
