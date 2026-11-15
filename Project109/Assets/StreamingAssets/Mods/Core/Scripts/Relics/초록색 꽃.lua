local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnBeforeTakeHeal(info)
    if info.target ~= self.owner.character then return end
    info.baseHealAmount = info.baseHealAmount * 1.3
end

return relic
