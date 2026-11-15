local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

function effect:OnBeforeDealDamage(damageInfo)
    -- 입히는 피해 25% 감소
    damageInfo.damageMultiplier = damageInfo.damageMultiplier * 0.75
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
