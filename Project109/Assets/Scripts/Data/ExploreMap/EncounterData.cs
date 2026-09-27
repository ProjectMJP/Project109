using UnityEngine;

public enum EncountType
{
    None,       //비어있음
    Elite,      //엘리트 몬스터
    Boss,       //보스
    Store,      //상점
    Restore,    //휴식
    Battle,     //전투
    SecretBox,  //박스
    Secret,     //비밀
}

public enum ExtraEncountType
{
    None,
    Insight,    //천리안
    ShineWell   //빛나는 우물
}

public enum BattleExtraRewardType
{
    None,
    Card,       //희귀등급 이상의 카드
    Relic,      //일반 등급 이상의 유물
    Gold        //보너스 재화
}

[System.Serializable]
public class EncounterData
{
    public LocationType locationType;
    public EncountType encountType;
    public ExtraEncountType extraEncountType;
    public BattleExtraRewardType battleExtraRewardType;

    // 맵 데이터, 이벤트 데이터, 배틀 데이터는 직렬화 및 결정론적 생성을 위해 ID 형태로 보유
    public string mapDataId;
    public string interactableDataId;
    public string battleDataId;

    public EncounterData()
    {
        locationType = LocationType.Temple;
        encountType = EncountType.None;
        extraEncountType = ExtraEncountType.None;
        battleExtraRewardType = BattleExtraRewardType.None;
        mapDataId = string.Empty;
        interactableDataId = string.Empty;
        battleDataId = string.Empty;
    }
}
