local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

local DEFAULT_DURATION = 10.0
local MODIFIER_SOURCE_ID = "Effect_Adrenaline"

function effect:OnAdded(effectInstance, caster, target, stack, duration)
    local d = (duration ~= nil and duration > 0 and duration < 999) and duration or DEFAULT_DURATION
    if self.base ~= nil then
        self.base.duration = d
        self.base.currentDuration = d
    end
    if target ~= nil then
        target:AddOrRefreshStatModifier(
            MODIFIER_SOURCE_ID,
            EventStructs.StatModifierTarget.StaminaRegen,
            EventStructs.StatModifierOp.Flat,
            20,
            EventStructs.StatModifierLifetime.DurationSeconds,
            d,
            true
        )
    end
end

function effect:OnStacked(effectInstance, stack, duration)
    -- 지속 시간 갱신
    if self.base ~= nil then
        local d = (duration ~= nil and duration > 0 and duration < 999) and duration or DEFAULT_DURATION
        local newDur = math.max(self.base.currentDuration, d)
        self.base.duration = math.max(self.base.duration, newDur)
        self.base.currentDuration = newDur
        if self.base.target ~= nil then
            self.base.target:AddOrRefreshStatModifier(
                MODIFIER_SOURCE_ID,
                EventStructs.StatModifierTarget.StaminaRegen,
                EventStructs.StatModifierOp.Flat,
                20,
                EventStructs.StatModifierLifetime.DurationSeconds,
                newDur,
                true
            )
        end
    end
end

function effect:OnTimeOut()
    if self.base == nil then return end
    self.base.currentStack = self.base.currentStack - 1
    if self.base.currentStack > 0 then
        local d = self.base.duration > 0 and self.base.duration or DEFAULT_DURATION
        self.base.duration = d
        self.base.currentDuration = d
        if self.base.target ~= nil then
            self.base.target:AddOrRefreshStatModifier(
                MODIFIER_SOURCE_ID,
                EventStructs.StatModifierTarget.StaminaRegen,
                EventStructs.StatModifierOp.Flat,
                20,
                EventStructs.StatModifierLifetime.DurationSeconds,
                d,
                true
            )
        end
    end
end

function effect:OnRemoved()
    if self.base ~= nil and self.base.target ~= nil then
        self.base.target:RemoveStatModifier(MODIFIER_SOURCE_ID)
    end
end

function effect:GetDescription(effectBase, template)
    return template
end

return effect
