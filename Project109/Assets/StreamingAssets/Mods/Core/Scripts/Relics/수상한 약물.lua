local relic = {}

local MODIFIER_SOURCE_ID = "Relic_SuspiciousDrug"

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
    self.base.Counter = 0
end

function relic:OnBattleStart()
    local character = self.owner.character
    if character == nil then return end
    character:AddOrRefreshStatModifier(
        MODIFIER_SOURCE_ID,
        EventStructs.StatModifierTarget.MaxStamina,
        EventStructs.StatModifierOp.Flat,
        20,
        EventStructs.StatModifierLifetime.UntilTurnEnd,
        0,
        true
    )
    character.curStamina = character.curStamina + 20
    self.base.Counter = 1
end

function relic:OnCalculateTurnEndStamina(info)
    if self.base ~= nil and self.base.Counter > 0 then
        info.preResetBonusStamina = info.preResetBonusStamina - 20
    end
    return info
end

function relic:OnTurnEnd()
    if self.base ~= nil and self.base.Counter > 0 then
        self.base.Counter = 0
    end
end

return relic
