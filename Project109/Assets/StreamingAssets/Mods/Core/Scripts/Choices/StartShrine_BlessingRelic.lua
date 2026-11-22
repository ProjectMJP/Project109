-- StartShrine_BlessingRelic.lua
function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:SpawnReward("relic", "Default_Relic_Table")
    dm:MarkActiveInteractableUsed()
end

function GetDescription(dm)
    return "무작위 유물 보상을 획득합니다."
end
