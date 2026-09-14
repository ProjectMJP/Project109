using EventStructs;
using System;
using UnityEngine;

/// <summary>
/// 바위, 상자, 폭탄 배럴, 고대 석판 등 피격을 받아 파괴될 수 있는 경량 맵 오브젝트 컴포넌트.
/// 스스로 턴을 소모하지 않으며, 승패 판정에 영향을 주지 않는 중립(Neutral) 엔티티로 동작합니다.
/// </summary>
public class DestructibleObject : MonoBehaviour, IDamageable
{
    [Header("Basic Info")]
    public string objectName = "DestructibleObject";
    [SerializeField]
    private float _maxHealth = 30f;
    public float maxHealth => _maxHealth;

    [SerializeField]
    private float _curHealth;
    public float curHealth => _curHealth;

    public bool isDead => _curHealth <= 0f;

    [Header("Faction")]
    public CharacterFaction faction = CharacterFaction.Neutral;
    CharacterFaction IDamageable.faction => faction;

    /// <summary>오브젝트가 완전히 파괴되었을 때 발행되는 이벤트 (MapManager 등이 구독하여 타일 복구/보상 드롭 처리)</summary>
    public event Action<DestructibleObject> OnDestroyed;

    /// <summary>오브젝트의 피격 시 체력 변경 이벤트</summary>
    public event Action<DestructibleObject, float> OnHealthChanged;

    private void Awake()
    {
        if (_curHealth <= 0f && _maxHealth > 0f)
        {
            _curHealth = _maxHealth;
        }
    }

    /// <summary>
    /// 오브젝트의 최대 체력과 현재 체력을 초기화합니다.
    /// </summary>
    public virtual void Initialize(float hp)
    {
        this._maxHealth = hp;
        this._curHealth = hp;
    }

    /// <summary>
    /// 외부 공격 카드 또는 스킬로부터 데미지를 받습니다. (IDamageable 구현)
    /// </summary>
    public virtual void TakeDamage(DamageInfo info)
    {
        if (isDead) return;

        float damage = info.finalDamageAmount > 0f ? info.finalDamageAmount : info.baseDamageAmount;
        _curHealth = Mathf.Max(0f, _curHealth - damage);

        OnHealthChanged?.Invoke(this, _curHealth);

        // 데미지 플로팅 텍스트 표시
        if (DamageIndicatorManager.Instance != null)
        {
            DamageIndicatorManager.Instance.ShowIndicator(transform.position, damage, info.damageFlags);
        }

        if (isDead)
        {
            DestroyObject();
        }
    }

    /// <summary>
    /// 오브젝트 파괴 처리 (OnDestroyed 이벤트 발행, 게임오브젝트 파괴)
    /// </summary>
    protected virtual void DestroyObject()
    {
        // 1. 파괴 이벤트 발행 (MapManager가 구독하여 타일 상태를 Empty로 복구하거나 보상 생성)
        OnDestroyed?.Invoke(this);

        // 2. 게임 오브젝트 파괴
        Destroy(gameObject);
    }
}
