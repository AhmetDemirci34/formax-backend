using System;
using System.Linq;
using System.Text.RegularExpressions;
using Formax.Application.Services.Matches;
using Formax.Application.Services.News.Intelligence;
using Formax.Domain.Constants;

namespace Formax.Application.Services.PostMatch
{
    /// <summary>
    /// VİDEO ↔ MAÇ KİMLİK KAPISI — bir videonun DB'ye girebilmesinin tek yolu.
    ///
    /// NEDEN AYRI BİR KAPI: <see cref="VideoMatchValidator"/> "bu içerik bu iki takımın
    /// bir maçına ait mi?" sorusunu yanıtlar. Çift maçlı turda bu YETMEZ — Fenerbahçe–Lyon
    /// 18.08.2026 ve Lyon–Fenerbahçe 26.08.2026 karşılaşmalarının İKİSİ de o testi geçer.
    /// Bu sınıf üstüne "HANGİ maç?" sorusunu koyar ve yanıtı takım adından değil şu üç
    /// kanıttan alır: (1) yayın anının maçın bitişine göre konumu, (2) diğer ayağa göre
    /// yakınlık, (3) başlıktaki ev–deplasman SIRASI.
    ///
    /// KURAL: emin değilsek bağlamayız. Yanlış ayağın videosunu göstermek, hiç video
    /// göstermemekten daha kötüdür.
    /// </summary>
    public static class MatchVideoIdentityValidator
    {
        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        /// <summary>Uzatma/devre arası/VAR dahil bir maçın makul en geç bitiş süresi.</summary>
        public static readonly TimeSpan MatchDuration = TimeSpan.FromMinutes(115);

        /// <summary>Resmî özetin yayımlanabileceği kuyruk. Mevcut hatla aynı tutulur.</summary>
        public static readonly TimeSpan PublishTail = VideoMatchValidator.PublishTail;

        /// <summary>
        /// Yön iddia EDİLMEMİŞ videolar için izin verilen kuyruk. Zaman tek kanıtsa
        /// kanıt hızla zayıflar; günler sonra gelen yönsüz bir video hangi ayağa ait
        /// olduğunu kanıtlayamaz.
        /// </summary>
        public static readonly TimeSpan UnassertedDirectionTail = TimeSpan.FromHours(48);

        /// <summary>Bu maçın son düdüğü (yayın tarihi bununla karşılaştırılır).</summary>
        public static DateTime EndOf(DateTime kickoffUtc) => kickoffUtc + MatchDuration;

        public static MatchVideoVerdict Validate(OfficialVideoCandidate candidate, VideoFixtureIdentity fixture)
        {
            if (candidate is null || fixture is null)
                return Reject("aday veya maç kimliği yok");

            if (string.IsNullOrWhiteSpace(candidate.ExternalVideoId))
                return Reject("kaynak video kimliği yok");

            // ── 1. RESMÎ KAYNAK ──────────────────────────────────────────────────
            // Kanal KİMLİĞİ ile eşleşir. "official" yazan başlık, doğrulanmış görünen
            // kanal adı veya yüksek izlenme sayısı kanıt DEĞİLDİR.
            var source = string.Equals(candidate.Platform, "YouTube", StringComparison.OrdinalIgnoreCase)
                ? OfficialVideoSources.ByYouTubeChannel(candidate.SourceIdentifier)
                : OfficialVideoSources.ByKey(candidate.SourceIdentifier);

            if (source == null)
                return Reject("kaynak resmî izin listesinde değil");

            // ── 2. YAYIN ANI MAÇIN BİTİŞİNDEN SONRA MI ───────────────────────────
            // Maç bitmeden yayımlanan içerik maç özeti olamaz; ilk ayağın özeti de
            // ikinci ayak için "maç öncesi" konumundadır — ayak sızıntısı burada kesilir.
            var thisEnd = EndOf(fixture.MatchDateUtc);
            if (candidate.PublishedUtc < thisEnd)
                return Reject("maç bitmeden yayımlanmış");
            if (candidate.PublishedUtc > thisEnd + PublishTail)
                return Reject("maç penceresinin dışında yayımlanmış");

            // ── 3. TAKIM ADLARI + MAÇ GÖRÜNTÜSÜ OLMA ─────────────────────────────
            // Mevcut kapı yeniden kullanılır (iki takım adı, olay/özet işareti,
            // antrenman/basın toplantısı/kamera arkası elemesi).
            var basic = VideoMatchValidator.Validate(
                candidate.Title, candidate.Description, candidate.PublishedUtc,
                new VideoMatchValidator.MatchContext
                {
                    HomeTeam   = fixture.HomeTeamName,
                    AwayTeam   = fixture.AwayTeamName,
                    KickoffUtc = fixture.MatchDateUtc
                },
                sourceTeam: source.Publisher);
            if (!basic.Accepted) return Reject(basic.Reason);

            // ── 4. EV/DEPLASMAN YÖNÜ ─────────────────────────────────────────────
            var folded = NewsTextNormalizer.Fold((candidate.Title ?? "") + " " + (candidate.Description ?? ""));
            var direction = ReadDirection(folded, fixture.HomeTeamName, fixture.AwayTeamName);
            if (direction == Direction.Reversed)
                return Reject("başlıktaki ev/deplasman sırası maçın yönüyle ters");

            // ── 5. AYAK AYRIMI ───────────────────────────────────────────────────
            // Diğer ayağın bitişine DAHA YAKIN ve o ayak da oynanmışsa, video o ayağındır.
            foreach (var other in fixture.OtherLegDatesUtc ?? Array.Empty<DateTime>())
            {
                var otherEnd = EndOf(other);
                if (candidate.PublishedUtc < otherEnd) continue;   // o ayak henüz oynanmamış
                if ((candidate.PublishedUtc - otherEnd).Duration() < (candidate.PublishedUtc - thisEnd).Duration())
                    return Reject("aynı eşleşmenin diğer ayağına daha yakın yayımlanmış");
            }

            // Yön İDDİA EDİLMEMİŞSE tek dayanak zaman kanıtıdır; o hâlde kuyruk kısalır.
            if (direction == Direction.NotAsserted
                && candidate.PublishedUtc > thisEnd + UnassertedDirectionTail)
                return Reject("ev/deplasman yönü iddia edilmemiş ve yayın anı maça uzak");

            // ── 6. TÜR ───────────────────────────────────────────────────────────
            var type = ClassifyType(folded);
            if (type == null)
                return Reject("maç görüntüsü değil (özet/gol/önemli an türlerinden biri değil)");

            return new MatchVideoVerdict(true, type, source,
                $"resmî kaynak={source.Publisher}; yön={direction}; " +
                $"yayın=maç bitişinden {(candidate.PublishedUtc - thisEnd).TotalMinutes:F0} dk sonra");
        }

        private static MatchVideoVerdict Reject(string reason) => new(false, null, null, reason);

        public enum Direction { Match, Reversed, NotAsserted }

        /// <summary>
        /// Başlıktaki "A - B" kalıbından ev/deplasman sırasını okur.
        ///
        /// Yön yalnız AÇIK bir ayraç kalıbında (tire/vs/x) iddia edilmiş sayılır. "Lyon
        /// deplasmanında Fenerbahçe" gibi cümlelerde sıra ev sahibini göstermez; böyle bir
        /// metni "ters" sayıp reddetmek doğru videoyu elerdi.
        /// </summary>
        public static Direction ReadDirection(string foldedText, string homeName, string awayName)
        {
            var home = BestToken(homeName);
            var away = BestToken(awayName);
            if (home == null || away == null) return Direction.NotAsserted;

            const string sep = @"\s*(?:-|–|—|:|vs\.?|v\.?|x)\s*";
            if (Regex.IsMatch(foldedText, Regex.Escape(home) + sep + Regex.Escape(away), Opts))
                return Direction.Match;
            if (Regex.IsMatch(foldedText, Regex.Escape(away) + sep + Regex.Escape(home), Opts))
                return Direction.Reversed;
            return Direction.NotAsserted;
        }

        /// <summary>Takımın en ayırt edici tek parçası ("fenerbahce", "lyon").</summary>
        private static string? BestToken(string? team)
            => NewsTextNormalizer.TeamTokens(team)
                   .Where(t => t.Length >= 4 && !t.Contains(' '))
                   .OrderByDescending(t => t.Length)
                   .FirstOrDefault()
               ?? NewsTextNormalizer.TeamTokens(team).FirstOrDefault(t => t.Length >= 4);

        /// <summary>
        /// TÜR SINIFLANDIRMA — sıra önemlidir. "Maç Özet &amp; Greenwood Golü" başlığı hem
        /// özet hem gol işareti taşır; bu bir GOL KLİBİ değil, tam özettir. Bu yüzden
        /// özet işareti gol işaretinden ÖNCE aranır.
        /// </summary>
        public static string? ClassifyType(string foldedText)
        {
            if (Extended.IsMatch(foldedText)) return MatchVideoTypes.ExtendedHighlights;
            if (Highlights.IsMatch(foldedText)) return MatchVideoTypes.MatchHighlights;
            if (GoalClip.IsMatch(foldedText)) return MatchVideoTypes.Goal;
            if (Moment.IsMatch(foldedText)) return MatchVideoTypes.ImportantMoment;
            return null;
        }

        private static readonly Regex Extended = new(
            @"(extended highlight|uzun ozet|genis ozet|full match|integrale)", Opts);

        // Çok dilli: resmî kaynaklar özeti kendi dillerinde yayımlar.
        private static readonly Regex Highlights = new(
            @"(\bozet\b|ozeti\b|ozetler|highlight|resumen|\bresume\b|compacto|sazetak|" +
            @"zusammenfassung|sintesi|melhores momentos)", Opts);

        private static readonly Regex GoalClip = new(
            @"(\bgol\b|\bgolu\b|goller|\bgoal\b|\bgoals\b|\bbut de\b)", Opts);

        private static readonly Regex Moment = new(
            @"(kirmizi kart|red card|penalti|penalty|\bvar\b|kurtaris|\bsave\b|" +
            @"onemli an|key moment)", Opts);
    }
}
