local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

-- 효과: 적에게 입히는 독, 맹독의 데미지가 2 증가합니다. (독/맹독 이펙트 효과 부여 및 데미지 계산식에서 직접 참조)

return relic
