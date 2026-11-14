-- StartShrine_BlessingLife.lua
function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:HealWithOverhealToMaxHealth(20, 0.5)
end

function GetDescription(dm)
    return "체력을 20 회복합니다. (초과 회복량의 50%만큼 최대 체력 증가)"
end
