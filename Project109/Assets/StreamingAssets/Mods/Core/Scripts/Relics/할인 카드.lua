local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

-- 효과: 상인의 물건 가격이 30% 감소합니다. (상점 코드 단에서 유물 유무를 직접 참조)

return relic
