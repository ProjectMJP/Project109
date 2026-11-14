local enemy = {}

function enemy:OnInit(character, controller)
    self.character = character
    self.controller = controller
end

-- NPCUnitController의 AI 의도 결정 함수
function enemy:ChooseCard(character)
    -- 플레이어에게 시전할 카드로 "몬스터 강타" 선택
    return "몬스터 강타"
end

function enemy:Evaluate(character)
    return {
        cardId = "몬스터 강타"
    }
end

return enemy
