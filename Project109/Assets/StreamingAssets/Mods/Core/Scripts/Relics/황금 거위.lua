local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

-- 효과: 다음 칸으로 이동할 때 마다 골드를 10 잃습니다. 상점에서 어떤 방식으로든 골드를 사용하게 되면 이 유물을 통해 잃은 골드의 140%만큼 골드를 획득합니다. (결제 및 맵 이동 시 직접 참조)

return relic
