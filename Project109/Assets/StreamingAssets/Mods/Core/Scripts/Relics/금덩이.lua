local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    owner.playerStat.InGameCurrencyGold = owner.playerStat.InGameCurrencyGold + 400
end

return relic
