using GameItem.Types;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemRewardUIHandler : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject cardRewardUIPrefab;

    private GameObject currentRewardUIObject;
    private ItemRewardUIType currentItemRewardUIType;

    [SerializeField] private RelicData rewardRelicData;
    private int rewardValue;

    [SerializeField] private Image itemTexture;
    [SerializeField] private TextMeshProUGUI itemText;

    [Header("ItemTextures")]
    [SerializeField] private Sprite goldTexture;
    [SerializeField] private Sprite memorySharpTexture;

    void Start()
    {

    }

    public void SetReward(ItemRewardUIType npcType, string referenceID, int value)
    {
        currentItemRewardUIType = npcType;

        switch (npcType)
        {
            case ItemRewardUIType.Gold:
                rewardValue = value;
                itemTexture.sprite = goldTexture;
                itemText.text = $"{value}";
                break;
            case ItemRewardUIType.MemorySharp:
                rewardValue = value;
                itemTexture.sprite = memorySharpTexture;
                itemText.text = $"{value}";
                break;
            case ItemRewardUIType.Card:
                currentRewardUIObject = Instantiate(cardRewardUIPrefab);
                CardRewardPanel cardRewardHandler = currentRewardUIObject.GetComponent<CardRewardPanel>();
                int defaultRewardCount = (GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null)
                    ? GameSceneManager.instance.player.playerStat.RewardCardCount : 3;
                cardRewardHandler.SettingCards(referenceID, value > 0 ? value : defaultRewardCount);
                cardRewardHandler.rootObject = this.gameObject;
                cardRewardHandler.gameObject.SetActive(false);

                RunManager.instance.currentMap.currentSpawnUIList.Add(currentRewardUIObject);

                itemTexture.gameObject.SetActive(false);
                itemText.text = "새로운 기억 보상!";
                break;
            case ItemRewardUIType.Relic:    //단일 유물 획득 보상
                rewardRelicData = GameItemRewardManager.instance.GetRandomRelicDataByDropTable(referenceID);

                // if (AssetCacheManager.instance.TryGetTexture(rewardRelicData.texturePath, out Sprite texture))
                // {
                //     itemTexture.sprite = texture;
                // }
                // else
                // {
                //     Debug.LogWarning($"[ItemRewardUIHandler] 유물 텍스처 로드 실패: {rewardRelicData.texturePath}");
                // }
                itemText.text = $"{rewardRelicData.relicName}";
                break;
        }
    }

    public GameObject GetRewardUI() { return currentRewardUIObject; }

    public void OnPointerClick(PointerEventData eventData)
    {
        //UI 클릭 시 보상 UI 활성화
        switch (currentItemRewardUIType)
        {
            case ItemRewardUIType.Gold:
                if (GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null)
                {
                    GameSceneManager.instance.player.playerStat.InGameCurrencyGold += rewardValue;
                }
                break;
            case ItemRewardUIType.MemorySharp:
                if (GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null)
                {
                    GameSceneManager.instance.player.playerStat.InGameCurrencyMemorySharp += rewardValue;
                }
                break;
            case ItemRewardUIType.Card:
                currentRewardUIObject.GetComponent<CardRewardPanel>().Open();
                break;
            case ItemRewardUIType.Relic:
                if (GameSceneManager.instance != null && GameSceneManager.instance.player != null)
                {
                    GameSceneManager.instance.player.AddRelic(rewardRelicData.relicName);
                }
                //이 유물 선택지를 제공한 UI 제거
                if (TooltipManager.Instance != null)
                {
                    TooltipManager.Instance.HideTooltip();
                }
                break;
        }

        Destroy(gameObject);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        //마우스 오버 시 UI 하이라이트 활성화
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        //마우스 오버 시 UI 하이라이트 비활성화
    }
}
