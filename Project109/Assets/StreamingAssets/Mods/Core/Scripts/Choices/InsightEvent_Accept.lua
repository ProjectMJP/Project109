-- InsightEvent_Accept.lua
function CanSelect(dm)
    return dm:GetGold() >= 100
end

function ExecuteChoice(dm)
    dm:ConsumeGold(100)
    dm:RevealAllNodes()
end

function GetDescription(dm)
    return "<color=red>100 소모</color>, 이벤트를 진행합니다."
end
