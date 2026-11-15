local card = {}
function card:OnInit(cardBase, owner)
    self.base = cardBase
    self.cardsPlayedThisTurn = 0
end
function card:OnTurnStart()
    self.cardsPlayedThisTurn = 0
end
function card:OnAfterUseCard(cardInfo)
    if cardInfo.caster == self.base.owner then
        self.cardsPlayedThisTurn = (self.cardsPlayedThisTurn or 0) + 1
    end
end
function card:GetDescription(cardBase, template)
    local baseDamage = cardBase:GetFormattedValue("damage")
    local extraHits = cardBase:GetFormattedValue("extraHits")
    return (template:gsub("{damage}", tostring(baseDamage)):gsub("{extraHits}", tostring(extraHits)))
end
function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if cardInfo.targets ~= nil then
        local baseDamage = self.base:GetEffectiveValue("damage")
        local extraHits = self.base:GetEffectiveValue("extraHits")
        local readyDamage = self.base:GetEffectiveValue("readyDamage")
        local comboLevel = self.base:GetMasteryLevel("Initiative_ComboAttack")

        -- 선봉대: 턴마다 처음 사용 시
        local isFirst = (self.cardsPlayedThisTurn or 0) <= 0
        local finalDamage = baseDamage
        local finalHits = 1

        if isFirst then
            finalHits = finalHits + extraHits
            finalDamage = finalDamage + readyDamage
        end

        if comboLevel > 0 then
            finalDamage = finalDamage * 0.6
            finalHits = finalHits * 2
        end

        for i = 0, cardInfo.targets.Count - 1 do
            local target = cardInfo.targets[i]
            for h = 1, finalHits do
                local dmgInfo = EventStructs.DamageInfo(caster, target, finalDamage, EventStructs.DamageFlag.Normal)
                target:TakeDamage(dmgInfo)
            end
        end
    end
end
return card