using System;
using System.Collections.Generic;
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

        public static MatchVideoVerdict Validate(OfficialVideoCandidate candidate, VideoFixtureIdentity fixture,
            IReadOnlyList<OfficialVideoSource>? sources = null)
        {
            if (candidate is null || fixture is null)
                return Reject("aday veya maç kimliği yok");

            if (string.IsNullOrWhiteSpace(candidate.ExternalVideoId))
                return Reject("kaynak video kimliği yok");

            // ── 1. RESMÎ KAYNAK ──────────────────────────────────────────────────
            // Kanal KİMLİĞİ ile eşleşir. "official" yazan başlık, doğrulanmış görünen
            // kanal adı veya yüksek izlenme sayısı kanıt DEĞİLDİR.
            var source = string.Equals(candidate.Platform, "YouTube", StringComparison.OrdinalIgnoreCase)
                ? OfficialVideoSources.ByYouTubeChannel(candidate.SourceIdentifier, sources)
                : OfficialVideoSources.ByKey(candidate.SourceIdentifier, sources);

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
                // Kulüp kanalında kulübün KANONİK adı ("RC Celta" yayıncı adı "Celta Vigo" ile eşleşmez).
                sourceTeam: source.ClubName ?? source.Publisher);
            if (!basic.Accepted) return Reject(basic.Reason);

            // ── 4. EV/DEPLASMAN YÖNÜ ─────────────────────────────────────────────
            var folded = NewsTextNormalizer.Fold((candidate.Title ?? "") + " " + (candidate.Description ?? ""));
            var titleScore = ReadScore(folded, fixture.HomeTeamName, fixture.AwayTeamName);
            var direction = titleScore.Direction != Direction.NotAsserted
                ? titleScore.Direction
                : ReadDirection(folded, fixture.HomeTeamName, fixture.AwayTeamName);
            if (direction == Direction.Reversed)
                return Reject("başlıktaki ev/deplasman sırası maçın yönüyle ters");

            // ── 4B. SKOR ─────────────────────────────────────────────────────────
            // Başlık "Ev X-Y Deplasman" yazıyorsa bu bir iddiadır ve KAYITLI sonuçla aynı olmalıdır:
            // aynı iki takımın başka bir maçının (rövanş, kupa, geçen sezon) özeti burada elenir.
            if (titleScore.Direction == Direction.Match && fixture.HomeScore is int hs && fixture.AwayScore is int aws
                && (titleScore.Home != hs || titleScore.Away != aws))
                return Reject($"başlıktaki skor {titleScore.Home}-{titleScore.Away} kayıtlı sonuçla ({hs}-{aws}) uyuşmuyor");

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

            // ── 6. STÜDYO/PROGRAM İÇERİĞİ ────────────────────────────────────────
            // ÖLÇÜLDÜ (03.09.2026): TRT SPOR'un "…Beşiktaş - Çorum FK, Amedspor -
            // Trabzonspor | Stadyum" programı, başlığında "gol" geçtiği için GOL KLİBİ
            // sayılmış ve İKİ ayrı maça birden bağlanmıştı. Resmî kanalda olmak, maç
            // görüntüsü olmak DEĞİLDİR.
            // ── 6A. MAÇ GÖRÜNTÜSÜ OLMAYAN TÜRLER ─────────────────────────────────
            // Oyun/simülasyon, tepki, tahmin/ön izleme, taraftar montajı ve haber videosu resmî kanalda
            // bile maç özeti değildir (ölçüldü: Forest kanalı "Liam Delap's Reaction", Serie A kanalı
            // "PRE-MATCH LIVE", "COACH CAM").
            // Yalnız BAŞLIK: resmî özet açıklamaları "news/subscribe" gibi genel kelimeler taşıyabilir.
            var foldedTitle = NewsTextNormalizer.Fold(candidate.Title);
            if (NonFootage.IsMatch(foldedTitle))
                return Reject("maç görüntüsü değil (oyun/tepki/tahmin/montaj/haber içeriği)",
                    MatchVideoRejectionReasons.NotMatchHighlights);

            // ── 6B. FARKLI SEZON ─────────────────────────────────────────────────
            if (MentionsOtherSeason(foldedTitle, fixture.MatchDateUtc))
                return Reject("başlıktaki sezon maçın sezonuyla uyuşmuyor");

            if (StudioContent.IsMatch(folded))
                return Reject("stüdyo/program içeriği (maç görüntüsü değil)",
                    MatchVideoRejectionReasons.NotMatchHighlights);

            // ── 7. BAŞLIKTA BİRDEN ÇOK KARŞILAŞMA ────────────────────────────────
            // Bir başlık iki ayrı maçı listeliyorsa hangisine ait olduğu belirsizdir;
            // ikisine birden bağlamak iki yanlış kayıt üretir.
            if (CountsDistinctFixtures(candidate.Title) > 1)
                return Reject("başlıkta birden çok karşılaşma var; tek maça bağlanamaz",
                    MatchVideoRejectionReasons.MultipleMatchesInTitle);

            // ── 8. TÜR ───────────────────────────────────────────────────────────
            // TÜR VE ÖZET İŞARETİ YALNIZ BAŞLIKTAN (14.09.2026 ölçümü): beIN açıklamaları özet etiketleri taşıyor;
            // "Ermal Krasniqi'nin oğlu … galibiyetinin ardından" kısa videosu açıklama yüzünden özet sayılmıştı.
            // Kısa dikey video (#shorts) tam maç özeti olamaz.
            var type = ClassifyType(foldedTitle);
            if (type is MatchVideoTypes.MatchHighlights or MatchVideoTypes.ExtendedHighlights
                && foldedTitle.Contains("#shorts", StringComparison.Ordinal))
                return Reject("kısa video (#shorts) tam maç özeti değildir", MatchVideoRejectionReasons.NotMatchHighlights);
            if (type == null)
                return Reject("maç görüntüsü değil (özet/gol/önemli an türlerinden biri değil)",
                    MatchVideoRejectionReasons.NoHighlightMarker);

            // ── 9. GERÇEK ÖZET İŞARETİ ───────────────────────────────────────────
            // "gol" kelimesi TEK BAŞINA hiçbir zaman kabul üretmez. Otomatik kabul için
            // başlıkta gerçek bir özet işareti (Özet / Highlights / …) bulunmalıdır;
            // yoksa kayıt insan gözüne bırakılır, kullanıcıya gösterilmez.
            if (!HighlightMarker.IsMatch(foldedTitle))
                return Reject("başlıkta gerçek özet işareti yok (yalnız 'gol' geçmesi yetmez)",
                    MatchVideoRejectionReasons.NoHighlightMarker);

            return new MatchVideoVerdict(true, type, source,
                $"resmî kaynak={source.Publisher}; yön={direction}; " +
                $"yayın=maç bitişinden {(candidate.PublishedUtc - thisEnd).TotalMinutes:F0} dk sonra");
        }

        private static MatchVideoVerdict Reject(string reason, string? code = null)
            => new(false, null, null, reason, code);

        /// <summary>
        /// STÜDYO/PROGRAM İÇERİĞİ — resmî kanalda olan ama maç GÖRÜNTÜSÜ olmayan yayınlar.
        ///
        /// Bu liste "şüpheli kelime avı" değildir: her biri, resmî spor kanallarının
        /// gerçekten yayımladığı program türlerinin adıdır. Bir tanesi bile geçiyorsa
        /// otomatik kabul kapanır.
        /// </summary>
        private static readonly Regex StudioContent = new(
            @"(\bstadyum\b|\bprogram\b|yorum|degerlendirme|\banaliz\b|canli yayin|" +
            @"basin toplantisi|roportaj|podcast|tahmin|studyo|\bgundem\b|" +
            @"\binside\b|behind the scenes|press conference|interview)", Opts);

        /// <summary>
        /// GERÇEK ÖZET İŞARETİ — otomatik kabul için başlıkta bulunması ZORUNLU.
        ///
        /// "gol" burada YOKTUR ve bilerek yoktur: "…25 golü geçer" gibi bir program
        /// başlığı da "gol" içerir. Gol klibi ancak insan doğrulamasıyla ya da gerçek
        /// özet işaretiyle birlikte kabul edilir.
        /// </summary>
        private static readonly Regex HighlightMarker = new(
            @"(\bozet\b|ozeti\b|ozetler|highlight|highlights|extended highlights|" +
            @"full highlights|goals ?(&|and) ?highlights|resumen|\bresume\b|compacto|" +
            @"sazetak|zusammenfassung|sintesi|melhores momentos|" +
            // Resmî kanallarda ölçülen yazım hataları — kontrollü liste (Aston Villa FC: "Premier League Highights").
            @"highights|hightlights|higlights)", Opts);

        /// <summary>
        /// Ortak metin normalizasyonu (diakritiksiz, küçük harf, Türkçe "İ" tuzağı çözülmüş).
        ///
        /// Normalleştirici Application'a İÇSELDİR; geriye dönük denetim Infrastructure'da
        /// yaşadığı için buradan açılır. İkinci bir kopya YAZILMAZ — eşleştirmenin dili
        /// tek olmalıdır.
        /// </summary>
        public static string Fold(string? text) => NewsTextNormalizer.Fold(text);

        /// <summary>Takım FOLD edilmiş metinde anılıyor mu?</summary>
        public static bool MentionsTeam(string foldedText, string? team)
            => NewsTextNormalizer.Mentions(foldedText, team);

        /// <summary>Başlık stüdyo/program içeriği mi? (girdi FOLD edilmiş metindir)</summary>
        public static bool IsStudioContent(string foldedText) => StudioContent.IsMatch(foldedText);

        /// <summary>Başlıkta gerçek özet işareti var mı? (girdi FOLD edilmiş metindir)</summary>
        public static bool HasHighlightMarker(string foldedText) => HighlightMarker.IsMatch(foldedText);

        /// <summary>
        /// Başlıkta kaç FARKLI karşılaşma tarif edilmiş?
        ///
        /// "A - B" kalıbı bir karşılaşmadır. "Beşiktaş - Çorum FK, Amedspor - Trabzonspor"
        /// İKİ karşılaşmadır ve tek bir maça ait olamaz. Sayım ham başlık üzerinde
        /// yapılır çünkü ayraçlar (virgül, tire) normalizasyonda korunur.
        /// </summary>
        public static int CountsDistinctFixtures(string? title)
        {
            var text = NewsTextNormalizer.Fold(title);
            if (text.Length == 0) return 0;

            // "kelime(ler) - kelime(ler)" — en az 3 harfli iki taraf.
            var pairs = Regex.Matches(text, @"[a-z][a-z0-9\.]{2,}(?:\s+[a-z0-9\.]{2,}){0,2}\s+-\s+[a-z][a-z0-9\.]{2,}(?:\s+[a-z0-9\.]{2,}){0,2}", Opts);
            return pairs.Count;
        }

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

        /// <summary>
        /// Başlıktaki skor kalıbı: "Ev 1-2 Deplasman" ya da "Ev vs Deplasman (1-2)". Skor her zaman
        /// başlığın yazdığı takım sırasına göre okunur ve maçın ev/deplasman yönüne çevrilir.
        /// Takım adı bütün ayırt edici parçalarıyla denenir ("aston villa", "villa").
        /// </summary>
        public static (Direction Direction, int? Home, int? Away) ReadScore(string foldedText, string homeName, string awayName)
        {
            var homeTokens = ScoreTokens(homeName);
            var awayTokens = ScoreTokens(awayName);
            foreach (var h in homeTokens)
                foreach (var a in awayTokens)
                {
                    if (TryScore(foldedText, h, a, out var x, out var y)) return (Direction.Match, x, y);
                    if (TryScore(foldedText, a, h, out x, out y)) return (Direction.Reversed, y, x);
                }
            return (Direction.NotAsserted, null, null);
        }

        private static bool TryScore(string text, string first, string second, out int x, out int y)
        {
            x = y = 0;
            const string score = @"(\d{1,2})\s*[-–—:]\s*(\d{1,2})";
            var inline = Regex.Match(text, Regex.Escape(first) + @"\s*" + score + @"\s*" + Regex.Escape(second), Opts);
            var trailing = inline.Success ? inline
                : Regex.Match(text, Regex.Escape(first) + @"\s*(?:-|–|—|vs\.?|v\.?|x)\s*" + Regex.Escape(second) + @"\s*\(?\s*" + score, Opts);
            if (!trailing.Success) return false;
            x = int.Parse(trailing.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            y = int.Parse(trailing.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        private static IReadOnlyList<string> ScoreTokens(string? team)
            => NewsTextNormalizer.TeamTokens(team).Where(t => t.Length >= 4)
                   .Concat(TeamNameAliases.For(team)).Distinct(StringComparer.Ordinal).ToList();

        private static readonly Regex NonFootage = new(
            @"(\bfc ?2[0-9]\b|\bfifa ?2[0-9]\b|efootball|\bpes ?20[0-9]{2}\b|simulation|simulasyon|gameplay|career mode|kariyer modu|" +
            @"\breaction\b|\breacts\b|\btepki|prediction|\bpreview\b|\bonizleme|\bprevia\b|pre-?match|coach cam|" +
            @"fan ?cam|montage|\bmontaj|taraftar|\bnews\b|\bhaberi?\b|son dakika|" +
            // ÖLÇÜLDÜ (14.09.2026): beIN SPORTS Türkiye "Gaziantep FK - Fenerbahçe Maç Sonu Teknik Direktör … Açıklamaları"
            // videoları özet diye kabul edilmişti (açıklama metnindeki "özet" etiketi yüzünden).
            @"\baciklama|teknik direktor|\bbasin\b|press conference|post-?match interview)", Opts);

        /// <summary>Başlık "2024/25" ya da "2024-25" gibi bir sezon yazıyorsa maçın sezonu olmalı.</summary>
        public static bool MentionsOtherSeason(string foldedText, DateTime matchDateUtc)
        {
            var start = matchDateUtc.Month >= 7 ? matchDateUtc.Year : matchDateUtc.Year - 1;
            foreach (Match m in Regex.Matches(foldedText, @"\b(20[0-9]{2})\s*[/-]\s*(20)?([0-9]{2})\b", Opts))
            {
                var y1 = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                var y2 = int.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (y2 != (y1 + 1) % 100) continue;           // sezon kalıbı değil (skor/tarih)
                if (y1 != start) return true;
            }
            return false;
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
            @"zusammenfassung|sintesi|melhores momentos|highights|hightlights|higlights)", Opts);

        private static readonly Regex GoalClip = new(
            @"(\bgol\b|\bgolu\b|goller|\bgoal\b|\bgoals\b|\bbut de\b)", Opts);

        private static readonly Regex Moment = new(
            @"(kirmizi kart|red card|penalti|penalty|\bvar\b|kurtaris|\bsave\b|" +
            @"onemli an|key moment)", Opts);
    }
}
