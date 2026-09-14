using UnityEngine;
using UnityEngine.Pool;
using EventStructs;

public class DamageIndicatorManager : MonoBehaviour
{
    private static DamageIndicatorManager _instance;
    public static DamageIndicatorManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<DamageIndicatorManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("DamageIndicatorManager");
                    _instance = go.AddComponent<DamageIndicatorManager>();
                }
            }
            return _instance;
        }
    }

    [SerializeField] private DamageIndicator _indicatorPrefab;
    [SerializeField] private int _defaultPoolSize = 20;
    [SerializeField] private int _maxPoolSize = 50;

    private IObjectPool<DamageIndicator> _pool;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            InitPool();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void InitPool()
    {
        _pool = new ObjectPool<DamageIndicator>(
            createFunc: () => {
                if (_indicatorPrefab == null)
                {
                    // 폴백용 임시 오브젝트 생성
                    GameObject go = new GameObject("DamageIndicatorFallback");
                    return go.AddComponent<DamageIndicator>();
                }
                return Instantiate(_indicatorPrefab, transform);
            },
            actionOnGet: target => {
                if (target != null) target.gameObject.SetActive(true);
            },
            actionOnRelease: target => {
                if (target != null) target.gameObject.SetActive(false);
            },
            actionOnDestroy: target => {
                if (target != null) Destroy(target.gameObject);
            },
            collectionCheck: false,
            defaultCapacity: _defaultPoolSize,
            maxSize: _maxPoolSize
        );
    }

    /// <summary>
    /// 외부 캐릭터 스크립트에서 데미지 발생 시 호출
    /// </summary>
    public void ShowIndicator(Vector3 worldPos, float amount, DamageFlag flags)
    {
        if (_pool == null) return;
        DamageIndicator indicator = _pool.Get();
        if (indicator == null) return;
        
        string text = Mathf.RoundToInt(amount).ToString();
        Color color = Color.white;
        float scale = 1f;

        // 데미지 플래그 별 색상 및 스케일 분기
        if (flags.HasFlag(DamageFlag.IgnoreShield)) // 실드 무시/관통
        {
            color = new Color(0.85f, 0.45f, 1.0f); // 보라색
            text = "⚡ " + text;
        }
        else if (amount <= 0) // 무효화/블록
        {
            text = "Blocked";
            color = Color.gray;
            scale = 0.8f;
        }
        else
        {
            // 기본 데미지 (주황/빨간 계열)
            color = new Color(1.0f, 0.3f, 0.2f);
        }

        // 월드 좌표 상에 약간의 랜덤 노이즈 오프셋 추가
        Vector3 spawnPos = worldPos + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.2f), Random.Range(-0.3f, 0.3f));

        indicator.Setup(text, color, scale, spawnPos, OnIndicatorFinished);
    }

    public void ShowHealIndicator(Vector3 worldPos, float amount)
    {
        if (_pool == null) return;
        DamageIndicator indicator = _pool.Get();
        if (indicator == null) return;

        string text = "+" + Mathf.RoundToInt(amount).ToString();
        Color color = new Color(0.2f, 0.85f, 0.3f); // 초록색
        
        Vector3 spawnPos = worldPos + new Vector3(Random.Range(-0.2f, 0.2f), 1.0f, 0f);
        indicator.Setup(text, color, 1.0f, spawnPos, OnIndicatorFinished);
    }

    private void OnIndicatorFinished(DamageIndicator indicator)
    {
        if (_pool != null && indicator != null)
        {
            _pool.Release(indicator);
        }
    }
}
