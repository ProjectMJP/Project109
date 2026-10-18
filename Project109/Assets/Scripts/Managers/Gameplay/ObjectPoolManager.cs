using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;
using System;

public class ObjectPoolManager : MonoBehaviour
{
    private static ObjectPoolManager _instance;
    public static ObjectPoolManager instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ObjectPoolManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[ObjectPoolManager]");
                    _instance = go.AddComponent<ObjectPoolManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [SerializeField] private GameObject cardUIPrefab;
    [SerializeField] private int initialPoolSize = 10;    //게임 시작 시 미리 생성할 갯수
    [SerializeField] private int maxPoolSize = 200;

    private IObjectPool<GameObject> cardUIPool;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
            EnsurePoolInitialized();
        }
        else if (_instance != this)
        {
            Destroy(this.gameObject);
        }
    }

    private void OnDestroy()
    {
        if (cardUIPool != null)
        {
            if (cardUIPool is IDisposable disposable)
            {
                disposable.Dispose();
            }
            cardUIPool = null;
        }

        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// 카드 UI 오브젝트 풀이 초기화되어 있는지 확인하고 없으면 생성합니다.
    /// </summary>
    public void EnsurePoolInitialized()
    {
        if (cardUIPool != null) return;

        TryResolveCardUIPrefab();

        cardUIPool = new ObjectPool<GameObject>(
            CreatePooledCardUI,
            OnTakeCardUIFromPool,
            OnReturnCardUIToPool,
            OnDestroyCardUIPoolObject,
            collectionCheck: true,
            initialPoolSize,
            maxPoolSize
        );

        if (cardUIPrefab != null)
        {
            // 시작 수량만큼 미리 생성하여 풀에 적재
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject initialUI = CreatePooledCardUI();
                if (initialUI != null)
                {
                    cardUIPool.Release(initialUI);
                }
            }
        }
    }

    /// <summary>
    /// 카드 UI 프리팹이 미지정된 경우 AssetCacheManager 또는 에디터 에셋 데이터베이스에서 로드합니다.
    /// </summary>
    private bool TryResolveCardUIPrefab()
    {
        if (cardUIPrefab != null) return true;

        if (AssetCacheManager.instance != null && AssetCacheManager.instance.TryGetUI("ActionCard", out GameObject cachedUI))
        {
            if (cachedUI != null)
            {
                cardUIPrefab = cachedUI;
                return true;
            }
        }

#if UNITY_EDITOR
        cardUIPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Card/ActionCard.prefab");
        if (cardUIPrefab != null)
        {
            return true;
        }
#endif

        return false;
    }

    #region CardUI ObjectPool Callback

    private GameObject CreatePooledCardUI()
    {
        if (cardUIPrefab == null)
        {
            TryResolveCardUIPrefab();
        }

        if (cardUIPrefab == null)
        {
            Debug.LogError("ObjectPoolManager: CardUIPrefab is not assigned and could not be resolved!");
            return null;
        }

        GameObject ui = Instantiate(cardUIPrefab, this.transform);
        ui.SetActive(false);    //생성 시에는 비활성화

        return ui;
    }

    private void OnTakeCardUIFromPool(GameObject ui)
    {
        if (ui != null)
        {
            ui.SetActive(true);     //오브젝트 활성화
        }
    }

    private void OnReturnCardUIToPool(GameObject ui)
    {
        if (ui != null)
        {
            ui.SetActive(false);
            ui.transform.SetParent(this.transform);
        }
    }

    private void OnDestroyCardUIPoolObject(GameObject ui)
    {
        if (ui != null)
        {
            Destroy(ui);
        }
    }

    #endregion

    //풀에서 카드UI 오브젝트를 가져옴
    public GameObject GetCardUI(Transform parent)
    {
        EnsurePoolInitialized();

        if (cardUIPool == null)
        {
            Debug.LogError("[ObjectPoolManager] cardUIPool is null!");
            return null;
        }

        GameObject ui = cardUIPool.Get();
        if (ui != null)
        {
            if (parent != null)
            {
                ui.transform.SetParent(parent, false);
            }
            ui.transform.localScale = Vector3.one;
            ui.transform.localPosition = Vector3.zero;
        }

        return ui;
    }

    //사용이 끝난 카드UI 오브젝트를 풀에 반환함
    public void ReturnCardUI(GameObject uiObject)
    {
        if (uiObject == null) return;

        EnsurePoolInitialized();

        if (cardUIPool != null)
        {
            try
            {
                cardUIPool.Release(uiObject);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ObjectPoolManager] 카드 UI 풀 반환 중 예외 발생 (객체 직접 파괴 처리): {ex.Message}");
                Destroy(uiObject);
            }
        }
        else
        {
            Destroy(uiObject);
        }
    }
}
