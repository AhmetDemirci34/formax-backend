using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Engine.Core.Personality;

public class PersonalityEngine
{
    public PersonalityType Resolve(double confidence, double accuracy)
    {
        if (confidence > 0.75 && accuracy > 0.6)
            return PersonalityType.Aggressive;

        if (confidence < 0.4)
            return PersonalityType.Safe;

        if (accuracy > 0.55)
            return PersonalityType.Opportunist;

        return PersonalityType.Balanced;
    }

    public string GetTone(PersonalityType type, int index)
    {
        return type switch
        {
            PersonalityType.Aggressive => index == 0 ? "🔥 YÜKLEN" : "FIRSAT",
            PersonalityType.Safe => "GÜVENLİ",
            PersonalityType.Opportunist => "TREND",
            _ => "DENGELİ SEÇİM"
        };
    }

    public string GetMessage(PersonalityType type)
    {
        return type switch
        {
            PersonalityType.Aggressive => "Formun çok iyi, agresif oynayabilirsin.",
            PersonalityType.Safe => "Daha kontrollü gitmelisin.",
            PersonalityType.Opportunist => "Trendleri yakalıyorsun.",
            _ => "Dengeli ilerliyorsun."
        };
    }
}
