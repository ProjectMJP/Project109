local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnTurnStart()
    local character = self.owner.character
    if character == nil then return end
    character:TakeShield(character, 4, 1)
end

return relic
