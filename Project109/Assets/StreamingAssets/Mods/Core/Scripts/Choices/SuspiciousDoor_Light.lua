-- SuspiciousDoor_Light.lua
function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:OpenUpgradeCardUI()
end

function GetDescription(dm)
    return "강화를 1 번 진행합니다."
end
