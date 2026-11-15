local card = {}

function card:OnInit(cardBase, owner)
    self.base = cardBase
    if cardBase ~= nil then
        cardBase:AddTag("Preserve")
    end
end

function card:GetDescription(cardBase, template)
    local shield = cardBase:GetBaseFormattedValue("shield")
    return (template:gsub("{shield}", tostring(shield)))
end

function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if caster ~= nil then
        local shield = self.base:GetBaseValue("shield")
        caster:TakeShield(caster, shield, 1)
    end
end

return card

