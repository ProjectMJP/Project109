-- EventTest_Battle.lua
function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:StartBattle("Battle_Test_Data")
end

function GetDescription(dm)
    return "전투를 진행합니다."
end
