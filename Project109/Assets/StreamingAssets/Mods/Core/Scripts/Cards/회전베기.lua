local card = {}
function card:OnInit(cardBase, owner)
    self.base = cardBase
    self.spinningSlashCount = 0
end
function card:OnTurnStart()
    self.spinningSlashCount = 0
end
function card:OnAfterUseCard(cardInfo)
    if cardInfo.caster == self.base.owner and cardInfo.card ~= nil and cardInfo.card.cardData ~= nil and cardInfo.card.cardData.cardName == "회전베기" then
        self.spinningSlashCount = (self.spinningSlashCount or 0) + 1
    end
end
function card:GetDescription(cardBase, template)
    local baseDamage = cardBase:GetFormattedValue("damage")
    return (template:gsub("{damage}", tostring(baseDamage)))
end
function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if cardInfo.targets ~= nil then
        local baseDamage = self.base:GetEffectiveValue("damage")
        local centrifugalBonus = self.base:GetEffectiveValue("centrifugalBonus")
        local count = self.spinningSlashCount or 0
        local totalDamage = baseDamage + (count * centrifugalBonus)

        for i = 0, cardInfo.targets.Count - 1 do
            local target = cardInfo.targets[i]
            local dmgInfo = EventStructs.DamageInfo(caster, target, totalDamage, EventStructs.DamageFlag.Normal)
            target:TakeDamage(dmgInfo)
        end
    end
end
return card