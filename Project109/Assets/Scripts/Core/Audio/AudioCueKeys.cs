/// <summary>
/// 프로젝트 전역 BGM 및 SFX 오디오 클립 식별자(Addressables 키 및 AudioManager 레지스트리 키) 상수 정의입니다.
/// </summary>
public static class AudioCueKeys
{
    // BGM 키
    public const string BGM_TITLE = "bgm_title";
    public const string BGM_HIDEOUT = "bgm_hideout";
    public const string BGM_BATTLE_NORMAL = "bgm_battle_normal";
    public const string BGM_BATTLE_BOSS = "bgm_battle_boss";

    // UI / 카드 SFX 키
    public const string SFX_UI_CLICK = "sfx_ui_click";
    public const string SFX_CARD_HOVER = "sfx_card_hover";
    public const string SFX_CARD_DRAW = "sfx_card_draw";
    public const string SFX_CARD_PLAY = "sfx_card_play";
    public const string SFX_CARD_UNPLAYABLE = "sfx_card_unplayable";

    // 전투 SFX 키
    public const string SFX_COMBAT_HIT = "sfx_combat_hit";
    public const string SFX_COMBAT_SHIELD = "sfx_combat_shield";
    public const string SFX_COMBAT_HEAL = "sfx_combat_heal";
    public const string SFX_COMBAT_DEATH = "sfx_combat_death";

    // 경제 / 보상 / 전투 결과 SFX 키
    public const string SFX_SHOP_PURCHASE = "sfx_shop_purchase";
    public const string SFX_REWARD_CLAIM = "sfx_reward_claim";
    public const string SFX_BATTLE_VICTORY = "sfx_battle_victory";
    public const string SFX_BATTLE_DEFEAT = "sfx_battle_defeat";
}
