using System.Collections.Generic;

/// <summary>
/// 시작 효과 정보 정의를 담는 클래스입니다.
/// </summary>
public class StartEffectInfo
{
    public string effectName { get; set; }
    public int stack { get; set; }
    public float duration { get; set; }
}

/// <summary>
/// 캐릭터 클래스 및 챌린지 시작 시 부여되는 카드, 유물, 골드 및 스탯 보너스를 정의하는 YAML 매핑 데이터 모델입니다.
/// </summary>
public class StarterKitData : IModAssetResolver
{
    // 시작 키트 고유 ID
    public string loadoutId { get; set; }
    
    // 인게임 표시용 이름
    public string displayName { get; set; }
    
    // 대응하는 캐릭터 클래스
    public string classType { get; set; }
    
    // 시작 골드량
    public int startGold { get; set; }
    
    // 시작 키트(무기)가 제공하는 기본 전투 스탯
    public CharacterStat characterStat { get; set; }

    // 시작 카드 ID 리스트 (동일 ID 기재 시 다수 지급)
    public List<string> startCards { get; set; } = new List<string>();

    // 시작 유물 ID 리스트
    public List<string> startRelics { get; set; } = new List<string>();

    // 시작 시 영구/일시 부여할 효과 리스트
    public List<StartEffectInfo> startEffects { get; set; } = new List<StartEffectInfo>();

    public bool ResolveAndValidate(string modDirectory)
    {
        // 런타임에 데이터 로딩 후 추가 검증이나 이미지 링크가 필요하지 않으므로 무조건 참을 반환합니다.
        return true;
    }
}
