/// <summary>
/// 게임 내의 재화 보상을 오브젝트를 생성하여 시각적으로 표현해주는 클래스
/// </summary>

using System.Collections;
using UnityEngine;

public enum CurrencyType
{
    Gold,
    MemorySharp
}

public class CurrencyRewardHandler : MonoBehaviour
{
    private CurrencyType currencyType;
    private int currencyValue;
    private static readonly WaitForSeconds _rewardDelay = new WaitForSeconds(1.0f);

    void Start()
    {

    }

    public void SetCurrencyReward(CurrencyType newCurrencyType, int value)
    {
        currencyType = newCurrencyType;
        currencyValue = value;

        //타입에 맞는 재화 오브젝트 생성
        if (AssetCacheManager.instance != null)
        {
            switch (currencyType)
            {
                case CurrencyType.Gold:
                    if (AssetCacheManager.instance.TryGetModel("Gold_Model", out GameObject goldModel))
                    {
                        Instantiate(goldModel, transform.position, Quaternion.identity, transform);
                    }
                    break;
                case CurrencyType.MemorySharp:
                    if (AssetCacheManager.instance.TryGetModel("MemorySharp_Model", out GameObject memorySharpModel))
                    {
                        Instantiate(memorySharpModel, transform.position, Quaternion.identity, transform);
                    }
                    break;
            }
        }

        //StartCoroutine(AddCurrencyValueToPlayer());
    }

    IEnumerator AddCurrencyValueToPlayer()
    {
        yield return _rewardDelay; //1초 대기 후 재화 추가

        if (GameSceneManager.instance != null && GameSceneManager.instance.player != null && GameSceneManager.instance.player.playerStat != null)
        {
            if (currencyType == CurrencyType.Gold)
            {
                GameSceneManager.instance.player.playerStat.InGameCurrencyGold += currencyValue;
            }
            else if (currencyType == CurrencyType.MemorySharp)
            {
                GameSceneManager.instance.player.playerStat.InGameCurrencyMemorySharp += currencyValue;
            }
        }

        //재화 추가 후 오브젝트 삭제
        Destroy(this.gameObject);
    }
}
