using System.Collections.Generic;
using UnityEngine;
using CardTypes;

[System.Serializable]
public class InteractableData : IIdentifiable
{
    public string interactableID;             // 상호작용 오브젝트 고유 식별자
    public string interactableName;           // 오브젝트의 표시 명칭
    public InteractableType interactableType = InteractableType.Normal; // 명시적 상호작용 타입 (Chest, Shop, RestoreBonfire, Normal)
    public string modelPrefabPath;            // Addressables 소환 모델 경로
    public List<int> eventAppearLevels;       // 이 이벤트가 등장할 수 있는 스테이지 층/레벨 제한 목록
    
    [Header("상호작용 타겟 분기")]
    public string targetDialogueID;           // 대화 NPC일 때 로드할 다이얼로그 ID

    [Header("보상 상자 추가 데이터")]
    public RewardData rewardData;             // 보상 상자일 때 획득 가능한 보상 기획 데이터


    public string ID => interactableID;

    public string LocalizedName => LocalizationManager.GetOrDefault($"{interactableID}.name", interactableName);

    /// <summary>
    /// 스테이지 인카운터 타입과 데이터의 명시적 설정을 기반으로 실제 상호작용 컴포넌트 타입을 결정합니다.
    /// </summary>
    public static InteractableType ResolveType(EncountType encountType, InteractableData eventData)
    {
        if (eventData != null)
        {
            if (eventData.interactableType != InteractableType.Normal)
            {
                return eventData.interactableType;
            }

            if (eventData.rewardData != null && eventData.rewardData.rewards != null && eventData.rewardData.rewards.Count > 0)
            {
                return InteractableType.Chest;
            }
        }

        switch (encountType)
        {
            case EncountType.Store:
                return InteractableType.Shop;
            case EncountType.Restore:
                return InteractableType.RestoreBonfire;
            case EncountType.SecretBox:
                return InteractableType.Chest;
            default:
                return InteractableType.Normal;
        }
    }
}
