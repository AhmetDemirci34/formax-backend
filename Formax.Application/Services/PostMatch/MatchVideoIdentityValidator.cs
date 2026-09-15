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
    ///
    /// 15.09.2026: kaynak kimliği resmî SİTE anahtarı da olabilir (YouTube videosu resmî sitede yayımlanmış
    /// bağlantıdan bulunur); yayın tarihi gün hassasiyetinde gelebilir; altyapı/kadın takımı, "cinematic" montaj ve
    /// farklı turnuva başlıkları elenir; gol klibi yalnız KANONİK golcü ve skor akışıyla kabul edilir.
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

        public const string PrecisionExact = "Exact";
        public const string PrecisionDay = "Day";
        public const string PrecisionSeenOnly = "SeenOnly";

        /// <summary>Bu maçın son düdüğü (yayın tarihi bununla karşılaştırılır).</summary>
        public static DateTime EndOf(DateTime kickoffUtc) => kickoffUtc + MatchDuration;

        /// <summary>Aday için resmî kaynak: önce anahtar (resmî site/tohum), sonra YouTube kanal kimliği.</summary>
        public static OfficialVideoSource? ResolveSource(OfficialVideoCandidate candidate, IReadOnlyList<OfficialVideoSource>? sources)
        {
            var byKey = OfficialVideoSources.ByKey(candidate.SourceIdentifier, sources);
            if (byKey != null) return byKey;
            return string.Equals(candidate.Platform, "YouTube", StringComparison.OrdinalIgnoreCase)
                ? OfficialVideoSources.ByYouTubeChannel(candidate.SourceIdentifier, sources)
                : null;
        }

        public static MatchVideoVerdict Validate(OfficialVideoCandidate candidate, VideoFixtureIdentity fixture,
            IReadOnlyList<OfficialVideoSource>? sources = null)
        {
            if (candidate is null || fixture is null)
                return Reject("aday veya maç kimliği yok");

            if (string.IsNullOrWhiteSpace(candidate.ExternalVideoId))
                return Reject("kaynak video kimliği yok");

            // ── 1. RESMÎ KAYNAK ──────────────────────────────────────────────────
            // Kaynak KİMLİĞİ ile eşleşir (resmî site anahtarı ya da kanal kimliği). "official" yazan başlık,
            // doğrulanmış görünen kanal adı veya yüksek izlenme sayısı kanıt DEĞİLDİR.
            var source = ResolveSource(candidate, sources);
            if (source == null)
                return Reject("kaynak resmî izin listesinde değil");

            return ValidateContent(candidate.Title, candidate.Description, candidate.PublishedUtc,
                candidate.DatePrecision, source, fixture);
        }

        /// <summary>
        /// İÇERİK + KİMLİK KURALLARI — kaynak çözümlendikten sonra. Kalıcı yeniden doğrulama işi, DB'deki kabul
        /// edilmiş kayıtları aynı kuralla buradan sınar (ikinci bir kural kopyası yazılmaz).
        /// </summary>
        public static MatchVideoVerdict ValidateContent(string? title, string? description, DateTime publishedUtc,
            string? datePrecision, OfficialVideoSource source, VideoFixtureIdentity fixture)
        {
            var precision = string.IsNullOrWhiteSpace(datePrecision) ? PrecisionExact : datePrecision!;

            // ── 1B. KAYNAK BU MAÇIN TARAFI MI ────────────────────────────────────
            // Kulüp sitesi yalnız kendi maçında, kapsamı tanımlı lig/yayıncı yalnız kendi liginde resmî kaynaktır.
            if (!OfficialVideoSources.IsRelevant(source, fixture.HomeTeamName, fixture.AwayTeamName, fixture.LeagueId,
                    fixture.HomeTeamId, fixture.AwayTeamId))
                return Reject("resmî kaynak bu maçın kulübü/ligi değil");

            // ── 2. YAYIN ANI MAÇIN BİTİŞİNDEN SONRA MI ───────────────────────────
            // Maç bitmeden yayımlanan içerik maç özeti olamaz; ilk ayağın özeti de
            // ikinci ayak için "maç öncesi" konumundadır — ayak sızıntısı burada kesilir.
            // Gün hassasiyetinde saat uydurulmaz: yayın günü maç gününden (UTC) önceyse ret, sonra kuyruk sınırı.
            var thisEnd = EndOf(fixture.MatchDateUtc);
            if (precision == PrecisionDay)
            {
                if (publishedUtc.Date < fixture.MatchDateUtc.Date)
                    return Reject("maç gününden önce yayımlanmış");
                if (publishedUtc.Date > (thisEnd + PublishTail).Date)
                    return Reject("maç penceresinin dışında yayımlanmış");
            }
            else if (precision == PrecisionSeenOnly)
            {
                // Tarihsiz bağlantı: yalnız resmî sayfada görülme anı bilinir; bu, yayının maçtan SONRA olduğunu
                // kanıtlamaz. Bu yüzden başlık skoru ve yönü birlikte doğrulamak ZORUNDADIR (aşağıda).
                if (publishedUtc < thisEnd)
                    return Reject("maç bitmeden görülmüş");
            }
            else
            {
                if (publishedUtc < thisEnd)
                    return Reject("maç bitmeden yayımlanmış");
                if (publishedUtc > thisEnd + PublishTail)
                    return Reject("maç penceresinin dışında yayımlanmış");
            }

            var foldedTitle = NewsTextNormalizer.Fold(title);

            // ── 3. TAKIM ADLARI + MAÇ GÖRÜNTÜSÜ OLMA ─────────────────────────────
            // Mevcut kapı yeniden kullanılır (iki takım adı, olay/özet işareti,
            // antrenman/basın toplantısı/kamera arkası elemesi). YALNIZ BAŞLIK: açıklama/etiket takım adı taşıdığı
            // için ilgisiz bir videonun eşleşmesi (Amed–Başakşehir kısa videosu) böylece kapanır.
            var structuralTitle = source.Tier is OfficialVideoSourceTiers.Club or OfficialVideoSourceTiers.League or OfficialVideoSourceTiers.Federation
                && IsFixtureResultTitle(foldedTitle, fixture, ReadScore(foldedTitle, fixture.HomeTeamName, fixture.AwayTeamName));
            var basic = VideoMatchValidator.Validate(
                title, null, precision == PrecisionExact ? publishedUtc : fixture.MatchDateUtc,
                new VideoMatchValidator.MatchContext
                {
                    HomeTeam   = fixture.HomeTeamName,
                    AwayTeam   = fixture.AwayTeamName,
                    KickoffUtc = fixture.MatchDateUtc
                },
                // Kulüp kaynağında kulübün KANONİK adı ("RC Celta" yayıncı adı "Celta Vigo" ile eşleşmez).
                sourceTeam: source.Tier == OfficialVideoSourceTiers.Club ? source.ClubName ?? source.Publisher : null,
                requireEventSignal: !structuralTitle);
            if (!basic.Accepted) return Reject(basic.Reason);

            // ── 3A. MAÇ GÖRÜNTÜSÜ OLMAYAN TÜRLER ─────────────────────────────────
            // Oyun/simülasyon, tepki, tahmin/ön izleme, taraftar montajı, haber, basın açıklaması resmî kanalda
            // bile maç özeti değildir (ölçüldü: Forest "Liam Delap's Reaction", Serie A "PRE-MATCH LIVE",
            // beIN "Maç Sonu Teknik Direktör Açıklamaları", legaseriea "Cinematic Highlights" kamera arkası montajı).
            if (NonFootage.IsMatch(foldedTitle))
                return Reject("maç görüntüsü değil (oyun/tepki/tahmin/montaj/haber/açıklama içeriği)",
                    MatchVideoRejectionReasons.NotMatchHighlights);

            // ── 3B. ALTYAPI / KADIN / REZERV TAKIMI ──────────────────────────────
            // ÖLÇÜLDÜ (15.09.2026): atalanta.it sitemap'i "Primavera 1 | Gli highlights di Atalanta-Torino 1-3" yayımlıyor —
            // iki takım adı birebir aynı ama bu A takımının maçı DEĞİL.
            if (YouthOrWomen.IsMatch(foldedTitle) && !YouthOrWomen.IsMatch(NewsTextNormalizer.Fold(fixture.HomeTeamName + " " + fixture.AwayTeamName)))
                return Reject("altyapı/kadın/rezerv takımı maçı", MatchVideoRejectionReasons.NotMatchHighlights);

            // ── 3C. FARKLI TURNUVA ───────────────────────────────────────────────
            if (MentionsOtherCompetition(foldedTitle, fixture.LeagueId))
                return Reject("başlıktaki turnuva maçın turnuvasıyla uyuşmuyor");

            // ── 4. EV/DEPLASMAN YÖNÜ + TÜR ───────────────────────────────────────
            var titleScore = ReadScore(foldedTitle, fixture.HomeTeamName, fixture.AwayTeamName);
            var direction = titleScore.Direction != Direction.NotAsserted
                ? titleScore.Direction
                : ReadDirection(foldedTitle, fixture.HomeTeamName, fixture.AwayTeamName);
            if (direction == Direction.Reversed)
                return Reject("başlıktaki ev/deplasman sırası maçın yönüyle ters");

            // TÜR VE ÖZET İŞARETİ YALNIZ BAŞLIKTAN (14.09.2026 ölçümü): beIN açıklamaları özet etiketleri taşıyor.
            var type = ClassifyType(foldedTitle);
            // YAPISAL ÖZET İŞARETİ (15.09.2026): resmî kulüp/lig sitesinin video sayfası başlığı YALNIZ karşılaşma + kayıtlı skor +
            // turnuva/hafta/sezon sözcüklerinden oluşuyorsa (ölçüldü: "Atalanta-Cagliari 1-2 | 4ª Serie A Enilive 2026/27") başlık
            // maç videosunun kendisini adlandırır. Kontrollü sözcük listesi dışında TEK sözcük kalırsa (kişi adı, "post partita",
            // "conferenza", alıntı) bu işaret YOKTUR.
            var structural = false;
            if (type == null && source.Tier is OfficialVideoSourceTiers.Club or OfficialVideoSourceTiers.League or OfficialVideoSourceTiers.Federation
                && IsFixtureResultTitle(foldedTitle, fixture, titleScore))
            {
                type = MatchVideoTypes.MatchHighlights;
                structural = true;
            }

            // ── 4B. SKOR ─────────────────────────────────────────────────────────
            // Başlık "Ev X-Y Deplasman" yazıyorsa bu bir iddiadır ve KAYITLI sonuçla aynı olmalıdır:
            // aynı iki takımın başka bir maçının (rövanş, kupa, geçen sezon) özeti burada elenir.
            // Gol klibinde skor o golden SONRAKİ ara skordur; aşağıda kanonik skor akışıyla sınanır.
            if (type != MatchVideoTypes.Goal
                && titleScore.Direction == Direction.Match && fixture.HomeScore is int hs && fixture.AwayScore is int aws
                && (titleScore.Home != hs || titleScore.Away != aws))
                return Reject($"başlıktaki skor {titleScore.Home}-{titleScore.Away} kayıtlı sonuçla ({hs}-{aws}) uyuşmuyor");

            // ── 5. AYAK AYRIMI ───────────────────────────────────────────────────
            // Diğer ayağın bitişine DAHA YAKIN ve o ayak da oynanmışsa, video o ayağındır.
            foreach (var other in fixture.OtherLegDatesUtc ?? Array.Empty<DateTime>())
            {
                var otherEnd = EndOf(other);
                if (publishedUtc < otherEnd && precision == PrecisionExact) continue;   // o ayak henüz oynanmamış
                if (precision == PrecisionDay && publishedUtc.Date < other.Date) continue;
                if (other > fixture.MatchDateUtc && publishedUtc >= otherEnd)
                {
                    if ((publishedUtc - otherEnd).Duration() < (publishedUtc - thisEnd).Duration())
                        return Reject("aynı eşleşmenin diğer ayağına daha yakın yayımlanmış");
                }
                else if (other < fixture.MatchDateUtc && (publishedUtc - otherEnd).Duration() < (publishedUtc - thisEnd).Duration())
                {
                    return Reject("aynı eşleşmenin diğer ayağına daha yakın yayımlanmış");
                }
            }

            // Yön İDDİA EDİLMEMİŞSE tek dayanak zaman kanıtıdır; o hâlde kuyruk kısalır. Gün hassasiyetinde ve tarihsiz
            // bağlantıda zaman kanıtı zayıftır: yön AÇIKÇA iddia edilmek zorundadır.
            if (direction == Direction.NotAsserted)
            {
                if (precision != PrecisionExact)
                    return Reject("yayın anı kesin değil ve başlık ev/deplasman yönünü iddia etmiyor");
                if (publishedUtc > thisEnd + UnassertedDirectionTail)
                    return Reject("ev/deplasman yönü iddia edilmemiş ve yayın anı maça uzak");
            }
            if (precision == PrecisionSeenOnly && titleScore.Direction != Direction.Match)
                return Reject("tarihsiz bağlantı: başlıkta doğrulanabilir skor yok");

            // ── 6. FARKLI SEZON ──────────────────────────────────────────────────
            if (MentionsOtherSeason(foldedTitle, fixture.MatchDateUtc))
                return Reject("başlıktaki sezon maçın sezonuyla uyuşmuyor");

            // ── 6B. STÜDYO/PROGRAM İÇERİĞİ ───────────────────────────────────────
            // ÖLÇÜLDÜ (03.09.2026): TRT SPOR'un "…Beşiktaş - Çorum FK, Amedspor -
            // Trabzonspor | Stadyum" programı, başlığında "gol" geçtiği için GOL KLİBİ
            // sayılmış ve İKİ ayrı maça birden bağlanmıştı. Resmî kanalda olmak, maç
            // görüntüsü olmak DEĞİLDİR.
            if (StudioContent.IsMatch(foldedTitle))
                return Reject("stüdyo/program içeriği (maç görüntüsü değil)",
                    MatchVideoRejectionReasons.NotMatchHighlights);

            // ── 7. BAŞLIKTA BİRDEN ÇOK KARŞILAŞMA ────────────────────────────────
            if (CountsDistinctFixtures(title) > 1)
                return Reject("başlıkta birden çok karşılaşma var; tek maça bağlanamaz",
                    MatchVideoRejectionReasons.MultipleMatchesInTitle);

            // ── 8. TÜR ───────────────────────────────────────────────────────────
            // Kısa dikey video (#shorts) ne tam maç özeti ne de gol klibidir.
            if (foldedTitle.Contains("#shorts", StringComparison.Ordinal) || ShortsWord.IsMatch(foldedTitle))
                return Reject("kısa video (#shorts) maç görüntüsü olarak kabul edilmez", MatchVideoRejectionReasons.NotMatchHighlights);
            if (type == null)
                return Reject("maç görüntüsü değil (özet/gol/önemli an türlerinden biri değil)",
                    MatchVideoRejectionReasons.NoHighlightMarker);

            var sourceNote = $"resmî kaynak={source.Publisher}; yön={direction}; tarih={precision}";

            // ── 9A. GOL KLİBİ — kanonik golcü + skor akışı ───────────────────────
            if (type == MatchVideoTypes.Goal)
            {
                var goal = MatchGoal(foldedTitle, fixture, titleScore);
                if (goal.Goal == null) return Reject(goal.Reason, MatchVideoRejectionReasons.NoHighlightMarker);
                return new MatchVideoVerdict(true, MatchVideoTypes.Goal, source,
                    $"{sourceNote}; gol={goal.Goal.PlayerName} {goal.Goal.Minute}'", null, goal.Goal);
            }

            // ── 9B. GERÇEK ÖZET İŞARETİ ──────────────────────────────────────────
            // "gol" kelimesi TEK BAŞINA hiçbir zaman tam özet üretmez. Otomatik kabul için
            // başlıkta gerçek bir özet işareti (Özet / Highlights / …) bulunmalıdır.
            if (type is MatchVideoTypes.MatchHighlights or MatchVideoTypes.ExtendedHighlights && !structural && !HighlightMarker.IsMatch(foldedTitle))
                return Reject("başlıkta gerçek özet işareti yok (yalnız 'gol' geçmesi yetmez)",
                    MatchVideoRejectionReasons.NoHighlightMarker);
            if (type == MatchVideoTypes.ImportantMoment)
                return Reject("tekil önemli an klibi kanonik olayla eşleştirilemiyor", MatchVideoRejectionReasons.NoHighlightMarker);

            return new MatchVideoVerdict(true, type, source,
                precision == PrecisionExact
                    ? $"{sourceNote}; yayın=maç bitişinden {(publishedUtc - thisEnd).TotalMinutes:F0} dk sonra"
                    : $"{sourceNote}; yayın günü={publishedUtc:yyyy-MM-dd}");
        }

        private static MatchVideoVerdict Reject(string reason, string? code = null)
            => new(false, null, null, reason, code);

        /// <summary>Başlıkta karşılaşma dışında izin verilen turnuva/hafta/sezon sözcükleri — KONTROLLÜ LİSTE.</summary>
        private static readonly HashSet<string> CompetitionWords = new(StringComparer.Ordinal)
        {
            "serie", "a", "enilive", "tim", "premier", "league", "laliga", "liga", "ea", "sports", "bundesliga", "ligue", "mcdonalds", "mcdonald",
            "super", "lig", "trendyol", "eredivisie", "vriendenloterij", "championship", "sky", "bet", "efl", "uefa", "champions", "europa",
            "conference", "giornata", "jornada", "journee", "matchday", "md", "speelronde", "hafta", "round", "turno", "gameweek", "gw", "j", "mw",
            "fc", "cf", "ac", "as", "ssc", "us", "sc", "afc", "calcio", "club", "sk", "fk", "rc", "cd", "ud", "sd", "vs", "v", "x", "stagione", "temporada", "season", "saison", "sezon"
        };

        /// <summary>
        /// Başlık YALNIZ bu karşılaşmayı ve kayıtlı sonucunu adlandırıyor mu? Şartlar: başlıkta ev–deplasman sırasıyla kayıtlı skor var,
        /// en az bir turnuva/hafta sözcüğü var ve takım adları, skor, hafta/sezon sayıları ile kontrollü turnuva sözcükleri çıkarıldığında
        /// HİÇBİR sözcük kalmıyor.
        /// </summary>
        public static bool IsFixtureResultTitle(string foldedTitle, VideoFixtureIdentity fixture,
            (Direction Direction, int? Home, int? Away) titleScore)
        {
            if (titleScore.Direction != Direction.Match || fixture.HomeScore is not int hs || fixture.AwayScore is not int aws
                || titleScore.Home != hs || titleScore.Away != aws) return false;

            var teamWords = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in new[] { fixture.HomeTeamName, fixture.AwayTeamName })
            {
                foreach (var t in NewsTextNormalizer.TeamTokens(name).Concat(TeamNameAliases.For(name)))
                    foreach (var w in Regex.Split(t, "[^a-z0-9]+")) if (w.Length > 0) teamWords.Add(w);
                foreach (var w in Regex.Split(NewsTextNormalizer.Fold(name), "[^a-z0-9]+")) if (w.Length > 0) teamWords.Add(w);
            }

            var season = fixture.MatchDateUtc.Month >= 7 ? fixture.MatchDateUtc.Year : fixture.MatchDateUtc.Year - 1;
            var hasCompetition = false;
            foreach (var w in Regex.Split(foldedTitle, "[^a-z0-9]+"))
            {
                if (w.Length == 0 || teamWords.Contains(w)) continue;
                if (CompetitionWords.Contains(w)) { if (w.Length > 2 && !IsClubWord(w)) hasCompetition = true; continue; }
                if (Regex.IsMatch(w, "^[0-9]{1,2}$"))
                {
                    var n = int.Parse(w, System.Globalization.CultureInfo.InvariantCulture);
                    if (n == hs || n == aws || n == (season % 100) || n == ((season + 1) % 100) || (n >= 1 && n <= 50)) continue;
                    return false;
                }
                if (Regex.IsMatch(w, "^[0-9]{1,2}(a|o|st|nd|rd|th|e|eme|ere)$")) { hasCompetition = true; continue; }   // 4ª, 3rd, 5e
                if (w == season.ToString(System.Globalization.CultureInfo.InvariantCulture) || w == (season + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)) continue;
                return false;
            }
            return hasCompetition;
        }

        private static bool IsClubWord(string w) => w is "fc" or "cf" or "ac" or "as" or "ssc" or "us" or "sc" or "afc" or "calcio" or "club" or "sk" or "fk" or "rc" or "cd" or "ud" or "sd" or "vs";

        /// <summary>
        /// GOL KLİBİ EŞLEŞMESİ — başlıktaki oyuncu soyadı kanonik gollerden TEK birine çıkmalı. Aynı oyuncunun birden çok
        /// golü varsa başlıktaki dakika ya da ara skor hangisi olduğunu söylemelidir; söylemiyorsa bağlanmaz.
        /// Başlıkta skor varsa o golden sonraki ara skorla ya da final skorla aynı olmalıdır.
        /// </summary>
        public static (FixtureGoal? Goal, string Reason) MatchGoal(string foldedTitle, VideoFixtureIdentity fixture,
            (Direction Direction, int? Home, int? Away) titleScore)
        {
            var goals = fixture.Goals ?? Array.Empty<FixtureGoal>();
            if (goals.Count == 0) return (null, "kanonik gol kaydı yok; gol klibi doğrulanamaz");

            var named = goals.Where(g => MentionsPlayer(foldedTitle, g.PlayerName)).ToList();
            if (named.Count == 0) return (null, "başlıktaki golcü kanonik gollerle eşleşmiyor");
            if (named.Select(g => NewsTextNormalizer.Fold(g.PlayerName)).Distinct().Count() > 1)
                return (null, "başlık birden çok golcü anıyor; tekil gol klibi değil");

            if (titleScore.Direction == Direction.Match)
            {
                var byScore = named.Where(g => g.HomeScoreAfter == titleScore.Home && g.AwayScoreAfter == titleScore.Away).ToList();
                var final = fixture.HomeScore == titleScore.Home && fixture.AwayScore == titleScore.Away;
                if (byScore.Count == 1) return (byScore[0], "ok");
                if (byScore.Count == 0 && !final) return (null, $"başlıktaki skor {titleScore.Home}-{titleScore.Away} gol akışıyla uyuşmuyor");
            }

            if (named.Count == 1) return (named[0], "ok");

            foreach (Match m in MinuteInTitle.Matches(foldedTitle))
            {
                var minute = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                var byMinute = named.Where(g => Math.Abs(g.Minute - minute) <= 1).ToList();
                if (byMinute.Count == 1) return (byMinute[0], "ok");
            }
            return (null, "aynı oyuncunun birden çok golü var; başlık hangisi olduğunu söylemiyor");
        }

        /// <summary>Oyuncu başlıkta anılıyor mu? Soyadı (≥4 harf) tam sözcük olarak; tek sözcüklü adlar bütün olarak.</summary>
        public static bool MentionsPlayer(string foldedTitle, string? playerName)
        {
            var name = Regex.Replace(NewsTextNormalizer.Fold(playerName), "[^a-z0-9 ]+", " ").Trim();
            if (name.Length < 3) return false;
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var spaced = " " + Regex.Replace(foldedTitle, "[^a-z0-9]+", " ") + " ";
            var last = parts[^1];
            if (last.Length >= 4 && spaced.Contains(" " + last + " ", StringComparison.Ordinal)) return true;
            return parts.Length > 1 && spaced.Contains(" " + string.Join(' ', parts) + " ", StringComparison.Ordinal);
        }

        private static readonly Regex MinuteInTitle = new(@"\b([0-9]{1,3})\s*(?:'|’|\.?\s*dk|min\b|\.?\s*minuto|\.?\s*minute)", Opts);

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
        /// başlığı da "gol" içerir.
        /// </summary>
        private static readonly Regex HighlightMarker = new(
            @"(\bozet\b|ozeti\b|ozetler|highlight|highlights|extended highlights|" +
            @"full highlights|goals ?(&|and) ?highlights|resumen|\bresume\b|compacto|" +
            @"sazetak|zusammenfassung|sintesi|melhores momentos|samenvatting|" +
            // Resmî kanallarda ölçülen yazım hataları — kontrollü liste (Aston Villa FC: "Premier League Highights").
            @"highights|hightlights|higlights)", Opts);

        /// <summary>
        /// Ortak metin normalizasyonu (diakritiksiz, küçük harf, Türkçe "İ" tuzağı çözülmüş).
        /// </summary>
        public static string Fold(string? text) => NewsTextNormalizer.Fold(text);

        /// <summary>Takımın ayırt edici parçaları (Infrastructure için açılır; ikinci normalizasyon kopyası yazılmaz).</summary>
        public static IReadOnlyList<string> TeamTokens(string? team) => NewsTextNormalizer.TeamTokens(team);

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
        /// İKİ karşılaşmadır ve tek bir maça ait olamaz.
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
        /// Yön yalnız AÇIK bir ayraç kalıbında (tire/vs/x) iddia edilmiş sayılır.
        /// </summary>
        public static Direction ReadDirection(string foldedText, string homeName, string awayName)
        {
            foreach (var home in DirectionTokens(homeName))
                foreach (var away in DirectionTokens(awayName))
                {
                    const string sep = @"\b(?:\s+[a-z]{1,4}){0,2}\s*(?:-|–|—|:|vs\.?|v\.?|x)\s*";
                    if (Regex.IsMatch(foldedText, Regex.Escape(home) + sep + Regex.Escape(away), Opts))
                        return Direction.Match;
                    if (Regex.IsMatch(foldedText, Regex.Escape(away) + sep + Regex.Escape(home), Opts))
                        return Direction.Reversed;
                }
            return Direction.NotAsserted;
        }

        /// <summary>
        /// Başlıktaki skor kalıbı: "Ev 1-2 Deplasman", "Ev-Deplasman 1-2" ya da "Ev vs Deplasman (1-2)". Skor her zaman
        /// başlığın yazdığı takım sırasına göre okunur ve maçın ev/deplasman yönüne çevrilir.
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
            // Takım adının ardından en fazla iki kısa ek ("fc", "cf", "calcio") gelebilir: "Venezia FC 3-2 Frosinone".
            const string suffix = @"(?:\s+[a-z]{1,6}){0,2}";
            var inline = Regex.Match(text, Regex.Escape(first) + @"\b" + suffix + @"\s*" + score + @"\s*" + Regex.Escape(second), Opts);
            var trailing = inline.Success ? inline
                : Regex.Match(text, Regex.Escape(first) + @"\b" + suffix + @"\s*(?:-|–|—|vs\.?|v\.?|x)\s*" + Regex.Escape(second) + @"\b" + suffix + @"\s*\|?\s*\(?\s*" + score + @"(?!\s*[/-]\s*\d)", Opts);
            if (!trailing.Success) return false;
            x = int.Parse(trailing.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            y = int.Parse(trailing.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        private static IReadOnlyList<string> ScoreTokens(string? team)
            => NewsTextNormalizer.TeamTokens(team).Where(t => t.Length >= 4)
                   .Concat(TeamNameAliases.For(team)).Distinct(StringComparer.Ordinal).ToList();

        private static IReadOnlyList<string> DirectionTokens(string? team)
        {
            var best = BestToken(team);
            var list = new List<string>();
            if (best != null) list.Add(best);
            list.AddRange(TeamNameAliases.For(team).Where(a => a.Length >= 4));
            return list.Distinct(StringComparer.Ordinal).ToList();
        }

        private static readonly Regex NonFootage = new(
            @"(\bfc ?2[0-9]\b|\bfifa ?2[0-9]\b|efootball|\bpes ?20[0-9]{2}\b|simulation|simulasyon|gameplay|career mode|kariyer modu|" +
            @"\breaction\b|\breacts\b|\btepki|prediction|\bpreview\b|\bonizleme|\bprevia\b|pre-?match|coach cam|" +
            @"fan ?cam|montage|\bmontaj|taraftar|\bnews\b|\bhaberi?\b|son dakika|cinematic|dietro le quinte|" +
            @"\bpost ?partita\b|\bpre ?partita\b|conferenza stampa|rueda de prensa|conference de presse|persconferentie|" +
            // ÖLÇÜLDÜ (14.09.2026): beIN SPORTS Türkiye "Gaziantep FK - Fenerbahçe Maç Sonu Teknik Direktör … Açıklamaları"
            // videoları özet diye kabul edilmişti (açıklama metnindeki "özet" etiketi yüzünden).
            @"\baciklama|teknik direktor|\bbasin\b|press conference|post-?match interview|\bmac sonu\b)", Opts);

        /// <summary>"#shorts" dışındaki kısa video işaretleri (başlık sözcüğü olarak).</summary>
        private static readonly Regex ShortsWord = new(@"(^|\s)(shorts|#short)(\s|$)", Opts);

        /// <summary>Altyapı, rezerv ve kadın takımı işaretleri — A takımı maçına bağlanmaz.</summary>
        private static readonly Regex YouthOrWomen = new(
            @"(\bprimavera\b|\bu ?1[6-9]\b|\bu ?2[0-3]\b|\bunder ?(1[6-9]|2[0-3])\b|\bwomen\b|\bfemminile\b|\bfemenino\b|\bfeminine\b|" +
            @"\bfrauen\b|\bvrouwen\b|\bkadin\b|\bpl2\b|\bpremier league 2\b|\bacademy\b|\byouth\b|\bjuvenil\b|\bnext gen\b|\bserie c\b|\bfutures\b|\bb takimi\b|\bii\b|\bcastilla\b)", Opts);

        /// <summary>Kanonik lig → turnuva adı işaretleri. Başlık BAŞKA bir turnuvayı açıkça anıyorsa ret.</summary>
        private static readonly (int[] Leagues, Regex Pattern)[] Competitions =
        {
            (new[] { 2 }, new Regex(@"(champions league|sampiyonlar ligi|\bucl\b|liga de campeones|ligue des champions)", Opts)),
            (new[] { 3 }, new Regex(@"(europa league|avrupa ligi|\buel\b)", Opts)),
            (new[] { 848 }, new Regex(@"(conference league|konferans ligi|\buecl\b)", Opts)),
            (Array.Empty<int>(), new Regex(@"(coppa italia|supercoppa|copa del rey|supercopa|fa cup|carabao|efl cup|league cup|dfb.?pokal|coupe de france|knvb.?beker|turkiye kupasi|super kupa|trophee des champions|community shield|friendly|amichevole|hazirlik maci|pre-?season)", Opts)),
        };

        /// <summary>Başlık maçın turnuvası dışında bir turnuvayı açıkça anıyor mu?</summary>
        public static bool MentionsOtherCompetition(string foldedText, int? leagueId)
        {
            // Maçın turnuvası bilinmiyorsa karşılaştırma yapılamaz; turnuva adı tek başına ret gerekçesi olmaz.
            if (leagueId is null) return false;
            foreach (var (leagues, pattern) in Competitions)
            {
                if (!pattern.IsMatch(foldedText)) continue;
                if (leagueId is int l && leagues.Contains(l)) continue;
                return true;
            }
            return false;
        }

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
        /// özet hem gol işareti taşır; bu bir GOL KLİBİ değil, tam özettir.
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
            @"(\bozet\b|ozeti\b|ozetler|highlight|resumen|\bresume\b|compacto|sazetak|samenvatting|" +
            @"zusammenfassung|sintesi|melhores momentos|highights|hightlights|higlights)", Opts);

        private static readonly Regex GoalClip = new(
            @"(\bgol\b|\bgolu\b|\bgolunu\b|\bgoal\b|\bgol de\b|\bbut de\b|\btor\b|\bdoelpunt\b|\brete\b)", Opts);

        private static readonly Regex Moment = new(
            @"(kirmizi kart|red card|penalti|penalty|\bvar\b|kurtaris|\bsave\b|" +
            @"onemli an|key moment)", Opts);
    }
}
