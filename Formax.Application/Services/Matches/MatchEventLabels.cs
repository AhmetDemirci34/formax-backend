using System;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// MAÇ OLAYI ETİKETLERİ — sağlayıcının ham İngilizce terimlerinin TEK Türkçe karşılığı.
    ///
    /// NEDEN BACKEND'DE ve NEDEN TEK YERDE (ölçüldü 07.09.2026): "Önemli Anlar" listesinde
    /// "Substitution 1", "Normal Goal", "Penalty cancelled" gibi ham sağlayıcı terimleri
    /// kullanıcıya olduğu gibi gösteriliyordu. Çeviri her yüzeyde ayrı yazılırsa er geç
    /// ayrışır. Karar burada verilir; ham değer yalnız teşhis alanında (Detail) kalır.
    ///
    /// DETERMİNİSTİK: sözlük eşlemesi. LLM YOK, tahmin YOK. Bilinmeyen bir değer
    /// gelirse ham kod kullanıcıya GÖSTERİLMEZ; olay türünün güvenli genel adı kullanılır.
    ///
    /// SAĞLAYICI DEĞERLERİ (depoda ölçülen tam liste, 11.09.2026):
    /// Goal/Normal Goal, Goal/Own Goal, Goal/Penalty, Card/Yellow Card, Card/Red Card,
    /// subst/Substitution N, Var/Goal confirmed, Var/Penalty confirmed, Var/Penalty cancelled.
    /// Belgelenen ama henüz görülmeyenler de (Missed Penalty, Second Yellow card, Goal
    /// cancelled, Goal Disallowed…) karşılanır.
    /// </summary>
    public static class MatchEventLabels
    {
        /// <summary>Olayın arayüzde biçimlendirilme türü.</summary>
        public static class Kinds
        {
            public const string Goal = "Goal";
            public const string OwnGoal = "OwnGoal";
            public const string PenaltyGoal = "PenaltyGoal";
            public const string MissedPenalty = "MissedPenalty";
            public const string Substitution = "Substitution";
            public const string YellowCard = "YellowCard";
            public const string SecondYellow = "SecondYellow";
            public const string RedCard = "RedCard";
            public const string Var = "Var";
            public const string Other = "Other";
        }

        /// <summary>Bilinmeyen olay için güvenli kullanıcı metni.</summary>
        public const string UnknownLabel = "Maç Olayı";

        public readonly record struct EventLabel(string Kind, string Label);

        public static EventLabel Resolve(string? eventType, string? detail)
        {
            var type = (eventType ?? string.Empty).Trim();
            var d = (detail ?? string.Empty).Trim();

            if (Is(type, "Goal"))
            {
                if (Has(d, "own goal")) return new(Kinds.OwnGoal, "Kendi Kalesine Gol");
                if (Has(d, "missed penalty")) return new(Kinds.MissedPenalty, "Kaçan Penaltı");
                if (Is(d, "Penalty")) return new(Kinds.PenaltyGoal, "Penaltı Golü");
                return new(Kinds.Goal, "Gol");
            }

            if (Is(type, "Card"))
            {
                if (Has(d, "second yellow")) return new(Kinds.SecondYellow, "İkinci Sarıdan Kırmızı Kart");
                if (Has(d, "red")) return new(Kinds.RedCard, "Kırmızı Kart");
                if (Has(d, "yellow")) return new(Kinds.YellowCard, "Sarı Kart");
                return new(Kinds.Other, "Kart");
            }

            if (Is(type, "subst") || Is(type, "Substitution") || d.StartsWith("Substitution", StringComparison.OrdinalIgnoreCase))
                return new(Kinds.Substitution, "Oyuncu Değişikliği");

            if (Is(type, "Var"))
            {
                if (Has(d, "goal") && (Has(d, "cancel") || Has(d, "disallow")))
                    return new(Kinds.Var, "VAR İncelemesi: Gol İptal Edildi");
                if (Has(d, "goal") && Has(d, "confirm"))
                    return new(Kinds.Var, "VAR İncelemesi: Gol Onaylandı");
                if (Has(d, "penalty") && Has(d, "cancel"))
                    return new(Kinds.Var, "VAR İncelemesi: Penaltı İptal Edildi");
                if (Has(d, "penalty") && Has(d, "confirm"))
                    return new(Kinds.Var, "VAR İncelemesi: Penaltı Onaylandı");
                if (Has(d, "card"))
                    return new(Kinds.Var, "VAR İncelemesi: Kart");
                return new(Kinds.Var, "VAR İncelemesi");
            }

            return new(Kinds.Other, UnknownLabel);
        }

        private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // Sağlayıcı terimleri ASCII İngilizcedir; OrdinalIgnoreCase Türkçe İ/ı tuzağına düşmez.
        private static bool Has(string s, string part) => s.Contains(part, StringComparison.OrdinalIgnoreCase);
    }
}
