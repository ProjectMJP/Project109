local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

local DEFAULT_DURATION = 10.0

function effect:OnAdded(effectInstance, caster, target, stack, duration)
    if target ~= nil and target.curCharacterStat ~= nil then
        target.curCharacterStat.staminaRegenPerSecond = target.curCharacterStat.staminaRegenPerSecond + 20
    end
    if self.base ~= nil and (duration == nil or duration <= 0 or duration >= 999) then
        self.base.duration = DEFAULT_DURATION
        self.base.currentDuration = DEFAULT_DURATION
    end
end

function effect:OnStacked(effectInstance, stack, duration)
    -- 지속 시간 갱신
    if self.base ~= nil then
        local d = (duration ~= nil and duration > 0 and duration < 999) and duration or DEFAULT_DURATION
        local newDur = math.max(self.base.currentDuration, d)
        self.base.duration = math.max(self.base.duration, newDur)
        self.base.currentDuration = newDur
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

