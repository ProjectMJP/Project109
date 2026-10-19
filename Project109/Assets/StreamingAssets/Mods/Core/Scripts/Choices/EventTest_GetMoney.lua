-- EventTest_GetMoney.lua
function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:AddGold(50)
end

function GetDescription(dm)
    return "재화를 50 획득합니다."
end
