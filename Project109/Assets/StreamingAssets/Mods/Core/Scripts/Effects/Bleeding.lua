local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
    self.turnsWithoutRefresh = 2
end

function effect:OnAdded(effectInstance, caster, target, stack, duration)
    self.turnsWithoutRefresh = 2
end

function effect:OnStacked(effectInstance, stack, duration)
    self.turnsWithoutRefresh = 2
    local maxStack = self.base.Data ~= nil and self.base.Data.maxStack or 2147483647
    self.base.currentStack = math.min(self.base.currentStack + stack, maxStack)
    self.base.currentDuration = duration
end

function effect:OnTurnEnd()
    local target = self.base.target
    local caster = self.base.caster
    
    local stack = self.base.currentStack
    local dmg = 1 + (2 ^ stack)
    
    local dmgInfo = EventStructs.DamageInfo(caster, target, dmg, EventStructs.DamageFlag.HPLoss)
    target:TakeDamage(dmgInfo)
    
    self.turnsWithoutRefresh = self.turnsWithoutRefresh - 1
    if self.turnsWithoutRefresh <= 0 then
        target.effectManager:RemoveEffect(self.base)
    end
end

function effect:GetDescription(effectBase, template)
    local stack = effectBase.currentStack
    local dmg = 1 + (2 ^ stack)
    return (template:gsub("{damage}", tostring(math.floor(dmg))))
end

return effect
