local card = {}
function card:OnInit(cardBase, owner)
    self.base = cardBase
end
function card:GetDescription(cardBase, template)
    local baseDamage = cardBase:GetBaseFormattedValue("damage")
    return (template:gsub("{damage}", tostring(baseDamage)))
end
function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if cardInfo.targets ~= nil and cardInfo.targets.Count > 0 then
        local target = cardInfo.targets[0]
        local baseDamage = self.base:GetBaseValue("damage")
        local dmgInfo = EventStructs.DamageInfo(caster, target, baseDamage, EventStructs.DamageFlag.Normal)
        target:TakeDamage(dmgInfo)
    end
end
return card