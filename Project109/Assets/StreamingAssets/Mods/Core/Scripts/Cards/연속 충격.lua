local card = {}

function card:OnInit(cardBase, owner)
    self.base = cardBase
    if cardBase ~= nil then
        cardBase:AddTag("Echo")
    end
end

function card:GetDescription(cardBase, template)
    local baseDamage = cardBase:GetBaseFormattedValue("damage")
    local collisionDmg = math.floor(baseDamage * 0.5)
    return (template:gsub("{damage}", tostring(baseDamage)):gsub("{collisionDamage}", tostring(collisionDmg)))
end

function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if cardInfo.targets ~= nil and cardInfo.targets.Count > 0 then
        local target = cardInfo.targets[0]
        local baseDamage = self.base:GetBaseValue("damage")

        -- 1. 기본 타격
        local dmgInfo = EventStructs.DamageInfo(caster, target, baseDamage, EventStructs.DamageFlag.Normal)
        target:TakeDamage(dmgInfo)

        -- 2. 넉백 및 충돌 판정
        if not target.isDead then
            local isCollided = target:KnockbackFrom(caster, 1)
            if isCollided then
                local colDmg = math.floor(baseDamage * 0.5)
                local colInfo = EventStructs.DamageInfo(caster, target, colDmg, EventStructs.DamageFlag.Normal)
                target:TakeDamage(colInfo)
            end
        end
    end
end

return card

