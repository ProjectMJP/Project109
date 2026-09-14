using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionQueueManager : MonoBehaviour
{
    private static ActionQueueManager _instance;
    public static ActionQueueManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ActionQueueManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("ActionQueueManager");
                    _instance = go.AddComponent<ActionQueueManager>();
                }
            }
            return _instance;
        }
    }

    private Queue<BattleAction> _actionQueue = new Queue<BattleAction>();
    private bool _isProcessing = false;

    // 선입력 최대 저장 수 제한 (너무 많은 큐가 쌓여 오조작되는 현상 예방)
    [SerializeField] private int _maxQueueSize = 2;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public bool IsBusy => _isProcessing || _actionQueue.Count > 0;

    /// <summary>
    /// 새로운 액션을 큐에 추가합니다.
    /// </summary>
    public bool EnqueueAction(BattleAction action)
    {
        if (_actionQueue.Count >= _maxQueueSize)
        {
            Debug.LogWarning("[ActionQueue] 선입력 큐가 가득 차 입력을 무시합니다.");
            return false; // 선입력 초과로 거부
        }

        _actionQueue.Enqueue(action);

        if (!_isProcessing)
        {
            StartCoroutine(ProcessQueueRoutine());
        }

        return true;
    }

    private IEnumerator ProcessQueueRoutine()
    {
        _isProcessing = true;

        while (_actionQueue.Count > 0)
        {
            BattleAction currentAction = _actionQueue.Dequeue();
            
            // 액션 코루틴 끝날 때까지 런타임 대기
            yield return StartCoroutine(currentAction.ExecuteRoutine());
        }

        _isProcessing = false;
    }

    /// <summary>
    /// 전투 종료 시 큐 강제 클리어
    /// </summary>
    public void ClearQueue()
    {
        StopAllCoroutines();
        _actionQueue.Clear();
        _isProcessing = false;
    }
}
