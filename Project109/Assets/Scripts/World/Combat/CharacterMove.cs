using EventStructs;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum LookDirection
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// 진정한 의미의 '캐릭터 이동 실행' 로직 클래스입니다.
/// 플레이어와 적(Enemy) 모두 이 클래스를 소유(has-a)하여 코루틴 이동을 수행합니다.
/// </summary>
public class CharacterMove
{
    private LookDirection _facingDirection = LookDirection.Down;
    public LookDirection facingDirection => _facingDirection;

    public Character character { private set; get; }

    protected Tile currentTile;
    public float moveSpeed = 50f;
    public float turnSpeed = 600f;
    public MoverCapability capabilities = MoverCapability.None;

    public CharacterMove(Character character)
    {
        this.character = character;
    }

    public void SetCurrentTile(Tile newTile)
    {
        currentTile = newTile;
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
    /// 특정 타일이 있는 방향을 향해 캐릭터를 즉시 회전시킵니다. (가장 가까운 상하좌우 축으로 스냅)
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
    /// 방향(LookDirection)에 매핑되는 3D 벡터를 반환합니다.
    /// </summary>
    public static Vector3 GetVectorFromDirection(LookDirection direction)
    {
        switch (direction)
        {
            case LookDirection.Up: return Vector3.forward;
            case LookDirection.Down: return Vector3.back;
            case LookDirection.Left: return Vector3.left;
            case LookDirection.Right: return Vector3.right;
            default: return Vector3.forward;
        }
    }

    /// <summary>
    /// 3D 방향 벡터(X-Z 평면 기준)를 기반으로 대응하는 LookDirection을 반환합니다.
    /// </summary>
    public static LookDirection GetDirectionFromVector(Vector3 direction)
    {
        float x = direction.x;
        float z = direction.z;

        if (Mathf.Abs(x) > Mathf.Abs(z))
        {
            return x > 0 ? LookDirection.Right : LookDirection.Left;
        }
        else
        {
            return z > 0 ? LookDirection.Up : LookDirection.Down;
        }
    }


    /// <summary>
    /// 계산된 경로(movePath)를 기반으로 실제 이동을 수행합니다.
    /// </summary>
    private Coroutine activeMoveCoroutine;

    public void MoveAlongPath(List<Tile> movePath, Tile destinationTile, Action onComplete = null)
    {
        if (activeMoveCoroutine != null)
        {
            this.character.StopCoroutine(activeMoveCoroutine);
            activeMoveCoroutine = null;
        }

        if (movePath == null || movePath.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        if (RunManager.instance != null && RunManager.instance.currentMap != null && RunManager.instance.currentMap.currentMapState == MapState.Battle)
        {
            this.character.curMoveCount--;
        }

        activeMoveCoroutine = this.character.StartCoroutine(StartMoveCoroutine(movePath, destinationTile, onComplete));
    }

    private IEnumerator StartMoveCoroutine(List<Tile> movePath, Tile destinationTile, Action onComplete = null)
    {
        // 1. 이동 직전 이벤트 (IOnBeforeMove)
        Vector2Int fromCoord = currentTile.GetCoord();
        Vector2Int toCoord = destinationTile.GetCoord();

        MoveInfo beforeInfo = new MoveInfo(this.character, fromCoord, toCoord, MoveFlag.Normal);
        this.character.eventBus?.Invoke<ICharacterEvent>(c => (c as IOnBeforeMove)?.OnBeforeMove(beforeInfo));

        if (beforeInfo.isCanceled)
        {
            activeMoveCoroutine = null;
            yield break;
        }

        if (this.character.currentState != CharacterState.Die)
        {
            this.character.currentState = CharacterState.Move;
        }

        int currentIndex = 0;
        while (currentIndex < movePath.Count)
        {
            // 목표 회전 및 이동
            Vector3 targetPos = movePath[currentIndex].transform.position;
            this.character.transform.position = Vector3.MoveTowards(this.character.transform.position, targetPos, moveSpeed * Time.deltaTime);

            Vector3 directionVector = targetPos - this.character.transform.position;
            if (directionVector != Vector3.zero)
            {
                // 이동 시작 시 논리적 바라보는 방향 업데이트 (실제 3D 회전은 아래에서 보간 처리되므로 updateRotation = false)
                LookDirection newDirection = GetDirectionFromVector(directionVector);
                SetFacingDirection(newDirection, false);

                Quaternion targetRotation = Quaternion.LookRotation(directionVector);
                this.character.transform.rotation = Quaternion.RotateTowards(this.character.transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            // 도착 판정
            if (Vector3.Distance(this.character.transform.position, targetPos) < 0.1f)
            {
                // 중간 타일 도달 시 현재 타일 논리적 업데이트
                SetCurrentTile(movePath[currentIndex]);
                currentIndex++;
            }

            yield return null;
        }

        // 이동 완료 후 최종 타일 갱신
        SetCurrentTile(destinationTile);

        if (this.character.currentState != CharacterState.Die)
        {
            this.character.currentState = CharacterState.Idle;
        }

        // 2. 이동 직후 이벤트 (IOnAfterMove)
        Vector2Int currentCoord = currentTile.GetCoord();
        MoveInfo afterInfo = new MoveInfo(this.character, currentCoord, currentCoord, MoveFlag.Normal);
        this.character.eventBus?.Invoke<ICharacterEvent>(c => (c as IOnAfterMove)?.OnAfterMove(afterInfo));

        // 콜백 호출
        onComplete?.Invoke();

        activeMoveCoroutine = null;
    }
}
