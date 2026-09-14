using System.Collections;
using UnityEngine;

namespace EventStructs
{
    public enum DefaultDamageTypeID
    {
        Normal = 0,
        Magic,
    }


    public struct DamageInfo
    {
        public Character caster;
        public Character target;


        public float originalDamageAmount;
        public float baseDamageAmount;
        public float finalDamageAmount;
        public int damageTypeID;
        public DamageFlag damageFlags;


        public int armorPiercing;
        public float damageMultiplier;
        public float shieldMultiplier;
        public float healthMultiplier;

        // 데미지 세부 정보 (Damage breakdown)
        public float shieldDamageAmount;
        public float hpDamageAmount;

        // 결과 플래그 (Result Flags)

        public bool isFatal;
        public bool isShieldBroken;
        public bool isBlocked;
        // 치명타/회피 등 (추후 구현을 위해 주석 처리)
        // public bool isCritical;
        // public bool isDodged;

        public DamageInfo(Character caster, Character target, float baseAmount, DamageFlag flags = DamageFlag.Normal) : this()
        {
            this.caster = caster;
            this.target = target;
            this.originalDamageAmount = baseAmount;
            this.baseDamageAmount = baseAmount;
            this.damageFlags = flags;
            this.damageMultiplier = 1.0f;
            this.shieldMultiplier = 1.0f;
            this.healthMultiplier = 1.0f;
        }
    }

    public struct HealInfo

    {
        public Character caster;
        public Character target;

        public float originalHealAmount;
        public float baseHealAmount;
        public float finalHealAmount;
        public float healMultiplier;
        public HealFlag healFlags;

        public HealInfo(Character caster, Character target, float baseAmount, HealFlag flags = HealFlag.Normal) : this()
        {
            this.caster = caster;
            this.target = target;
            this.originalHealAmount = baseAmount;
            this.baseHealAmount = baseAmount;
            this.healFlags = flags;
            this.healMultiplier = 1.0f;
        }
    }

    public struct StaminaInfo

    {
        public Character caster;
        public Character target;

        public float originalStaminaAmount;
        public float baseStaminaAmount;
        public float finalStaminaAmount;
        public float staminaMultiplier;
        public StaminaFlag staminaFlags;

        public StaminaInfo(Character caster, Character target, float baseAmount, StaminaFlag flags = StaminaFlag.Normal) : this()
        {
            this.caster = caster;
            this.target = target;
            this.originalStaminaAmount = baseAmount;
            this.baseStaminaAmount = baseAmount;
            this.staminaFlags = flags;
            this.staminaMultiplier = 1.0f;
        }

        public StaminaInfo(Character target, float baseAmount, StaminaFlag flags = StaminaFlag.Normal) : this(null, target, baseAmount, flags)
        {
        }
    }


    public struct ShieldInfo
    {
        public Character caster;
        public Character target;


        public float originalShieldAmount;
        public float baseShieldAmount;
        public float finalShieldAmount;
        public float shieldMultiplier;
        public ShieldFlag shieldFlags;
        public int durationTurns;

        public ShieldInfo(Character caster, Character target, float baseAmount, int durationTurns = 1, ShieldFlag flags = ShieldFlag.Normal) : this()
        {
            this.caster = caster;
            this.target = target;
            this.originalShieldAmount = baseAmount;
            this.baseShieldAmount = baseAmount;
            this.shieldFlags = flags;
            this.shieldMultiplier = 1.0f;
            this.durationTurns = durationTurns;
        }
    }

    public class CardInfo
    {
        public Character caster;
        // 이 스킬(카드)의 타겟인 캐릭터 목록, 주 타겟은 첫번째 요소가 됨
        public System.Collections.Generic.List<Character> targets;
        public Vector2Int targetPosition;


        public object cardData;

        public CardFlag cardFlags;

        public static readonly System.Collections.Generic.List<Character> EmptyTargets = new System.Collections.Generic.List<Character>(0);

        public CardInfo(Character caster, System.Collections.Generic.List<Character> targets, Vector2Int targetPos, object cardData, CardFlag flags = CardFlag.Normal)
        {
            this.caster = caster;
            this.targets = targets ?? EmptyTargets;
            this.targetPosition = targetPos;
            this.cardData = cardData;
            this.cardFlags = flags;
        }

        public void AddFlag(CardFlag flag)
        {
            this.cardFlags |= flag;
        }

        public void RemoveFlag(CardFlag flag)
        {
            this.cardFlags &= ~flag;
        }

        public bool HasFlag(CardFlag flag)
        {
            return this.cardFlags.HasFlag(flag);
        }
    }

    public struct MoveInfo
    {
        public Character mover;
        public Vector2Int fromUnscaled;
        public Vector2Int toUnscaled;
        public MoveFlag moveFlags;
        public bool isCanceled;

        // 이동 거리, 속도 등에 대한 처리를 위해 multiplier를 둠

        public float moveMultiplier;

        public MoveInfo(Character mover, Vector2Int from, Vector2Int to, MoveFlag flags = MoveFlag.Normal) : this()
        {
            this.mover = mover;
            this.fromUnscaled = from;
            this.toUnscaled = to;
            this.moveFlags = flags;
            this.moveMultiplier = 1.0f;
        }
    }

    public struct EffectInfo
    {
        public Character caster;
        public Character target;
        public Effect effect;

        public int baseStack;
        public float baseDuration;
        public int bonusStack;
        public float durationMultiplier;

        public EffectFlag effectFlags;

        public EffectInfo(Character caster, Character target, Effect effect, int stack = 1, float duration = 0f, EffectFlag flags = EffectFlag.Normal) : this()
        {
            this.caster = caster;
            this.target = target;
            this.effect = effect;
            this.baseStack = stack;
            this.baseDuration = duration;
            this.bonusStack = 0;
            this.durationMultiplier = 1.0f;
            this.effectFlags = flags;
        }

        // 최종적으로 적용될 프로퍼티
        public int FinalStack => baseStack + bonusStack;
        public float FinalDuration => baseDuration * durationMultiplier;
    }
}
