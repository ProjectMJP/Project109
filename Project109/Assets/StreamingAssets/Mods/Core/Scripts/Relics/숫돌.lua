local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

-- 효과: 쉼터에 입장 시 무작위 기억 1개가 강화됩니다. (쉼터 기능 처리 시 직접 참조)

return relic
