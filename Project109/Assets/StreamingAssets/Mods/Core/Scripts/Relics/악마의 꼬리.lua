local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnTurnStart()
    local character = self.owner.character
    if character == nil then return end
    
    local cTile = character.characterMove:GetCurrentTile()
    if cTile == nil then return end
    
    local all = RunManager.instance.battleManager:GetAllCombatants()
    if all == nil then return end
    
    for i = 0, all.Count - 1 do
        local target = all[i].controlledCharacter
        if target ~= nil and character:IsHostileTo(target) then
            local tTile = target.characterMove:GetCurrentTile()
            if tTile ~= nil then
                if cTile:GetDistanceTo(tTile) <= 2 then
                    target:TakeEffect(character, "Weakness", 1, 999)
                end
            end
        end
    end
end

return relic
