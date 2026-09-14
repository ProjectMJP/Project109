using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;
using System;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager instance { get; private set; }

    [SerializeField] private GameObject cardUIPrefab;
    [SerializeField] private int initialPoolSize = 10;    //게임 시작 시 미리 생성할 갯수
    [SerializeField] private int maxPoolSize = 200;

    private IObjectPool<GameObject> cardUIPool;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);

            cardUIPool = new ObjectPool<GameObject>(CreatePooledCardUI,
                                                    OnTakeCardUIFromPool,
                                                    OnReturnCardUIToPool,
                                                    OnDestroyCardUIPoolObject,
                                                    collectionCheck: true,
                                                    initialPoolSize,
                                                    maxPoolSize);

            //시작 수량만큼 미리 생성
            for(int i = 0; i < initialPoolSize; i++)
            {
                cardUIPool.Release(CreatePooledCardUI());
            }
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    private void OnDestroy()
    {
        if(cardUIPool != null)
        {
            (cardUIPool as IDisposable)?.Dispose();
        }
    }

    #region CardUI ObjectPool Callback

    private GameObject CreatePooledCardUI()
    {
        if (cardUIPool == null)
        {
            Debug.LogError($"ObjectPoolManager: CardUIPrefab is not assigned!");
        }

        GameObject ui = Instantiate(cardUIPrefab, this.transform);
        ui.SetActive(false);    //생성 시에는 비활성화
        Debug.Log($"Created new CardUIs for ObjectPool");

        return ui;
    }

    private void OnTakeCardUIFromPool(GameObject ui)
    {
        ui.SetActive(true);     //오브젝트 활성화
    }

    private void OnReturnCardUIToPool(GameObject ui)
    {
        ui.SetActive(false);
        ui.transform.SetParent(this.transform);
    }

    private void OnDestroyCardUIPoolObject(GameObject ui)
    {
        Destroy(ui);
    }

    #endregion

    //풀에서 카드UI 오브젝트를 가져옴
    public GameObject GetCardUI(Transform parent)
    {
        GameObject ui = cardUIPool.Get();
        if (ui != null)
        {
            ui.transform.SetParent(parent);
        }

        return ui;
    }

    //사용이 끝난 카드UI 오브젝트를 풀에 반환함
    public void ReturnCardUI(GameObject uiObject)
    {
        if(uiObject != null)
        {
            cardUIPool.Release(uiObject);
        }
    }
}
