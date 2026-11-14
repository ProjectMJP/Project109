local card = {}

function card:OnInit(cardBase, owner)
    self.base = cardBase
end

function card:GetDescription(cardBase, template)
    local bonusRegen = cardBase:GetBaseFormattedValue("bonusRegen")
    return (template:gsub("{bonusRegen}", tostring(bonusRegen)))
end

function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if caster ~= nil then
        -- 1스택, 지속 시간 10초 부여
        caster:TakeEffect(caster, "Adrenaline", 1, 10)
    end
end

return card

