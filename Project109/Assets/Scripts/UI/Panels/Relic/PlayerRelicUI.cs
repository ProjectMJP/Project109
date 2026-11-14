using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 유물 인벤토리를 UI로 보여주는 클래스 (순수 View)
/// </summary>
public class PlayerRelicUI : MonoBehaviour
{
    [Header("InGame Relic UI")]
    public GameObject relicUIPrefab;
    public Transform relicSpawnTransform;
    [SerializeField] private float relicIconSize = 44f;
    
    private Dictionary<string, GameObject> relicUIObjects = new Dictionary<string, GameObject>();
    private Player boundPlayer;

    public void BindPlayer(Player player)
    {
        if (boundPlayer != null && boundPlayer.relicManager != null)
        {
            boundPlayer.relicManager.OnRelicAddedEvent -= AddRelicUI;
            boundPlayer.relicManager.OnRelicRemovedEvent -= RemoveRelicUI;
        }

        boundPlayer = player;

        ClearAllRelicUIs();

        if (boundPlayer != null && boundPlayer.relicManager != null)
        {
            boundPlayer.relicManager.OnRelicAddedEvent += AddRelicUI;
            boundPlayer.relicManager.OnRelicRemovedEvent += RemoveRelicUI;

            foreach (var relic in boundPlayer.relicManager.GetRelics())
            {
                AddRelicUI(relic);
            }
        }
    }

    private void ClearAllRelicUIs()
    {
        foreach (var obj in relicUIObjects.Values)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        relicUIObjects.Clear();
    }

    public void AddRelicUI(Relic newRelic)
    {
        if (newRelic == null || newRelic.Data == null) return;
        
        if (relicSpawnTransform == null)
        {
            relicSpawnTransform = this.transform;
        }

        if (relicUIPrefab == null && AssetCacheManager.instance != null)
        {
            if (AssetCacheManager.instance.TryGetUI("Relic", out GameObject relicPrefabObj))
            {
                relicUIPrefab = relicPrefabObj;
            }
        }

        if (relicSpawnTransform && relicUIPrefab)
        {
            if (relicUIObjects.ContainsKey(newRelic.Data.relicName))
            {
                return;
            }

            RelicUI relicUI = Instantiate(relicUIPrefab, relicSpawnTransform).GetComponent<RelicUI>();
            if (relicUI != null)
            {
                RectTransform relicRect = relicUI.GetComponent<RectTransform>();
                if (relicRect != null)
                {
                    // TopHUD 유물 영역(50px) 내에 맞춰 아이콘 크기 정규화 (ShopNPCUI는 GridLayoutGroup 100x100 유지)
                    relicRect.sizeDelta = new Vector2(relicIconSize, relicIconSize);
                }

                relicUIObjects.Add(newRelic.Data.relicName, relicUI.gameObject);
                relicUI.UpdateRelicData(newRelic);
            }
        }
    }

    public void RemoveRelicUI(Relic relic)
    {
        if (relic == null || relic.Data == null) return;
        
        if (relicUIObjects.TryGetValue(relic.Data.relicName, out GameObject relicUIObject))
        {
            Destroy(relicUIObject);
            relicUIObjects.Remove(relic.Data.relicName);
        }
    }

    private void OnDestroy()
    {
        if (boundPlayer != null && boundPlayer.relicManager != null)
        {
            boundPlayer.relicManager.OnRelicAddedEvent -= AddRelicUI;
            boundPlayer.relicManager.OnRelicRemovedEvent -= RemoveRelicUI;
        }
    }
}
