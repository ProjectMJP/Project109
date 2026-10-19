-- EventTest_LoseRelicGetGold.lua
local selectedIndex = -1

function CanSelect(dm)
    return dm:GetRelicCount() > 0
end

function GetDescription(dm)
    if dm:GetRelicCount() == 0 then
        return "제거할 유물이 없습니다."
    end
    if selectedIndex == -1 then
        selectedIndex = CS.UnityEngine.Random.Range(0, dm:GetRelicCount())
    end
    local relicName = dm:GetRelicNameAt(selectedIndex)
    return "<color=red>" .. relicName .. " 제거</color>, 재화를 100 획득합니다."
end

function ExecuteChoice(dm)
    if selectedIndex == -1 and dm:GetRelicCount() > 0 then
        selectedIndex = 0
    end
    if selectedIndex ~= -1 then
        dm:RemoveRelicAt(selectedIndex)
    end
    dm:AddGold(100)
    selectedIndex = -1
end
