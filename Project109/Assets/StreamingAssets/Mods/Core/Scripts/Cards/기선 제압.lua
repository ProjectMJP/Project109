local card = {}

function card:OnInit(cardBase, owner)
    self.base = cardBase
    self.cardsPlayedThisTurn = 0
    if cardBase ~= nil then
        cardBase:AddTag("Vanguard")
    end
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
    local baseDamage = cardBase:GetBaseFormattedValue("damage")
    local vanguardBonus = cardBase:GetBaseFormattedValue("vanguardBonus")
    local collisionDmg = math.floor((baseDamage + vanguardBonus) * 0.5)
    return (template:gsub("{damage}", tostring(baseDamage))
                    :gsub("{vanguardBonus}", tostring(vanguardBonus))
                    :gsub("{collisionDamage}", tostring(collisionDmg)))
end

function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if cardInfo.targets ~= nil and cardInfo.targets.Count > 0 then
        local target = cardInfo.targets[0]
        local baseDamage = self.base:GetBaseValue("damage")
        local vanguardBonus = self.base:GetBaseValue("vanguardBonus")

        -- 선봉대: 이번 턴에 사용된 카드가 없을 때 발동
        local isFirst = (self.cardsPlayedThisTurn or 0) <= 0
        local finalDamage = baseDamage
        if isFirst then
            finalDamage = finalDamage + vanguardBonus
        end

        -- 1. 타격
        local dmgInfo = EventStructs.DamageInfo(caster, target, finalDamage, EventStructs.DamageFlag.Normal)
        target:TakeDamage(dmgInfo)

        -- 2. 선봉대 발동 시 넉백 + 충돌 피해
        if isFirst and not target.isDead then
            local isCollided = target:KnockbackFrom(caster, 1)
            if isCollided then
                local colDmg = math.floor(finalDamage * 0.5)
                local colInfo = EventStructs.DamageInfo(caster, target, colDmg, EventStructs.DamageFlag.Normal)
                target:TakeDamage(colInfo)
            end
        end
    end
end

return card

