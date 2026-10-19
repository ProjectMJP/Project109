-- AltarGold_TakeGold.lua
function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:AddGold(300)
    dm:AddCard("욕심")
end

function GetDescription(dm)
    return "재화를 300 획득합니다, 욕심 획득합니다."
end
