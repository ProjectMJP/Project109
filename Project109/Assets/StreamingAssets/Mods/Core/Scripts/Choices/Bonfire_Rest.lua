-- Bonfire_Rest.lua
-- 모닥불 휴식 (체력 30% 회복)

function CanSelect(dm)
    return dm:GetCurrentHealth() < dm:GetMaxHealth()
end

function ExecuteChoice(dm)
    local maxHp = dm:GetMaxHealth()
    local healAmount = math.max(1, math.floor(maxHp * 0.3))
    dm:HealPlayer(healAmount)
    dm:MarkActiveInteractableUsed()
end

function GetDescription(dm)
    if dm:GetCurrentHealth() >= dm:GetMaxHealth() then
        return "이미 체력이 가득 차 있습니다."
    end
    local maxHp = dm:GetMaxHealth()
    local healAmount = math.max(1, math.floor(maxHp * 0.3))
    return string.format("체력을 30%% (%d) 회복합니다.", healAmount)
end
