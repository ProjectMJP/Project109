using UnityEngine;

public class CardDescriptionHandler : MonoBehaviour
{
    //public string MakeCardDescription(CardEffect cardData)
    //{
    //    string effectDescription = "";
    //    string featureDescription = "";

    //    foreach (SkillEffect effect in cardData.effects)
    //    {
    //        effectDescription += MakeEffectDescription(effect);
    //        foreach (CardConditionalEffect cardConditionalEffect in effect.conditionalEffect)
    //        {
    //            effectDescription += cardConditionalEffect.condition.description + "\n";
    //        }
    //    }

    //    foreach (CardFeature feature in cardData.features)
    //    {
    //        featureDescription += MakeFeatureDescription(feature);
    //    }

    //    return featureDescription + effectDescription;
    //}

    //string MakeEffectDescription(SkillEffect effect)
    //{
    //    string description = "";

    //    switch (effect.effectType)
    //    {
    //        case "Damage":
    //            description += "피해를 ";
    //            if (effect.times > 1)
    //            {
    //                description += $" 만큼 {effect.times} 번 줍니다.\n";
    //            }
    //            else
    //            {
    //                description += " 줍니다.\n";
    //            }
    //            break;
    //        case "Heal":
    //            description += $"체력을  얻습니다.\n";
    //            break;
    //        case "Shield":
    //            description += $"쉴드를  얻습니다.\n";
    //            break;
    //        case "TemporaryShield":
    //            description += $"보호막을  얻습니다.\n";
    //            break;
    //        case "Move":
    //            description += "만큼 이동합니다.";
    //            break;
    //    }
        
    //    if(effect.effectType == "Buff")
    //    {
    //        switch (effect.statusEffectType)
    //        {
    //            case "Strength":
    //                description += $"힘을  얻습니다.\n";
    //                break;
    //            case "Armor":
    //                description += $"방어를  얻습니다.\n";
    //                break;
    //        }
    //    }
    //    else if(effect.effectType == "Debuff")
    //    {
    //        switch (effect.statusEffectType)
    //        {
    //            case "Strength":
    //                description += $"힘을  잃습니다.\n";
    //                break;
    //            case "Armor":
    //                description += $"방어를  잃습니다.\n";
    //                break;
    //            case "Bleed":
    //                description += $"출혈을  부여합니다.\n";
    //                break;
    //            case "Poison":
    //                description += $"독을  부여합니다.\n";
    //                break;
    //            case "DeadlyPoison":
    //                description += $"맹독을  부여합니다.\n";
    //                break;
    //            case "Debilitate":
    //                description += $"쇠약을  부여합니다.\n";
    //                break;
    //            case "Weaken":
    //                description += $"약화를  부여합니다.\n";
    //                break;
    //        }
    //    }

    //    switch (effect.scalingStat)
    //    {
    //        case "Strength":
    //            description += $"힘의 효과가 {effect.scalingRatio} 배 증가합니다.";
    //            break;
    //        case "Armor":

    //            break;
    //        case "MaxHp":

    //            break;
    //        case "CurrentHp":

    //            break;
    //    }

    //    return description;
    //}

    //string MakeFeatureDescription(CardFeature feature)
    //{
    //    string description = "";

    //    switch (feature.cardFeatureType)
    //    {
    //        case "Start_Action":
    //            description += "개전\n";
    //            break;
    //        case "Vanguard":
    //            description += "선봉대: " + feature.description + "\n";
    //            break;
    //        case "Finale":
    //            description += "종전\n";
    //            break;
    //        case "Echo":
    //            description += "메아리\n";
    //            break;
    //        case "Single_use":
    //            description += "일회성\n";
    //            break;
    //        case "Destroy":
    //            description += "파괴\n";
    //            break;
    //        case "Divide":
    //            description += "분열\n";
    //            break;
    //        case "Chain":
    //            description += "연쇄\n";
    //            break;
    //        case "Unavailable":
    //            description += "사용불가\n";
    //            break;
    //    }

    //    return description;
    //}
}
