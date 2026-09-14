using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueData : IIdentifiable
{
    public string dialogueID;                 // 다이얼로그 고유 식별자
    public string startNodeID;                // 시작 대화 노드 ID
    public List<DialogueNode> nodes;          // 이 대화에 포함된 모든 장면 노드 목록

    public string ID => dialogueID;
}

[System.Serializable]
public class DialogueNode
{
    public string nodeID;                     // 노드 고유 식별자 (예: "START", "STAGE_1")
    public string speakerName;                // 출력될 화자의 이름
    public List<string> lines;                // 플레이어 클릭 시 순차적으로 출력되는 대화 지문 리스트
    public List<ChoiceData> choices;          // 대화 지문이 모두 종료된 후 출력될 선택지 목록
}

[System.Serializable]
public class ChoiceData
{
    public string description;                // 선택지 버튼에 표기될 기본 내용
    public string nextNodeID;                 // 선택 시 이동할 다음 DialogueNode ID (비어 있으면 대화 종료)
    public string luaScript;                  // 선택 조건 검사 및 샌드박스 정산 처리를 진행할 Lua 스크립트 경로
}
