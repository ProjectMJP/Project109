-- Finale.lua
local Finale = DefineCardTag("Finale")

function Finale:OnInit(tagBase, card)
    self.base = tagBase
    self.card = card
end

function Finale:GetDisplayName()
    return "종전"
end

function Finale:GetDescription()
    return "턴 종료 시 남은 스태미너를 전부 소모하여 X당 한 번 씩 시전합니다."
end

function Finale:OnRemoved()
end

return Finale
