using System.Collections.Generic;

[System.Serializable]
public class DropTableData : IIdentifiable
{
    public string dropTableID { get; set; }                 // 드롭 테이블 고유 식별자

    // 등급별 등장 가중치
    public int commonWeight { get; set; } = 0;
    public int uncommonWeight { get; set; } = 0;
    public int rareWeight { get; set; } = 0;
    public int uniqueWeight { get; set; } = 0;

    // 동적 필터 조건
    public List<string> allowedClassTypes { get; set; } = new List<string>(); // 허용할 직업군 (비어있으면 전체 대상)
    public List<string> allowedCardTypes { get; set; } = new List<string>();  // 허용할 카드 타입 (비어있으면 전체 대상)
    public List<string> excludedItemIDs { get; set; } = new List<string>();   // 드롭 풀에서 배제할 아이템 ID 목록

    // 고정/특정 아이템 풀 (지정 시 가중치/필터를 무시하고 이 리스트 내에서만 선택)
    public List<string> specificItemIDs { get; set; } = new List<string>();

    public string ID => dropTableID;
}
