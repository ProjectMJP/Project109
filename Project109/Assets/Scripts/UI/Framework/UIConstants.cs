using UnityEngine;

/// <summary>
/// UI 시스템 전반에서 사용되는 프리팹 식별자, 기본 위치 오프셋, 색상 팔레트 등의 정적 상수를 정의합니다.
/// 하드코딩된 매직 스트링 및 수치를 일원화하여 관리합니다.
/// </summary>
public static class UIConstants
{
    // Addressable / UI Prefab 키 (World UI)
    public const string PREFAB_CHARACTER_STATUS_BAR = "CharacterStatusBarUI";
    public const string PREFAB_CHARACTER_EFFECT_LIST = "CharacterEffectListUI";
    public const string PREFAB_CHARACTER_EFFECT_ITEM = "CharacterEffectItemUI";

    // Addressable / UI Panel 키 (Screen / Canvas UI)
    public const string PANEL_TOP_HUD = "TopHUDPanel";
    public const string PANEL_CARD_DECK = "CardDeckCanvas";
    public const string PANEL_CARD_DETAIL = "CardDetailUI";
    public const string PANEL_EXPLORE_MAP = "ExploreMap";
    public const string PANEL_BATTLE_HAND = "BattleHandPanel";
    public const string PANEL_CONFIRM_DIALOG = "ConfirmDialog";
    public const string PANEL_RESTORE_NPC = "RestoreNPCUI";
    public const string PANEL_DIALOGUE = "DialogueUI";
    public const string PANEL_SHOP_NPC = "ShopNPCUI";
    public const string PANEL_REWARD_LIST = "RewardListCanvas";
    public const string PANEL_UPGRADE_CARD_DECK = "UpgradeCardDeckCanvas";
    public const string PANEL_ERASE_CARD_DECK = "EraseCardDeckCanvas";
    public const string PANEL_BATTLE_NOTICE = "UIBattleNotice";

    // 기본 월드 위치 오프셋 (캐릭터 머리/발 위치 기준)
    public static readonly Vector3 DEFAULT_STATUS_BAR_OFFSET = new Vector3(0f, -2.2f, 0f);
    public static readonly Vector3 DEFAULT_EFFECT_LIST_OFFSET = new Vector3(0f, -1.8f, 0f);

    // 기본 게이지 색상 팔레트
    public static readonly Color COLOR_HEALTH = new Color(0.9f, 0.2f, 0.2f, 1.0f);
    public static readonly Color COLOR_SHIELD = new Color(0.2f, 0.6f, 1.0f, 1.0f);
    public static readonly Color COLOR_STAMINA = new Color(0.2f, 0.85f, 0.3f, 1.0f);
}

