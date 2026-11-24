local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    if relicBase == nil or not relicBase.IsRestoredFromSave then
        owner.playerStat.InGameCurrencyGold = owner.playerStat.InGameCurrencyGold + 400
    end
end

return relic
