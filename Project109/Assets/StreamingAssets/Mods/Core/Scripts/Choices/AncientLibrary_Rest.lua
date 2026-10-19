-- AncientLibrary_Rest.lua
function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:HealPlayer(25)
end

function GetDescription(dm)
    return "체력을 25 회복합니다."
end
