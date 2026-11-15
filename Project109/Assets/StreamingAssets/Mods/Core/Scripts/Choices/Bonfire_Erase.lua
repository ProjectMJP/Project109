-- Bonfire_Erase.lua
-- 모닥불 망각 (카드 1장 제거)

function CanSelect(dm)
    return dm:GetCardCount() > 0
end

function ExecuteChoice(dm)
    dm:OpenEraseCardUI(1)
    dm:MarkActiveInteractableUsed()
end

function GetDescription(dm)
    if dm:GetCardCount() == 0 then
        return "제거할 카드가 없습니다."
    end
    return "카드 1장을 덱에서 영구히 제거합니다."
end
