local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

function effect:OnBeforeTakeDamage(damageInfo)
    -- 받는 피해 50% 증가
    damageInfo.damageMultiplier = damageInfo.damageMultiplier * 1.5
    return damageInfo
end

function effect:OnTurnEnd()
    -- 턴 종료 시 1스택 차감
    self.base.currentStack = self.base.currentStack - 1
    if self.base.currentStack <= 0 then
        self.base.target.effectManager:RemoveEffect(self.base)
    end
end

function effect:GetDescription(effectBase, template)
    return template
end

return effect
