local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnAfterDealDamage(info)
    if info.caster ~= self.owner.character then return end
    if info.hpDamageAmount > 0 then
        info.target:TakeEffect(info.caster, "Bleeding", 1, 3)
    end
end

return relic
