local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

function effect:OnTurnStart()
    local target = self.base.target
    if target == nil then return end

    target.curStamina = 0
    target.curMoveRange = 0

    self.base.currentStack = self.base.currentStack - 1
    if target.effectManager ~= nil then
        if self.base.currentStack <= 0 then
            target.effectManager:RemoveEffect(self.base)
        else
            target.effectManager:NotifyEffectUpdated(self.base)
        end
    end

    if RunManager ~= nil and RunManager.instance ~= nil and RunManager.instance.battleManager ~= nil then
        RunManager.instance.battleManager:EndTurn()
    end
end

function effect:GetDescription(effectBase, template)
    local stack = math.floor(effectBase.currentStack)
    return (template:gsub("{stacks}", tostring(stack)))
end

return effect
