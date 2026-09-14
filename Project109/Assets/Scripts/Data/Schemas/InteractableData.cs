using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InteractableData : IIdentifiable
{
    public string interactableID;             // 상호작용 오브젝트 고유 식별자
    public string interactableName;           // 오브젝트의 표시 명칭
    public string modelPrefabPath;            // Addressables 소환 모델 경로
    public List<int> eventAppearLevels;       // 이 이벤트가 등장할 수 있는 스테이지 층/레벨 제한 목록
    
    [Header("상호작용 타겟 분기")]
    public string targetDialogueID;           // 대화 NPC일 때 로드할 다이얼로그 ID

    [Header("보상 상자 추가 데이터")]
    public RewardData rewardData;             // 보상 상자일 때 획득 가능한 보상 기획 데이터

    public string ID => interactableID;
}
