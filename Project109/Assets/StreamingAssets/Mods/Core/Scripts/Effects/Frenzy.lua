local effect = {}

local function ApplyCostReduction(self, card)
    if card ~= nil and card.cardData ~= nil and card.cardData.cardType == "Attack" then
        if self.reducedCards == nil then
            self.reducedCards = {}
        end
        if not self.reducedCards[card.runtimeID] then
            card.currentCost = math.max(0, card.currentCost - 5)
            self.reducedCards[card.runtimeID] = true
        end
    end
end

local function RestoreCardCost(self, card)
    if card ~= nil and card.cardData ~= nil and self.reducedCards ~= nil and self.reducedCards[card.runtimeID] then
        local originalCost = card.cardData.stamina + card:GetEffectiveValue("cost")
        card.currentCost = math.max(0, math.floor(originalCost))
        self.reducedCards[card.runtimeID] = nil
    end
end

local function GetPlayerHand()
    if RunManager.instance ~= nil and RunManager.instance.playerBattleController ~= nil then
        local deck = RunManager.instance.playerBattleController.battleDeck
        if deck ~= nil then
            return deck.hand
        end
    end
    return nil
end

local function RestoreAllCosts(self)
    if RunManager.instance ~= nil and RunManager.instance.playerBattleController ~= nil then
        local deck = RunManager.instance.playerBattleController.battleDeck
        if deck ~= nil then
            if deck.hand ~= nil then
                for i = 0, deck.hand.Count - 1 do
                    RestoreCardCost(self, deck.hand[i])
                end
            end
            if deck.drawPile ~= nil then
                for i = 0, deck.drawPile.Count - 1 do
                    RestoreCardCost(self, deck.drawPile[i])
                end
            end
            if deck.discardPile ~= nil then
                for i = 0, deck.discardPile.Count - 1 do
                    RestoreCardCost(self, deck.discardPile[i])
                end
            end
        end
    end
    self.reducedCards = {}
end

function effect:OnInit(effectBase)
    self.base = effectBase
    self.reducedCards = {}
end

function effect:OnAdded(effectInstance, caster, target, stack, duration)
    local hand = GetPlayerHand()
    if hand ~= nil then
        for i = 0, hand.Count - 1 do
            ApplyCostReduction(self, hand[i])
        end
    end
end

function effect:OnStacked(effectInstance, stack, duration)
    local maxStack = self.base.Data ~= nil and self.base.Data.maxStack or 2147483647
    self.base.currentStack = math.min(self.base.currentStack + stack, maxStack)
    
    local hand = GetPlayerHand()
    if hand ~= nil then
        for i = 0, hand.Count - 1 do
            ApplyCostReduction(self, hand[i])
        end
    end
end

function effect:OnDrawCard(card)
    ApplyCostReduction(self, card)
end

function effect:OnAfterUseCard(cardInfo)
    local card = cardInfo.cardData
    if card ~= nil and card.cardData ~= nil and card.cardData.cardType == "Attack" then
        self.base.currentStack = self.base.currentStack - 1
        if self.base.currentStack <= 0 then
            self.base.target.effectManager:RemoveEffect(self.base)
        end
    end
end

function effect:OnRemoved()
    RestoreAllCosts(self)
end

function effect:OnTurnEnd()
    self.base.target.effectManager:RemoveEffect(self.base)
end

function effect:GetDescription(effectBase, template)
    return template
end

return effect
