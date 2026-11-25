using EventStructs;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 진정한 의미의 '캐릭터 이동 실행' 로직 클래스입니다.
/// 플레이어와 적(Enemy) 모두 이 클래스를 소유(has-a)하여 코루틴 이동을 수행합니다.
/// </summary>
public class CharacterMove
{
    private LookDirection _facingDirection = LookDirection.East;
    public LookDirection facingDirection => _facingDirection;

    public Character character { private set; get; }

    public event Action<Character, Tile, Tile> OnTileChanged;

    protected Tile currentTile;
    public float moveSpeed = 5f;
    public float turnSpeed = 600f;
    public MoverCapability capabilities = MoverCapability.None;

    public CharacterMove(Character character)
    {
        this.character = character;
    }

    public void SetCurrentTile(Tile newTile, bool triggerTileHazards = false)
    {
        Tile previousTile = currentTile;
        bool tileChanged = newTile != previousTile;

        if (previousTile != null && previousTile.occupyingCharacter == this.character)
        {
            previousTile.occupyingCharacter = null;
        }

        currentTile = newTile;

        if (currentTile != null && this.character != null && !this.character.isDead)
        {
            currentTile.occupyingCharacter = this.character;
        }

        if (triggerTileHazards && tileChanged)
        {
            if (previousTile != null)
            {
                previousTile.NotifyUnitExited(this.character);
            }

            if (newTile != null && this.character != null && !this.character.isDead)
            {
                newTile.NotifyUnitEntered(this.character);
            }
        }

        if (tileChanged)
        {
            OnTileChanged?.Invoke(this.character, previousTile, newTile);
        }
    }

    public Tile GetCurrentTile()
    {
        return currentTile;
    }

    /// <summary>
    /// 캐릭터가 바라보는 방향을 설정하고, 필요 시 실제 3D 회전(rotation)도 업데이트합니다.
    /// </summary>
    public void SetFacingDirection(LookDirection direction, bool updateRotation = true)
    {
        _facingDirection = direction;
        if (updateRotation)
        {
            Vector3 forward = GetVectorFromDirection(direction);
            if (forward != Vector3.zero)
            {
                character.transform.rotation = Quaternion.LookRotation(forward);
            }
        }
    }

    /// <summary>
    /// 특정 타일이 있는 방향을 향해 캐릭터를 즉시 회전시킵니다. (가장 가까운 6방향 육각 축으로 스냅)
    /// </summary>
    public void LookAtTile(Tile targetTile)
    {
        if (targetTile == null) return;
        Vector3 diff = targetTile.transform.position - character.transform.position;
        if (diff != Vector3.zero)
        {
            LookDirection targetDir = GetDirectionFromVector(diff);
            SetFacingDirection(targetDir, true);
        }
    }

    /// <summary>
    /// 방향(LookDirection)에 매핑되는 3D 단위 벡터를 반환합니다.
    /// </summary>
    public static Vector3 GetVectorFromDirection(LookDirection direction)
    {
        return HexMath.DirectionToWorldVector(direction);
    }

    /// <summary>
    /// 3D 방향 벡터(X-Z 평면 기준)를 기반으로 가장 가까운 6방향 LookDirection을 반환합니다.
    /// </summary>
    public static LookDirection GetDirectionFromVector(Vector3 direction)
    {
        return HexMath.WorldVectorToDirection(direction, LookDirection.East);
    }


    /// <summary>
    /// 계산된 경로(movePath)를 기반으로 실제 이동을 수행합니다.
    /// </summary>
    private Coroutine activeMoveCoroutine;

    /// <summary>
    /// 현재 진행 중인 이동 코루틴을 강제 정지하고 캐릭터 상태를 복구합니다.
    /// 전투 종료, 행동 취소, 피격/사망 인터럽트 시 호출됩니다.
    /// </summary>
    public void StopMoving()
    {
        if (activeMoveCoroutine != null && this.character != null)
        {
            this.character.StopCoroutine(activeMoveCoroutine);
            activeMoveCoroutine = null;
        }

        if (this.character != null && !this.character.isDead)
        {
            if (this.character.currentState == CharacterState.Move)
            {
                this.character.currentState = CharacterState.Idle;
            }

            if (currentTile != null)
            {
                Vector3 tilePos = currentTile.transform.position;
                this.character.transform.position = new Vector3(tilePos.x, this.character.transform.position.y, tilePos.z);
            }

            SetFacingDirection(_facingDirection, true);
        }
    }

    public void MoveAlongPath(List<Tile> movePath, Tile destinationTile, Action onComplete = null, MoveFlag moveFlags = MoveFlag.Normal, float speedMultiplier = 1.0f, Action onHalfway = null)
    {
        if (activeMoveCoroutine != null && this.character != null)
        {
            this.character.StopCoroutine(activeMoveCoroutine);
            activeMoveCoroutine = null;
        }

        Vector2Int fromCoord = currentTile != null ? currentTile.GetCoord() : Vector2Int.zero;
        Vector2Int toCoord = destinationTile != null ? destinationTile.GetCoord() : fromCoord;

        if (movePath == null || movePath.Count == 0 || (movePath.Count == 1 && movePath[0] == currentTile))
        {
            if (moveFlags.HasFlag(MoveFlag.Forced) && this.character != null)
            {
                MoveInfo zeroInfo = new MoveInfo(this.character, fromCoord, toCoord, moveFlags);
                zeroInfo.moveMultiplier = speedMultiplier;
                CombatEventDispatcher.DispatchBeforeForcedMove(this.character.eventBus, ref zeroInfo);
                CombatEventDispatcher.DispatchAfterForcedMove(this.character.eventBus, zeroInfo);
            }
            onHalfway?.Invoke();
            onComplete?.Invoke();
            return;
        }

        activeMoveCoroutine = this.character.StartCoroutine(StartMoveCoroutine(movePath, destinationTile, onComplete, moveFlags, speedMultiplier, onHalfway));
    }

    /// <summary>
    /// 이미 인접한 목표 타일을 향해 반 칸 전진 돌격했다가 원래 위치로 복귀하며,
    /// 전진 정점(50% 지점)에서 onHalfway 콜백을 실행합니다.
    /// </summary>
    public void LungeTowardsTile(Tile targetTile, Action onHalfway = null, Action onComplete = null, float speedMultiplier = 2.5f)
    {
        if (this.character == null || targetTile == null)
        {
            onHalfway?.Invoke();
            onComplete?.Invoke();
            return;
        }

        if (activeMoveCoroutine != null)
        {
            this.character.StopCoroutine(activeMoveCoroutine);
            activeMoveCoroutine = null;
        }

        activeMoveCoroutine = this.character.StartCoroutine(LungeRoutine(targetTile, onHalfway, onComplete, speedMultiplier));
    }

    private IEnumerator LungeRoutine(Tile targetTile, Action onHalfway, Action onComplete, float speedMultiplier)
    {
        if (this.character == null || this.character.isDead || targetTile == null)
        {
            activeMoveCoroutine = null;
            onHalfway?.Invoke();
            onComplete?.Invoke();
            yield break;
        }

        this.character.currentState = CharacterState.Move;
        LookAtTile(targetTile);

        Vector3 startPos = currentTile != null
            ? new Vector3(currentTile.transform.position.x, this.character.transform.position.y, currentTile.transform.position.z)
            : this.character.transform.position;
        Vector3 targetPosXZ = new Vector3(targetTile.transform.position.x, startPos.y, targetTile.transform.position.z);
        Vector3 midPoint = Vector3.Lerp(startPos, targetPosXZ, 0.5f);
        float baseEffectiveSpeed = moveSpeed * Mathf.Max(0.1f, speedMultiplier);

        while (Vector3.Distance(this.character.transform.position, midPoint) > 0.02f)
        {
            if (this.character == null || this.character.isDead)
            {
                activeMoveCoroutine = null;
                yield break;
            }

            float battleSpeed = Mathf.Max(0.1f, BattleManager.GlobalBattleSpeed);
            float effectiveSpeed = baseEffectiveSpeed * battleSpeed;
            this.character.transform.position = Vector3.MoveTowards(this.character.transform.position, midPoint, effectiveSpeed * Time.deltaTime);
            yield return null;
        }

        if (this.character == null || this.character.isDead)
        {
            activeMoveCoroutine = null;
            yield break;
        }

        this.character.transform.position = midPoint;
        onHalfway?.Invoke();

        while (this.character != null && !this.character.isDead && Vector3.Distance(this.character.transform.position, startPos) > 0.02f)
        {
            float battleSpeed = Mathf.Max(0.1f, BattleManager.GlobalBattleSpeed);
            float effectiveSpeed = baseEffectiveSpeed * battleSpeed;
            this.character.transform.position = Vector3.MoveTowards(this.character.transform.position, startPos, effectiveSpeed * Time.deltaTime);
            yield return null;
        }

        if (this.character != null && !this.character.isDead)
        {
            this.character.transform.position = startPos;
            SetFacingDirection(_facingDirection, true);
            if (this.character.currentState != CharacterState.Die)
            {
                this.character.currentState = CharacterState.Idle;
            }
        }

        activeMoveCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator StartMoveCoroutine(List<Tile> movePath, Tile destinationTile, Action onComplete = null, MoveFlag moveFlags = MoveFlag.Normal, float speedMultiplier = 1.0f, Action onHalfway = null)
    {
        // 1. 이동 직전 이벤트 (IOnBeforeMove / IOnBeforeForcedMove)
        Vector2Int fromCoord = currentTile != null ? currentTile.GetCoord() : Vector2Int.zero;
        Vector2Int toCoord = destinationTile != null ? destinationTile.GetCoord() : fromCoord;

        MoveInfo beforeInfo = new MoveInfo(this.character, fromCoord, toCoord, moveFlags);
        beforeInfo.moveMultiplier = speedMultiplier;

        if (this.character != null)
        {
            if (moveFlags.HasFlag(MoveFlag.Forced))
            {
                CombatEventDispatcher.DispatchBeforeForcedMove(this.character.eventBus, ref beforeInfo);
            }
            else
            {
                CombatEventDispatcher.DispatchBeforeMove(this.character.eventBus, ref beforeInfo);
            }
        }

        if (beforeInfo.isCanceled)
        {
            if (this.character != null && this.character.currentState != CharacterState.Die)
            {
                this.character.currentState = CharacterState.Idle;
            }
            activeMoveCoroutine = null;
            onHalfway?.Invoke();
            onComplete?.Invoke();
            yield break;
        }

        if (this.character != null && this.character.currentState != CharacterState.Die)
        {
            this.character.currentState = CharacterState.Move;
        }

        Vector3 startPosXZ = this.character != null
            ? new Vector3(this.character.transform.position.x, 0f, this.character.transform.position.z)
            : Vector3.zero;
        Vector3 finalPosXZ = destinationTile != null
            ? new Vector3(destinationTile.transform.position.x, 0f, destinationTile.transform.position.z)
            : startPosXZ;
        float totalDistanceXZ = Vector3.Distance(startPosXZ, finalPosXZ);
        bool halfwayInvoked = false;

        bool triggerHazards = !moveFlags.HasFlag(MoveFlag.Teleport);
        float baseEffectiveSpeed = moveSpeed * Mathf.Max(0.1f, speedMultiplier);
        int currentIndex = 0;
        while (currentIndex < movePath.Count)
        {
            if (this.character == null || this.character.isDead)
            {
                activeMoveCoroutine = null;
                onComplete?.Invoke();
                yield break;
            }

            Tile stepTile = movePath[currentIndex];
            if (stepTile == null)
            {
                currentIndex++;
                continue;
            }

            float battleSpeed = Mathf.Max(0.1f, BattleManager.GlobalBattleSpeed);
            float effectiveSpeed = baseEffectiveSpeed * battleSpeed;
            float effectiveTurnSpeed = turnSpeed * battleSpeed;

            // 목표 위치(캐릭터 지상 Y 평면 유지)로 이동 및 일반 이동 시 수평 회전 보간
            Vector3 rawTargetPos = stepTile.transform.position;
            Vector3 targetPos = new Vector3(rawTargetPos.x, this.character.transform.position.y, rawTargetPos.z);
            this.character.transform.position = Vector3.MoveTowards(this.character.transform.position, targetPos, effectiveSpeed * Time.deltaTime);

            Vector3 directionVector = targetPos - this.character.transform.position;
            directionVector.y = 0f;
            if (directionVector.sqrMagnitude > 0.0001f && !moveFlags.HasFlag(MoveFlag.Forced))
            {
                // 이동 시작 시 논리적 바라보는 방향 업데이트 (실제 3D 회전은 아래에서 보간 처리되므로 updateRotation = false)
                LookDirection newDirection = GetDirectionFromVector(directionVector);
                SetFacingDirection(newDirection, false);

                Quaternion targetRotation = Quaternion.LookRotation(directionVector);
                this.character.transform.rotation = Quaternion.RotateTowards(this.character.transform.rotation, targetRotation, effectiveTurnSpeed * Time.deltaTime);
            }

            if (!halfwayInvoked && onHalfway != null)
            {
                Vector3 currentPosXZ = new Vector3(this.character.transform.position.x, 0f, this.character.transform.position.z);
                float remainingXZ = Vector3.Distance(currentPosXZ, finalPosXZ);
                if (totalDistanceXZ <= 0.01f || remainingXZ <= totalDistanceXZ * 0.5f)
                {
                    halfwayInvoked = true;
                    onHalfway.Invoke();

                    if (this.character == null || this.character.isDead)
                    {
                        activeMoveCoroutine = null;
                        onComplete?.Invoke();
                        yield break;
                    }
                }
            }

            // 도착 판정 및 좌표 스냅
            if (Vector3.Distance(this.character.transform.position, targetPos) < 0.1f)
            {
                this.character.transform.position = targetPos;
                // 중간 타일 도달 시 현재 타일 논리적 업데이트 및 타일 위험 요소 판정
                SetCurrentTile(stepTile, triggerTileHazards: triggerHazards);
                currentIndex++;

                // 함정 피격 등으로 이동 도중 사망한 경우 즉시 이동을 중단하고 콜백 정리
                if (this.character == null || this.character.isDead)
                {
                    activeMoveCoroutine = null;
                    onComplete?.Invoke();
                    yield break;
                }
            }

            yield return null;
        }

        // 이동 완료 후 최종 타일 및 수직 정렬/회전 스냅 보장 (마지막 스텝과 동일 타일이면 중복 발동하지 않음)
        if (!halfwayInvoked && onHalfway != null)
        {
            halfwayInvoked = true;
            onHalfway.Invoke();
        }

        if (this.character == null || this.character.isDead)
        {
            activeMoveCoroutine = null;
            onComplete?.Invoke();
            yield break;
        }

        if (destinationTile != null)
        {
            Vector3 destPos = destinationTile.transform.position;
            this.character.transform.position = new Vector3(destPos.x, this.character.transform.position.y, destPos.z);
        }
        SetFacingDirection(_facingDirection, true);

        SetCurrentTile(destinationTile, triggerTileHazards: triggerHazards);

        if (this.character == null || this.character.isDead)
        {
            activeMoveCoroutine = null;
            onComplete?.Invoke();
            yield break;
        }

        if (this.character != null && this.character.currentState != CharacterState.Die)
        {
            this.character.currentState = CharacterState.Idle;
        }

        // 2. 이동 직후 이벤트 (IOnAfterMove / IOnAfterForcedMove)
        Vector2Int currentCoord = currentTile != null ? currentTile.GetCoord() : toCoord;
        MoveInfo afterInfo = new MoveInfo(this.character, fromCoord, currentCoord, moveFlags);
        afterInfo.moveMultiplier = speedMultiplier;

        if (this.character != null)
        {
            if (moveFlags.HasFlag(MoveFlag.Forced))
            {
                CombatEventDispatcher.DispatchAfterForcedMove(this.character.eventBus, afterInfo);
            }
            else
            {
                CombatEventDispatcher.DispatchAfterMove(this.character.eventBus, afterInfo);
            }
        }

        activeMoveCoroutine = null;

        // 콜백 호출
        onComplete?.Invoke();
    }
}
