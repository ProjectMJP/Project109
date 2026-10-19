-- EventTest_LoseCardGetGold.lua
local selectedIndex = -1

function CanSelect(dm)
    return dm:GetCardCount() > 0
end

function GetDescription(dm)
    if dm:GetCardCount() == 0 then
        return "제거할 카드가 없습니다."
    end
    if selectedIndex == -1 then
        selectedIndex = CS.UnityEngine.Random.Range(0, dm:GetCardCount())
    end
    local cardName = dm:GetCardNameAt(selectedIndex)
    return "<color=red>" .. cardName .. " 제거</color>, 재화를 100 획득합니다."
end

function ExecuteChoice(dm)
    if selectedIndex == -1 and dm:GetCardCount() > 0 then
        selectedIndex = 0
    end
    if selectedIndex ~= -1 then
        dm:RemoveCardAt(selectedIndex)
    end
    dm:AddGold(100)
    selectedIndex = -1
end
