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
    private BattleAction _currentAction;
    private bool _isProcessing = false;

    // 선입력 최대 저장 수 제한 (너무 많은 큐가 쌓여 오조작되는 현상 예방)
    [SerializeField] private int _maxQueueSize = 4;

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
    /// 새로운 액션을 큐에 추가합니다. forceEnqueue가 true인 경우 큐 크기 제한을 무시합니다 (몬스터 AI 및 시스템 전용).
    /// </summary>
    public bool EnqueueAction(BattleAction action, bool forceEnqueue = false)
    {
        if (!forceEnqueue && _actionQueue.Count >= _maxQueueSize)
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
        try
        {
            while (_actionQueue.Count > 0)
            {
                _currentAction = _actionQueue.Dequeue();
                if (_currentAction == null) continue;

                // 안전 래퍼를 통해 액션 코루틴 실행 및 예외 격리
                yield return StartCoroutine(SafeExecuteAction(_currentAction));
                _currentAction = null;
            }
        }
        finally
        {
            // 어떤 예외나 중단이 발생해도 _isProcessing 잠금 해제 보장
            _currentAction = null;
            _isProcessing = false;
        }
    }

    private IEnumerator SafeExecuteAction(BattleAction action)
    {
        IEnumerator routine = null;
        try
        {
            routine = action.ExecuteRoutine();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ActionQueue] 액션 초기화 중 예외 발생 ({action.GetType().Name}):\n{ex}");
            action.OnCanceled();
            yield break;
        }

        while (true)
        {
            object currentYield = null;
            try
            {
                if (!routine.MoveNext())
                    break;
                currentYield = routine.Current;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ActionQueue] 액션 실행 중 예외 발생 ({action.GetType().Name}):\n{ex}");
                // 비정상 종료 시 캐릭터 상태 강제 복구 및 리소스 정리
                if (action.caster != null && !action.caster.isDead && action.caster.currentState != CharacterState.Idle)
                {
                    action.caster.currentState = CharacterState.Idle;
                }
                action.OnCanceled();
                yield break;
            }

            yield return currentYield;
        }
    }

    /// <summary>
    /// 전투 종료 시 큐 강제 클리어 및 대기 중 액션 리소스 정리
    /// </summary>
    public void ClearQueue()
    {
        StopAllCoroutines();
        if (_currentAction != null)
        {
            _currentAction.OnCanceled();
            _currentAction = null;
        }
        while (_actionQueue.Count > 0)
        {
            var action = _actionQueue.Dequeue();
            action?.OnCanceled();
        }
        _isProcessing = false;
    }

}
