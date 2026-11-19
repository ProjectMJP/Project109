using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 드래그(카드 등) 진행 중 월드 입력 및 카메라 드래그가 동시에 발생하지 않도록 전역 드래그 소유자를 추적합니다.
/// [L0 Core] 계층에 위치하며 L4 UI와 L5 Input 사이의 직접 참조 없이 상태를 공유합니다.
/// </summary>
public static class UIDragInputGuard
{
    private static readonly HashSet<object> _activeDragOwners = new HashSet<object>();
    private static readonly Predicate<object> _isDeadOwnerPredicate = IsDeadOwner;

    /// <summary>
    /// 현재 활성화된 UI 드래그 소유자가 하나 이상 존재하는지 여부입니다.
    /// 파괴된 UnityEngine.Object 소유자는 조회 시 자동으로 정리됩니다.
    /// </summary>
    public static bool IsAnyUIDragging
    {
        get
        {
            if (_activeDragOwners.Count == 0)
            {
                return false;
            }

            PruneDeadOwners();
            return _activeDragOwners.Count > 0;
        }
    }

    /// <summary>
    /// 지정한 소유자의 UI 드래그 시작을 등록합니다.
    /// </summary>
    public static void BeginDrag(object owner)
    {
        if (IsDeadOwner(owner))
        {
            return;
        }

        _activeDragOwners.Add(owner);
    }

    /// <summary>
    /// 지정한 소유자의 UI 드래그 종료를 해제합니다. 반복 호출되어도 안전합니다.
    /// </summary>
    public static void EndDrag(object owner)
    {
        if (owner == null || _activeDragOwners.Count == 0)
        {
            return;
        }

        if (owner is UnityEngine.Object unityObj && unityObj == null)
        {
            PruneDeadOwners();
            return;
        }

        _activeDragOwners.Remove(owner);
    }

    /// <summary>
    /// 등록된 모든 UI 드래그 소유자를 즉시 초기화합니다.
    /// </summary>
    public static void ClearAll()
    {
        _activeDragOwners.Clear();
    }

    private static void PruneDeadOwners()
    {
        if (_activeDragOwners.Count > 0)
        {
            _activeDragOwners.RemoveWhere(_isDeadOwnerPredicate);
        }
    }

    private static bool IsDeadOwner(object owner)
    {
        return owner == null || (owner is UnityEngine.Object unityObj && unityObj == null);
    }
}
