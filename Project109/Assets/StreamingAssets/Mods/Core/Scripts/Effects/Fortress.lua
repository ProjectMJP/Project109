local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

function effect:OnTurnEnd()
    local target = self.base.target
    if target == nil then return end

    if target.shield > 0 then
        target.shieldDurationTurns = target.shieldDurationTurns + 1
    end

    self.base.currentStack = self.base.currentStack - 1
    if target.effectManager ~= nil then
        if self.base.currentStack <= 0 then
            target.effectManager:RemoveEffect(self.base)
        else
            target.effectManager:NotifyEffectUpdated(self.base)
        end
    end
end

function effect:GetDescription(effectBase, template)
    local stack = math.floor(effectBase.currentStack)
    return (template:gsub("{stacks}", tostring(stack)))
end

return effect
