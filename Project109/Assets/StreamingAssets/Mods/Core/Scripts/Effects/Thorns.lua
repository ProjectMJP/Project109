local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

function effect:OnAfterTakeDamage(damageInfo)
    if damageInfo.caster ~= nil and not damageInfo.damageFlags:HasFlag(EventStructs.DamageFlag.NoTargetEvents) then
        local target = self.base.target
        local attacker = damageInfo.caster
        local dmg = self.base.currentStack
        
        local reflectedDmgInfo = EventStructs.DamageInfo(target, attacker, dmg, EventStructs.DamageFlag.Reflected)
        attacker:TakeDamage(reflectedDmgInfo)
    end
end

function effect:GetDescription(effectBase, template)
    local stack = effectBase.currentStack
    return (template:gsub("{stacks}", tostring(stack)))
end

return effect
