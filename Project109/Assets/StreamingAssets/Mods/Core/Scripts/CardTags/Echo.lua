-- Echo.lua
local Echo = DefineCardTag("Echo")

function Echo:OnInit(tagBase, card)
    self.base = tagBase
    self.card = card
end

function Echo:GetDisplayName()
    return "메아리"
end

function Echo:GetDescription()
    return "카드를 사용한 턴에 한해 그 카드를 다시 사용할 수 있습니다."
end

function Echo:OnRemoved()
end

return Echo
