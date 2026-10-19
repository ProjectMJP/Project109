-- AltarErase_Touch.lua
function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:OpenEraseCardUI(1)
end

function GetDescription(dm)
    return "카드를 1 번 제거합니다."
end
