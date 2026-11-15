local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    owner.playerStat.RewardCardCount = owner.playerStat.RewardCardCount + 1
end

function relic:OnRemoved(relicBase, owner)
    owner.playerStat.RewardCardCount = owner.playerStat.RewardCardCount - 1
end

return relic
