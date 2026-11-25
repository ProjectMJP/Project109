local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
    self.origMoveRange = 3
end

function effect:OnAdded(effectInstance, caster, target, stack, duration)
    if target ~= nil and target.curCharacterStat ~= nil then
        self.origMoveRange = target.curCharacterStat.moveRange
        target.curCharacterStat.moveRange = 0
    end
end

function effect:OnAfterUseCard(cardInfo)
    local target = self.base.target
    if target == nil or target.isDead then return end

    if cardInfo.caster == target and cardInfo.card ~= nil and cardInfo.card.cardData ~= nil and cardInfo.card.cardData.cardType == "Attack" then
        -- 캐릭터의 6방향 시선(LookDirection) 조회
        local look = LookDirection.East
        if target.characterMove ~= nil then
            look = target.characterMove.facingDirection
        end

        -- 전방 1칸 강제 전진
        target:ForcedMoveInDirection(look, 1)

        -- 타깃이 존재하면 타깃도 같은 방향으로 1칸 밀쳐냄
        if cardInfo.targets ~= nil and cardInfo.targets.Count > 0 then
            local enemy = cardInfo.targets[0]
            if enemy ~= nil and not enemy.isDead then
                enemy:KnockbackFrom(target, 1)
            end
        end
    end
end

function effect:OnRemoved()
    if self.base ~= nil and self.base.target ~= nil and self.base.target.curCharacterStat ~= nil then
        self.base.target.curCharacterStat.moveRange = self.origMoveRange or 3
    end
end

function effect:OnTurnEnd()
    if self.base ~= nil and self.base.target ~= nil and self.base.target.effectManager ~= nil then
        self.base.target.effectManager:RemoveEffect(self.base)
    end
end

function effect:GetDescription(effectBase, template)
    return template
end

return effect

