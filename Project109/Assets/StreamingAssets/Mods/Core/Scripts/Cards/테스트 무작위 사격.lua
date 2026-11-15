local card = {}
function card:OnInit(cardBase, owner)
    self.base = cardBase
end
function card:GetDescription(cardBase, template)
    local dmg = cardBase:GetFormattedValue("damage")
    return (template:gsub("{damage}", tostring(dmg)))
end
function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if cardInfo.targets ~= nil then
        local dmg = self.base:GetEffectiveValue("damage")
        for i = 0, cardInfo.targets.Count - 1 do
            local target = cardInfo.targets[i]
            if target ~= nil and not target.isDead then
                local dmgInfo = EventStructs.DamageInfo(caster, target, dmg, EventStructs.DamageFlag.Normal)
                target:TakeDamage(dmgInfo)
            end
        end
    end
end
return card
