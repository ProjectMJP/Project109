local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
    self.previousDamage = 0
end

function effect:OnTurnEnd()
    local target = self.base.target
    local caster = self.base.caster
    local stack = self.base.currentStack
    
    local dmg = stack + (self.previousDamage or 0)
    self.previousDamage = dmg
    
    local dmgInfo = EventStructs.DamageInfo(caster, target, dmg, EventStructs.DamageFlag.HPLoss)
    target:TakeDamage(dmgInfo)
end

function effect:GetDescription(effectBase, template)
    local stack = effectBase.currentStack
    local prevDmg = self.previousDamage or 0
    local nextDmg = stack + prevDmg
    return (template:gsub("{damage}", tostring(math.floor(nextDmg))))
end

return effect
