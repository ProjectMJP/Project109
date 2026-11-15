local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

function effect:OnBeforeTakeDamage(damageInfo)
    damageInfo.baseDamageAmount = math.max(0, damageInfo.baseDamageAmount - self.base.currentStack)
    return damageInfo
end

function effect:GetDescription(effectBase, template)
    local stack = effectBase.currentStack
    return (template:gsub("{stacks}", tostring(stack)))
end

return effect
