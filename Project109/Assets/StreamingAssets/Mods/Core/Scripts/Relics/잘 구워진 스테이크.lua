local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    local character = owner.character
    if character ~= nil and character.curCharacterStat ~= nil then
        character.curCharacterStat.maxHealth = character.curCharacterStat.maxHealth + 20
        character.curHealth = character.curHealth + 20
    end
end

function relic:OnRemoved(relicBase, owner)
    local character = owner.character
    if character ~= nil and character.curCharacterStat ~= nil then
        character.curCharacterStat.maxHealth = character.curCharacterStat.maxHealth - 20
        character.curHealth = math.min(character.curHealth, character.curCharacterStat.maxHealth)
    end
end

return relic
