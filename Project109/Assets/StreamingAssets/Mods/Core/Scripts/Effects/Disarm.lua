local effect = {}

function effect:OnInit(effectBase)
    self.base = effectBase
end

function effect:OnTryUseCard(cardInfo)
    if cardInfo == nil then return end
    if cardInfo.card ~= nil and cardInfo.card.cardData ~= nil and cardInfo.card.cardData.cardType == "Attack" then
        cardInfo:AddFlag(EventStructs.CardFlag.Cancel)
    end
end

function effect:OnTurnEnd()
    local target = self.base.target
    if target == nil or target.effectManager == nil then return end

    self.base.currentStack = self.base.currentStack - 1
    if self.base.currentStack <= 0 then
        target.effectManager:RemoveEffect(self.base)
    else
        target.effectManager:NotifyEffectUpdated(self.base)
    end
end

function effect:GetDescription(effectBase, template)
    local stack = math.floor(effectBase.currentStack)
    return (template:gsub("{stacks}", tostring(stack)))
end

return effect
