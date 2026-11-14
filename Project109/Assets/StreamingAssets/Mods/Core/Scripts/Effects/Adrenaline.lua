local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

function effect:OnAdded(effectInstance, caster, target, stack, duration)
    if target ~= nil and target.curCharacterStat ~= nil then
        target.curCharacterStat.staminaRegenPerSecond = target.curCharacterStat.staminaRegenPerSecond + 20
    end
end

function effect:OnStacked(effectInstance, stack, duration)
    -- 지속 시간 갱신
    if self.base ~= nil then
        self.base.currentDuration = math.max(self.base.currentDuration, duration)
    end
end

function effect:OnRemoved()
    if self.base ~= nil and self.base.target ~= nil and self.base.target.curCharacterStat ~= nil then
        self.base.target.curCharacterStat.staminaRegenPerSecond = math.max(0, self.base.target.curCharacterStat.staminaRegenPerSecond - 20)
    end
end

function effect:GetDescription(effectBase, template)
    return template
end

return effect

