-- SuspiciousDoor_Dark.lua
function CanSelect(dm)
    return dm:GetMaxHealth() > 7
end

function ExecuteChoice(dm)
    dm:ModifyMaxHealth(-7)
    dm:SpawnReward("relic", "common")
end

function GetDescription(dm)
    return "<color=red>최대 체력 7 감소</color>, 유물 보상을 1번 획득합니다."
end
