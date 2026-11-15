local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnAfterMove(info)
    if info.mover ~= self.owner.character then return end
    if info.moveFlags:HasFlag(EventStructs.MoveFlag.Forced) then return end
    
    local character = self.owner.character
    local cTile = character.characterMove:GetCurrentTile()
    if cTile == nil then return end
    local cCoord = cTile:GetCoord()
    
    local all = RunManager.instance.battleManager:GetAllCombatants()
    if all == nil then return end
    
    for i = 0, all.Count - 1 do
        local target = all[i].controlledCharacter
        if target ~= nil and character:IsHostileTo(target) then
            local tTile = target.characterMove:GetCurrentTile()
            if tTile ~= nil then
                local tCoord = tTile:GetCoord()
                local dx = math.abs(cCoord.x - tCoord.x)
                local dy = math.abs(cCoord.y - tCoord.y)
                if dx + dy <= 1 then
                    local dmgInfo = EventStructs.DamageInfo(character, target, 3, EventStructs.DamageFlag.Normal)
                    target:TakeDamage(dmgInfo)
                end
            end
        end
    end
end

return relic
