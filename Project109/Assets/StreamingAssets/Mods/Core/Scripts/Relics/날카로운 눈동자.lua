local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnBattleStart()
    if RunManager == nil or RunManager.instance == nil then return end
    local node = RunManager.instance.currentExploreNode
    if node == nil or node.encounterData == nil then return end

    local encountType = tostring(node.encounterData.encountType)
    if encountType == "Elite" or encountType == "Boss" then
        local all = RunManager.instance.battleManager:GetAllCombatants()
        if all == nil then return end
        local character = self.owner.character
        for i = 0, all.Count - 1 do
            local target = all[i].controlledCharacter
            if target ~= nil and character:IsHostileTo(target) then
                local hpToReduce = math.floor(target.curHealth * 0.15)
                if hpToReduce > 0 then
                    target:TakeDamage(character, hpToReduce, EventStructs.DamageFlag.HPLoss)
                end
            end
        end
    end
end

return relic
