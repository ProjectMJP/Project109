local card = {}
function card:OnInit(cardBase, owner)
    self.base = cardBase
end
function card:GetDescription(cardBase, template)
    local baseDamage = cardBase:GetBaseFormattedValue("damage")
    local count = cardBase:GetBaseFormattedValue("count")
    return (template:gsub("{damage}", tostring(baseDamage)):gsub("{count}", tostring(count)))
end
function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if cardInfo.targets ~= nil and cardInfo.targets.Count > 0 then
        local target = cardInfo.targets[0]
        local baseDamage = self.base:GetBaseValue("damage")
        local count = self.base:GetBaseValue("count")
        local dmgInfo = EventStructs.DamageInfo(caster, target, baseDamage, EventStructs.DamageFlag.Normal)
        target:TakeDamage(dmgInfo)
        
        if RunManager.instance ~= nil and RunManager.instance.playerBattleController ~= nil then
            local deck = RunManager.instance.playerBattleController.battleDeck
            if deck ~= nil and deck.discardPile.Count > 0 then
                for i = 1, math.min(count, deck.discardPile.Count) do
                    local lastIdx = deck.discardPile.Count - 1
                    local c = deck.discardPile[lastIdx]
                    deck.discardPile:RemoveAt(lastIdx)
                    deck.hand:Add(c)
                end
            end
        end
    end
end
return card