local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    self.base.Counter = 1
end

function relic:OnBattleStart()
    self.base.Counter = 1
    self:CheckSkull()
end

function relic:OnAfterTakeDamage(info)
    if info.target ~= self.owner.character then return end
    self:CheckSkull()
end

function relic:CheckSkull()
    local character = self.owner.character
    if character == nil or self.base.Counter <= 0 then return end
    if character.curHealthRate < 0.40 then
        self.base.Counter = 0
        character:TakeEffect(character, "Strength", 3, 999)
    end
end

return relic
