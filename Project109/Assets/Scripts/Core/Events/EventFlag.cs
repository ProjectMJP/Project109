using System;
using UnityEngine;

namespace EventStructs
{
    [Flags]
    public enum DamageFlag
    {
        Normal = 0,
        IgnoreArmor = 1 << 0,     // 장갑/방어력 무시
        IgnoreShield = 1 << 1,    // 쉴드 블록 무시 (ex. 관통)
        NoCasterEvents = 1 << 2,  // 공격자의 콜백 이벤트 (흡혈 등) 무발생
        NoTargetEvents = 1 << 3,  // 피격자의 콜백 이벤트 (가시 공격효과) 무발생

        // --- 임의의 조합 프리셋(Presets) ---
        Reflected = NoCasterEvents | NoTargetEvents,                      // 가시/반사 데미지
        HPLoss = IgnoreShield | NoCasterEvents | NoTargetEvents,          // 순수 체력 상실 (직접)
    }

    [Flags]
    public enum HealFlag
    {
        Normal = 0,
        OverHeal = 1 << 0,       // 최대 생명력을 초과해서 회복
        NoCasterEvents = 1 << 1, // 시전자의 회복 증가 버프 등 무시
        NoTargetEvents = 1 << 2, // 대상자의 피격/회복 유물 등 무시

        // --- 임의의 조합 프리셋 ---
        Regen = NoCasterEvents | NoTargetEvents, // 일반 회복이 아닌 재생/지속회복
    }

    [Flags]
    public enum StaminaFlag
    {
        Normal = 0,
        OverStamina = 1 << 0,
        NoCasterEvents = 1 << 1,
        NoTargetEvents = 1 << 2,
        Drain = 1 << 3,

        // --- 임의의 조합 프리셋 ---
        Regen = NoCasterEvents | NoTargetEvents,
    }

    [Flags]
    public enum ShieldFlag
    {
        Normal = 0,
        NoCasterEvents = 1 << 1,
        NoTargetEvents = 1 << 2,
    }

    [Flags]
    public enum CardFlag
    {
        Normal = 0,
        NoExhaust = 1 << 0,      // 카드가 원래 가지고 있는 소멸 무시
        NoDiscard = 1 << 1,      // 사용 후 묘지로 가지 않음 (특수 처리용)
        NoCasterEvents = 1 << 2, // 카드 사용 관련 이벤트 발동 안함
        FreeToPlay = 1 << 3,         // 코스트 소모 없이 사용 (조건 성립 플래그로 쓰임)

        // --- 임의의 조합 프리셋 ---
        AutoPlayed = NoCasterEvents, // 강제 시전
    }

    [Flags]
    public enum MoveFlag
    {
        Normal = 0,
        Teleport = 1 << 0,       // 이동 경로의 함정/효과 무시 (순간이동)
        Forced = 1 << 1,         // 밀치기, 당기기 등 강제 이동
        NoCasterEvents = 1 << 2,
    }

    [Flags]
    public enum EffectFlag
    {
        Normal = 0,
        Unremovable = 1 << 0,    // '모든 버프 해제' 계열로 지워지지 않는 고유/영구 버프
        NoTargetEvents = 1 << 1, // 대상자의 "버프를 받을 때" 등의 이벤트/유물 등 무시
        Cancel = 1 << 2,         // 캐스팅 중인 기술 취소
    }
}
