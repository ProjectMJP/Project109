local enemy = {}

function enemy:OnInit(character, controller)
    self.character = character
    self.controller = controller
end

-- NPCUnitController의 AI 의도 결정 함수
function enemy:ChooseCard(character)
    return "몬스터 강타"
end

function enemy:Evaluate(character)
    return {
        cardId = self:ChooseCard(character)
    }
end

return enemy
