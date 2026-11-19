local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

-- 선계산 훅: 타임라인 예측과 실제 BattleManager.EndTurn() 커밋이 동일한 계산을 공유
function effect:OnCalculateTurnEndStamina(info)
    if self.base ~= nil then
        local staminaRecovery = 10 * self.base.currentStack
        info.postResetBonusStamina = info.postResetBonusStamina + staminaRecovery
    end
    return info
end

function effect:OnTurnEnd()
    local target = self.base ~= nil and self.base.target or nil
    if target ~= nil and target.effectManager ~= nil then
        target.effectManager:RemoveEffect(self.base)
    end
end

function effect:GetDescription(effectBase, template)
    local stack = effectBase.currentStack
    return (template:gsub("{stamina}", tostring(stack * 10)))
end

return effect
