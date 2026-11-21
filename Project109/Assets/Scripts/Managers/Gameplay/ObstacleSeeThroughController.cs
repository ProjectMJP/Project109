using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 직교/원근 카메라와 전장 내 활성 유닛(플레이어, 생존 적, 인터랙터블 등) 사이의 시선을
/// SphereCastNonAlloc(Zero-Alloc)으로 감지하여 가로막는 FadeableObstacle을 부드럽게 반투명화합니다.
/// </summary>
public class ObstacleSeeThroughController : MonoBehaviour
{
    private const float PositionChangeThresholdSqr = 0.0004f;
    private const float CameraPosChangeThresholdSqr = 0.0001f;
    private const float CameraRotChangeThresholdDeg = 0.05f;

    [Header("Camera & Detection")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField, Min(0.05f)] private float castRadius = 0.35f;
    [SerializeField, Min(1f)] private float castDistance = 60f;
    [SerializeField] private float unitHeightOffset = 0.9f;
    [SerializeField, Min(0f)] private float scanInterval = 0.05f;
    [SerializeField, Range(4, 64)] private int maxHitsPerUnit = 16;

    private RaycastHit[] _hitBuffer;
    private readonly List<Transform> _trackedTargets = new List<Transform>(16);
    private readonly List<Vector3> _lastTargetPositions = new List<Vector3>(16);
    private readonly List<Transform> _customTargets = new List<Transform>(8);

    private HashSet<FadeableObstacle> _currentlyOccluding = new HashSet<FadeableObstacle>();
    private HashSet<FadeableObstacle> _previousOccluding = new HashSet<FadeableObstacle>();
    private readonly List<FadeableObstacle> _activeFadingList = new List<FadeableObstacle>(16);
    private readonly HashSet<FadeableObstacle> _activeFadingSet = new HashSet<FadeableObstacle>();

    private Vector3 _lastCameraPos;
    private Quaternion _lastCameraRot;
    private int _lastStageEtcCount = -1;
    private float _scanTimer;
    private bool _hasScannedOnce;
    private int _cachedDefaultObstacleMask = -1;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        _hasScannedOnce = false;
        _scanTimer = scanInterval;
    }

    private void OnDisable()
    {
        ResetAllOccludingImmediate();
    }

    private void OnDestroy()
    {
        ResetAllOccludingImmediate();
        _trackedTargets.Clear();
        _lastTargetPositions.Clear();
        _customTargets.Clear();
        _currentlyOccluding.Clear();
        _previousOccluding.Clear();
        _activeFadingList.Clear();
        _activeFadingSet.Clear();
    }

    private void LateUpdate()
    {
        EnsureInitialized();

        float unscaledDt = Time.unscaledDeltaTime;
        _scanTimer += unscaledDt;
        if (!_hasScannedOnce || _scanTimer >= scanInterval)
        {
            _scanTimer = 0f;
            ScanAndApplyIfNeeded(force: false);
        }

        UpdateActiveFades(unscaledDt);
    }

    /// <summary>
    /// 외부 시스템 또는 테스트에서 추적 대상 트랜스폼을 수동 등록합니다.
    /// </summary>
    public void RegisterTarget(Transform target)
    {
        if (target != null && !_customTargets.Contains(target))
        {
            _customTargets.Add(target);
            _hasScannedOnce = false;
        }
    }

    /// <summary>
    /// 수동 등록된 추적 대상 트랜스폼을 해제합니다.
    /// </summary>
    public void UnregisterTarget(Transform target)
    {
        if (target != null && _customTargets.Remove(target))
        {
            _hasScannedOnce = false;
        }
    }

    /// <summary>
    /// 테스트 또는 스테이지 전환 직후 가림 상태를 즉시 재스캔하고 선택적으로 페이드 보간을 진행합니다.
    /// </summary>
    public void ForceScanNow(float fadeDeltaTime = 0f)
    {
        EnsureInitialized();
        ScanAndApplyIfNeeded(force: true);

        if (fadeDeltaTime > 0f)
        {
            UpdateActiveFades(fadeDeltaTime);
        }
    }

    /// <summary>
    /// 현재 추적 중인 타깃 수가 0으로 변경되었거나 캐시 무효화가 필요할 때 사용합니다.
    /// </summary>
    public void InvalidateScanCache()
    {
        _hasScannedOnce = false;
    }

    private void EnsureInitialized()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        int desiredCapacity = Mathf.Clamp(maxHitsPerUnit, 4, 64);
        if (_hitBuffer == null || _hitBuffer.Length != desiredCapacity)
        {
            _hitBuffer = new RaycastHit[desiredCapacity];
        }
    }

    private int ResolveObstacleLayerMask()
    {
        if (obstacleLayerMask.value != 0)
        {
            return obstacleLayerMask.value;
        }

        if (_cachedDefaultObstacleMask == -1)
        {
            int mask = LayerMask.GetMask("Obstacle");
            _cachedDefaultObstacleMask = mask != 0 ? mask : Physics.DefaultRaycastLayers;
        }

        return _cachedDefaultObstacleMask;
    }

    private void ScanAndApplyIfNeeded(bool force)
    {
        if (targetCamera == null)
        {
            return;
        }

        CollectTrackedTargets();

        if (!force && !ShouldScan())
        {
            return;
        }

        PerformOcclusionScan();
        SnapshotCurrentState();
    }

    private void CollectTrackedTargets()
    {
        _trackedTargets.Clear();

        if (GameSceneManager.instance != null &&
            GameSceneManager.instance.player != null &&
            GameSceneManager.instance.player.character != null)
        {
            Character playerChar = GameSceneManager.instance.player.character;
            if (!playerChar.isDead && playerChar.gameObject.activeInHierarchy)
            {
                _trackedTargets.Add(playerChar.transform);
            }
        }

        if (RunManager.instance != null && RunManager.instance.currentStage != null)
        {
            StageManager stage = RunManager.instance.currentStage;

            IReadOnlyList<GameObject> enemies = stage.currentSpawnEnemyList;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    GameObject enemyGo = enemies[i];
                    if (enemyGo == null || !enemyGo.activeInHierarchy)
                    {
                        continue;
                    }

                    if (enemyGo.TryGetComponent<Character>(out Character enemyChar) && enemyChar.isDead)
                    {
                        continue;
                    }

                    AddTrackedTargetUnique(enemyGo.transform);
                }
            }

            IReadOnlyList<GameObject> npcs = stage.currentSpawnNPCList;
            if (npcs != null)
            {
                for (int i = 0; i < npcs.Count; i++)
                {
                    GameObject npcGo = npcs[i];
                    if (npcGo != null && npcGo.activeInHierarchy)
                    {
                        AddTrackedTargetUnique(npcGo.transform);
                    }
                }
            }
        }

        for (int i = _customTargets.Count - 1; i >= 0; i--)
        {
            Transform customTarget = _customTargets[i];
            if (customTarget == null)
            {
                _customTargets.RemoveAt(i);
                continue;
            }

            if (customTarget.gameObject.activeInHierarchy)
            {
                AddTrackedTargetUnique(customTarget);
            }
        }
    }

    private void AddTrackedTargetUnique(Transform target)
    {
        if (target == null)
        {
            return;
        }

        for (int i = 0; i < _trackedTargets.Count; i++)
        {
            if (_trackedTargets[i] == target)
            {
                return;
            }
        }

        _trackedTargets.Add(target);
    }

    private bool ShouldScan()
    {
        if (!_hasScannedOnce)
        {
            return true;
        }

        Vector3 camPos = targetCamera.transform.position;
        Quaternion camRot = targetCamera.transform.rotation;
        if ((camPos - _lastCameraPos).sqrMagnitude > CameraPosChangeThresholdSqr ||
            Quaternion.Angle(camRot, _lastCameraRot) > CameraRotChangeThresholdDeg)
        {
            return true;
        }

        int stageEtcCount = (RunManager.instance != null && RunManager.instance.currentStage != null)
            ? RunManager.instance.currentStage.currentSpawnEtcList.Count
            : -1;
        if (stageEtcCount != _lastStageEtcCount)
        {
            return true;
        }

        if (_trackedTargets.Count != _lastTargetPositions.Count)
        {
            return true;
        }

        for (int i = 0; i < _trackedTargets.Count; i++)
        {
            if ((_trackedTargets[i].position - _lastTargetPositions[i]).sqrMagnitude > PositionChangeThresholdSqr)
            {
                return true;
            }
        }

        foreach (FadeableObstacle obs in _previousOccluding)
        {
            if (obs == null || !obs.isActiveAndEnabled)
            {
                return true;
            }
        }

        return false;
    }

    private void SnapshotCurrentState()
    {
        _lastCameraPos = targetCamera.transform.position;
        _lastCameraRot = targetCamera.transform.rotation;
        _lastStageEtcCount = (RunManager.instance != null && RunManager.instance.currentStage != null)
            ? RunManager.instance.currentStage.currentSpawnEtcList.Count
            : -1;

        _lastTargetPositions.Clear();
        for (int i = 0; i < _trackedTargets.Count; i++)
        {
            _lastTargetPositions.Add(_trackedTargets[i].position);
        }

        _hasScannedOnce = true;
    }

    private void PerformOcclusionScan()
    {
        _currentlyOccluding.Clear();

        int mask = ResolveObstacleLayerMask();
        bool isOrtho = targetCamera.orthographic;
        Vector3 orthoToCamDir = -targetCamera.transform.forward;
        float orthoMaxDist = Mathf.Min(castDistance, targetCamera.farClipPlane);
        Vector3 camWorldPos = targetCamera.transform.position;

        for (int i = 0; i < _trackedTargets.Count; i++)
        {
            Transform target = _trackedTargets[i];
            if (target == null)
            {
                continue;
            }

            Vector3 origin = target.position + Vector3.up * unitHeightOffset;
            Vector3 toCameraDir;
            float maxDist;

            if (isOrtho)
            {
                toCameraDir = orthoToCamDir;
                maxDist = orthoMaxDist;
            }
            else
            {
                Vector3 delta = camWorldPos - origin;
                float distToCam = delta.magnitude;
                if (distToCam <= 0.0001f)
                {
                    continue;
                }

                toCameraDir = delta / distToCam;
                maxDist = Mathf.Min(castDistance, distToCam);
            }

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                castRadius,
                toCameraDir,
                _hitBuffer,
                maxDist,
                mask,
                QueryTriggerInteraction.Ignore);

            for (int h = 0; h < hitCount; h++)
            {
                Collider hitCollider = _hitBuffer[h].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                // 시작 구체가 이미 콜라이더와 겹친 경우(distance == 0), 카메라 방향 앞쪽에 위치한 장애물만 가림으로 인정
                if (_hitBuffer[h].distance <= 0f)
                {
                    Vector3 toObstacleCenter = hitCollider.bounds.center - origin;
                    if (Vector3.Dot(toObstacleCenter, toCameraDir) <= 0f)
                    {
                        continue;
                    }
                }

                FadeableObstacle obstacle = hitCollider.GetComponentInParent<FadeableObstacle>();
                if (obstacle != null && obstacle.isActiveAndEnabled)
                {
                    _currentlyOccluding.Add(obstacle);
                }
            }
        }

        foreach (FadeableObstacle obs in _currentlyOccluding)
        {
            if (obs != null && !_previousOccluding.Contains(obs))
            {
                obs.SetOccluding(true);
                AddToActiveFading(obs);
            }
        }

        foreach (FadeableObstacle obs in _previousOccluding)
        {
            if (obs != null && !_currentlyOccluding.Contains(obs))
            {
                obs.SetOccluding(false);
                AddToActiveFading(obs);
            }
        }

        HashSet<FadeableObstacle> temp = _previousOccluding;
        _previousOccluding = _currentlyOccluding;
        _currentlyOccluding = temp;
        _currentlyOccluding.Clear();
    }

    private void AddToActiveFading(FadeableObstacle obstacle)
    {
        if (obstacle != null && _activeFadingSet.Add(obstacle))
        {
            _activeFadingList.Add(obstacle);
        }
    }

    private void UpdateActiveFades(float deltaTime)
    {
        for (int i = _activeFadingList.Count - 1; i >= 0; i--)
        {
            FadeableObstacle obstacle = _activeFadingList[i];
            if (obstacle == null || !obstacle.isActiveAndEnabled)
            {
                RemoveActiveFadeAt(i, obstacle);
                continue;
            }

            bool needsMoreTick = obstacle.TickFade(deltaTime);
            if (!needsMoreTick)
            {
                RemoveActiveFadeAt(i, obstacle);
            }
        }
    }

    private void RemoveActiveFadeAt(int index, FadeableObstacle obstacle)
    {
        int lastIndex = _activeFadingList.Count - 1;
        _activeFadingList[index] = _activeFadingList[lastIndex];
        _activeFadingList.RemoveAt(lastIndex);

        if (!ReferenceEquals(obstacle, null))
        {
            _activeFadingSet.Remove(obstacle);
        }
    }

    private void ResetAllOccludingImmediate()
    {
        foreach (FadeableObstacle obs in _previousOccluding)
        {
            if (obs != null)
            {
                obs.ResetImmediate();
            }
        }
        _previousOccluding.Clear();
        _currentlyOccluding.Clear();

        for (int i = 0; i < _activeFadingList.Count; i++)
        {
            FadeableObstacle obs = _activeFadingList[i];
            if (obs != null)
            {
                obs.ResetImmediate();
            }
        }
        _activeFadingList.Clear();
        _activeFadingSet.Clear();
        _hasScannedOnce = false;
    }
}
