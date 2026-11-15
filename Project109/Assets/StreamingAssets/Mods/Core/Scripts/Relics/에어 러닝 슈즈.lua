local relic = {}

function relic:OnInit(relicBase, owner)
    self.base  = relicBase
    self.owner = owner
end

function relic:OnDrawCard(card)
    if card.cardData ~= nil and card.cardData.cardType:ToString() == "Move" then
        card.currentCost = math.floor(card.currentCost * 0.8)
    end
end

return relic
