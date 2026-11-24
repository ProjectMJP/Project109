local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnDrawCard(card)
    if card ~= nil and card.cardData ~= nil and card.cardData.cardType == "Move" then
        card.currentCost = math.floor(card.currentCost * 0.8)
    end
end

return relic
