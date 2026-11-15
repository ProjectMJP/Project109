-- Vanguard.lua
local Vanguard = DefineCardTag("Vanguard")

function Vanguard:OnInit(tagBase, card)
    self.base = tagBase
    self.card = card
end

function Vanguard:GetDisplayName()
    return "선봉대"
end

function Vanguard:GetDescription()
    return "턴마다 처음으로 사용될 시 추가 효과를 시전합니다."
end

function Vanguard:OnRemoved()
end

return Vanguard
