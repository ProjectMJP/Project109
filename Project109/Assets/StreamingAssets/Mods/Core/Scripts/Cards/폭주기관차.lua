local card = {}

function card:OnInit(cardBase, owner)
    self.base = cardBase
end

function card:GetDescription(cardBase, template)
    return template
end

function card:Execute(cardInfo)
    local caster = cardInfo.caster
    if caster ~= nil then
        -- 턴 종료 시까지 유지되는 폭주기관차 버프 부여
        caster:TakeEffect(caster, "Juggernaut", 1, 0)
    end
end

return card

