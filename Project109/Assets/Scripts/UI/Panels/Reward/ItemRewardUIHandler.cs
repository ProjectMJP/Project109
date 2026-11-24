using GameItem.Types;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemRewardUIHandler : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private PendingRewardEntry _boundEntry;
    private RewardListUIHandler _ownerPanel;
    private RelicData _resolvedRelicData;
    private CardData _resolvedSpecificCard;
    private RewardType _rewardType;
    private int _rewardValue;

    [SerializeField] private Image itemTexture;
    [SerializeField] private TextMeshProUGUI itemText;

    [Header("ItemTextures")]
    [SerializeField] private Sprite goldTexture;
    [SerializeField] private Sprite memorySharpTexture;

    public PendingRewardEntry BoundEntry => _boundEntry;

    public void BindPendingEntry(PendingRewardEntry entry, RewardListUIHandler ownerPanel)
    {
        _boundEntry = entry;
        _ownerPanel = ownerPanel;
        _resolvedRelicData = null;
        _resolvedSpecificCard = null;

        if (entry == null)
        {
            return;
        }

        _rewardType = entry.rewardType == RewardType.Card ? RewardType.CardChoice : entry.rewardType;
        _rewardValue = entry.amount;

        switch (_rewardType)
        {
            case RewardType.Gold:
                if (itemTexture != null)
                {
                    itemTexture.gameObject.SetActive(true);
                    if (goldTexture != null) itemTexture.sprite = goldTexture;
                }
                if (itemText != null)
                {
                    itemText.text = $"{entry.amount} 골드";
                }
                break;

            case RewardType.MemorySharp:
                if (itemTexture != null)
                {
                    itemTexture.gameObject.SetActive(true);
                    if (memorySharpTexture != null) itemTexture.sprite = memorySharpTexture;
                }
                if (itemText != null)
                {
                    itemText.text = $"기억의 파편 x{entry.amount}";
                }
                break;

            case RewardType.CardChoice:
                if (itemTexture != null)
                {
                    itemTexture.gameObject.SetActive(false);
                }
                if (itemText != null)
                {
                    itemText.text = "새로운 기억 보상!";
                }
                break;

            case RewardType.SpecificCard:
                if (itemTexture != null)
                {
                    itemTexture.gameObject.SetActive(false);
                }
                if (GameItemRewardManager.instance != null)
                {
                    _resolvedSpecificCard = GameItemRewardManager.instance.GetCardDataByName(entry.referenceID);
                }
                if (itemText != null)
                {
                    itemText.text = _resolvedSpecificCard != null ? _resolvedSpecificCard.LocalizedName : "새로운 기억 보상!";
                }
                break;

            case RewardType.Relic:
            case RewardType.SpecificRelic:
                if (GameItemRewardManager.instance != null)
                {
                    _resolvedRelicData = GameItemRewardManager.instance.ResolveOrRollRelicForEntry(entry);
                }

                if (itemTexture != null)
                {
                    if (_resolvedRelicData != null && _resolvedRelicData.iconSprite != null)
                    {
                        itemTexture.sprite = _resolvedRelicData.iconSprite;
                        itemTexture.gameObject.SetActive(true);
                    }
                    else
                    {
                        itemTexture.gameObject.SetActive(false);
                    }
                }

                if (itemText != null)
                {
                    itemText.text = _resolvedRelicData != null ? _resolvedRelicData.LocalizedName : "유물 보상";
                }
                break;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_boundEntry == null)
        {
            return;
        }

        if (TooltipManager.HasInstance)
        {
            TooltipManager.Instance.HideTooltip();
        }

        if (_rewardType == RewardType.CardChoice || _rewardType == RewardType.SpecificCard)
        {
            if (UIManager.instance != null)
            {
                CardRewardPanel cardPanel = UIManager.instance.OpenUI<CardRewardPanel>(UIConstants.PANEL_CARD_REWARD, UILayerType.Popup);
                if (cardPanel != null)
                {
                    cardPanel.BindPendingEntry(_boundEntry, _ownerPanel);
                }
            }
            return;
        }

        if (RunManager.instance != null && RunManager.instance.TryClaimPendingReward(_boundEntry.entryId, _resolvedSpecificCard, _resolvedRelicData))
        {
            if (_ownerPanel != null)
            {
                _ownerPanel.NotifyRewardClaimed();
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.Instance == null || _boundEntry == null)
        {
            return;
        }

        RectTransform targetRect = transform as RectTransform;
        if ((_rewardType == RewardType.Relic || _rewardType == RewardType.SpecificRelic) && _resolvedRelicData != null)
        {
            TooltipManager.Instance.ShowTooltip(_resolvedRelicData.LocalizedName, _resolvedRelicData.LocalizedDescription, targetRect);
        }
        else if (_rewardType == RewardType.CardChoice)
        {
            int count = _boundEntry.amount > 0 ? _boundEntry.amount : 3;
            TooltipManager.Instance.ShowTooltip("카드 보상", $"제시된 {count}장의 카드 중 1장을 선택해 덱에 추가합니다.", targetRect);
        }
        else if (_rewardType == RewardType.Gold)
        {
            TooltipManager.Instance.ShowTooltip("골드", $"{_rewardValue} 골드를 획득합니다.", targetRect);
        }
        else if (_rewardType == RewardType.MemorySharp)
        {
            TooltipManager.Instance.ShowTooltip("기억의 파편", $"기억의 파편 {_rewardValue}개를 획득합니다.", targetRect);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.HasInstance)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }
}
