-- AltarGold_PickUpGold.lua
function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:AddGold(75)
end

function GetDescription(dm)
    return "재화를 75 획득합니다."
end
