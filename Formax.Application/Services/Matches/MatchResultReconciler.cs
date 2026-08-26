using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// SONUÇ UZLAŞTIRICI — kayıtlı skor ile kayıtlı olaylar çelişiyorsa bunu ÖRTMEZ,
    /// açığa çıkarır.
    ///
    /// ÖLÇÜLEN GERÇEK SORUN (maç 2276, Celje–Egnatia, 28.07.2026):
    ///   MatchLiveStats: 1-1, Minute=90, Phase='FT'
    ///   MatchLiveEvents: 97' ve 114'te goller + 121-123'te penaltı atışları
    /// Yani kayıtlı "final skor" aslında 90. DAKİKA skorudur; maç uzatmaya gitmiş ve
    /// penaltılarla belirlenmiştir. Skor satırı 90'da donmuş, olay akışı devam etmiş.
    /// AYNI ÇELİŞKİ 6 MAÇTA VAR (ölçüldü, sıkı eşikle) — tekil bir kayıt hatası değil,
    /// alım hattının sistematik davranışıdır.
    ///
    /// NE YAPAR: çelişkiyi deterministik olarak tespit eder ve dürüst etiket üretir.
    /// NE YAPMAZ: uzatma sonrası skoru olaylardan YENİDEN HESAPLAMAZ. Sebep: kendi
    /// kalesine gollerde sağlayıcının <c>Team</c> alanının golü ATAN mı yoksa YARARLANAN
    /// takımı mı gösterdiği bu veriyle kesinleştirilemiyor (2276'da yararlanan gibi
    /// davranıyor, 5120'de atan gibi). Belirsiz alandan skor üretmek uydurma olurdu.
    ///
    /// Penaltı atışları BELİRSİZ DEĞİLDİR: "Penalty" = attı, "Missed Penalty" = kaçırdı,
    /// ikisi de atışı KULLANAN takıma yazılır. Bu yüzden seri sonucu güvenle sayılır.
    /// </summary>
    public static class MatchResultReconciler
    {
        /// <summary>Normal süre + uzatma sınırı; üzeri penaltı atışıdır.</summary>
        public const int RegulationPlusExtraTime = 120;

        /// <summary>
        /// Normal sürenin uzatma dakikası payı. Sağlayıcı "90+5"i dakika 95 olarak yazar;
        /// bu uzatma devresi DEĞİLDİR. 90+15'in ötesi gerçek uzatma sayılır.
        /// </summary>
        public const int StoppageTimeTolerance = 15;

        public sealed class Reconciliation
        {
            /// <summary>Kayıtlı skor maçın TAMAMINI kapsamıyor (uzatma/penaltı sonrası).</summary>
            public bool StoredScoreIsPartial { get; init; }

            /// <summary>Kayıtlı skorun ait olduğu dakika (ör. 90).</summary>
            public int? StoredScoreMinute { get; init; }

            /// <summary>Kayıtlı skordan SONRA gerçekleşmiş gol sayısı (olaylardan).</summary>
            public int GoalsAfterStoredMinute { get; init; }

            public bool WentToExtraTime { get; init; }
            public bool WentToShootout { get; init; }

            /// <summary>Penaltı seri sonucu — yalnız seri varsa dolu.</summary>
            public int? ShootoutHome { get; init; }
            public int? ShootoutAway { get; init; }

            /// <summary>Kullanıcıya gösterilecek dürüst açıklama; çelişki yoksa null.</summary>
            public string? Note { get; init; }
        }

        public static Reconciliation Reconcile(
            MatchLiveStats? stats,
            IReadOnlyList<MatchLiveEvent> events,
            string homeTeam,
            string awayTeam)
        {
            if (events == null || events.Count == 0 || stats == null)
                return new Reconciliation();

            var storedMinute = stats.Minute ?? 0;
            var maxEventMinute = events.Max(e => e.Minute);

            // UZATMA vs. NORMAL SÜRENİN UZATMA DAKİKASI:
            // "90+2" olayı sağlayıcıda dakika 92 olarak yazılıyor — bu UZATMA DEĞİLDİR.
            // Ölçüldü (maç 5120, gerçek skor 2-5): 92'deki gol yüzünden maç yanlışlıkla
            // "uzatmaya gitti" sayılıyordu. Bu yüzden yalnız NORMAL SÜRENİN UZATMASININ
            // ötesindeki dakikalar (>105) gerçek uzatma sayılır; penaltı serisi (>120) ise
            // her hâlükârda kesin kanıttır.
            var extraTimeFloor  = 90 + StoppageTimeTolerance;
            var wentToExtraTime = events.Any(e => e.Minute > extraTimeFloor && e.Minute <= RegulationPlusExtraTime);
            var shootoutEvents  = events.Where(e => e.Minute > RegulationPlusExtraTime).ToList();
            var wentToShootout  = shootoutEvents.Count > 0;

            var goalsAfter = events.Count(e =>
                e.Minute > Math.Max(storedMinute, extraTimeFloor) &&
                e.Minute <= RegulationPlusExtraTime &&
                string.Equals(e.EventType, "Goal", StringComparison.OrdinalIgnoreCase) &&
                !(e.Detail ?? "").Contains("missed", StringComparison.OrdinalIgnoreCase));

            var partial = storedMinute > 0 && maxEventMinute > storedMinute && (goalsAfter > 0 || wentToShootout);

            int? shHome = null, shAway = null;
            if (wentToShootout)
            {
                shHome = CountShootout(shootoutEvents, homeTeam);
                shAway = CountShootout(shootoutEvents, awayTeam);
            }

            string? note = null;
            if (partial)
            {
                var parts = new List<string> { $"Gösterilen skor {storedMinute}. dakika sonucudur" };
                if (wentToExtraTime) parts.Add("maç uzatmaya gitti");
                if (wentToShootout && shHome.HasValue && shAway.HasValue)
                    parts.Add($"penaltılarla belirlendi ({shHome}-{shAway})");
                else if (wentToShootout)
                    parts.Add("penaltılarla belirlendi");
                note = string.Join("; ", parts) + ". Kaynak kaydı uzatma sonrası skoru içermiyor.";
            }

            return new Reconciliation
            {
                StoredScoreIsPartial   = partial,
                StoredScoreMinute      = storedMinute > 0 ? storedMinute : null,
                GoalsAfterStoredMinute = goalsAfter,
                WentToExtraTime        = wentToExtraTime,
                WentToShootout         = wentToShootout,
                ShootoutHome           = shHome,
                ShootoutAway           = shAway,
                Note                   = note
            };
        }

        /// <summary>
        /// Seri golü sayımı. "Penalty" = gol, "Missed Penalty" = kaçtı; ikisi de atışı
        /// KULLANAN takıma yazıldığı için belirsizlik yoktur.
        /// </summary>
        private static int CountShootout(IEnumerable<MatchLiveEvent> shootout, string team)
            => shootout.Count(e =>
                string.Equals((e.Team ?? "").Trim(), team.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.EventType, "Goal", StringComparison.OrdinalIgnoreCase) &&
                (e.Detail ?? "").Contains("penalty", StringComparison.OrdinalIgnoreCase) &&
                !(e.Detail ?? "").Contains("missed", StringComparison.OrdinalIgnoreCase));
    }
}
