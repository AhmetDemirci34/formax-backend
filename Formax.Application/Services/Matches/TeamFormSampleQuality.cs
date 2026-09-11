using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Domain.Constants;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// TAKIM FORM ÖRNEKLEM KALİTESİ — "bu takım hakkında ne kadar konuşabiliriz?"
    ///
    /// NEDEN AYRI BİR KAVRAM (ölçüldü 06.09.2026):
    /// Form değerlendirmesinin kapısı LİG düzeyindeki veri tamlığıydı
    /// (<see cref="Standings.SeasonDataCompleteness"/>). Sonuç: Süper Lig'de sonucu
    /// kesinleşmemiş TEK maç — Başakşehir–Galatasaray (04.09.2026) — yüzünden
    /// Trabzonspor ve Gençlerbirliği'nin 4'er maçlık GERÇEK ve TAM formu gizleniyor,
    /// yerine "Sezon verileri henüz tamamlanmadı (1 lig maçı bekliyor)" teknik uyarısı
    /// gösteriliyordu. Aynı gün 11 kilitli ligin 8'inde en az bir eksik sonuç vardı;
    /// yani hata tek maça değil, neredeyse tüm maçlara yayılmıştı.
    ///
    /// AYRIM:
    ///  • <see cref="Standings.SeasonDataCompleteness"/> = LİG verisi tam mı?
    ///    (puan durumu tablosunun güncelliği — teşhis ve sıralama sorusu)
    ///  • Bu sınıf = BU TAKIMIN örneklemi ne kadar sağlam?
    ///    (form cümlesi kurulabilir mi — anlatı sorusu)
    ///
    /// İkisi ayrı sorudur. Ligin başka bir maçının sonucu, incelenen iki takımın
    /// oynadığı ve sonucu kesinleşmiş maçların GERÇEKLİĞİNİ değiştirmez.
    /// </summary>
    public static class TeamFormSampleQuality
    {
        /// <summary>Hiç tamamlanmış maç yok — sayı bile söylenemez.</summary>
        public const string None = "None";

        /// <summary>1–2 maç — yalnız sayılar söylenir, hiçbir genelleme kurulmaz.</summary>
        public const string Minimal = "Minimal";

        /// <summary>3–4 maç — sınırlılık açıkça belirtilerek ölçülü değerlendirme yapılır.</summary>
        public const string Limited = "Limited";

        /// <summary>5+ maç — son 5 maç üzerinden dikkatli form özeti kurulabilir.</summary>
        public const string Sufficient = "Sufficient";

        /// <summary>Ölçülü değerlendirmenin (3 maç) alt sınırı.</summary>
        public const int LimitedThreshold = 3;

        /// <summary>"Son 5 maç" ifadesinin alt sınırı.</summary>
        public const int SufficientThreshold = 5;

        /// <summary>Tamamlanmış maç sayısından kaliteyi çözer.</summary>
        public static string Classify(int playedCount) => playedCount switch
        {
            <= 0 => None,
            < LimitedThreshold => Minimal,
            < SufficientThreshold => Limited,
            _ => Sufficient
        };

        /// <summary>
        /// TAKIMI ETKİLEYEN EKSİK SONUÇ — ligin geri kalanı değil, YALNIZ bu takımın maçı.
        ///
        /// <paramref name="seasonFixtures"/> içinde, sonucu kesinleşmesi beklendiği hâlde
        /// kesinleşmemiş ve bu takımın oynadığı maçlar sayılır. Ertelenen/iptal edilen maç
        /// eksiklik DEĞİLDİR: oynanmamıştır, sonucu beklenmez.
        /// </summary>
        public static IReadOnlyList<int> MissingResultMatchIdsFor(
            int teamId,
            IReadOnlyList<Match>? seasonFixtures,
            DateTime nowUtc)
        {
            if (seasonFixtures == null || seasonFixtures.Count == 0) return Array.Empty<int>();

            var cutoff = nowUtc.AddMinutes(-Standings.SeasonDataCompleteness.SettleMarginMinutes);

            return seasonFixtures
                .Where(f => f.HomeTeamId == teamId || f.AwayTeamId == teamId)
                .Where(f => f.MatchDate <= cutoff)
                .Where(f => !MatchStatuses.IsNotPlayed(f.Status))
                .Where(f => !string.Equals(f.Status, MatchStatuses.Finished, StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Id)
                .ToList();
        }
    }
}
