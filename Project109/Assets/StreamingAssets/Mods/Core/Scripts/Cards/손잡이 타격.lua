local card = {}
function card:OnInit(cardBase, owner)
    self.base = cardBase
end
function card:GetDescription(cardBase, template)
    local baseDamage = cardBase:GetBaseFormattedValue("damage")
    local draw = cardBase:GetBaseFormattedValue("draw")
    return (template:gsub("{damage}", tostring(baseDamage)):gsub("{draw}", tostring(draw)))
end
function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if cardInfo.targets ~= nil and cardInfo.targets.Count > 0 then
        local target = cardInfo.targets[0]
        local baseDamage = self.base:GetBaseValue("damage")
        local dmgInfo = EventStructs.DamageInfo(caster, target, baseDamage, EventStructs.DamageFlag.Normal)
        target:TakeDamage(dmgInfo)
    end
    local draw = self.base:GetBaseValue("draw")
    if RunManager.instance ~= nil and RunManager.instance.playerBattleController ~= nil then
        RunManager.instance.playerBattleController.battleDeck:DrawCards(math.floor(draw))
    end
end
return card