local card = {}
function card:OnInit(cardBase, owner)
    self.base = cardBase
end
function card:GetDescription(cardBase, template)
    local baseDamage = cardBase:GetBaseFormattedValue("damage")
    local vulnerable = cardBase:GetBaseFormattedValue("vulnerable")
    local weak = cardBase:GetBaseFormattedValue("weak")
    return (template:gsub("{damage}", tostring(baseDamage))
                    :gsub("{vulnerable}", tostring(vulnerable))
                    :gsub("{weak}", tostring(weak)))
end
function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if cardInfo.targets ~= nil then
        local baseDamage = self.base:GetBaseValue("damage")
        local vulnerable = self.base:GetBaseValue("vulnerable")
        local weak = self.base:GetBaseValue("weak")
        for i = 0, cardInfo.targets.Count - 1 do
            local target = cardInfo.targets[i]
            target:TakeDamage(caster, baseDamage)
            target:TakeEffect(caster, "Vulnerable", vulnerable, 0)
            target:TakeEffect(caster, "Weakness", weak, 0)
        end
    end
end
return card