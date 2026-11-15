local effect = {}

local DEFAULT_CYCLE_DURATION = 5.0

local function resolveCycleDuration(duration)
    if duration == nil or duration <= 0 or duration >= 999 then
        return DEFAULT_CYCLE_DURATION
    end
    return duration
end

function effect:OnInit(effectBase)
    self.base = effectBase
    self.cycleDuration = DEFAULT_CYCLE_DURATION
end

function effect:OnAdded(effectInstance, caster, target, stack, duration)
    local d = resolveCycleDuration(duration)
    self.cycleDuration = d
    self.base.duration = d
    self.base.currentDuration = d
end

function effect:OnStacked(effectInstance, stack, duration)
    local maxStack = self.base.Data ~= nil and self.base.Data.maxStack or 2147483647
    self.base.currentStack = math.min(self.base.currentStack + stack, maxStack)
    local d = resolveCycleDuration(duration)
    self.cycleDuration = d
    self.base.duration = d
    self.base.currentDuration = d
end

function effect:OnBeforeTakeDamage(damageInfo)
    -- 받는 피해 50% 증가
    damageInfo.damageMultiplier = damageInfo.damageMultiplier * 1.5
    return damageInfo
end

function effect:OnTimeOut()
    -- 시간 경과 시 1스택 차감
    self.base.currentStack = self.base.currentStack - 1
    if self.base.currentStack > 0 then
        local d = self.cycleDuration or DEFAULT_CYCLE_DURATION
        self.base.duration = d
        self.base.currentDuration = d
    end
end

function effect:GetDescription(effectBase, template)
    return template
end

return effect
