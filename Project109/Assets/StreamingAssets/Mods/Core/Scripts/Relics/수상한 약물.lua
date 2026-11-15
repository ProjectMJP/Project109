local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    self.base.Counter = 0
end

function relic:OnBattleStart()
    local character = self.owner.character
    if character == nil then return end
    character.curCharacterStat.maxStamina = character.curCharacterStat.maxStamina + 20
    character.curStamina = character.curStamina + 20
    self.base.Counter = 1
end

function relic:OnTurnEnd()
    local character = self.owner.character
    if character == nil then return end
    if self.base.Counter > 0 then
        self.base.Counter = 0
        character.curCharacterStat.maxStamina = character.curCharacterStat.maxStamina - 20
        character.curStamina = math.max(0, character.curStamina - 20)
    end
end

return relic
