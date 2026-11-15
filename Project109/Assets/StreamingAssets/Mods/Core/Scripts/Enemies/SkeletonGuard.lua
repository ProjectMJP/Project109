local enemy = {}

function enemy:OnInit(character, controller)
    self.character = character
    self.controller = controller
end

-- NPCUnitController의 AI 의도 결정 함수 (공격과 방어 교대)
function enemy:ChooseCard(character)
    if character ~= nil and (character.currentTurn % 2 == 1) then
        return "방어"
    end
    return "몬스터 강타"
end

function enemy:Evaluate(character)
    return {
        cardId = self:ChooseCard(character)
    }
end

return enemy
