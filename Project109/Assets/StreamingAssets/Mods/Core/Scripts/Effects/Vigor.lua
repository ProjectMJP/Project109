local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

function effect:OnTurnEnd()
    local target = self.base.target
    local staminaRecovery = 10 * self.base.currentStack
    
    target:TakeStamina(target, staminaRecovery, EventStructs.StaminaFlag.Normal)
    target.effectManager:RemoveEffect(self.base)
end

function effect:GetDescription(effectBase, template)
    local stack = effectBase.currentStack
    return (template:gsub("{stamina}", tostring(stack * 10)))
end

return effect
