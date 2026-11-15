-- AncientLibrary_Collect.lua
function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:SpawnReward("card", "common")
    dm:SpawnReward("card", "common")
end

function GetDescription(dm)
    return "카드 보상을 2번 획득합니다."
end
