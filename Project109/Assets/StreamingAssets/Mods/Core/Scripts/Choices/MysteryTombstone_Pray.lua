-- MysteryTombstone_Pray.lua
function CanSelect(dm)
    return dm:GetGold() >= 50
end

function ExecuteChoice(dm)
    dm:ConsumeGold(50)
    dm:AddCard("슬래시")
end

function GetDescription(dm)
    return "<color=red>50 소모</color>, 슬래시 획득합니다."
end
