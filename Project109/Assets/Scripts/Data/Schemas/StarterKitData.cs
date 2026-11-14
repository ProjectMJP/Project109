using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YamlDotNet.Serialization;

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
/// 시작 무장(무기)의 역사 및 배경 스토리 해금 항목 데이터 모델입니다.
/// </summary>
[System.Serializable]
public class WeaponLoreEntry
{
    // 스토리 제목 또는 로컬라이제이션 키
    public string title { get; set; }

    // 스토리 본문 또는 로컬라이제이션 키
    public string description { get; set; }

    // 향후 자동 해금 연동을 위한 조건 타입 (예: BossKill, RunClear, None 등)
    public string unlockConditionType { get; set; }

    // 해금 조건 비교값
    public string unlockConditionValue { get; set; }

    // 잠금 상태에서 표시할 해금 조건 힌트 문구
    public string unlockHint { get; set; }

    // 기본 해금 여부
    public bool isUnlockedByDefault { get; set; }

    [YamlIgnore]
    public string LocalizedTitle => LocalizationManager.GetOrDefault(title, title);

    [YamlIgnore]
    public string LocalizedDescription => LocalizationManager.GetOrDefault(description, description);

    [YamlIgnore]
    public string LocalizedUnlockHint => LocalizationManager.GetOrDefault(unlockHint, unlockHint);
}

/// <summary>
/// 캐릭터 클래스 및 챌린지 시작 시 부여되는 카드, 유물, 골드 및 스탯 보너스를 정의하는 YAML 매핑 데이터 모델입니다.
/// </summary>
public class StarterKitData : IModAssetResolver, IIdentifiable
{
    // 시작 키트 고유 ID
    public string loadoutId { get; set; }
    
    public string ID => loadoutId;
    
    // 인게임 표시용 이름
    public string displayName { get; set; }
    
    // 대응하는 캐릭터 클래스
    public string classType { get; set; }
    
    // 시작 골드량
    public int startGold { get; set; }
    
    // 시작 기본 드로우 수 (기본 5장)
    public int baseDrawCount { get; set; } = 5;

    // 진열대 무기 대표 이미지 경로 (모드 루트 기준 상대 경로 또는 AssetCacheManager/Icons 키)
    public string weaponImageAddress { get; set; }

    // 기본 해금 여부 (false 시 기억의 조각으로 해금 필요)
    public bool isDefaultUnlocked { get; set; } = true;

    // 해금에 필요한 기억의 조각 비용
    public int unlockShardCost { get; set; } = 0;

    // 진열대 표시 우선순위 (작을수록 좌측 우선 배치)
    public int sortOrder { get; set; } = 0;

    // 시작 키트(무기)가 제공하는 기본 전투 스탯
    public CharacterStat characterStat { get; set; }

    // 시작 카드 ID 리스트 (동일 ID 기재 시 다수 지급)
    public List<string> startCards { get; set; } = new List<string>();

    // 시작 유물 ID 리스트
    public List<string> startRelics { get; set; } = new List<string>();

    // 시작 시 영구/일시 부여할 효과 리스트
    public List<StartEffectInfo> startEffects { get; set; } = new List<StartEffectInfo>();

    // 무기 역사 스토리 목록
    public List<WeaponLoreEntry> loreEntries { get; set; } = new List<WeaponLoreEntry>();

    // 런타임에 로드된 무기 대표 스프라이트
    [YamlIgnore]
    public Sprite weaponSprite { get; private set; }

    // 바닐라(Core) 모드 소속 여부 (진열대 코어 -> 모드 순 정렬용)
    [YamlIgnore]
    public bool isCoreMod { get; set; } = true;

    // 모드 로딩 순서 (동일 우선순위 정렬 보조용)
    [YamlIgnore]
    public int loadOrder { get; set; }

    [YamlIgnore]
    public string LocalizedDisplayName => LocalizationManager.GetOrDefault(displayName, displayName);

    public bool ResolveAndValidate(string modDirectory)
    {
        weaponSprite = null;

        // 1. weaponImageAddress가 명시된 경우 모드 루트 상대 경로 -> AssetCacheManager -> Icons/StarterKits 순으로 탐색합니다.
        if (!string.IsNullOrEmpty(weaponImageAddress) && !string.IsNullOrEmpty(modDirectory))
        {
            string normalizedRelative = weaponImageAddress.Replace('/', Path.DirectorySeparatorChar);
            string candidatePath = Path.Combine(modDirectory, normalizedRelative);
            if (File.Exists(candidatePath))
            {
                weaponSprite = ImageLoader.LoadCustomSprite(candidatePath, 512);
                return true;
            }

            if (AssetCacheManager.instance != null && AssetCacheManager.instance.TryGetTexture(weaponImageAddress, out Sprite cachedSprite))
            {
                weaponSprite = cachedSprite;
                return true;
            }

            string fileKey = Path.GetFileNameWithoutExtension(weaponImageAddress);
            if (!string.IsNullOrEmpty(fileKey) &&
                ModAssetLoader.TryLoadIconWithFallback(modDirectory, "StarterKits", fileKey, 512, out Sprite fallbackSprite))
            {
                weaponSprite = fallbackSprite;
                return true;
            }

            Debug.LogWarning($"[StarterKit: {loadoutId}] 무기 이미지 '{weaponImageAddress}'를 찾을 수 없어 기본 대체 뷰를 사용합니다.");
            return true;
        }

        // 2. 주소가 생략된 경우에도 관례 경로(Icons/StarterKits/{loadoutId}.png)가 존재하면 조용히 로드합니다.
        if (!string.IsNullOrEmpty(modDirectory) && !string.IsNullOrEmpty(loadoutId))
        {
            string conventionPath = Path.Combine(modDirectory, "Icons", "StarterKits", $"{loadoutId}.png");
            if (File.Exists(conventionPath))
            {
                weaponSprite = ImageLoader.LoadCustomSprite(conventionPath, 512);
            }
        }

        // 무기 이미지는 선택 사항이므로 없더라도 데이터 로딩을 통과시킵니다.
        return true;
    }
}
