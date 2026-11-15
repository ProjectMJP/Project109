local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    self.base.Counter = 1
end

function relic:OnBattleStart()
    self.base.Counter = 1
end

function relic:OnBeforeDealDamage(info)
    if info.caster ~= self.owner.character then return end
    if self.base.Counter > 0 then
        self.base.Counter = 0
        info.baseDamageAmount = info.baseDamageAmount + 10
    end
end

return relic
