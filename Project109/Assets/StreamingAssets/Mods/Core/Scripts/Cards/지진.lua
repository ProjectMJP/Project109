local card = {}
function card:OnInit(cardBase, owner)
    self.base = cardBase
end
function card:GetDescription(cardBase, template)
    local baseDamage = cardBase:GetFormattedValue("damage")
    local staminaLoss = cardBase:GetFormattedValue("staminaLoss")
    return (template:gsub("{damage}", tostring(baseDamage)):gsub("{staminaLoss}", tostring(staminaLoss)))
end
function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if cardInfo.targets ~= nil then
        local baseDamage = self.base:GetEffectiveValue("damage")
        local staminaLoss = self.base:GetEffectiveValue("staminaLoss")

        for i = 0, cardInfo.targets.Count - 1 do
            local target = cardInfo.targets[i]
            local dmgInfo = EventStructs.DamageInfo(caster, target, baseDamage, EventStructs.DamageFlag.Normal)
            target:TakeDamage(dmgInfo)
            
            -- ?�태미너 감소 ?�용
            local stamInfo = EventStructs.StaminaInfo(caster, target, staminaLoss, EventStructs.StaminaFlag.Normal)
            target:SpendStamina(stamInfo)
        end
    end
end
return card