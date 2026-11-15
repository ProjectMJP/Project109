-- EventTest_Battle.lua
function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:StartBattle("Temple_Battle_01")
end

function GetDescription(dm)
    return "전투를 진행합니다."
end
