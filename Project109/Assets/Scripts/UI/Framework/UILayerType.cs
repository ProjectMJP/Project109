public enum UILayerType
{
    World,      // 캐릭터 머리 위 HP 바, 상태이상, 데미지 텍스트 등 (UIManager Stack 관리 제외)
    Normal,     // 스택 기반 화면 전환 UI (상점, NPC 대화, 휴식 등)
    Top,        // 항상 맨 위에 떠 있는 UI (재화 UI, 덱 버튼 등, Stack 제외)
    Popup       // 카드/유물 툴팁, 시스템 설정(ESC) 창 등 (전면 노출 및 우선권 닫기 가능)
}
