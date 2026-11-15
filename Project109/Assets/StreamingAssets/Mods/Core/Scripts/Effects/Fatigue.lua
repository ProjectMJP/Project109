local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

function effect:OnTurnStart()
    local target = self.base.target
    if target == nil then return end

    target.curStamina = math.max(0, math.floor(target.curStamina * 0.7))

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
