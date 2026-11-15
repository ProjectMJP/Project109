-- special_test_choice.lua
-- 테스트용 개별 커스텀 선택지 스크립트 (Custom Consequence Script)

function CanSelect(dm)
    print("[Lua special_test_choice] CanSelect 조건 검사 시작")
    if dm:GetCurrentHealth() > 10 then
        print("[Lua special_test_choice] 조건 충족 (체력 > 10)")
        return true
    end
    print("[Lua special_test_choice] 조건 미달 (체력 <= 10)")
    return false
end

function ExecuteChoice(dm)
    print("--------------------------------------------------")
    print("[Lua special_test_choice] 커스텀 선택지 처리 시작!")
    
    local hpLoss = math.floor(dm:GetMaxHealth() * 0.5)
    dm:DamagePlayer(hpLoss)
    print(string.format("커스텀 대가 -> 체력 %d 소실 (현재 체력: %d)", hpLoss, dm:GetCurrentHealth()))

    dm:AddGold(200)
    print("커스텀 보상 -> 200 골드 지급")

    dm:SpawnReward("relic", "rare")
    print("커스텀 보상 -> 희귀 유물 보상 스폰")

    print("[Lua special_test_choice] 커스텀 정산 완료, C# 스테이지 제어 대기")
    print("--------------------------------------------------")
end
