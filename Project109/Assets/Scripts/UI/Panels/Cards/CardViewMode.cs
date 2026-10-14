public enum CardViewMode
{
    /// <summary>
    /// 단순 열람 모드 (전체 덱, 뽑을 카드 더미, 버린 카드 더미, 소멸 더미 등).
    /// 카드 클릭 시 상세 정보 팝업(CardDetailPanel) 표시.
    /// </summary>
    ViewOnly,

    /// <summary>
    /// 1장 선택 모드 (카드 제거, 특정 카드 복제/변환 등).
    /// 카드 클릭 시 이전 선택을 해제하고 새 카드를 선택하며 확인 버튼 활성화.
    /// </summary>
    SingleSelect,

    /// <summary>
    /// 다중 선택 모드 (N장 선택).
    /// 목표 수량(targetSelectCount)만큼 선택 시 확인 버튼 활성화.
    /// </summary>
    MultiSelect,

    /// <summary>
    /// 카드 강화 선택 모드.
    /// 강화 가능한 카드 클릭 시 강화 프리뷰/확인 뷰(CardUpgradeDetailView 등) 표시.
    /// </summary>
    UpgradeSelect
}
