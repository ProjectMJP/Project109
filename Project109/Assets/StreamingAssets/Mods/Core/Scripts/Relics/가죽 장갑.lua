local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnBattleStart()
    local character = self.owner.character
    if character == nil then return end
    character:TakeEffect(character, "Strength", 1, 999)
end

return relic
