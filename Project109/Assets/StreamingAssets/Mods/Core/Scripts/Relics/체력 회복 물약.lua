local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    self.base.Counter = 1
end

function relic:OnBattleStart()
    self.base.Counter = 1
end

function relic:OnAfterTakeDamage(info)
    if info.target ~= self.owner.character then return end
    local character = self.owner.character
    if character == nil or self.base.Counter <= 0 then return end
    if character.curHealthRate < 0.20 then
        self.base.Counter = 0
        local healAmount = math.floor(character.curCharacterStat.maxHealth * 0.3)
        character:TakeHeal(character, healAmount)
    end
end

return relic
