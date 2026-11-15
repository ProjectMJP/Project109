local card = {}
function card:OnInit(cardBase, owner)
    self.base = cardBase
end
function card:GetDescription(cardBase, template)
    local baseDamage = cardBase:GetBaseFormattedValue("damage")
    local hits = cardBase:GetBaseFormattedValue("hits")
    return (template:gsub("{damage}", tostring(baseDamage)):gsub("{hits}", tostring(hits)))
end
function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if cardInfo.targets ~= nil and cardInfo.targets.Count > 0 then
        local target = cardInfo.targets[0]
        local baseDamage = self.base:GetBaseValue("damage")
        local hits = self.base:GetBaseValue("hits")
        for i = 1, hits do
            local dmgInfo = EventStructs.DamageInfo(caster, target, baseDamage, EventStructs.DamageFlag.Normal)
            target:TakeDamage(dmgInfo)
        end
    end
end
return card