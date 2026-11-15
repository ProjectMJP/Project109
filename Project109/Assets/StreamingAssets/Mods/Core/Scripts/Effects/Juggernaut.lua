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

    if cardInfo.caster == target and cardInfo.cardData ~= nil and cardInfo.cardData.cardType == "Attack" then
        -- 캐릭터의 시선 방향 계산
        local dir = Vector2Int(0, 1)
        if target.characterMove ~= nil then
            local look = target.characterMove.facingDirection
            -- LookDirection: Up(0), Down(1), Left(2), Right(3)
            local lookStr = tostring(look)
            if lookStr:find("Down") then
                dir = Vector2Int(0, -1)
            elseif lookStr:find("Left") then
                dir = Vector2Int(-1, 0)
            elseif lookStr:find("Right") then
                dir = Vector2Int(1, 0)
            else
                dir = Vector2Int(0, 1)
            end
        end

        -- 전방 1칸 강제 전진
        target:ForcedMoveInDirection(dir, 1)

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

