-- Bonfire_Upgrade.lua
-- 모닥불 강화 (카드 1장 업그레이드) - 유물 또는 시스템 확장용

function IsVisible(dm)
    -- 필요 시 특정 유물 소지 조건 등을 부여 가능 (기본적으로 항상 노출할 경우 true)
    return true
end

function CanSelect(dm)
    return true
end

function ExecuteChoice(dm)
    dm:OpenUpgradeCardUI()
    dm:MarkActiveInteractableUsed()
end

function GetDescription(dm)
    return "카드 1장을 강화합니다."
end
