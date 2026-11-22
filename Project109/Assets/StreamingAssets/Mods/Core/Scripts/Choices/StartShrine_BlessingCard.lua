-- StartShrine_BlessingCard.lua
function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:SpawnReward("card", "Default_Card_Table")
    dm:MarkActiveInteractableUsed()
end

function GetDescription(dm)
    return "무작위 카드 보상을 획득합니다."
end
