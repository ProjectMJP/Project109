local effect = {}

local DEFAULT_CYCLE_DURATION = 3.0

local function resolveCycleDuration(duration)
    if duration == nil or duration <= 0 or duration >= 999 then
        return DEFAULT_CYCLE_DURATION
    end
    return duration
end

local function applyMoveBleedDamage(self, info)
    local target = self.base.target
    if target == nil or target.isDead then return end

    local stack = self.base.currentStack
    if stack <= 0 then return end

    local dist = math.abs(info.toUnscaled.x - info.fromUnscaled.x) + math.abs(info.toUnscaled.y - info.fromUnscaled.y)
    local steps = math.max(1, math.floor(dist))
    local dmg = steps * stack

    local dmgInfo = EventStructs.DamageInfo(self.base.caster, target, dmg, EventStructs.DamageFlag.HPLoss)
    target:TakeDamage(dmgInfo)
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

function effect:OnAfterMove(info)
    applyMoveBleedDamage(self, info)
end

function effect:OnAfterForcedMove(info)
    applyMoveBleedDamage(self, info)
end

function effect:OnTimeOut()
    self.base.currentStack = self.base.currentStack - 1
    if self.base.currentStack > 0 then
        local d = self.cycleDuration or DEFAULT_CYCLE_DURATION
        self.base.duration = d
        self.base.currentDuration = d
    end
end

function effect:GetDescription(effectBase, template)
    local stack = math.floor(effectBase.currentStack)
    return (template:gsub("{stacks}", tostring(stack)):gsub("{damage}", tostring(stack)))
end

return effect
