local card = {}
function card:OnInit(cardBase, owner)
    self.base = cardBase
end
function card:GetDescription(cardBase, template)
    local baseDamage = cardBase:GetFormattedValue("damage")
    local frenzyBonus = cardBase:GetFormattedValue("frenzyBonus")
    return (template:gsub("{damage}", tostring(baseDamage)):gsub("{frenzyBonus}", tostring(frenzyBonus)))
end
local function getEffectStack(character, effectName)
    if character == nil or character.effectManager == nil then return 0 end
    local effects = character.effectManager:GetEffects()
    if effects == nil then return 0 end
    for i = 0, effects.Count - 1 do
        local eff = effects[i]
        if eff.Data ~= nil and eff.Data.effectName == effectName then
            return eff.currentStack
        end
    end
    return 0
end
function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if cardInfo.targets ~= nil then
        local baseDamage = self.base:GetEffectiveValue("damage")
        local frenzyBonus = self.base:GetEffectiveValue("frenzyBonus")
        local frenzyStack = getEffectStack(caster, "Frenzy")
        local totalDamage = baseDamage + (frenzyStack * frenzyBonus)

        for i = 0, cardInfo.targets.Count - 1 do
            local target = cardInfo.targets[i]
            local dmgInfo = EventStructs.DamageInfo(caster, target, totalDamage, EventStructs.DamageFlag.Normal)
            target:TakeDamage(dmgInfo)
        end
    end
end
return card