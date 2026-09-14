using System.Collections.Generic;
using UnityEngine;
using GameItem.Types;

public class RelicRewardPanel : MonoBehaviour
{
    public GameObject rootObject;
    public GameObject relicObjectPrefab;
    [SerializeField] private Transform relicSpawnTransform;

    void Start()
    {

    }

    public void SettingRelics(string dropTableID, int rewardRelicCount)
    {
        for (int count = 0; count < rewardRelicCount; count++)
        {
            //유물UI 생성
            RelicUI relic = Instantiate(relicObjectPrefab, relicSpawnTransform).GetComponent<RelicUI>();

            if (relic == null)
                continue;

            RelicData relicData = GameItemRewardManager.instance.GetRandomRelicDataByDropTable(dropTableID);

            relic.UpdateRelicData(relicData);

            relic.OnRelicClick.AddListener(() => GetRelic(relicData));
        }
    }


    void GetRelic(RelicData newRelicData)
    {
        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null)
        {
            GameSceneManager.instance.player.AddRelic(newRelicData.relicName);
        }
        //이 카드 선택지를 제공한 NPC오브젝트 제거 및 캔버스 제거
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
        Destroy(rootObject);
        Destroy(gameObject);
    }
}
