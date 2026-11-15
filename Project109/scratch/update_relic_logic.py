import os

output_lua_dir = r"e:\unity\Project109\Project109\Assets\StreamingAssets\Mods\Core\Scripts\Relics"
os.makedirs(output_lua_dir, exist_ok=True)

relic_codes = {
    "재생성 철판": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnTurnStart()
    local character = self.owner.character
    if character == nil then return end
    character:TakeShield(character, 4, 1)
end

return relic
""",

    "낡은 검": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnBeforeDealDamage(info)
    if info.caster ~= self.owner.character then return end
    local cTile = info.caster.characterMove:GetCurrentTile()
    local tTile = info.target.characterMove:GetCurrentTile()
    if cTile ~= nil and tTile ~= nil then
        local cCoord = cTile:GetCoord()
        local tCoord = tTile:GetCoord()
        local dx = math.abs(cCoord.x - tCoord.x)
        local dy = math.abs(cCoord.y - tCoord.y)
        if dx + dy <= 1 then
            info.baseDamageAmount = info.baseDamageAmount + 2
        end
    end
end

return relic
""",

    "낡은 갑옷": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnBattleStart()
    local character = self.owner.character
    if character == nil then return end
    character:TakeShield(character, 1, 999)
end

return relic
""",

    "체력 회복 물약": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    self.base.Counter = 1
end

function relic:OnBattleStart()
    self.base.Counter = 1
end

function relic:OnAfterTakeDamage(info)
    if info.target ~= self.owner.character then return end
    local character = self.owner.character
    if character == nil or self.base.Counter <= 0 then return end
    if character.curHealthRate < 0.20 then
        self.base.Counter = 0
        local healAmount = math.floor(character.curCharacterStat.maxHealth * 0.3)
        character:TakeHeal(character, healAmount)
    end
end

return relic
""",

    "할인 카드": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

-- 효과: 상인의 물건 가격이 30% 감소합니다. (상점 코드 단에서 유물 유무를 직접 참조)

return relic
""",

    "식권": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

-- 효과: 상점 칸에 진입할 경우 체력을 20 회복합니다. (상점 진입 시점 혹은 탐험 UI에서 직접 참조)

return relic
""",

    "가시덩굴": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnBattleStart()
    local character = self.owner.character
    if character == nil then return end
    character:TakeEffect(character, "Thorns", 3, 999)
end

return relic
""",

    "가죽 장갑": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnBattleStart()
    local character = self.owner.character
    if character == nil then return end
    character:TakeEffect(character, "Strength", 1, 999)
end

return relic
""",

    "빨간 해골": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    self.base.Counter = 1
end

function relic:OnBattleStart()
    self.base.Counter = 1
    self:CheckSkull()
end

function relic:OnAfterTakeDamage(info)
    if info.target ~= self.owner.character then return end
    self:CheckSkull()
end

function relic:CheckSkull()
    local character = self.owner.character
    if character == nil or self.base.Counter <= 0 then return end
    if character.curHealthRate < 0.40 then
        self.base.Counter = 0
        character:TakeEffect(character, "Strength", 3, 999)
    end
end

return relic
""",

    "방울뱀": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

-- 효과: 적에게 입히는 독, 맹독의 데미지가 2 증가합니다. (독/맹독 이펙트 효과 부여 및 데미지 계산식에서 직접 참조)

return relic
""",

    "권투 글러브": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    self.base.Counter = 1
end

function relic:OnBattleStart()
    self.base.Counter = 1
end

function relic:OnBeforeDealDamage(info)
    if info.caster ~= self.owner.character then return end
    if self.base.Counter > 0 then
        self.base.Counter = 0
        info.baseDamageAmount = info.baseDamageAmount + 10
    end
end

return relic
""",

    "가시 밑창": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnAfterMove(info)
    if info.mover ~= self.owner.character then return end
    if info.moveFlags:HasFlag(EventStructs.MoveFlag.Forced) then return end
    
    local character = self.owner.character
    local cTile = character.characterMove:GetCurrentTile()
    if cTile == nil then return end
    local cCoord = cTile:GetCoord()
    
    local all = RunManager.instance.battleManager:GetAllCombatants()
    if all == nil then return end
    
    for i = 0, all.Count - 1 do
        local target = all[i].controlledCharacter
        if target ~= nil and character:IsHostileTo(target) then
            local tTile = target.characterMove:GetCurrentTile()
            if tTile ~= nil then
                local tCoord = tTile:GetCoord()
                local dx = math.abs(cCoord.x - tCoord.x)
                local dy = math.abs(cCoord.y - tCoord.y)
                if dx + dy <= 1 then
                    local dmgInfo = EventStructs.DamageInfo(character, target, 3, EventStructs.DamageFlag.Normal)
                    target:TakeDamage(dmgInfo)
                end
            end
        end
    end
end

return relic
""",

    "에어 러닝 슈즈": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnDrawCard(card)
    if card.cardData ~= nil and card.cardData.cardType:ToString() == "Move" then
        card.currentCost = math.floor(card.currentCost * 0.8)
    end
end

return relic
""",

    "수상한 약물": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    self.base.Counter = 0
end

function relic:OnBattleStart()
    local character = self.owner.character
    if character == nil then return end
    character.curCharacterStat.maxStamina = character.curCharacterStat.maxStamina + 20
    character.curStamina = character.curStamina + 20
    self.base.Counter = 1
end

function relic:OnTurnEnd()
    local character = self.owner.character
    if character == nil then return end
    if self.base.Counter > 0 then
        self.base.Counter = 0
        character.curCharacterStat.maxStamina = character.curCharacterStat.maxStamina - 20
        character.curStamina = math.max(0, character.curStamina - 20)
    end
end

return relic
""",

    "황금 거위": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

-- 효과: 다음 칸으로 이동할 때 마다 골드를 10 잃습니다. 상점에서 어떤 방식으로든 골드를 사용하게 되면 이 유물을 통해 잃은 골드의 140%만큼 골드를 획득합니다. (결제 및 맵 이동 시 직접 참조)

return relic
""",

    "잘 구워진 스테이크": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    local character = owner.character
    if character ~= nil and character.curCharacterStat ~= nil then
        character.curCharacterStat.maxHealth = character.curCharacterStat.maxHealth + 20
        character.curHealth = character.curHealth + 20
    end
end

function relic:OnRemoved(relicBase, owner)
    local character = owner.character
    if character ~= nil and character.curCharacterStat ~= nil then
        character.curCharacterStat.maxHealth = character.curCharacterStat.maxHealth - 20
        character.curHealth = math.min(character.curHealth, character.curCharacterStat.maxHealth)
    end
end

return relic
""",

    "날카로운 눈동자": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnBattleStart()
    local incountType = RunManager.instance.currentIncountNode.incountType:ToString()
    if incountType == "Elite" or incountType == "Boss" then
        local all = RunManager.instance.battleManager:GetAllCombatants()
        if all == nil then return end
        local character = self.owner.character
        for i = 0, all.Count - 1 do
            local target = all[i].controlledCharacter
            if target ~= nil and character:IsHostileTo(target) then
                local hpToReduce = math.floor(target.curHealth * 0.15)
                if hpToReduce > 0 then
                    target:TakeDamage(character, hpToReduce, EventStructs.DamageFlag.HPLoss)
                end
            end
        end
    end
end

return relic
""",

    "숫돌": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

-- 효과: 쉼터에 입장 시 무작위 기억 1개가 강화됩니다. (쉼터 기능 처리 시 직접 참조)

return relic
""",

    "금덩이": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    owner.playerStat.InGameCurrencyGold = owner.playerStat.InGameCurrencyGold + 400
end

return relic
""",

    "초록색 꽃": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnBeforeTakeHeal(info)
    if info.target ~= self.owner.character then return end
    info.baseHealAmount = info.baseHealAmount * 1.3
end

return relic
""",

    "비타민": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    owner.playerStat.RewardCardCount = owner.playerStat.RewardCardCount + 1
end

function relic:OnRemoved(relicBase, owner)
    owner.playerStat.RewardCardCount = owner.playerStat.RewardCardCount - 1
end

return relic
""",

    "날카로운 날": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnAfterDealDamage(info)
    if info.caster ~= self.owner.character then return end
    if info.hpDamageAmount > 0 then
        info.target:TakeEffect(info.caster, "Bleed", 1, 999)
    end
end

return relic
""",

    "악마의 꼬리": """local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnTurnStart()
    local character = self.owner.character
    if character == nil then return end
    
    local cTile = character.characterMove:GetCurrentTile()
    if cTile == nil then return end
    local cCoord = cTile:GetCoord()
    
    local all = RunManager.instance.battleManager:GetAllCombatants()
    if all == nil then return end
    
    for i = 0, all.Count - 1 do
        local target = all[i].controlledCharacter
        if target ~= nil and character:IsHostileTo(target) then
            local tTile = target.characterMove:GetCurrentTile()
            if tTile ~= nil then
                local tCoord = tTile:GetCoord()
                local dx = math.abs(cCoord.x - tCoord.x)
                local dy = math.abs(cCoord.y - tCoord.y)
                if dx + dy <= 2 then
                    target:TakeEffect(character, "Weakness", 1, 999)
                end
            end
        end
    end
end

return relic
"""
}

# 각 파일 쓰기
for name, code in relic_codes.items():
    file_path = os.path.join(output_lua_dir, f"{name}.lua")
    with open(file_path, "w", encoding="utf-8") as f:
        f.write(code)
    print(f"Updated relic script: {name}.lua")

print(f"Successfully updated all {len(relic_codes)} relic Lua files.")
