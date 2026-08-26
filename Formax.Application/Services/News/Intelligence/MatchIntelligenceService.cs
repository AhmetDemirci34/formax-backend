using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Formax.Application.Services.News.Discovery;

namespace Formax.Application.Services.News.Intelligence
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — Match Intelligence orchestrator.
    /// Tekilleştirilmiş haberleri (v2) → Futbol triyajı + Signal Extraction + Source Quality +
    /// Freshness + Maç bağlama uygulayarak Evidence'a dönüştürür ve Reasoning için
    /// MatchIntelligenceContext üretir.
    ///
    /// İLKE: BACKEND GERÇEĞİ ÜRETİR. Bir haberin BULUNMASI, FORMAX'ın onu gerçek ve bu maça
    /// ait kabul ettiği anlamına GELMEZ. Karar burada verilir; LLM yalnız buradan geçeni okur.
    /// </summary>
    public sealed class MatchIntelligenceService
    {
        private readonly SignalExtractor _signals;
        private readonly SourceQualityResolver _quality;
        private readonly FreshnessPolicy _freshness;

        public MatchIntelligenceService(
            SignalExtractor signals, SourceQualityResolver quality, FreshnessPolicy freshness)
        {
            _signals = signals;
            _quality = quality;
            _freshness = freshness;
        }

        /// <summary>Kapıların ELEDİĞİ haber sayısı (son BuildEvidence çağrısı; teşhis/ölçüm içindir).</summary>
        public sealed record GateStats(
            int Total, int DroppedFreshness, int DroppedQuality, int DroppedRelevance,
            int DroppedContent, int Accepted, int DroppedNonFootball = 0, int DroppedHistorical = 0);

        /// <summary>Son <see cref="BuildEvidence"/> çağrısının kapı istatistiği.</summary>
        public GateStats LastGateStats { get; private set; } = new(0, 0, 0, 0, 0, 0);

        /// <summary>
        /// Haberin maçla ilişkisi — Backend'in verdiği karar (LLM'e "acaba ilgili mi" diye sordurulmaz).
        /// </summary>
        public static class Relation
        {
            /// <summary>Haber maçın HER İKİ takımını da anıyor → doğrudan maç haberi.</summary>
            public const string DirectMatch = "Maç";
            /// <summary>Haber takımlardan birini anıyor ve bu maç o takımın sıradaki maçı.</summary>
            public const string TeamNews = "Takım";
        }

        /// <summary>Haberin maça göre zaman konumu (TTL'den AYRI bir bilgi).</summary>
        public static class Timing
        {
            public const string PreMatch = "MaçÖncesi";
            public const string MatchDay = "MaçGünü";
            public const string PostMatch = "MaçSonrası";
            public const string Historical = "Eski";
        }

        /// <summary>
        /// Tekilleştirilmiş haberlerden taze, kalite-ağırlıklı ve MAÇA AİT Evidence üretir.
        ///
        /// GÜVENLİK KAPILARI (sıra önemlidir — ucuz ve kesin olan önce):
        ///   0. FUTBOL TRİYAJI — içerik futbolla ilgili değilse (boks, basketbol, tenis, F1,
        ///      reklam, spam) hiçbir koşulda evidence olamaz.
        ///   1. İÇERİK TÜRÜ — bahis/oran, TV-yayın rehberi, fikstür/istatistik sayfası, skor.
        ///   2. TARİHSEL İÇERİK — geçmiş sezonun maç sonucu / anma içeriği bugünkü maçın
        ///      "son gelişmesi" değildir.
        ///   3. FRESHNESS — sinyal kategorisinin TTL'i.
        ///   4. SOURCE QUALITY — <paramref name="minQuality"/> altındaki kaynak factual olamaz.
        ///   5. MATCH LINKING — haber bu maça mı ait? İki takım da anılıyorsa doğrudan; tek
        ///      takım anılıyorsa YALNIZ o takımın SIRADAKİ maçına ve yalnız maç öncesi
        ///      penceresinde bağlanır; başka bir eşleşmeden söz ediyorsa hiç bağlanmaz.
        ///
        /// Elenen haberler <c>MatchNewsArticles</c>'ta DURMAYA DEVAM EDER (keşif katmanı);
        /// yalnız AI'ın factual evidence zincirine girmezler.
        /// </summary>
        public List<MatchEvidence> BuildEvidence(
            string formaxMatchId,
            IEnumerable<DedupedNewsItem> items,
            string? homeTeam = null,
            string? awayTeam = null,
            int minQuality = 0,
            bool requireBothTeams = false,
            DateTime? kickoffUtc = null,
            bool homeIsNextMatch = true,
            bool awayIsNextMatch = true,
            IEnumerable<string>? knownPlayerNames = null)
        {
            var evidence = new List<MatchEvidence>();
            int total = 0, dropFresh = 0, dropQuality = 0, dropRelevance = 0,
                dropContent = 0, dropSport = 0, dropHistory = 0;

            var teams = new[] { homeTeam ?? "", awayTeam ?? "" }.Where(t => t.Length > 0).ToArray();

            foreach (var i in items)
            {
                total++;
                var text = (i.Headline ?? "") + " " + (i.Summary ?? "");

                // KAPI 0 — FUTBOL TRİYAJI (maçın takımları bilindiği için takım-farkındalıklı).
                if (!IsFootballRelevant(text, teams)) { dropSport++; continue; }

                // KAPI 1 — İÇERİK TÜRÜ (bahis/TV/fikstür/skor/söylenti/bilgi taşımayan).
                if (IsNonFactualContent(i.Headline, i.Summary)) { dropContent++; continue; }
                if (IsBareFixtureTitle(i.Headline, i.Summary, homeTeam, awayTeam)) { dropContent++; continue; }

                // KAPI 2 — TARİHSEL İÇERİK (geçmiş sezon / anma / eski sonuç).
                if (IsHistoricalContent(text, kickoffUtc)) { dropHistory++; continue; }

                var signals = _signals.Extract(i.Headline ?? "", i.Summary ?? "");
                var primary = _signals.Primary(signals);

                // KAPI 3 — Freshness: sinyal kategorisinin TTL'ini geçen kanıt düşer.
                if (!_freshness.IsFresh(primary, i.PublishedUtc)) { dropFresh++; continue; }

                // Kaynak kalitesi: takım adları verilir → kulübün KENDİ resmi sitesi tanınır.
                var itemSources = i.Sources ?? new List<string>();
                var quality = _quality.BestQuality(itemSources, teams);

                // KAPI 4 — Source Quality.
                if (minQuality > 0 && quality < minQuality) { dropQuality++; continue; }

                // KAPI 5 — MATCH LINKING.
                var relation = ResolveRelation(
                    text, homeTeam, awayTeam, requireBothTeams,
                    kickoffUtc, i.PublishedUtc, homeIsNextMatch, awayIsNextMatch);
                if (relation == null) { dropRelevance++; continue; }

                // KAPI 5b — KAPSAMI ÇÖZÜLMEMİŞ MAÇ KADROSU İDDİASI (bkz. IsUnscopedMatchdaySquadClaim).
                if (IsUnscopedMatchdaySquadClaim(text, relation)) { dropRelevance++; continue; }

                var timing = ResolveTiming(i.PublishedUtc, kickoffUtc);
                if (timing == Timing.Historical) { dropHistory++; continue; }

                // Confidence: haber güveni (tekrar/tazelik) + kaynak kalitesi harmanı.
                var confidence = (int)Math.Round(i.Confidence * 0.6 + quality * 0.4);

                var subject = ResolveSubject(text, homeTeam, awayTeam, knownPlayerNames, itemSources);
                var eventType = ResolveEventType(i.Headline, i.Summary);
                var role = ResolvePersonRole(text, subject.Player, eventType, knownPlayerNames);
                var sourceCount = Math.Max(1, i.SourceCount);

                evidence.Add(new MatchEvidence
                {
                    FormaxMatchId = formaxMatchId,
                    Type = primary,
                    Cluster = string.Join(",", signals),
                    Source = BestSource(itemSources, teams),
                    SourceQuality = quality,
                    Confidence = Math.Clamp(confidence, 0, 99),
                    PublishedUtc = i.PublishedUtc,
                    Headline = i.Headline ?? "",
                    Summary = i.Summary ?? "",
                    ContentHash = i.ContentHash,
                    SourceCount = Math.Max(1, i.SourceCount),
                    Sources = itemSources.ToList(),
                    Relation = relation,
                    Timing = timing,
                    RelatedTeam = subject.Team,
                    OpponentTeam = subject.Opponent,

                    // Kişi adı: rolü KESİN olarak çözülebildiyse taşınır (bkz. ResolvePersonRole).
                    Player = role.Player,
                    Coach = role.Coach,

                    EventType = eventType,
                    Importance = ResolveImportance(eventType, sourceCount, timing)
                });
            }

            LastGateStats = new GateStats(
                total, dropFresh, dropQuality, dropRelevance, dropContent, evidence.Count,
                dropSport, dropHistory);
            return evidence;
        }

        // ────────────────────────────────────────────────────────────────────────────────
        // KAPI 0 — FUTBOL TRİYAJI
        // ────────────────────────────────────────────────────────────────────────────────

        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        /// <summary>
        /// TÜM KAPI KALIPLARI FOLD'LANMIŞ METİN ÜZERİNDE ÇALIŞIR. Ölçüldü: kalıplar ham
        /// metinde aranınca "teknik direktör" (ö) ASCII kalıba takılmıyor ve gerçek Türkçe
        /// kadro haberi "futbol değil" sayılıp eleniyordu. Metin de kalıplar da aynı ASCII
        /// alfabesine indirilir → Türkçe kaynak ayrıcalıksız ve kayıpsız değerlendirilir.
        /// </summary>
        private static string Gate(string? text) => NewsTextNormalizer.Fold(text);

        /// <summary>Futbol DIŞI spor dalları — bu içerik Match Intelligence'a hiç giremez.</summary>
        private static readonly Regex OtherSports = new(
            @"\b(boxing|boxer|heavyweight|welterweight|middleweight|knockout|undercard|"
            + @"ringside|prizefight|ufc|mma|octagon|bellator|"
            + @"nba|basketball|basketbol|euroleague|nfl|super\s*bowl|touchdown|quarterback|"
            + @"mlb|baseball|nhl|ice\s*hockey|"
            + @"tennis|tenis|wimbledon|atp\s*tour|wta|roland\s*garros|us\s*open\s*tennis|"
            + @"formula\s*1|formula\s*one|f1\s*(gp|grand\s*prix|race)|grand\s*prix|motogp|nascar|rally|"
            + @"cricket|kriket|ipl\s*20|rugby|golf|pga\s*tour|masters\s*golf|"
            + @"volleyball|voleybol|handball|hentbol|"
            + @"cycling|bisiklet|tour\s*de\s*france|"
            + @"athletics|swimming|yuzme|olympic\s*(swimming|athletics)|"
            + @"darts|snooker|esports|e-?sports|wwe|wrestling|gures|"
            + @"horse\s*racing|at\s*yarisi)\b", Opts);

        /// <summary>Reklam / spam / clickbait — haber olayı değildir.</summary>
        private static readonly Regex Promotional = new(
            @"(\bsponsored\b|\badvertorial\b|\bpromo\s*code\b|\bsign[-\s]?up\s*(bonus|offer)\b|"
            + @"\bfree\s*bets?\b|\bdeposit\s*bonus\b|\breklam\b|\bindirim\s*kodu\b|"
            + @"\bclick\s*here\b|\byou\s*won'?t\s*believe\b|\bshop\s*now\b|\bbuy\s*tickets?\s*now\b)", Opts);

        /// <summary>Futbolun kendi kelime dağarcığı — pozitif teyit için (fold'lanmış metin).</summary>
        private static readonly Regex FootballVocabulary = new(
            @"\b(football|soccer|futbol|futbolcu|goal|goals|gol|golu|golcu|striker|forvet|midfield(er)?|"
            + @"orta\s*saha|defender|defence|defans|stoper|kaleci|goalkeeper|kanat|"
            + @"lineup|line-?up|starting\s*xi|ilk\s*11|muhtemel\s*11|kadro|kadrosu|kadroda|squad|"
            + @"formation|dizilis|"
            + @"manager|head\s*coach|teknik\s*direktor|hoca|transfer|bonservis|imza|loan|kiralik|"
            + @"injur(y|ed|ies)|sakat|sakatlik|sakatlandi|suspend(ed|sion)|cezali|kart|"
            + @"premier\s*league|la\s*liga|serie\s*a|bundesliga|ligue\s*1|super\s*lig|superlig|"
            + @"champions\s*league|europa\s*league|conference\s*league|sampiyonlar\s*ligi|"
            + @"uefa|fifa|tff|derby|derbi|kick[-\s]?off|matchday|hafta|lig|ligi|kupa|cup|"
            + @"stadium|stad|stadi|penalt(y|i)|offside|ofsayt|var\s*decision|hakem|referee|"
            + @"scored|assist|clean\s*sheet|relegation|kume|play[-\s]?off|antrenman|training|"
            + @"teknik\s*heyet|futbol\s*subesi|kulup|kulubu|taraftar|tribun|santrfor)\b", Opts);

        /// <summary>
        /// İÇERİK FUTBOLLA İLGİLİ Mİ? Kural: başka bir spor dalının imzası varsa DROP
        /// ("Watch Shields vs Scott" → boks). Futbol kelimesi hiç yoksa da içerik futbol
        /// olduğunu kanıtlayamamıştır → DROP (takım adı eşleşmesi kapı 5'te ayrıca aranır,
        /// burada içerik türüne bakılır).
        /// </summary>
        public static bool IsFootballContent(string? text)
        {
            var t = Gate(text).Trim();
            if (t.Length == 0) return false;

            if (IsForeignSportOrPromo(text)) return false;

            return FootballVocabulary.IsMatch(t);
        }

        /// <summary>
        /// İÇERİK BU MAÇIN FUTBOL BAĞLAMINA AİT Mİ? Kesin ret (başka spor dalı / reklam)
        /// her koşulda uygulanır. Pozitif teyit için İKİ yol vardır:
        ///   • metin maçın takımlarından birini anıyor → futbol bağlamı kanıtlanmıştır,
        ///   • ya da futbol kelime dağarcığı taşıyor.
        ///
        /// NEDEN İKİ YOL: yalnız sözlüğe bakmak gerçek futbol haberini eliyordu — ölçüldü,
        /// "Coventry City vs AS Monaco player ratings…" gibi başlıklar sözlükteki hiçbir
        /// kelimeyi taşımadığı için "futbol değil" sayılıyordu. Takım kimliği, sözlükten
        /// daha güçlü ve daha doğru bir futbol kanıtıdır.
        /// </summary>
        public static bool IsFootballRelevant(string? text, IEnumerable<string>? teams)
        {
            var raw = (text ?? "").Trim();
            if (raw.Length == 0) return false;
            if (IsForeignSportOrPromo(raw)) return false;

            if (teams != null)
            {
                var folded = NewsTextNormalizer.Fold(raw);
                foreach (var team in teams)
                    if (NewsTextNormalizer.Mentions(folded, team)) return true;
            }

            return FootballVocabulary.IsMatch(Gate(raw));
        }

        /// <summary>
        /// KESİN RET: içerik başka bir spor dalına ya da reklama ait. Bu karar keşif anında
        /// da verilebilir (futbol kelimesi aranmaz, yalnız yabancı imza aranır) — böylece
        /// boks/basketbol/F1 içeriği keşif deposuna bile girmez.
        /// </summary>
        public static bool IsForeignSportOrPromo(string? text)
        {
            var t = Gate(text).Trim();
            if (t.Length == 0) return false;
            return OtherSports.IsMatch(t) || Promotional.IsMatch(t);
        }

        // ────────────────────────────────────────────────────────────────────────────────
        // KAPI 1 — İÇERİK TÜRÜ
        // ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// İÇERİK TÜRÜ KAPISI — kaynak kalitesinden bağımsızdır. Güvenilir bir yayıncının
        /// sayfası da olsa şu türler FACTUAL EVIDENCE sayılmaz:
        ///   • başka/geçmiş maçın skoru ("Ajax - Shelbourne 3-1", "1 - 0")
        ///   • oran / bahis / tahmin metni ("Predictions, Picks &amp; Odds", "prediction &amp; tips")
        ///   • fikstür-istatistik sayfası, TV/yayın listesi, canlı skor, maç özeti/highlight
        ///   • söylenti / forum / taraftar içeriği
        /// Bu kapı diğer kapıların YERİNE geçmez; onlara EKLENİR. Aynı kural yazma ve okuma
        /// tarafında uygulanır.
        /// </summary>
        public static bool IsNonFactualContent(string? headline, string? summary = null)
        {
            var raw = ((headline ?? "") + " " + (summary ?? "")).Trim();
            if (raw.Length == 0) return true;

            var text = Gate(raw);

            // Skor kalıbı: "3-1", "1 - 0", "2:1". Tarih ("13.08.2026") ve yıl aralığı girmez.
            if (Regex.IsMatch(text, @"\b\d{1,2}\s*[-–:]\s*\d{1,2}\b")) return true;

            // Oran / bahis / tahmin.
            if (Regex.IsMatch(text,
                    @"\b(odds|bet(ting|s)?|bookmaker|accumulator|tips?|picks?|predictions?|preview\s*&|"
                    + @"parlay|betslip|punter|wager|bahis|iddaa|kupon|oran|orani|banko|misli|nesine)\b",
                    Opts)) return true;

            // Fikstür / yayın / canlı skor / özet sayfası.
            if (Regex.IsMatch(text,
                    @"\b(box\s*score|live\s*score|livescore|live\s*stream(ing)?|live\s*coverage|"
                    + @"full\s*match|highlights?|samenvatting|"
                    + @"mac\s*ozeti|head[-\s]?to[-\s]?head|stats\s*&|on\s+tv|tv\s*channel|"
                    + @"where\s*to\s*watch|how\s*to\s*watch|kick[-\s]?off\s*time|watch\s*.*\blive\b|"
                    + @"canli\s*izle|nerede\s*izlenir|hangi\s*kanalda|yayin\s*bilgisi|sifresiz|"
                    + @"to\s*stream|stream\s*(live|on)\b|tv\s*live|streaming\s*details|"
                    + @"line[-\s]?ups?\s*&\s*stats|"
                    + @"fikstur|fixtures?|puan\s*durumu|standings)\b", Opts)) return true;

            // Söylenti / forum / taraftar yorumu.
            if (Regex.IsMatch(text,
                    @"\b(rumou?r|gossip|reddit|forum|fan\s*view|fan\s*talk|soylenti|dedikodu)\b",
                    Opts)) return true;

            // BİLGİ TAŞIMAYAN KULÜP İÇERİĞİ: bilet, hospitality, mağaza, galeri, podcast,
            // "matchday info", "away guide" — resmi kulüp kaynağından gelir ve kalite kapısını
            // rahatça geçer, ama futbol GELİŞMESİ değildir. Ölçüldü: kalite 95 ile pack'e
            // giriyor ve model bunları "son gelişme" sanıyordu.
            if (Regex.IsMatch(text,
                    @"(\bmatch\s*pack\b|\bmatchday\s*info\b|\baway\s*guide\b|\bclub\s*connections\b|"
                    + @"\bseason\s*tickets?\b|\bticket\s*(info|news|sales?|update)\b|\bhospitality\b|"
                    + @"\bgallery\b|\bphoto\s*gallery\b|\bpodcast\b|\bquiz\b|\bcompetition:\s|"
                    + @"\bkit\s*launch\b|\bclub\s*shop\b|\bmerchandise\b|\bmembership\b|"
                    + @"\bmatchday\s*programme\b|\bhandbook\b|\bmeet\s*the\s*opposition\b|"
                    + @"\bfans\s*asked\b|\btribute\b|\bminute'?s?\s*(silence|applause)\b|"
                    + @"\bbilet\s*(satis|fiyat)|\bkombine\b|\bmagaza\b|\bcekilis\b)", Opts)) return true;

            // YORUM / KANAAT: pundit görüşü, köşe yazısı, anket. Olgu değildir.
            if (Regex.IsMatch(text,
                    @"(\bpundit\b|\bverdict\b|\bcolumn\b|\bopinion\b|\bhave\s*your\s*say\b|"
                    + @"\brate\s*the\s*players?\b|\bfan\s*poll\b|\btalking\s*points\b|"
                    + @"\byorum\s*yazisi\b|\bkose\s*yazisi\b|\banket\b)", Opts)) return true;

            // CANLI ANLATIM / MAÇ RAPORU: oynanmış (ya da oynanan) bir maçın akışıdır —
            // yaklaşan maçın "son gelişmesi" DEĞİLDİR. Ölçüldü: depodaki kanıtların büyük
            // kısmı bu türdendi ("Wolves vs Blackburn live: Score and latest updates",
            // "player ratings as new signing looks a class apart") ve anlatıya girdiğinde
            // model başka bir maçın akışını bu maç sanıyordu.
            if (Regex.IsMatch(text,
                    @"(\bplayer\s*ratings?\b|\blive\s*[:|-]|\blive\s*updates?\b|\blive\s*blog\b|"
                    + @"\bas\s*it\s*happened\b|\bminute[-\s]?by[-\s]?minute\b|\bbuild\s*up\s*to\b|"
                    + @"\breaction\s*&|\bfull[-\s]?time\b|\bhalf[-\s]?time\b|\bmatch\s*report\b|"
                    + @"\bopens?\s*the\s*scoring\b|\btakes?\s*the\s*lead\b|\bequalise[rs]?\b|"
                    + @"\bcanli\s*anlatim\b|\bdakika\s*dakika\b|\bmac\s*sonucu\b|\bgeride\s*kaldi\b)",
                    Opts)) return true;

            return false;
        }

        // ────────────────────────────────────────────────────────────────────────────────
        // KAPI 2 — TARİHSEL İÇERİK
        // ────────────────────────────────────────────────────────────────────────────────

        private static readonly Regex RetroPhrases = new(
            @"\b(on\s*this\s*day|throwback|flashback|retro|years?\s*ago|"
            + @"a\s*look\s*back|classic\s*(match|encounter)|vintage|nostalgia|"
            + @"anniversary|all[-\s]?time|greatest\s*(ever|xi)|"
            + @"yil\s*donumu|yillar\s*once|nostalji|tarihe\s*gecen|"
            + @"unutulmaz\s*mac|arsiv|gecmis\s*sezon)\b", Opts);

        private static readonly Regex YearToken = new(@"\b(19|20)\d{2}\b", RegexOptions.CultureInvariant);

        /// <summary>
        /// HABER ESKİ BİR OLAYI MI ANLATIYOR? Kesinlikle kabul edilemeyen hata şudur:
        /// 2026 Celta Vigo – Osasuna maçına 2003 Celta Vigo – Osasuna sonucunun bağlanması.
        ///
        /// İki ölçüt:
        ///   • Anma/arşiv dili ("on this day", "yıllar önce").
        ///   • Metinde geçen EN YENİ yıl, maçın sezonundan eskiyse. "2003" geçip 2026 hiç
        ///     geçmiyorsa içerik bugünün gelişmesi değildir.
        /// Kickoff bilinmiyorsa bugünün yılı esas alınır.
        /// </summary>
        public static bool IsHistoricalContent(string? text, DateTime? kickoffUtc)
        {
            var t = Gate(text).Trim();
            if (t.Length == 0) return true;

            if (RetroPhrases.IsMatch(t)) return true;

            var referenceYear = (kickoffUtc ?? DateTime.UtcNow).Year;

            // KULÜP ADINDAKİ YIL KURULUŞ YILIDIR, MAÇ YILI DEĞİL. Ölçüldü: "DAC 1904
            // Dunajska Streda", "CSKA 1948 Sofia", "1899 Hoffenheim" gibi takımların
            // güncel maç haberleri "eski içerik" sanılıp eleniyordu. Kuruluş yılları
            // neredeyse tamamen 1990 öncesidir; eski MAÇ SONUCU ise modern dönemdedir.
            var years = YearToken.Matches(t)
                                 .Select(m => int.TryParse(m.Value, out var y) ? y : 0)
                                 .Where(y => y >= 1990 && y <= referenceYear + 5)
                                 .ToList();
            if (years.Count == 0) return false;

            // Sezon yazımı (2025/26) ve önceki sezon güncel sayılır → referans-1 kabul edilir.
            return years.Max() < referenceYear - 1;
        }

        // ────────────────────────────────────────────────────────────────────────────────
        // KAPI 5 — MATCH LINKING
        // ────────────────────────────────────────────────────────────────────────────────

        /// <summary>Maç öncesi penceresi: bu süreden eski haber "güncel gelişme" sayılmaz.</summary>
        private const int PreMatchWindowDays = 21;

        /// <summary>
        /// Haber bu maça mı ait? Karar Backend'in; LLM'e sorulmaz.
        ///   • Her iki takım da anılıyor → <see cref="Relation.DirectMatch"/>.
        ///   • Tek takım anılıyor → yalnız o takımın SIRADAKİ maçı ise ve haber maç öncesi
        ///     penceresindeyse <see cref="Relation.TeamNews"/>.
        ///   • Metin BAŞKA bir eşleşmeden söz ediyorsa ("Arsenal vs Tottenham") → null.
        /// null = bu maça bağlanamaz.
        /// </summary>
        public static string? ResolveRelation(
            string? text, string? homeTeam, string? awayTeam, bool strictBothTeams,
            DateTime? kickoffUtc, DateTime publishedUtc, bool homeIsNextMatch, bool awayIsNextMatch)
        {
            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
                return null;

            var folded = NewsTextNormalizer.Fold(text);
            if (folded.Length == 0) return null;

            var hasHome = NewsTextNormalizer.Mentions(folded, homeTeam);
            var hasAway = NewsTextNormalizer.Mentions(folded, awayTeam);

            if (hasHome && hasAway) return Relation.DirectMatch;
            if (strictBothTeams) return null;
            if (!hasHome && !hasAway) return null;

            // BAŞKA EŞLEŞME KORUMASI: haber bir maçtan söz ediyorsa, o maç BİZİM maçımız olmalı.
            if (MentionsForeignFixture(folded, homeTeam!, awayTeam!)) return null;
            if (MentionsForeignOpponentContext(folded, homeTeam!, awayTeam!)) return null;

            // Tek takım haberi ancak o takımın SIRADAKİ maçına bağlanır — aksi hâlde aynı
            // haber takımın gelecek 7 gündeki tüm maçlarına kopyalanır.
            if (hasHome && !homeIsNextMatch) return null;
            if (hasAway && !awayIsNextMatch) return null;

            // Ve yalnız maç öncesi penceresinde: eski bir takım haberi bugünkü maçın
            // "son gelişmesi" değildir.
            if (kickoffUtc.HasValue)
            {
                var daysBefore = (kickoffUtc.Value - publishedUtc).TotalDays;
                if (daysBefore > PreMatchWindowDays) return null;
            }

            return Relation.TeamNews;
        }

        private static readonly Regex FixturePattern = new(
            @"([a-z0-9\.\s]{3,28})\s+(?:vs\.?|v\.|versus|karsi|karsisinda|-\s*)\s*([a-z0-9\.\s]{3,28})",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Metin BİZİM maçımız olmayan bir eşleşmeden mi söz ediyor? "Arsenal vs Tottenham"
        /// haberi Arsenal–Chelsea maçına bağlanamaz. Eşleşme kalıbının İKİ tarafı da bizim
        /// takımlarımıza denk gelmiyorsa yabancı fikstürdür.
        /// </summary>
        private static bool MentionsForeignFixture(string foldedText, string home, string away)
        {
            foreach (Match m in FixturePattern.Matches(foldedText))
            {
                var left = m.Groups[1].Value.Trim();
                var right = m.Groups[2].Value.Trim();
                if (left.Length < 3 || right.Length < 3) continue;

                var leftOurs = NewsTextNormalizer.Mentions(left, home) || NewsTextNormalizer.Mentions(left, away);
                var rightOurs = NewsTextNormalizer.Mentions(right, home) || NewsTextNormalizer.Mentions(right, away);

                // Bir taraf bizim takımımız, diğer taraf başka bir takım → yabancı eşleşme.
                if (leftOurs ^ rightOurs) return true;
            }
            return false;
        }

        /// <summary>
        /// ÇIPLAK FİKSTÜR BAŞLIĞI: metin iki takım adından (ve ayraçtan) ibarettir —
        /// "Middlesbrough - Lincoln", "Charlton Athletic v Derby County". Bu bir haber
        /// değil, fikstür/skor sayfasının adıdır: hiçbir gelişme taşımaz. Ölçüldü, kaliteli
        /// yayıncıdan (Flashscore, BBC fikstür sayfası) geldiği için kapıları geçiyordu.
        /// Gerçek özeti olan kayıtlar korunur — orada bilgi vardır.
        /// </summary>
        public static bool IsBareFixtureTitle(string? headline, string? summary,
            string? homeTeam, string? awayTeam)
        {
            if (!string.IsNullOrWhiteSpace(summary)) return false;
            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam)) return false;

            var rest = NewsTextNormalizer.Fold(headline);
            if (rest.Length == 0) return true;

            foreach (var team in new[] { homeTeam!, awayTeam! })
                foreach (var token in NewsTextNormalizer.TeamTokens(team))
                    rest = rest.Replace(token, " ", StringComparison.Ordinal);

            // Takım adları ve ayraçlar çıkınca geriye anlamlı kelime kalmıyorsa bilgi yoktur.
            var remaining = Regex.Replace(rest, @"[^a-z0-9]", "");
            foreach (var filler in new[] { "vs", "versus", "live", "fc", "afc", "city", "united", "county", "athletic" })
                remaining = remaining.Replace(filler, "", StringComparison.Ordinal);

            return remaining.Length < 6;
        }

        /// <summary>Haberin maça göre zaman konumu.</summary>
        public static string ResolveTiming(DateTime publishedUtc, DateTime? kickoffUtc)
        {
            if (!kickoffUtc.HasValue)
                return (DateTime.UtcNow - publishedUtc).TotalDays > PreMatchWindowDays
                    ? Timing.Historical : Timing.PreMatch;

            var kickoff = kickoffUtc.Value;
            var hoursBefore = (kickoff - publishedUtc).TotalHours;

            if (hoursBefore < -3) return Timing.PostMatch;   // maç başladıktan 3+ saat sonra
            if (hoursBefore <= 24) return Timing.MatchDay;    // maç günü / son 24 saat
            if (hoursBefore <= PreMatchWindowDays * 24) return Timing.PreMatch;
            return Timing.Historical;
        }

        /// <summary>
        /// OLAY ÖZNESİ — haber HANGİ takımın gelişmesi, rakibi kim, öznesi hangi oyuncu.
        ///
        /// NEDEN ZORUNLU: metin her iki takımı da andığında eski davranış özneyi BOŞ
        /// bırakıyordu ve model atfı kendi tahmin ediyordu. Ölçüldü — "Trabzonspor'un
        /// Kasımpaşa kadrosu açıklandı! Mohamed Salah da var" haberi kullanıcıya
        /// "Salah'ın Kasımpaşa kadrosuna eklenmesi" diye çıktı: doğru haber, yanlış takım.
        /// Özne artık dilbilgisel işaretlerden BACKEND'de belirlenir.
        ///
        /// İŞARETLER (yalnız kesin olanlar; hiçbiri yoksa özne BOŞ kalır — tahmin YOK):
        ///   • Türkçe çekim eki: "Trabzonspor'un …", "Galatasaray'da …" → o takım ÖZNEDİR.
        ///   • İngilizce iyelik: "Stoke City's signing" → o takım ÖZNEDİR.
        ///   • Maç betimleyicisi: "&lt;takım&gt; kadrosu / maçı / deplasmanı" → o takım RAKİPTİR
        ///     (haber o takımın değil, o takımla oynanacak maçın kadrosu hakkındadır).
        ///   • Edat: "against &lt;takım&gt;", "&lt;takım&gt; karşısında" → o takım RAKİPTİR.
        /// </summary>
        public static (string Team, string Opponent, string Player) ResolveSubject(
            string? text, string? homeTeam, string? awayTeam,
            IEnumerable<string>? knownPlayerNames = null,
            IEnumerable<string>? sources = null)
        {
            var folded = NewsTextNormalizer.Fold(text);
            var hasHome = NewsTextNormalizer.Mentions(folded, homeTeam);
            var hasAway = NewsTextNormalizer.Mentions(folded, awayTeam);

            string team = "", opponent = "";

            if (hasHome ^ hasAway)
            {
                // Metin TEK takım anıyor. Ama yayıncı DİĞER takımın kaynağıysa çelişki
                // vardır: ölçüldü — "SWANS: … Matos heads for Galbraith reunion at Stoke"
                // metninde yalnız Stoke geçiyor, oysa haber Swansea Bay News'ün ve konu
                // Swansea'nın teknik direktörü. Çelişkide özne BOŞ bırakılır; iki adaydan
                // birini seçmek tahmin olur.
                var textTeam = hasHome ? homeTeam ?? "" : awayTeam ?? "";
                var textOpponent = hasHome ? awayTeam ?? "" : homeTeam ?? "";

                var (srcTeam, _) = ResolveSubjectFromSource(sources, homeTeam, awayTeam);
                if (srcTeam.Length > 0 && !srcTeam.Equals(textTeam, StringComparison.OrdinalIgnoreCase))
                {
                    team = "";
                    opponent = "";
                }
                else
                {
                    team = textTeam;
                    opponent = textOpponent;
                }
            }
            else if (hasHome && hasAway)
            {
                // Her iki takım da anılıyor → özneyi YALNIZ dilbilgisel işaret belirler.
                //
                // İşaretlerin gücü EŞİT DEĞİLDİR:
                //   • Çekim eki / iyelik ("Trabzonspor'un", "Stoke City's") → KESİN özne.
                //   • Edat ("against Swansea", "Kasımpaşa karşısında")      → KESİN rakip.
                //   • İsim betimleyicisi ("Kasımpaşa kadrosu")              → TEK BAŞINA YETMEZ.
                // Betimleyici tek başına yeterli sayılırsa "Kasımpaşa Trabzonspor karşılaşması
                // öncesi hazırlıklar" gibi ÖZNESİZ fikstür cümlesine özne uydurulur. Bu yüzden
                // betimleyici yalnız DESTEKLEYİCİ ölçüttür; kararı çekim eki veya edat verir.
                var homeSubject = HasSubjectMarker(folded, homeTeam);
                var awaySubject = HasSubjectMarker(folded, awayTeam);
                var homeIsOpponentByPreposition = HasOpponentPreposition(folded, homeTeam);
                var awayIsOpponentByPreposition = HasOpponentPreposition(folded, awayTeam);

                if (homeSubject && !awaySubject && !homeIsOpponentByPreposition)
                { team = homeTeam ?? ""; opponent = awayTeam ?? ""; }
                else if (awaySubject && !homeSubject && !awayIsOpponentByPreposition)
                { team = awayTeam ?? ""; opponent = homeTeam ?? ""; }
                else if (awayIsOpponentByPreposition && !homeIsOpponentByPreposition)
                { team = homeTeam ?? ""; opponent = awayTeam ?? ""; }
                else if (homeIsOpponentByPreposition && !awayIsOpponentByPreposition)
                { team = awayTeam ?? ""; opponent = homeTeam ?? ""; }
                else
                {
                    // DİLBİLGİSİ ÇÖZEMEDİ → YAYINCI KİMLİĞİ. "Charlton v Derby: Nathan Jones
                    // previews…" başlığında hiçbir ek/edat yoktur; ama haberi YAYINLAYAN
                    // kaynak Charlton'ın resmi sitesidir. Yayıncı takımlardan YALNIZ BİRİNİN
                    // adını taşıyorsa haber o takımın cephesinden yazılmıştır. Bu bir tahmin
                    // değil, kaynağın kendi kimliğidir. İki takım da (ya da hiçbiri) geçmiyorsa
                    // özne yine BOŞ kalır.
                    var (srcTeam, srcOpponent) = ResolveSubjectFromSource(sources, homeTeam, awayTeam);
                    team = srcTeam;
                    opponent = srcOpponent;
                }
            }

            // Oyuncu yalnız ÖZNE ÇÖZÜLDÜYSE ve adı BİLİNEN kadro listesinde geçiyorsa
            // taşınır. Ad çıkarımı tahmine dayanmaz: eşleşme yoksa alan boş kalır.
            var player = team.Length > 0
                ? MatchKnownPlayer(text, folded, knownPlayerNames, homeTeam, awayTeam)
                : "";

            return (team, opponent, player);
        }

        /// <summary>
        /// Yayıncı kimliğinden özne: kaynak adı/domaini takımlardan yalnız birini içeriyorsa
        /// haber o takımın cephesindendir ("Charlton Athletic Football Club",
        /// "Norwich Evening News"). İkisi de ya da hiçbiri geçmiyorsa boş döner.
        /// </summary>
        private static (string Team, string Opponent) ResolveSubjectFromSource(
            IEnumerable<string>? sources, string? homeTeam, string? awayTeam)
        {
            if (sources == null) return ("", "");

            var homeHit = false;
            var awayHit = false;
            foreach (var src in sources)
            {
                if (string.IsNullOrWhiteSpace(src)) continue;
                var folded = NewsTextNormalizer.Fold(src);
                if (NewsTextNormalizer.Mentions(folded, homeTeam)) homeHit = true;
                if (NewsTextNormalizer.Mentions(folded, awayTeam)) awayHit = true;
            }

            if (homeHit && !awayHit) return (homeTeam ?? "", awayTeam ?? "");
            if (awayHit && !homeHit) return (awayTeam ?? "", homeTeam ?? "");
            return ("", "");
        }

        // ────────────────────────────────────────────────────────────────────────────────
        // FUTBOL OLAYI — haberin ne anlattığı (backend kararı)
        // ────────────────────────────────────────────────────────────────────────────────

        /// <summary>Kullanıcıya anlatılabilir olay türleri (pack'e bu adlarla gider).</summary>
        public static class EventTypes
        {
            public const string Transfer = "Transfer";
            public const string Injury = "Sakatlık";
            public const string Suspension = "Ceza";
            public const string Squad = "Kadro";
            public const string Lineup = "İlk 11";
            public const string CoachStatement = "Teknik Direktör Açıklaması";
            public const string PlayerStatement = "Oyuncu Açıklaması";
            public const string ClubStatement = "Kulüp Açıklaması";
            public const string MatchPreview = "Maç Önizlemesi";
            public const string Training = "Antrenman";
            public const string CompetitionUpdate = "Müsabaka Gelişmesi";
            public const string Other = "Diğer";
        }

        private static readonly (string Type, Regex Pattern)[] EventRules =
        {
            (EventTypes.Injury, new Regex(
                @"\b(injur(y|ed|ies)|knock|strain|sidelined|ruled\s*out|surgery|fitness\s*(doubt|test)|"
                + @"sakat|sakatlik|sakatlandi|tedavi|ameliyat|kanama|odem|adductor|hamstring|achilles)\b", Opts)),

            (EventTypes.Suspension, new Regex(
                @"\b(suspend(ed|sion)|banned|red\s*card|disciplinary|cezali|ceza\s*aldi|kirmizi\s*kart|"
                + @"men\s*cezasi|pfdk|tahkim)\b", Opts)),

            (EventTypes.Transfer, new Regex(
                @"\b(transfer|signing|signs|signed|loan|kiralik|bonservis|imza|sozlesme|anlasti|"
                + @"medical|release\s*clause|kadrosuna\s*katti|kadrosuna\s*kattigini|takviye|ayrildi)\b", Opts)),

            (EventTypes.Lineup, new Regex(
                @"\b(starting\s*xi|line[-\s]?up|predicted\s*(xi|line)|ilk\s*11|muhtemel\s*11|"
                + @"onbir|sahaya\s*cikacak\s*11)\b", Opts)),

            (EventTypes.Squad, new Regex(
                @"\b(squad|kamp\s*kadrosu|kadro\s*(aciklandi|belli)|kadrosunda|kadroda|"
                + @"aciklanan\s*kadro|maç\s*kadrosu|mac\s*kadrosu|team\s*news)\b", Opts)),

            (EventTypes.CoachStatement, new Regex(
                @"\b(boss|manager|head\s*coach|teknik\s*direktor|hoca|previews|presser|"
                + @"press\s*conference|basin\s*toplantisi|gorevden\s*alindi|appointed)\b", Opts)),

            (EventTypes.PlayerStatement, new Regex(
                @"\b(oyuncu\s*aciklamasi|roportaj|interview|speaks\s*(out|about)|konustu|"
                + @"aciklamalarda\s*bulundu)\b", Opts)),

            (EventTypes.ClubStatement, new Regex(
                @"\b(club\s*statement|official\s*statement|kulup\s*aciklamasi|resmi\s*aciklama|"
                + @"resmi\s*sitesinden|duyurdu|aciklama\s*yayimladi|kap\b)\b", Opts)),

            (EventTypes.Training, new Regex(
                @"\b(training|antrenman|idman|hazirlik\s*kampi|training\s*ground)\b", Opts)),

            (EventTypes.CompetitionUpdate, new Regex(
                @"\b(postponed|rescheduled|ertelendi|kura|draw\s*result|play[-\s]?off\s*turu|"
                + @"rakipleri\s*belli|fikstur\s*aciklandi|hakem\s*atamasi|referee\s*appointed)\b", Opts)),

            (EventTypes.MatchPreview, new Regex(
                @"\b(preview|opener|mac\s*oncesi|karsilasma\s*oncesi|onizleme)\b", Opts)),
        };

        /// <summary>
        /// Haberin FUTBOL OLAY TÜRÜ. Sıra önceliklidir: somut olay (sakatlık/ceza/transfer)
        /// genel olandan (önizleme) önce gelir. Hiçbiri tutmazsa "Diğer" döner ve olay
        /// pack'e yalnız gerçek içeriği varsa taşınır.
        /// </summary>
        public static string ResolveEventType(string? headline, string? summary)
        {
            var text = Gate((headline ?? "") + " " + (summary ?? ""));
            if (text.Trim().Length == 0) return EventTypes.Other;

            foreach (var (type, pattern) in EventRules)
                if (pattern.IsMatch(text)) return type;

            return EventTypes.Other;
        }

        /// <summary>
        /// Olayın maç açısından ÖNEMİ — deterministik: olay türü (kadroyu doğrudan etkileyen
        /// gelişme yüksek), doğrulayan kaynak sayısı ve zaman konumu birlikte değerlendirilir.
        /// </summary>
        public static string ResolveImportance(string eventType, int sourceCount, string timing)
        {
            var squadAffecting = eventType is EventTypes.Injury or EventTypes.Suspension
                or EventTypes.Lineup or EventTypes.Squad or EventTypes.Transfer;

            var nearMatch = timing == Timing.MatchDay;

            if (squadAffecting && (sourceCount >= 3 || nearMatch)) return "Yüksek";
            if (squadAffecting) return "Orta";
            if (eventType is EventTypes.CoachStatement or EventTypes.ClubStatement
                or EventTypes.CompetitionUpdate) return sourceCount >= 3 ? "Orta" : "Düşük";
            return "Düşük";
        }

        /// <summary>
        /// Metindeki kişi TEKNİK DİREKTÖR bağlamında mı anılıyor? Böyleyse ad Coach alanına,
        /// değilse Player alanına yazılır — böylece teknik direktör oyuncu sanılmaz.
        /// </summary>
        private static readonly Regex CoachContext = new(
            @"\b(boss|manager|head\s*coach|previews|presser|press\s*conference|"
            + @"teknik\s*direktor|hoca|teknik\s*patron|basin\s*toplantisi|"
            // Teknik direktörü ADIYLA anan kalıplar: "Mark Robins prepares his squad",
            // "Fatih Tekke yönetimindeki Trabzonspor". Bu kişiler OYUNCU DEĞİLDİR;
            // ölçüldü, Oyuncu alanına yazılıyorlardı.
            + @"prepares\s*his|his\s*(squad|side|team)|takes\s*charge|"
            + @"yonetimindeki|yonetiminde|kadrosunu\s*hazirl|gozetiminde)\b", Opts);

        public static bool IsCoachContext(string? text) => CoachContext.IsMatch(Gate(text));

        /// <summary>
        /// KİŞİ ROLÜ — bu ad OYUNCU mu, TEKNİK DİREKTÖR mü? Rol KESİN değilse ad hiç
        /// taşınmaz: yanlış rol, rolsüz haberden kötüdür.
        ///
        /// Kesinlik sırası:
        ///   1) Ad, bu maç için ZATEN çekilmiş kadro listesinde geçiyorsa → OYUNCU.
        ///      Ölçüldü: bu kural olmadan, koç bağlamı taşıyan bir başlıkta ("Mark Robins
        ///      prepares his squad… Salah fit") kadroda YAZAN oyuncu teknik direktör
        ///      alanına yazılıyordu.
        ///   2) Metin teknik direktör bağlamı taşıyorsa ya da olay bir teknik direktör
        ///      açıklamasıysa → TEKNİK DİREKTÖR (Chris Davies, Mark Robins örnekleri).
        ///   3) Olay doğrudan oyuncuya ait bir gelişmeyse (sakatlık, ceza, kadro, ilk 11,
        ///      transfer, oyuncu açıklaması) → OYUNCU.
        ///   4) Hiçbiri değilse → ad taşınmaz (rol belirsiz).
        /// </summary>
        internal static (string Player, string Coach) ResolvePersonRole(
            string? text, string? personName, string eventType,
            IEnumerable<string>? knownPlayerNames)
        {
            var name = (personName ?? "").Trim();
            if (name.Length == 0) return ("", "");

            if (MatchFromKnownList(NewsTextNormalizer.Fold(text), knownPlayerNames).Length > 0)
                return (name, "");

            if (IsCoachContext(text) || eventType == EventTypes.CoachStatement)
                return ("", name);

            var playerEvent = eventType is EventTypes.Injury or EventTypes.Suspension
                or EventTypes.Lineup or EventTypes.Squad or EventTypes.Transfer
                or EventTypes.PlayerStatement;

            return playerEvent ? (name, "") : ("", "");
        }

        /// <summary>Geriye dönük ad — mevcut çağıranlar için (özne çözümlemesine yönlendirir).</summary>
        internal static (string Team, string Opponent) ResolveRelatedTeam(
            string? text, string? homeTeam, string? awayTeam)
        {
            var (team, opponent, _) = ResolveSubject(text, homeTeam, awayTeam);
            return (team, opponent);
        }

        /// <summary>
        /// Takım adı ÖZNE işareti taşıyor mu? Türkçe çekim eki apostrofla ayrılır
        /// ("Trabzonspor'un", "Galatasaray'da"); İngilizcede iyelik "'s" ile gelir.
        /// </summary>
        private static bool HasSubjectMarker(string foldedText, string? team)
        {
            foreach (var token in NewsTextNormalizer.TeamTokens(team))
            {
                if (token.Length < 4) continue;
                var idx = 0;
                while ((idx = foldedText.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
                {
                    var after = idx + token.Length;
                    if (after < foldedText.Length && foldedText[after] == '\'') return true;
                    idx = after;
                }
            }
            return false;
        }

        /// <summary>
        /// BAŞKA BİR MAÇIN BAĞLAMI: "&lt;Kulüp&gt; karşılaşmaları için kadro listesi",
        /// "&lt;Kulüp&gt; maçı öncesi", "&lt;Kulüp&gt; Teknik Direktörü …" kalıplarında adı geçen kulüp
        /// BİZİM rakibimiz değilse, haber BAŞKA bir maçı anlatıyordur ve bu maça bağlanamaz.
        ///
        /// Ölçüldü: "Fenerbahçe'nin Olympique Lyon karşılaşmaları için kadro listesi belli oldu"
        /// ve "Lyon Teknik Direktörü Paulo Fonseca'dan Fenerbahçe açıklaması" haberleri
        /// Gençlerbirliği–Fenerbahçe maçına kanıt olarak giriyordu: özne (Fenerbahçe) doğru,
        /// ama olay BAŞKA maçın kadrosu/teknik direktörü. Mevcut "X vs Y" koruması bu
        /// kalıpları görmüyordu.
        /// </summary>
        private static readonly Regex ForeignFixtureContext = new(
            @"([a-z0-9\.]{4,}(?:\s+[a-z0-9\.]{3,})?)\s+"
            + @"(karsilasmalari|karsilasmasi|maci\s+oncesi|macina|macinda|macindaki|macinin\s+kadrosu[a-z]*|maci\s+kadrosu[a-z]*|"
            + @"teknik\s+direktoru|teknik\s+direktori)",
            RegexOptions.CultureInvariant);

        private static bool MentionsForeignOpponentContext(string foldedText, string home, string away)
        {
            foreach (Match m in ForeignFixtureContext.Matches(foldedText))
            {
                var subject = m.Groups[1].Value.Trim();
                if (subject.Length < 4) continue;

                // Bağlamdaki kulüp bizim takımlarımızdan biriyse sorun yok (kendi maçımız).
                if (NewsTextNormalizer.Mentions(subject, home) ||
                    NewsTextNormalizer.Mentions(subject, away)) continue;

                // Kulüp adı olmayan jenerik sözcükler ("bu", "ilk", "sezon") yanlış tetiklemesin.
                if (GenericFixtureWords.Contains(subject)) continue;

                return true;   // başka bir kulübün maçı/teknik direktörü → bu maça ait değil
            }
            return false;
        }

        /// <summary>
        /// MAÇ KADROSU İDDİASI — hangi maça ait olduğu ÇÖZÜLMEDEN kullanılamaz.
        ///
        /// "kadrodan çıkarıldı", "kadroda yok", "kadrosuna alınmadı", "forma giyemeyecek"
        /// ifadeleri BİR MAÇA özgüdür: oyuncunun BELİRLİ bir maçın kadrosunda olmadığını
        /// söyler, genel bir durum bildirmez. Haber hangi maçtan söz ettiğini yazmadığında
        /// (tek takım haberi → Relation=TeamNews) backend bu iddiayı BU maça bağlayamaz.
        ///
        /// Ölçüldü (Fenerbahçe–Lyon, 18.08.2026): "Fenerbahçe'de sakatlık şoku: Mert Günok
        /// kadrodan çıkarıldı" başlığı ÖNCEKİ (Gençlerbirliği) maçın kadro kararıydı; başlıkta
        /// o maç anılmadığı için yabancı-fikstür kapısı göremedi ve kanıt Lyon maçına girdi.
        /// Model de bunu "bir süre kadroda yer alamayacak" diye BU maça taşıdı.
        ///
        /// KURAL: sakatlık bir DURUMDUR, kadro dışılık bir MAÇ KARARIDIR. Durum bilgisi
        /// zaten deterministik Availability katmanından (MatchPlayerStatuses) gelir; maç
        /// kararı ise ancak haber BU maçı adıyla anıyorsa (Relation=DirectMatch) kullanılır.
        /// </summary>
        private static readonly Regex MatchdaySquadClaim = new(
            @"(kadro\s*disi|kadrodan\s*(cikarildi|cikartildi|cikarti)|kadroda\s*yok|"
            + @"kadrosuna\s*(alinmadi|dahil\s*edilmedi)|kadroya\s*alinmadi|"
            + @"forma\s*giyemeyecek|oynayamayacak|macta\s*yok|"
            + @"(left|dropped)\s+out\s+of\s+the\s+squad|out\s+of\s+the\s+squad|"
            + @"omitted\s+from\s+the\s+squad)",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Bu kanıt, hangi maça ait olduğu bilinmeyen bir MAÇ KADROSU iddiası mı taşıyor?
        /// true → kanıt anlatıya girmez (durum bilgisi Availability katmanında zaten vardır).
        /// </summary>
        public static bool IsUnscopedMatchdaySquadClaim(string? text, string? relation)
        {
            // Haber BU maçın iki tarafını da anıyorsa iddia bu maça aittir → serbest.
            if (string.Equals(relation, Relation.DirectMatch, StringComparison.Ordinal)) return false;

            var folded = NewsTextNormalizer.Fold(text);
            return folded.Length > 0 && MatchdaySquadClaim.IsMatch(folded);
        }

        private static readonly HashSet<string> GenericFixtureWords = new(StringComparer.Ordinal)
        {
            "bu", "ilk", "son", "sezon", "sezonun", "lig", "ligin", "kupa", "kupasi", "hafta",
            "haftanin", "super", "gelecek", "onumuzdeki", "gecen", "derbi", "zorlu", "kritik",
            "deplasman", "ic", "dis", "resmi", "yeni", "eski", "buyuk", "onemli"
        };

        /// <summary>Takımı RAKİP konumuna koyan edatlar (öncesinde gelir).</summary>
        private static readonly string[] OpponentPrepositionsBefore =
        {
            "against", "versus", "faces", "face", "hosts", "host", "visits", "visit",
            "travel to", "at home to", "away to"
        };

        /// <summary>Takımı RAKİP konumuna koyan Türkçe son-çekim edatları (sonrasında gelir).</summary>
        private static readonly string[] OpponentPostpositionsAfter =
        {
            "karsisinda", "karsisina", "karsisindan", "onunde", "deplasmaninda", "deplasmanina"
        };

        /// <summary>
        /// Takım adı EDATLA rakip konumuna konmuş mu? Yalnız kesin edatlar sayılır:
        /// "against Swansea" (öncesinde) veya "Kasımpaşa karşısında" (sonrasında).
        /// İsim tamlaması ("Kasımpaşa kadrosu") burada KASITLI olarak sayılmaz — tek
        /// başına özneyi belirlemeye yetmez.
        /// </summary>
        private static bool HasOpponentPreposition(string foldedText, string? team)
        {
            foreach (var token in NewsTextNormalizer.TeamTokens(team))
            {
                if (token.Length < 4) continue;

                var idx = 0;
                while ((idx = foldedText.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
                {
                    var after = idx + token.Length;

                    var head = foldedText[..idx].TrimEnd(' ', '-', ':', ',');
                    if (OpponentPrepositionsBefore.Any(p =>
                            head.EndsWith(" " + p, StringComparison.Ordinal)
                         || head.Equals(p, StringComparison.Ordinal)))
                        return true;

                    var tail = foldedText[Math.Min(after, foldedText.Length)..]
                        .TrimStart('\'', ' ', '-', ':');
                    if (OpponentPostpositionsAfter.Any(p => tail.StartsWith(p, StringComparison.Ordinal)))
                        return true;

                    idx = after;
                }
            }
            return false;
        }

        /// <summary>
        /// Başlıkta geçen oyuncu adı — YALNIZ verilen bilinen ad listesiyle eşleşirse.
        /// Serbest ad çıkarımı YAPILMAZ: Türkçe başlıklarda çok sayıda kelime büyük harfle
        /// başlar ve "Süper Lig", "Kasımpaşa Kadrosu" gibi öbekler oyuncu sanılır.
        /// </summary>
        private static string MatchKnownPlayer(string? rawText, string foldedText,
            IEnumerable<string>? knownPlayerNames, string? homeTeam, string? awayTeam)
        {
            var known = MatchFromKnownList(foldedText, knownPlayerNames);
            if (known.Length > 0) return known;

            // Bilinen listede yoksa başlıktan ÇOK DAR bir çıkarım yapılır. Bu ad, özne
            // takımı ZATEN çözülmüş bir haberden gelir → hangi takıma ait olduğu bellidir,
            // uydurma bağ kurulmaz. Yalnız kişi adı olabilecek biçimler kabul edilir.
            return ExtractPersonName(rawText, homeTeam, awayTeam);
        }

        private static string MatchFromKnownList(string foldedText, IEnumerable<string>? knownPlayerNames)
        {
            if (knownPlayerNames == null) return "";

            foreach (var name in knownPlayerNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                // Sağlayıcı adları "C. Burgess" biçiminde gelir; soyad en ayırt edici parçadır.
                var surname = NewsTextNormalizer.Fold(name)
                    .Split(new[] { ' ', '.', '\'' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => p.Length >= 4)
                    .OrderByDescending(p => p.Length)
                    .FirstOrDefault();

                if (surname != null && foldedText.Contains(surname, StringComparison.Ordinal))
                    return name.Trim();
            }
            return "";
        }

        /// <summary>
        /// Başlıktaki kişi adı — DAR ve temkinli. Yalnız büyük harfle başlayan, tümü büyük
        /// harf OLMAYAN, takım/lig/kurum sözlüğünde bulunmayan sözcükler kişi adı sayılır.
        /// Bulunamazsa boş döner (alan hiç gönderilmez).
        /// </summary>
        private static string ExtractPersonName(string? rawText, string? homeTeam, string? awayTeam)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return "";

            // MAÇIN TAKIM ADLARI KİŞİ ADI OLAMAZ. Ölçüldü: "Stoke City injury news …
            // vs Swansea" başlığından "Swansea Stoke" kişi adı çıkarılıyordu.
            var teamWords = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in new[] { homeTeam, awayTeam })
                foreach (var token in NewsTextNormalizer.TeamTokens(t))
                    foreach (var part in token.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        teamWords.Add(part);

            var words = Regex.Split(rawText, @"[^\p{L}\p{Nd}'’]+")
                             .Where(w => w.Length >= 3)
                             .ToList();

            var candidates = new List<string>();
            foreach (var w in words)
            {
                var bare = w.Split('\'', '’')[0];               // "Salah'ın" → "Salah"
                if (bare.Length < 4) continue;
                if (!char.IsUpper(bare[0])) continue;
                if (bare.ToUpperInvariant() == bare) continue;  // TÜMÜ BÜYÜK → başlık vurgusu

                var folded = NewsTextNormalizer.Fold(bare);
                if (NonPersonWords.Contains(folded)) continue;
                if (teamWords.Contains(folded)) continue;

                candidates.Add(bare);
            }

            // YALNIZ AD + SOYAD ÇİFTİ kabul edilir. Tek sözcük kabul edilirse başlıkta geçen
            // başka bir kulüp adı ("Beşiktaş") oyuncu sanılır — ölçüldü. Aynı olayın başka
            // bir başlığında tam ad geçiyorsa olay birleştirmesinde oradan devralınır.
            var pairs = new List<string>();
            for (var i = 0; i + 1 < candidates.Count; i++)
            {
                var pair = candidates[i] + " " + candidates[i + 1];
                if (!rawText.Contains(pair, StringComparison.Ordinal)) continue;
                if (LooksLikeClubName(rawText, pair)) continue;   // kulüp adı kişi sayılmaz
                pairs.Add(pair);
            }

            // BAŞLIKTAKİ İLK AD ≠ OLAYIN ÖZNESİ. Ölçüldü: "Xabi Alonso gets his man! Chelsea
            // confirms seventh summer signing as Pep Chavarria arrives from Rayo Vallecano"
            // başlığında ilk ad çifti alınınca transfer olan oyuncu Xabi Alonso sanıldı —
            // oysa transfer edilen Pep Chavarria. Başlıkta BİRDEN FAZLA kişi adayı varsa
            // hangisinin olayın öznesi olduğu KESİN DEĞİLDİR → ad hiç taşınmaz.
            // Tek aday varsa özne tartışmasızdır.
            return pairs.Count == 1 ? pairs[0] : "";
        }

        /// <summary>
        /// İKİ SÖZCÜKLÜ AD, KİŞİ DEĞİL KULÜP MÜ? Ölçüldü: "…as Pep Chavarria arrives from
        /// Rayo Vallecano" gibi başlıklarda kulüp adı da ad+soyad görünümündedir ve kişi
        /// sanılabiliyor. Kulüp konumunda gelen adlar bir edatın ("from/to/against/vs")
        /// hemen ardında ya da kulüp ekinin ("FC", "CF", "SC") yanında geçer.
        /// </summary>
        private static bool LooksLikeClubName(string rawText, string pair)
        {
            var idx = rawText.IndexOf(pair, StringComparison.Ordinal);
            if (idx < 0) return false;

            var before = rawText[..idx].TrimEnd();
            foreach (var prep in new[] { "from", "to", "against", "vs", "vs.", "v", "at", "join", "joins", "joined" })
                if (before.EndsWith(" " + prep, StringComparison.OrdinalIgnoreCase)) return true;

            var after = rawText[(idx + pair.Length)..].TrimStart();
            return after.StartsWith("FC", StringComparison.Ordinal)
                || after.StartsWith("CF", StringComparison.Ordinal)
                || after.StartsWith("SC", StringComparison.Ordinal);
        }

        /// <summary>Kişi adı OLMAYAN, başlıklarda büyük harfle geçen yaygın sözcükler.</summary>
        private static readonly HashSet<string> NonPersonWords = new(StringComparer.Ordinal)
        {
            "super", "lig", "ligi", "trendyol", "spor", "kupa", "kupasi", "avrupa", "uefa", "fifa",
            "sampiyonlar", "konferans", "premier", "league", "championship", "eredivisie",
            "bundesliga", "liga", "serie", "ligue", "play", "off", "turu", "hafta", "sezon",
            "kadro", "kadrosu", "kadrosunda", "maci", "macinda", "transfer", "sakatlik",
            "teknik", "direktor", "resmi", "aciklama", "aciklamasi", "haber", "haberi",
            "son", "dakika", "iste", "futbol", "futbolcu", "stadyum", "deplasman",
            "city", "united", "town", "county", "athletic", "club", "fylkir", "news",
            "boss", "manager", "coach", "injury", "team", "squad", "star", "striker",
            "ocak", "subat", "mart", "nisan", "mayis", "haziran", "temmuz", "agustos",
            "eylul", "ekim", "kasim", "aralik", "pazartesi", "sali", "carsamba",
            "persembe", "cuma", "cumartesi", "pazar"
        };

        /// <summary>Kanıda gösterilecek EN GÜVENİLİR kaynak (arama motoru relay'i değil).</summary>
        private string BestSource(IEnumerable<string>? sources, IEnumerable<string>? teams)
        {
            if (sources == null) return "";
            return sources.Where(s => !string.IsNullOrWhiteSpace(s))
                          .OrderByDescending(s => _quality.Quality(s, teams))
                          .FirstOrDefault() ?? "";
        }

        // ────────────────────────────────────────────────────────────────────────────────
        // OKUMA TARAFI
        // ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Haber metni hedef maçın HER İKİ takımını da anıyor mu? (Geriye dönük API — mevcut
        /// çağıranlar için korunur; kural artık <see cref="NewsTextNormalizer"/> üzerinden
        /// diakritik-duyarsız çalışır, böylece "Beşiktaş" ile "Besiktas" eşleşir.)
        /// </summary>
        internal static bool MentionsBothTeams(DedupedNewsItem item, string? homeTeam, string? awayTeam)
            => MentionsBothTeams((item.Headline ?? "") + " " + (item.Summary ?? ""), homeTeam, awayTeam);

        internal static bool MentionsBothTeams(string? text, string? homeTeam, string? awayTeam)
        {
            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
                return false;

            var folded = NewsTextNormalizer.Fold(text);
            if (folded.Length == 0) return false;

            return NewsTextNormalizer.Mentions(folded, homeTeam)
                && NewsTextNormalizer.Mentions(folded, awayTeam);
        }

        /// <summary>
        /// OKUMA tarafı güvenlik kapıları. Depoda duran kanıtı AI'a vermeden önce YAZMA
        /// tarafıyla AYNI kurallardan geçirir: futbol triyajı + içerik türü + tarihsel içerik
        /// + kalite + maç bağlama. Freshness çağıran tarafta (repository) uygulanır.
        ///
        /// Kapılar yazma tarafına sonradan eklendiği için depoda kapıdan geçmemiş ESKİ
        /// kayıtlar bulunur; bu metot onların AI'ın factual katmanına sızmasını engeller.
        /// </summary>
        public List<MatchEvidence> ApplyReadGates(
            IEnumerable<MatchEvidence> evidence, string? homeTeam, string? awayTeam,
            int minQuality, bool requireBothTeams, DateTime? kickoffUtc = null,
            IEnumerable<string>? knownPlayerNames = null)
        {
            var teams = new[] { homeTeam ?? "", awayTeam ?? "" }.Where(t => t.Length > 0).ToArray();
            var kept = new List<MatchEvidence>();

            foreach (var e in evidence)
            {
                var text = (e.Headline ?? "") + " " + (e.Summary ?? "");

                if (!IsFootballRelevant(text, teams)) continue;
                if (IsNonFactualContent(e.Headline, e.Summary)) continue;
                if (IsBareFixtureTitle(e.Headline, e.Summary, homeTeam, awayTeam)) continue;
                if (IsHistoricalContent(text, kickoffUtc)) continue;

                // Kalite: kayıtta yazılı değer ESKİ (dar) whitelist ile hesaplanmış olabilir;
                // kaynağı bugünkü çözücüyle yeniden değerlendirip yükseğini alırız. Böylece
                // gerçek bir Türk/kulüp kaynağı, eski kayıtta 60 yazıyor diye elenmez.
                var quality = Math.Max(e.SourceQuality, _quality.BestQuality(
                    e.Sources.Count > 0 ? e.Sources : new List<string> { e.Source }, teams));
                if (minQuality > 0 && quality < minQuality) continue;
                e.SourceQuality = quality;

                if (!string.IsNullOrWhiteSpace(homeTeam) && !string.IsNullOrWhiteSpace(awayTeam))
                {
                    var relation = ResolveRelation(
                        text, homeTeam, awayTeam, requireBothTeams,
                        kickoffUtc, e.PublishedUtc, true, true);
                    if (relation == null) continue;
                    e.Relation = relation;

                    // OLAY ÖZNESİ HER OKUMADA YENİDEN ÇÖZÜLÜR. Kanıt kaydı özne sütunu
                    // taşımaz (şema değişmedi); özne başlık+özetten deterministik olarak
                    // türetilir, böylece kapılar eklenmeden önce yazılmış kayıtlar da
                    // doğru özneyle AI'a gider.
                    var subject = ResolveSubject(
                        text, homeTeam, awayTeam, knownPlayerNames,
                        e.Sources.Count > 0 ? e.Sources : new List<string> { e.Source });

                    e.RelatedTeam = subject.Team;
                    e.OpponentTeam = subject.Opponent;

                    // Rol kararı YAZMA tarafıyla AYNI kuralı kullanır (tek kaynak).
                    var role = ResolvePersonRole(
                        text, subject.Player, ResolveEventType(e.Headline, e.Summary), knownPlayerNames);
                    e.Player = role.Player;
                    e.Coach = role.Coach;
                }

                // Kapsamı çözülmemiş maç kadrosu iddiası YAZMA tarafıyla AYNI kuralla elenir:
                // depoda kapı eklenmeden önce yazılmış kayıtlar da anlatıya sızmaz.
                if (IsUnscopedMatchdaySquadClaim(text, e.Relation)) continue;

                e.Timing = ResolveTiming(e.PublishedUtc, kickoffUtc);
                if (e.Timing == Timing.Historical) continue;

                // FUTBOL OLAYI: türü ve önemi okuma anında da belirlenir (kayıt sütunu yok).
                e.EventType = ResolveEventType(e.Headline, e.Summary);
                e.Importance = ResolveImportance(e.EventType, Math.Max(1, e.SourceCount), e.Timing);

                kept.Add(e);
            }

            return CollapseEvents(kept);
        }

        /// <summary>
        /// OLAY TEKİLLEŞTİRME — aynı gelişmeyi anlatan kayıtlar TEK OLAY + ÇOKLU KAYNAK olur.
        ///
        /// Neden okuma tarafında: yazma tarafındaki dedup yalnız TEK tarama turunun içinde
        /// çalışır. Aynı olay 5 dakika sonraki turda başka bir yayıncının başlığıyla tekrar
        /// gelir ve ikinci bir kayıt olur. Depoda biriken bu kopyalar burada, olay anahtarı
        /// (<see cref="NewsTextNormalizer.EventKey"/>) ile tek kayda iner; temsilci en
        /// güvenilir/en açıklayıcı olandır, KaynakSayısı gerçek yayıncı sayısıdır.
        /// </summary>
        public static List<MatchEvidence> CollapseEvents(IEnumerable<MatchEvidence> evidence)
        {
            // BİREBİR ANAHTAR YETMEZ: aynı olayın başlıkları yayıncıdan yayıncıya kelime
            // ekler/çıkarır ("Fenerbahçe'de X sakatlandı" / "X sakatlandı: Fenerbahçe'de
            // kadro dışı"). Bu yüzden gruplama, ek-kırpılmış gövde kelimeleri üzerinden
            // ÖRTÜŞME ile yapılır; ilk eşleşen gruba katılır (deterministik, tek geçiş).
            var groups = new List<(HashSet<string> Tokens, List<MatchEvidence> Items)>();

            foreach (var e in evidence.OrderByDescending(x => x.SourceQuality)
                                      .ThenByDescending(x => x.PublishedUtc))
            {
                var tokens = NewsTextNormalizer.EventTokens(e.Headline);
                var hit = groups.FirstOrDefault(g => NewsTextNormalizer.SameEvent(g.Tokens, tokens));

                if (hit.Items != null)
                {
                    hit.Items.Add(e);
                    hit.Tokens.UnionWith(tokens);   // grup gövdesi zenginleşir
                }
                else
                {
                    groups.Add((tokens, new List<MatchEvidence> { e }));
                }
            }

            var result = new List<MatchEvidence>();
            foreach (var (_, g) in groups)
            {
                // Temsilci: ÖZNESİ ÇÖZÜLMÜŞ, gerçek özeti olan, en kaliteli, en yeni kayıt.
                // Özne önceliklidir: aynı olayın bir başlığı özneyi açıkça veriyorsa
                // ("Trabzonspor'un … kadrosu"), temsilci o olmalıdır.
                var rep = g.OrderByDescending(x => x.RelatedTeam.Length > 0)
                           .ThenByDescending(x => x.Summary.Length > 0)
                           .ThenByDescending(x => x.SourceQuality)
                           .ThenByDescending(x => x.PublishedUtc)
                           .First();

                // ÖZNE ÇELİŞKİSİ = ÖZNESİZLİK. Grup üyeleri farklı takımı özne gösteriyorsa
                // hiçbiri taşınmaz; yanlış özne üretmek, özne vermemekten kötüdür.
                var subjects = g.Select(x => x.RelatedTeam)
                                .Where(s => s.Length > 0)
                                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (subjects.Count > 1)
                {
                    rep.RelatedTeam = "";
                    rep.OpponentTeam = "";
                    rep.Player = "";
                }
                else if (subjects.Count == 1 && rep.RelatedTeam.Length == 0)
                {
                    var donor = g.First(x => x.RelatedTeam.Length > 0);
                    rep.RelatedTeam = donor.RelatedTeam;
                    rep.OpponentTeam = donor.OpponentTeam;
                }

                // OYUNCU ADI GRUPTAN DEVRALINIR: aynı olayın bir başlığı kişiyi kısaca
                // ("Salah"), bir diğeri tam adıyla ("Mohamed Salah") anar. Tam ad hangi
                // kayıtta geçiyorsa olayın öznesi odur.
                if (rep.Player.Length == 0)
                    rep.Player = g.Select(x => x.Player).FirstOrDefault(p => p.Length > 0) ?? "";
                if (rep.Coach.Length == 0)
                    rep.Coach = g.Select(x => x.Coach).FirstOrDefault(c => c.Length > 0) ?? "";

                var sources = g.SelectMany(x => x.Sources.Count > 0
                                   ? x.Sources
                                   : new List<string> { x.Source })
                               .Where(s => !string.IsNullOrWhiteSpace(s))
                               .Distinct(StringComparer.OrdinalIgnoreCase)
                               .ToList();

                rep.Sources = sources;
                rep.SourceCount = Math.Max(g.Sum(x => Math.Max(1, x.SourceCount)), sources.Count);
                rep.SourceQuality = g.Max(x => x.SourceQuality);
                rep.Confidence = g.Max(x => x.Confidence);
                rep.PublishedUtc = g.Max(x => x.PublishedUtc);

                result.Add(rep);
            }

            return result;
        }

        /// <summary>Evidence listesinden Reasoning'in tüketeceği context'i kurar.</summary>
        public MatchIntelligenceContext BuildContext(string formaxMatchId, List<MatchEvidence> evidence)
        {
            var signals = Histogram(evidence.Select(e => e.Type));
            var clusters = Histogram(evidence.SelectMany(e =>
                e.Cluster.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim())));

            return new MatchIntelligenceContext
            {
                FormaxMatchId = formaxMatchId,
                TotalEvidence = evidence.Count,
                TotalProviders = evidence.SelectMany(e => e.Sources.Count > 0
                                             ? e.Sources
                                             : new List<string> { e.Source })
                                         .Where(s => !string.IsNullOrWhiteSpace(s))
                                         .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Signals = signals,
                Clusters = clusters,
                Confidence = evidence.Count == 0 ? 0 : (int)Math.Round(evidence.Average(e => e.Confidence)),
                TopHeadlines = evidence.OrderByDescending(e => e.Confidence).Take(5).Select(e => e.Headline).ToList(),
                LatestHeadlines = evidence.OrderByDescending(e => e.PublishedUtc).Take(5).Select(e => e.Headline).ToList(),

                // SIRALAMA: gerçek içeriği olan, çok kaynaklı ve maç gününe yakın gelişme önce.
                TopEvidence = evidence
                    .OrderByDescending(e => e.Summary.Length > 0)
                    .ThenByDescending(e => e.SourceCount)
                    .ThenByDescending(e => e.SourceQuality)
                    .ThenByDescending(e => e.Confidence)
                    .ThenByDescending(e => e.PublishedUtc)
                    .Take(8).ToList()
            };
        }

        private static Dictionary<string, int> Histogram(IEnumerable<string> values)
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in values)
            {
                if (string.IsNullOrWhiteSpace(v)) continue;
                d[v] = d.TryGetValue(v, out var n) ? n + 1 : 1;
            }
            return d;
        }
    }
}
