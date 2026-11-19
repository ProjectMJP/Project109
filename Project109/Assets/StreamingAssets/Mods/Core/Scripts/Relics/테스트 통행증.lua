local relic = {}

function relic:OnInit(relicBase, owner)
    self.base = relicBase
    self.owner = owner
    if owner ~= nil and owner.playerStat ~= nil then
        owner.playerStat.BattleMapLockBypassCount = owner.playerStat.BattleMapLockBypassCount + 1
    end
end

function relic:OnRemoved(relicBase, owner)
    local targetOwner = owner or self.owner
    if targetOwner ~= nil and targetOwner.playerStat ~= nil then
        targetOwner.playerStat.BattleMapLockBypassCount = math.max(0, targetOwner.playerStat.BattleMapLockBypassCount - 1)
    end
end

return relic
