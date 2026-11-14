local card = {}

function card:OnInit(cardBase, owner)
    self.base = cardBase
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

        -- 1. 시전자가 적 방향으로 돌진하며, 반쯤 이동했을 때 타격 및 밀치기 발동
        caster:DashTo(target, function()
            if caster == nil or caster.isDead or target == nil or target.isDead then
                return
            end

            -- 2. 기본 타격
            local dmgInfo = EventStructs.DamageInfo(caster, target, baseDamage, EventStructs.DamageFlag.Normal)
            target:TakeDamage(dmgInfo)

            -- 3. 적을 돌진 방향으로 1칸 밀쳐냄
            if not target.isDead then
                local isCollided = target:KnockbackFrom(caster, 1)
                if isCollided then
                    local colDmg = math.floor(baseDamage * 0.5)
                    local colInfo = EventStructs.DamageInfo(caster, target, colDmg, EventStructs.DamageFlag.Normal)
                    target:TakeDamage(colInfo)
                end
            end
        end)
    end
end

return card

