local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

-- 효과: 상점 칸에 진입할 경우 체력을 20 회복합니다. (상점 진입 시점 혹은 탐험 UI에서 직접 참조)

return relic
