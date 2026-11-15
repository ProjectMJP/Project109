local relic = {}

-- 유물 초기화 (획득 시 1회 호출)
function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

-- 전투 시작 시 호출 (IOnBattleStart 대응)
-- owner.character가 전투에 참여하는 캐릭터이므로 자기 자신에게 스태미나를 부여합니다.
function relic:OnBattleStart()
    local character = self.owner.character
    if character == nil then return end

    -- StaminaInfo(caster, target, baseAmount)
    -- 자기 자신이 출처이므로 caster = target = character
    local info = EventStructs.StaminaInfo(character, character, 40)
    character:TakeStamina(info)
end

return relic