local effect = {}

local DEFAULT_TICK_DURATION = 1.0

local function resolveCycleDuration(duration)
    if duration == nil or duration <= 0 or duration >= 999 then
        return DEFAULT_TICK_DURATION
    end
    return duration
end

function effect:OnInit(effectBase)
    self.base = effectBase
end

function effect:OnAdded(effectInstance, caster, target, stack, duration)
    local d = resolveCycleDuration(duration)
    self.base.duration = d
    self.base.currentDuration = d
end

function effect:OnStacked(effectInstance, stack, duration)
    local maxStack = self.base.Data ~= nil and self.base.Data.maxStack or 2147483647
    self.base.currentStack = math.min(self.base.currentStack + stack, maxStack)
    local d = resolveCycleDuration(duration)
    self.base.duration = d
    self.base.currentDuration = d
end

function effect:OnTick()
    local target = self.base.target
    if target == nil or target.isDead then return end

    local dmg = self.base.currentStack
    if dmg > 0 then
        local dmgInfo = EventStructs.DamageInfo(self.base.caster, target, dmg, EventStructs.DamageFlag.HPLoss)
        target:TakeDamage(dmgInfo)
    end
end

function effect:OnTimeOut()
    self.base.currentStack = self.base.currentStack - 1
    if self.base.currentStack > 0 then
        self.base.duration = DEFAULT_TICK_DURATION
        self.base.currentDuration = DEFAULT_TICK_DURATION
    end
end

function effect:GetDescription(effectBase, template)
    local stack = math.floor(effectBase.currentStack)
    return (template:gsub("{damage}", tostring(stack)):gsub("{stacks}", tostring(stack)))
end

return effect
