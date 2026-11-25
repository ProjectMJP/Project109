local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnBeforeDealDamage(info)
    if info.caster ~= self.owner.character then return end
    local cTile = info.caster.characterMove:GetCurrentTile()
    local tTile = info.target.characterMove:GetCurrentTile()
    if cTile ~= nil and tTile ~= nil then
        if cTile:GetDistanceTo(tTile) <= 1 then
            info.baseDamageAmount = info.baseDamageAmount + 2
        end
    end
end

return relic
