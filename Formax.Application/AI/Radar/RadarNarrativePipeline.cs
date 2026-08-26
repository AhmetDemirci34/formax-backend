using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.LLM;
using Formax.Application.AI.Radar.Reasoning;
using Microsoft.Extensions.Logging;

namespace Formax.Application.AI.Radar
{
    /// <summary>
    /// FORMAX Radar v3 — anlatı hattı (pipeline):
    ///   Context → ReasoningEngine → Intelligence Pack → Prompt → LLM → Guard → Snapshot.
    ///
    /// LLM artık ham context değil, Reasoning Layer'ın hazırladığı Intelligence Pack'i
    /// okur. LLM boş/geçersiz/kapalı olduğunda deterministik fallback üretir → çıktı asla
    /// boş kalmaz. Aynı context için sonuç cache'lenir.
    /// </summary>
    public sealed class RadarNarrativePipeline
    {
        private readonly ILLMClient _llm;
        private readonly ReasoningEngine _reasoning;
        private readonly RadarPromptComposer _composer;
        private readonly RadarOutputGuard _guard;
        private readonly IRadarNarrativeStore _store;
        private readonly ILogger<RadarNarrativePipeline> _logger;

        public RadarNarrativePipeline(
            ILLMClient llm,
            ReasoningEngine reasoning,
            RadarPromptComposer composer,
            RadarOutputGuard guard,
            IRadarNarrativeStore store,
            ILogger<RadarNarrativePipeline> logger)
        {
            _llm = llm;
            _reasoning = reasoning;
            _composer = composer;
            _guard = guard;
            _store = store;
            _logger = logger;
        }

        public async Task<RadarNarrativeResult> GenerateAsync(
            MatchIntelligenceContext ctx,
            RadarSurface surface,
            bool aiAllowed,
            CancellationToken ct = default)
        {
            var key = $"{surface}:{ctx.MatchId}:{Hash(ctx.ToPromptJson())}";

            if (_store.TryGet(key, out var cached))
                return cached;

            // Reasoning Layer: ham context'ten Intelligence Pack üret (FORMAX'ın beyni).
            var pack = _reasoning.Build(ctx);

            RadarNarrativeResult? result = null;

            if (aiAllowed)
            {
                try
                {
                    var raw = await _llm.GenerateAsync(_composer.System(surface), _composer.User(surface, pack), ct);
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        result = ParseAndGuard(raw, surface);
                        // VERİ SINIRI DENETİMİ: prompt kuralına uyulmadığında sistem zorlar.
                        if (result != null)
                        {
                            string Primary(RadarNarrativeResult x) => surface switch
                            {
                                RadarSurface.Discover => x.RadarSummary,
                                RadarSurface.AiIncele => x.AiIncele,
                                _ => x.MatchReport
                            };

                            var before = Primary(result);
                            EnforceDataBoundaries(result, pack, surface);
                            var primary = Primary(result);

                            // Ana alan tamamen elendiyse anlatı kullanılamaz → fallback.
                            // TEŞHİS: metin ÜRETİLDİĞİ hâlde denetimden geçemediyse bunu
                            // görmeden hangi kuralın fazla eleştiğini anlamak mümkün olmuyor.
                            if (string.IsNullOrWhiteSpace(primary))
                            {
                                _logger.LogWarning(
                                    "[RADAR] {Surface} maç {MatchId}: LLM metni denetimden geçemedi → fallback. Elenen metin: {Text}",
                                    surface, ctx.MatchId, before);
                                result = null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[RADAR] LLM anlatısı başarısız — fallback");
                }
            }

            if (result == null)
            {
                // Fallback metni de kullanıcıya gider → aynı veri sınırı denetiminden geçer.
                result = Fallback(ctx, pack, surface);
                EnforceDataBoundaries(result, pack, surface);
            }
            result.ReasoningConfidence = pack.ReasoningConfidence; // her iki yolda da Reasoning skoru

            // OLASI SONUÇ ETİKETİ LLM'DEN GELMEZ. Sonuç adı, sırası ve sayısı
            // MarketProbabilityEngine'in ürettiği değerdir; model yalnız GEREKÇE cümlesini
            // yazar. Ölçüldü: model etiketi yeniden yazarken Türkçe karakterleri düşürüyordu
            // ("Kasımpaşa Gol Atar" → "Kasimpasa Gol Atar"). Etiketi modele yazdırmak
            // gereksizdi; backend değeri doğrudan bağlanır (hesap DEĞİŞMEZ).
            BindCanonicalMarkets(result, pack);

            // Aynı gerekçeyle takım adı: canonical ad backend'de vardır, modelin yeniden
            // yazmasına gerek yoktur. Anlatı metnindeki bozulmuş yazımlar canonical ada
            // geri çevrilir (metnin kendisi korunur, yalnız ad düzeltilir).
            NormalizeTeamNames(result, pack);

            _store.Set(key, result);
            return result;
        }

        // ── BACKEND SONUCU = TEK DOĞRULUK KAYNAĞI ──────────────────────────────

        /// <summary>
        /// Olası Sonuçları backend değerine sabitler: ad, sıra ve sayı paketten gelir;
        /// modelden YALNIZ gerekçe cümlesi alınır. Model bir sonucu hiç açıklamadıysa
        /// gerekçe boş kalır — uydurma gerekçe üretilmez. Yüzde ve olasılık hesabına
        /// dokunulmaz (bu metot hiçbir sayı okumaz/yazmaz).
        /// </summary>
        private static void BindCanonicalMarkets(RadarNarrativeResult r, IntelligencePack pack)
        {
            var backend = pack.Scenarios;
            if (backend == null || backend.Count == 0)
            {
                r.ScenarioReasons.Clear();
                r.ScenarioExplanations.Clear();
                return;
            }

            r.ScenarioReasons = Rebind(r.ScenarioReasons, backend);
            r.ScenarioExplanations = Rebind(r.ScenarioExplanations, backend);
        }

        private static List<RadarScenarioReason> Rebind(
            List<RadarScenarioReason> produced, List<IntelligencePack.ScenarioInsight> backend)
        {
            var used = new bool[produced?.Count ?? 0];
            var bound = new List<RadarScenarioReason>(backend.Count);

            for (var i = 0; i < backend.Count; i++)
            {
                var market = backend[i].Market;
                var reason = "";

                // 1) Modelin yazdığı etiketle eşleştir (diakritik/boşluk duyarsız).
                if (produced != null)
                {
                    for (var j = 0; j < produced.Count; j++)
                    {
                        if (used[j]) continue;
                        if (!SameLabel(produced[j].Market, market)) continue;
                        reason = produced[j].Reason ?? "";
                        used[j] = true;
                        break;
                    }

                    // 2) Eşleşme yoksa aynı sıradaki gerekçeye düş (model sırayı korur).
                    if (reason.Length == 0 && i < produced.Count && !used[i])
                    {
                        reason = produced[i].Reason ?? "";
                        used[i] = true;
                    }
                }

                bound.Add(new RadarScenarioReason { Market = market, Reason = reason });
            }
            return bound;
        }

        /// <summary>İki etiket aynı sonucu mu gösteriyor? Diakritik ve boşluk farkı önemsizdir.</summary>
        private static bool SameLabel(string? a, string? b) =>
            FoldLabel(a).Equals(FoldLabel(b), System.StringComparison.Ordinal);

        private static string FoldLabel(string? s) =>
            Services.News.Intelligence.NewsTextNormalizer.Alphanumeric(s);

        /// <summary>
        /// Anlatı metnindeki takım adını canonical yazıma çevirir. Model "Kasımpaşa"yı
        /// "Kasimpasa" ya da "Kasım Paşa" diye yazabiliyor; kullanıcıya backend'deki ad
        /// gösterilir. Yalnız AD değişir, cümle kurulumu korunur.
        /// </summary>
        private static void NormalizeTeamNames(RadarNarrativeResult r, IntelligencePack pack)
        {
            var names = new[] { pack.HomeTeam, pack.AwayTeam }
                .Where(n => !string.IsNullOrWhiteSpace(n) && n!.Length >= 4)
                .ToList();
            if (names.Count == 0) return;

            string Fix(string? text)
            {
                var s = text ?? "";
                foreach (var name in names) s = RepairTruncatedTeamName(s, name!);
                foreach (var name in names) s = ReplaceTeamName(s, name!);
                foreach (var name in names) s = RepairMisspelledTeamName(s, name!);
                return s;
            }

            r.RadarSummary = Fix(r.RadarSummary);
            r.MatchReport = Fix(r.MatchReport);
            r.WhyThisMatch = Fix(r.WhyThisMatch);
            r.ReasoningSummary = Fix(r.ReasoningSummary);
            r.NewsSummary = Fix(r.NewsSummary);
            r.SocialSummary = Fix(r.SocialSummary);
            r.StatisticalSummary = Fix(r.StatisticalSummary);
            r.EvidenceSummary = Fix(r.EvidenceSummary);
            r.AiIncele = Fix(r.AiIncele);
            r.Highlights = r.Highlights.Select(Fix).ToList();
            r.KeyInsights = r.KeyInsights.Select(Fix).ToList();
            foreach (var s in r.ScenarioReasons) s.Reason = Fix(s.Reason);
            foreach (var s in r.ScenarioExplanations) s.Reason = Fix(s.Reason);
        }

        /// <summary>
        /// KIRPILMIŞ TAKIM ADI ONARIMI — canonical ad geri yazılır.
        ///
        /// Ölçüldü: model bir yüzeyde "Kasımpaşa" yerine "Kasım..." yazdı (aynı maçın diğer
        /// iki yüzeyinde ad tamdı). Üç nokta cümle sonu sanıldığı için ardındaki "ise henüz
        /// galibiyet alamamış." parçası da öznesiz kaldı. Burada ad TAMAMLANIR; böylece
        /// cümle bölünmesi de oluşmaz. Yalnız canonical adın GERÇEK ön eki (en az 4 harf)
        /// + kısaltma işareti eşleşir — başka hiçbir metne dokunulmaz.
        /// </summary>
        private static string RepairTruncatedTeamName(string text, string canonical)
        {
            if (text.Length == 0 || canonical.Length < 6) return text;

            for (var len = canonical.Length - 1; len >= 4; len--)
            {
                var prefix = canonical[..len];
                var pattern = @"\b" + System.Text.RegularExpressions.Regex.Escape(prefix)
                              + @"(\.{2,}|…)";
                if (!System.Text.RegularExpressions.Regex.IsMatch(text, pattern, Rx)) continue;

                text = System.Text.RegularExpressions.Regex.Replace(text, pattern, canonical, Rx);
            }
            return text;
        }

        /// <summary>
        /// YANLIŞ HARFLİ TAKIM ADI ONARIMI — canonical ad geri yazılır.
        ///
        /// Ölçüldü: model "Kasımpaşa" yerine "Kasımspasa" yazdı (harf eklemiş). Diakritik
        /// deseni bunu yakalayamaz. Burada metindeki tek kelimeler canonical adla
        /// KARŞILAŞTIRILIR (diakritiksiz, küçük harf) ve en fazla BİR harf farkı varsa
        /// canonical ada çevrilir. Eşik dar tutulur: iki farklı takımın adı bir harfle
        /// ayrılmıyorsa yanlış eşleşme olmaz.
        /// </summary>
        private static string RepairMisspelledTeamName(string text, string canonical)
        {
            var target = Services.News.Intelligence.NewsTextNormalizer.Alphanumeric(canonical);
            if (text.Length == 0 || target.Length < 6) return text;

            return System.Text.RegularExpressions.Regex.Replace(
                text, @"\p{Lu}[\p{L}]{4,}",
                m =>
                {
                    var word = Services.News.Intelligence.NewsTextNormalizer.Alphanumeric(m.Value);
                    if (word.Equals(target, System.StringComparison.Ordinal)) return m.Value;
                    if (System.Math.Abs(word.Length - target.Length) > 1) return m.Value;
                    return EditDistanceAtMostOne(word, target) ? canonical : m.Value;
                });
        }

        /// <summary>İki dizi arasında en fazla bir ekleme/silme/değiştirme var mı.</summary>
        private static bool EditDistanceAtMostOne(string a, string b)
        {
            if (a == b) return true;

            int i = 0, j = 0, diff = 0;
            while (i < a.Length && j < b.Length)
            {
                if (a[i] == b[j]) { i++; j++; continue; }
                if (++diff > 1) return false;

                if (a.Length > b.Length) i++;
                else if (a.Length < b.Length) j++;
                else { i++; j++; }
            }
            return diff + (a.Length - i) + (b.Length - j) <= 1;
        }

        /// <summary>
        /// Takım adının bozulmuş yazımlarını canonical ada çevirir. Desen, canonical adın
        /// harflerinden kurulur: her harf kendi diakritik varyantlarını kabul eder ve
        /// harfler arasında TEK bir boşluk bulunabilir ("Kasım Paşa"). Ad zaten doğruysa
        /// değişiklik olmaz.
        /// </summary>
        private static string ReplaceTeamName(string text, string canonical)
        {
            if (text.Length == 0) return text;

            // DESEN HARFLER ARASINA kurulur; SONA boşluk eklenmez. Ölçüldü: son harften
            // sonra da "\s?" konunca desen kendisinden sonraki boşluğu yutuyor ve
            // "Kasımpaşa evinde" → "Kasımpaşaevinde" oluyordu (kelimeler birleşiyordu).
            var pieces = new List<string>();
            foreach (var ch in canonical)
            {
                if (char.IsWhiteSpace(ch)) { pieces.Add(@"\s*"); continue; }
                pieces.Add(char.IsLetterOrDigit(ch)
                    ? CharClass(ch)
                    : System.Text.RegularExpressions.Regex.Escape(ch.ToString()));
            }
            if (pieces.Count == 0) return text;

            var sb = new System.Text.StringBuilder(@"\b");
            for (var i = 0; i < pieces.Count; i++)
            {
                sb.Append(pieces[i]);
                var isLast = i == pieces.Count - 1;
                var nextIsSpace = !isLast && pieces[i + 1] == @"\s*";
                if (!isLast && pieces[i] != @"\s*" && !nextIsSpace) sb.Append(@"\s?");
            }
            sb.Append(@"\b");

            try
            {
                return System.Text.RegularExpressions.Regex.Replace(
                    text, sb.ToString(), canonical, Rx,
                    System.TimeSpan.FromMilliseconds(200));
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException) { return text; }
        }

        /// <summary>Bir harfin kabul edilen diakritik varyantları.</summary>
        private static string CharClass(char ch) => char.ToLowerInvariant(ch) switch
        {
            'c' or 'ç' => "[cç]",
            'g' or 'ğ' => "[gğ]",
            'i' or 'ı' or 'î' => "[iıîí]",
            'o' or 'ö' => "[oö]",
            's' or 'ş' => "[sş]",
            'u' or 'ü' or 'û' => "[uüû]",
            'a' or 'â' => "[aâ]",
            'e' => "[eê]",
            _ => System.Text.RegularExpressions.Regex.Escape(ch.ToString())
        };

        // ── VERİ SINIRI DENETİMİ (deterministik) ───────────────────────────────
        // Ölçüldü: prompt kuralı + veri kilidi yetmedi; model 5 maçın 2'sinde "deplasmanda iki
        // galibiyet" (böyle bir kırılım YOK), "sadece bir galibiyet" (pakette galibiyet YOK) ve
        // kadro alanı hiç yokken "kadrolar açıklandı" yazdı. Bu denetim, kuralı ihlal eden
        // CÜMLEYİ atar; metnin geri kalanı korunur. Sistem gerçeği zorlar, modele güvenmez.
        private static readonly System.Text.RegularExpressions.RegexOptions Rx =
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant;

        /// <summary>İç sistem terimleri — kullanıcı metnine giremez.</summary>
        private const string TechPattern =
            @"\b(pack|sinyal\w*|signal\w*|evidence|form\s*skoru|güç\s*skoru|oynanma\w*|" +
            @"reasoning\w*|senaryo\w*|probability|confidence|availability|hasdata|importance\w*|" +
            // Ölçüldü: model iç etiketleri kullanıcı diline çeviriyormuş gibi yapıp
            // "haber akışı yoğun", "güç dengesi Derby lehine" yazıyordu; bunlar futbol
            // cümlesi değil, sistemin ölçüm dilidir.
            @"haber\s*(hacmi|akışı)|güç\s*dengesi|form\s*gösterge\w*|" +
            // "Maçın önemi düşük/yüksek" pakette bir ETİKETTİR, futbol cümlesi değil;
            // ölçüldü, model bunu olduğu gibi kullanıcıya yazdı.
            @"maçın\s*önemi|topluluk\s*ilgisi)\b";

        /// <summary>
        /// Ev/deplasman KIRILIMLI form iddiası — veride böyle bir döküm yok.
        /// SON SÖZCÜKTE KELİME SINIRI ARANMAZ: ölçüldü, "evinde son iki mağlubiyeti"
        /// cümlesi yalnızca "mağlubiyet" ek aldığı için (mağlubiyet+i) desenden kaçıyor
        /// ve ev/deplasman kırılımı kullanıcıya çıkıyordu.
        /// </summary>
        private const string HomeAwayFormPattern =
            @"\b(evinde|evindeki|deplasmanda|deplasmandaki|iç\s*saha\w*|dış\s*saha\w*)\b[^.]{0,70}?" +
            @"\b(galibiyet|beraberlik|mağlubiyet|son\s*(beş|5)|form|performans)";

        /// <summary>Kadro/eksik oyuncu — Availability yokken hiç konuşulamaz.</summary>
        private const string SquadPattern =
            @"\b(kadro\w*|ilk\s*11|ilk\s*on\s*bir|sakat\w*|eksik\s+oyuncu|oyuncu\s+yokluğu)\b";

        /// <summary>
        /// Hava/zemin — FORMAX anlatısında hiç yok. SAHA KOŞULU da buraya dâhildir:
        /// ölçüldü (Celta–Osasuna), model bir haberden "sahadaki mantar istilası" ve
        /// "saha koşulları nedeniyle erteleme" cümleleri kurdu; zemin FORMAX'ın konusu değil.
        /// </summary>
        private const string WeatherPattern =
            @"\b(hava|hava\s*durumu|yağmur\w*|rüzgâr\w*|rüzgar\w*|sıcaklık|zemin\w*|" +
            @"saha\s*koşul\w*|saha\s*şartlar\w*|çim\w*|mantar\w*)\b";

        /// <summary>
        /// MEKÂN — pakette stat, şehir, saha ya da seyirci bilgisi YOKTUR. Ölçüldü
        /// (Charlton–Derby): model kendi dünya bilgisinden "The Valley'de" yazdı. Doğru
        /// olması hatayı değiştirmez: pakette olmayan bilgi anlatıya giremez.
        /// </summary>
        private const string VenuePattern =
            @"\b(stad\w*|stadyum\w*|arena|tribün\w*|seyirci\w*|taraftarların\s*önünde)\b";

        /// <summary>
        /// Puan durumu / lig konumu iddiası. Tablo pakette YOKSA bu cümlelerin hiçbiri
        /// kurulamaz; VARSA sayı ve konum tabloyla doğrulanır.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex StandingsClaim =
            // SIRA YAZIYLA DA YAZILABİLİR. Ölçüldü (Utrecht–AZ): "ligde dördüncü sırada
            // gelen konuk" cümlesi yalnız rakam arandığı için denetimden kaçmıştı.
            new(@"\b(\d{1,2})\s*\.\s*(sıra\w*|basamak\w*)" +
                @"|\b(birinci|ikinci|üçüncü|dördüncü|beşinci|altıncı|yedinci|sekizinci|" +
                @"dokuzuncu|onuncu|sonuncu)\s*(sıra\w*|basamak\w*)" +
                @"|\b(sıralamada|puan\s*durumu\w*|puan\s*tablosu\w*|averaj\w*|" +
                @"lider\w*|zirve\w*|son\s*sıra\w*|alt\s*sıra\w*|üst\s*sıra\w*|dip\w*|" +
                @"küme\s*düşme\w*|düşme\s*hattı\w*|play[-\s]?off\w*|şampiyonluk\s*yarış\w*|" +
                @"avrupa\s*kupaları|puan\s*yarış\w*)\b", Rx);

        /// <summary>Sıra numarasını YALNIZ sayıyla eşleştirmek için (grup 1 dolu olduğunda).</summary>
        private static readonly System.Text.RegularExpressions.Regex PositionNumber =
            new(@"\b(\d{1,2})\s*\.\s*(sıra|basamak)", Rx);

        /// <summary>
        /// Hafta/aşama iddiası — pakette "Hafta" ya da "Asama" yoksa kurulamaz.
        /// Ölçüldü: pakette hafta yokken model puan sırasından "beşinci hafta" üretti;
        /// aşama yokken lig maçını "play-off yarışı" diye anlattı.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex WeekClaim =
            new(@"\b(\d{1,2}|birinci|ikinci|üçüncü|dördüncü|beşinci|altıncı|yedinci|sekizinci)\s*\.?\s*hafta" +
                @"|\bhafta\s*(\d{1,2})\b", Rx);

        private static readonly System.Text.RegularExpressions.Regex StageClaim =
            new(@"\b(çeyrek\s*final\w*|yarı\s*final\w*|final\w*|grup\s*aşama\w*|eleme\s*tur\w*|" +
                @"play[-\s]?off\s*tur\w*|tur\s*atlama\w*|rövanş\w*|ilk\s*maç\w*\s*ardından)\b", Rx);

        /// <summary>
        /// KADRO DURUMU İDDİASI — "iki oyuncu sakat", "3 cezalı", "dört ismin durumu belirsiz".
        /// Sayı ve statü birlikte okunur; statüler ASLA birleştirilemez.
        /// </summary>
        /// <remarks>
        /// PARAFRAZ DA SAYILIR. Ölçüldü (Atletico Madrid–Malaga, 19.08.2026): model statü
        /// kelimesini hiç kullanmadan "Malaga tarafında ALTI OYUNCUNUN DURUMUNUN BELİRSİZ
        /// olması" yazdı; gerçekte üç sakat + üç şüpheli vardı, yani iki AYRI statü
        /// toplanmıştı. Desen yalnız "sakat/cezalı/şüpheli" kelimelerini aradığı için
        /// (docstring'i "durumu belirsiz"i kapsadığını söylese de) cümle guard'dan geçti.
        /// "durumu belirsiz / durumu net değil / durumu soru işareti" artık ŞÜPHELİ statüsünün
        /// eşdeğeri olarak okunur ve aynı sayı denetimine girer.
        /// </remarks>
        private static readonly System.Text.RegularExpressions.Regex AvailabilityClaim =
            new(@"(\d+|bir|iki|üç|dört|beş|altı|yedi|sekiz|dokuz|on|tek|hiç)\s+" +
                @"(?:oyuncu\w*\s+|isim\w*\s+|futbolcu\w*\s+)?" +
                @"(sakat|cezalı|cezali|şüpheli|supheli|kadro\s*dışı" +
                @"|durum\w*\s+(?:belirsiz|net\s+değil|soru\s+işareti|meçhul))", Rx);

        /// <summary>
        /// STATÜ BİRLEŞTİRME — "sakat ve şüpheli", "sakat/cezalı", "sakat ya da cezalı".
        /// Sakat ≠ Cezalı ≠ Şüpheli; hiçbiri diğerinin yerine ya da toplamı olarak anlatılamaz.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex AvailabilityMerge =
            new(@"\b(sakat\w*|cezalı|cezali|şüpheli|supheli)\s*(ve|veya|ya\s*da|ile|/|,)\s*" +
                @"(sakat\w*|cezalı|cezali|şüpheli|supheli)\w*\s*(oyuncu\w*|isim\w*|futbolcu\w*|sayı\w*)", Rx);

        /// <summary>
        /// PAKET DIŞI ÖZEL AD — "The Valley'de" gibi, yer/kurum adı gibi çekim eki almış
        /// iki sözcüklü özel adlar. Adın ilk sözcüğü pakette geçmiyorsa o ad pakette YOKTUR.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex ProperNounLocative =
            new(@"\b(\p{Lu}[\p{Ll}]{1,})\s+(\p{Lu}[\p{Ll}]{2,})['’](d[ae]|t[ae]|nd[ae]|n[ıi]n|n[uü]n)\b",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>
        /// KİŞİ ADI — "Ad Soyad" biçimindeki iki sözcüklü özel adlar. Pakette geçmeyen bir
        /// kişi anlatıda kullanılamaz. Ölçüldü (Gençlerbirliği–Fenerbahçe): pakette hiç
        /// geçmeyen "Mert Günok" kaleci olarak anlatıya girdi ve sakat gösterildi — model
        /// bunu kendi dünya bilgisinden üretmişti. Kadro bloğu yalnız SAYI taşır, isim
        /// taşımaz; isim ancak bir haber olayında geçiyorsa pakette bulunur.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex PersonName =
            new(@"\b(\p{Lu}[\p{Ll}]{2,})\s+(\p{Lu}[\p{Ll}]{2,})\b",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>
        /// HAM İSTATİSTİK SAYISI — ondalık ortalama ("maç başına 1.7 gol") ve yüzde
        /// ("%80 atak verimliliği"). Ölçüldü (Celta–Osasuna, Watford–Southampton): model
        /// TakimIstatistikleri bloğundaki sayıları olduğu gibi kullanıcıya döktü. Bu
        /// sayılar anlatının değil, motorun dilidir; anlatı onları futbol diline çevirir.
        /// İzinli sayılar (form dökümü ve skorlar) tam sayıdır, bu desene takılmaz.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex RawStatNumber =
            new(@"\d+[.,]\d+|%\s*\d+|\byüzde\s*\d+", Rx);

        /// <summary>
        /// TEK SÖZCÜKLÜ ÖZEL AD + çekim eki ("Amsterdam'ın"). Pakette hiç geçmiyorsa o ad
        /// pakette yoktur. Ölçüldü (Ajax–Heerenveen): model şehir adı ekleyip
        /// "Amsterdam'ın güçlü takımı Ajax" yazdı; şehir FORMAX anlatısının konusu değil.
        /// Pakette geçen adlar (takımlar, lig, haberdeki kişiler) bu denetimden etkilenmez.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex SingleProperNounLocative =
            new(@"\b(\p{Lu}[\p{Ll}]{3,})['’](d[ae]|t[ae]|nd[ae]|n[ıi]n|n[uü]n)\b",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>Cümle bir KİŞİDEN söz ediyor mu? (Ad denetimi yalnız burada çalışır.)</summary>
        private static readonly System.Text.RegularExpressions.Regex PersonContext =
            new(@"\b(sakat\w*|cezal\w*|şüpheli|kadro\w*|transfer\w*|sözleşme\w*|imza\w*|" +
                @"kiralık|oyuncu\w*|futbolcu\w*|kaleci\w*|forvet\w*|stoper\w*|golcü\w*|" +
                @"teknik\s*direktör\w*|açıkla\w*|söyle\w*|belirtti|ismin|isimlerin)\b", Rx);

        private static void EnforceDataBoundaries(RadarNarrativeResult r, IntelligencePack pack, RadarSurface surface)
        {
            var squadAllowed = pack.Availability is { HasData: true };
            var allowed = new HashSet<int>
            {
                pack.Form.HomeWins, pack.Form.AwayWins,
                pack.Form.HomeDraws, pack.Form.AwayDraws,
                pack.Form.HomeLosses, pack.Form.AwayLosses
            };

            // OLAY ÖZNESİ DENETİMİ — backend haberin öznesini belirledi; model onu değiştiremez.
            // Ölçüldü: "Trabzonspor'un Kasımpaşa kadrosu … Salah da var" haberi, pakette
            // Takim=Trabzonspor yazmasına rağmen kullanıcıya "Salah'ın Kasımpaşa kadrosuna
            // katılması" diye çıktı. Prompt kuralı tek başına yetmiyor; kuralı ihlal eden
            // CÜMLE burada atılır. Yanlış özne, haberi hiç kullanmamaktan kötüdür.
            var subjectRules = BuildSubjectRules(pack);

            // SKOR DENETİMİ — pakette GEÇMEYEN hiçbir skor kullanıcıya çıkamaz. Model geçmiş
            // bilgisinden skor hatırlayamaz. İzinli skorlar doğrudan modele GÖNDERİLEN
            // paketin kendisinden toplanır (H2H, haber başlığı/özeti); başka kaynak yoktur.
            var allowedScores = CollectScores(pack.ToPromptJson());

            // FORM SAHİPLİĞİ — her G/B/M ifadesi ait olduğu takımla eşleşmeli. Ölçüldü:
            // "ikisi de iki galibiyet, iki mağlubiyet ve bir beraberlik" cümlesi bir takımın
            // formunu diğerine aktarıyordu.
            var formFacts = new FormFacts(
                pack.HomeTeam ?? "", pack.AwayTeam ?? "",
                pack.Form.HomeWins, pack.Form.HomeDraws, pack.Form.HomeLosses,
                pack.Form.AwayWins, pack.Form.AwayDraws, pack.Form.AwayLosses);

            // PAKETİN GERÇEKLERİ — aşağıdaki denetimlerin TEK dayanağı. Hiçbiri yeni bir
            // değer hesaplamaz; yalnız pakette YAZAN değeri okur.
            var facts = new PackFacts(
                squadAllowed, allowed, subjectRules, allowedScores, formFacts,
                Positions: PositionsOf(pack),
                Week: pack.Week,
                HasStage: !string.IsNullOrWhiteSpace(pack.StageLabel),
                StageLabel: pack.StageLabel,
                Injured: SideCounts(pack, s => s.Injured),
                Suspended: SideCounts(pack, s => s.Suspended),
                Doubtful: SideCounts(pack, s => s.Doubtful),
                PackText: NormalizeForLookup(pack.ToPromptJson()),
                KnownNames: BuildKnownNames(pack),
                PersonNames: BuildPersonNames(pack),

                // ZAMANSAL KESİNLİK İZNİ — backend'in kararı, cümle bazında uygulanır.
                // Dayanak yoksa izin de yoktur (form hakkında konuşulamaz).
                HomeTrendAllowed: pack.Form.Basis?.Home?.AllowsTrendClaim ?? false,
                AwayTrendAllowed: pack.Form.Basis?.Away?.AllowsTrendClaim ?? false,
                HomeSample: pack.Form.Basis?.Home?.SampleCount ?? 0,
                AwaySample: pack.Form.Basis?.Away?.SampleCount ?? 0,
                HomeInjured: pack.Availability?.Home.Injured ?? 0,
                AwayInjured: pack.Availability?.Away.Injured ?? 0,
                HomeSuspended: pack.Availability?.Home.Suspended ?? 0,
                AwaySuspended: pack.Availability?.Away.Suspended ?? 0,
                HomeDoubtful: pack.Availability?.Home.Doubtful ?? 0,
                AwayDoubtful: pack.Availability?.Away.Doubtful ?? 0);

            string Filter(string? text) => FilterSentences(text, facts);

            r.RadarSummary = Filter(r.RadarSummary);
            r.MatchReport = Filter(r.MatchReport);
            r.WhyThisMatch = Filter(r.WhyThisMatch);
            r.ReasoningSummary = Filter(r.ReasoningSummary);
            r.NewsSummary = Filter(r.NewsSummary);
            r.SocialSummary = Filter(r.SocialSummary);
            r.StatisticalSummary = Filter(r.StatisticalSummary);
            r.EvidenceSummary = Filter(r.EvidenceSummary);
            r.AiIncele = Filter(r.AiIncele);
            r.Highlights = r.Highlights.Select(Filter).Where(x => x.Length > 0).ToList();
            r.KeyInsights = r.KeyInsights.Select(Filter).Where(x => x.Length > 0).ToList();
            foreach (var s in r.ScenarioReasons) s.Reason = Filter(s.Reason);
            foreach (var s in r.ScenarioExplanations) s.Reason = Filter(s.Reason);

            // AYNI CÜMLEYİ İKİ ALANDA GÖSTERME. Ölçüldü: model haberi hem "newsSummary"
            // hem "evidenceSummary" alanına BİREBİR aynı cümleyle yazdı; kullanıcı aynı
            // metni iki kez okuyor. Gelişmenin kendisi haber alanında kalır.
            if (r.EvidenceSummary.Length > 0 &&
                string.Equals(r.EvidenceSummary.Trim(), r.NewsSummary.Trim(), StringComparison.OrdinalIgnoreCase))
                r.EvidenceSummary = "";

            // GELİŞME YOKSA GELİŞMENİN ETKİSİ DE YOKTUR. Ölçüldü: haber alanı boş kalırken
            // (gelişme anlatılamadı) kanıt alanı "transfer sürecindeki beklenmedik sakatlık
            // haberi…" diye dolu kaldı — kullanıcı neye dayandığı belli olmayan bir sonuç
            // cümlesi okuyor. Kanıt alanı haber alanına bağlıdır.
            if (surface == RadarSurface.MatchDetail && r.NewsSummary.Trim().Length == 0)
                r.EvidenceSummary = "";
        }

        private static string FilterSentences(string? text, PackFacts f)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            var squadAllowed = f.SquadAllowed;
            var allowedCounts = f.AllowedCounts;
            var subjectRules = f.SubjectRules;
            var allowedScores = f.AllowedScores;
            var formFacts = f.Form;

            var kept = new List<string>();
            var droppedPrevious = false;   // bir önceki cümle denetime takıldı mı

            // CÜMLE SINIRI: Türkçede sıra sayısı noktayla yazılır ("ligde 12. sırada",
            // "Süper Lig 1. haftada"). Eski desen burada cümleyi bölüyordu; bölünen ilk
            // parça bir denetime takılınca geriye "sırada yer alıyor." gibi BAŞSIZ bir
            // artık kalıyordu. Rakamdan sonra gelen nokta cümle sonu SAYILMAZ.
            // KISALTMA DA CÜMLE SONU DEĞİLDİR. Ölçüldü (Gençlerbirliği S.K.–Fenerbahçe):
            // takım adındaki "S.K." cümleyi ortadan bölüyordu; bölünen parçada takım adı
            // kalmadığı için form iddiası YANLIŞ takıma yazılmış sanılıp atılıyor ve
            // kullanıcıya "Gençlerbirliği S.K. Maç öncesi tablo…" gibi kırık bir metin
            // çıkıyordu. Tek büyük harften sonra gelen nokta cümleyi bitirmez.
            foreach (var sentence in System.Text.RegularExpressions.Regex.Split(
                         text, @"(?<=[.!?])(?<![0-9]\.)(?<!\b\p{Lu}\.)\s+"))
            {
                var s = sentence.Trim();
                if (s.Length == 0) continue;

                if (System.Text.RegularExpressions.Regex.IsMatch(s, TechPattern, Rx)) { droppedPrevious = true; continue; }
                if (System.Text.RegularExpressions.Regex.IsMatch(s, HomeAwayFormPattern, Rx)) { droppedPrevious = true; continue; }
                if (System.Text.RegularExpressions.Regex.IsMatch(s, WeatherPattern, Rx)) { droppedPrevious = true; continue; }
                if (System.Text.RegularExpressions.Regex.IsMatch(s, VenuePattern, Rx)) { droppedPrevious = true; continue; }
                if (!squadAllowed &&
                    System.Text.RegularExpressions.Regex.IsMatch(s, SquadPattern, Rx)) { droppedPrevious = true; continue; }
                if (RawStatNumber.IsMatch(s)) { droppedPrevious = true; continue; }
                if (SwapsHomeAway(s, f)) { droppedPrevious = true; continue; }
                if (MisstatesStandings(s, f)) { droppedPrevious = true; continue; }
                if (MisstatesWeekOrStage(s, f)) { droppedPrevious = true; continue; }
                if (MisstatesAvailability(s, f)) { droppedPrevious = true; continue; }
                if (ClaimsPlayerUnavailable(s, f)) { droppedPrevious = true; continue; }
                if (ClaimsUnsupportedTrend(s, f)) { droppedPrevious = true; continue; }
                if (MisstatesSampleSize(s, f)) { droppedPrevious = true; continue; }
                if (UsesUnknownProperNoun(s, f)) { droppedPrevious = true; continue; }
                if (HasUnknownScore(s, allowedScores)) { droppedPrevious = true; continue; }
                if (MisattributesForm(s, formFacts)) { droppedPrevious = true; continue; }
                if (HasWrongResultCount(s, allowedCounts)) { droppedPrevious = true; continue; }
                if (MisattributesSubject(s, subjectRules)) { droppedPrevious = true; continue; }

                // BİR ÖNCEKİ CÜMLE ATILDIYSA ve bu parça bağlaçla/küçük harfle başlıyorsa,
                // artık kendi başına ayakta duramaz — atılan cümlenin devamıdır. Metni
                // ortadan KESMEK yerine bu parça da bütün olarak düşer.
                if (droppedPrevious && IsDanglingFragment(s)) continue;

                kept.Add(s);
                droppedPrevious = false;
            }

            // ARTIK CÜMLE TEMİZLİĞİ: bir cümle atıldığında ondan sonraki cümle bağlaçla
            // ("… ise", "ancak", "bu yüzden") ya da küçük harfle başlıyorsa, kendi başına
            // ayakta duramaz — atılan cümlenin devamıdır. Metni ortadan KESMEK yerine o
            // parça da bütün olarak çıkarılır; kelime ortasından kırpma YAPILMAZ.
            //
            // Baştaki artık parçalar (öncesi tamamen atılmış olabilir) ayrıca temizlenir.
            while (kept.Count > 0 && IsDanglingFragment(kept[0]))
                kept.RemoveAt(0);

            return string.Join(" ", kept).Trim();
        }

        /// <summary>Baştaki parça, kendisinden önce atılmış bir cümlenin devamı mı?</summary>
        private static bool IsDanglingFragment(string sentence)
        {
            var s = sentence.TrimStart('"', '\'', '(', '“', '‘', ' ');
            if (s.Length == 0) return true;

            // Küçük harfle başlayan parça bir cümle başlangıcı değildir.
            if (char.IsLower(s[0])) return true;

            if (System.Text.RegularExpressions.Regex.IsMatch(
                    s, @"^(ise|ancak|ama|fakat|oysa|bu\s+yüzden|bu\s+nedenle|dolayısıyla|" +
                       @"buna\s+karşın|öte\s+yandan|ayrıca|üstelik|çünkü|zira)\b", Rx))
                return true;

            // KARŞILAŞTIRMA ARTIĞI: "Heerenveen'de ise iki futbolcunun durumu net değil."
            // gibi cümleler bir ÖNCEKİ tarafın cümlesine yaslanır; o cümle atıldığında
            // tek başına havada kalır. Ölçüldü (Ajax–Heerenveen): ev sahibinin kadro
            // cümlesi düştüğü hâlde konuk tarafın "…ise…" cümlesi metinde kaldı.
            return System.Text.RegularExpressions.Regex.IsMatch(
                s, @"^\S{1,30}\s+ise\b", Rx);
        }

        /// <summary>
        /// Denetimlerin dayandığı PAKET GERÇEKLERİ. Hepsi pack'te YAZAN değerdir; burada
        /// hiçbir futbol hesabı yapılmaz (sayılar okunur, karşılaştırılır).
        /// </summary>
        private sealed record PackFacts(
            bool SquadAllowed,
            HashSet<int> AllowedCounts,
            IReadOnlyList<SubjectRule> SubjectRules,
            HashSet<string> AllowedScores,
            FormFacts Form,
            HashSet<int> Positions,
            int? Week,
            bool HasStage,
            string? StageLabel,
            HashSet<int> Injured,
            HashSet<int> Suspended,
            HashSet<int> Doubtful,
            string PackText,
            HashSet<string> KnownNames,
            HashSet<string> PersonNames,
            bool HomeTrendAllowed, bool AwayTrendAllowed,
            int HomeSample, int AwaySample,
            int HomeInjured, int AwayInjured,
            int HomeSuspended, int AwaySuspended,
            int HomeDoubtful, int AwayDoubtful);

        /// <summary>
        /// Bir iddianın SAHİBİ hangi taraf? Kural form denetimiyle aynıdır: iddiadan önce
        /// anılan en yakın takım adı sahibidir. Takım belirsizse null döner (denetim uygulanmaz).
        /// </summary>
        private static bool? OwnerSide(string sentence, PackFacts f, int claimIndex)
        {
            var home = f.Form.HomeTeam;
            var away = f.Form.AwayTeam;
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)) return null;

            var upTo = Math.Max(0, claimIndex - 1);
            var lastHome = sentence.LastIndexOf(home, upTo, StringComparison.OrdinalIgnoreCase);
            var lastAway = sentence.LastIndexOf(away, upTo, StringComparison.OrdinalIgnoreCase);

            if (lastHome < 0 && lastAway < 0) return null;
            return lastHome > lastAway;
        }

        /// <summary>
        /// Bu maçın KENDİ adları: iki takım ve lig. Yer/kurum adı denetiminin beyaz listesi
        /// budur — haber başlığında geçen bir stat ya da şehir adı buraya girmez.
        /// </summary>
        private static HashSet<string> BuildKnownNames(IntelligencePack pack)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in new[] { pack.HomeTeam, pack.AwayTeam, pack.League })
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                foreach (var word in Services.News.Intelligence.NewsTextNormalizer.Fold(name)
                             .Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    if (word.Length >= 2) set.Add(word);
            }
            return set;
        }

        /// <summary>
        /// Pakette ADI GEÇEN kişiler (haber notlarının öznesi olan futbolcu/teknik direktör).
        /// Backend bu adları olayın öznesi olarak ÇÖZMÜŞTÜR; kadro kararı denetimi bu
        /// kişileri arar. Liste boşsa denetim çalışmaz (kişi adı zaten anlatıya girmez).
        /// </summary>
        private static HashSet<string> BuildPersonNames(IntelligencePack pack)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in pack.FactualEvidence?.Notes ?? new List<IntelligencePack.EvidenceNote>())
            {
                foreach (var raw in new[] { n.Player, n.Coach })
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    set.Add(raw.Trim());
                    // Tek tek ad/soyad parçaları da aranır: model ""Günok"" diye kısaltabilir.
                    foreach (var part in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        if (part.Length >= 4) set.Add(part.Trim());
                }
            }
            return set;
        }

        /// <summary>Pakette YAZAN puan durumu sıraları (yoksa boş → sıra konuşulamaz).</summary>
        private static HashSet<int> PositionsOf(IntelligencePack pack)
        {
            var set = new HashSet<int>();
            if (pack.Standings?.EvSahibi is { Sira: > 0 } h) set.Add(h.Sira);
            if (pack.Standings?.Deplasman is { Sira: > 0 } a) set.Add(a.Sira);
            return set;
        }

        /// <summary>Pakette YAZAN kadro sayıları (taraf ayrımı olmadan; iddia bunlardan biri olmalı).</summary>
        private static HashSet<int> SideCounts(
            IntelligencePack pack, Func<IntelligencePack.AvailabilityBlock.Side, int> pick)
        {
            var set = new HashSet<int>();
            if (pack.Availability is not { HasData: true } av) return set;
            set.Add(pick(av.Home));
            set.Add(pick(av.Away));
            return set;
        }

        /// <summary>Paket metnini arama için sadeleştirir (diakritik/büyük-küçük duyarsız).</summary>
        private static string NormalizeForLookup(string s) =>
            Services.News.Intelligence.NewsTextNormalizer.Fold(s);

        /// <summary>
        /// DENETİM — EV SAHİBİ / DEPLASMAN ROLÜ. Bu maçın tarafları pakette yazılıdır;
        /// model rolü ters çeviremez. Ölçüldü: "Utrecht, deplasmanda AZ Alkmaar ile
        /// karşılaşacak" (oysa Utrecht ev sahibi) ve "Heerenveen, evinde dengeyi
        /// koruyarak" (oysa Heerenveen konuk). Kural dar: takım adının hemen ardında
        /// gelen rol sözcüğü, o takımın GERÇEK rolüyle çelişiyorsa cümle atılır.
        /// </summary>
        private static bool SwapsHomeAway(string sentence, PackFacts f)
        {
            var home = f.Form.HomeTeam;
            var away = f.Form.AwayTeam;
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)) return false;

            const string AwayRole = @"deplasman(da|ında|ına)?|konuk\s*(ekip|olarak)|misafir";
            const string HomeRole = @"evinde|ev\s*sahibi|kendi\s*saha";

            var wrong = Claims(sentence, home, AwayRole) || Claims(sentence, away, HomeRole);
            if (!wrong) return false;

            // "Sevilla, Rayo Vallecano'yu evinde ağırlıyor" cümlesinde rol sözcüğü konuk
            // takımın hemen ardında geçer ama iddia DOĞRUDUR. Cümlede rolü doğru kuran bir
            // eşleşme de varsa ters okuma sayılmaz.
            var right = Claims(sentence, home, HomeRole) || Claims(sentence, away, AwayRole);
            return !right;
        }

        /// <summary>Takım adının hemen ardında (en çok 25 karakter) verilen rol geçiyor mu?</summary>
        private static bool Claims(string sentence, string team, string rolePattern) =>
            System.Text.RegularExpressions.Regex.IsMatch(
                sentence,
                System.Text.RegularExpressions.Regex.Escape(team) + @"[^.]{0,25}?\b(" + rolePattern + @")\b",
                Rx);

        /// <summary>
        /// DENETİM — LİG SIRASI. Pakette puan durumu YOKSA sıra/konum hakkında hiçbir cümle
        /// kurulamaz. VARSA yazılan sıra numarası pakettekilerden biri olmalıdır; ayrıca
        /// pakette doğrulanamayan konum iddiaları ("son sırada", "lider", "küme hattı")
        /// kullanılamaz — kullanıcının örneğindeki gibi 18. sıradaki takım "son sırada"
        /// diye anlatılamaz.
        /// </summary>
        private static bool MisstatesStandings(string sentence, PackFacts f)
        {
            if (!StandingsClaim.IsMatch(sentence)) return false;
            if (f.Positions.Count == 0) return true;   // tablo yok → sıra konuşulamaz

            foreach (System.Text.RegularExpressions.Match m in PositionNumber.Matches(sentence))
                if (int.TryParse(m.Groups[1].Value, out var n) && !f.Positions.Contains(n))
                    return true;

            // Pakette "kaçıncı sıra" dışında konum bilgisi yoktur; nitel konum iddiaları
            // (lider / son sıra / üst sıralar / küme hattı / play-off yarışı) doğrulanamaz.
            // "üst sıralar" ölçüldü: tabloda tek maç oynanmışken model iki takımı da
            // "tablonun üst sıralarında" gösterdi.
            return System.Text.RegularExpressions.Regex.IsMatch(
                sentence,
                @"\b(lider\w*|zirve\w*|son\s*sıra\w*|alt\s*sıra\w*|üst\s*sıra\w*|dip\w*|" +
                @"küme\s*düşme\w*|düşme\s*hattı\w*|play[-\s]?off\w*|şampiyonluk\s*yarış\w*|" +
                @"avrupa\s*kupaları|tablonun\s*(üst|alt)\w*)\b", Rx);
        }

        /// <summary>
        /// DENETİM — HAFTA / AŞAMA. Pakette "Hafta" yoksa hafta numarası, "Asama" yoksa
        /// tur/aşama adı kullanılamaz. Değer varsa metindeki değerle birebir uyuşmalıdır.
        /// </summary>
        private static bool MisstatesWeekOrStage(string sentence, PackFacts f)
        {
            foreach (System.Text.RegularExpressions.Match m in WeekClaim.Matches(sentence))
            {
                if (f.Week is not int week) return true;         // hafta bilgisi yok

                var raw = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                var n = WordToNumber(raw) is var v && v >= 0 ? v : OrdinalWordToNumber(raw);
                if (n > 0 && n != week) return true;             // yanlış hafta
            }

            if (StageClaim.IsMatch(sentence))
            {
                if (!f.HasStage) return true;                    // aşama bilgisi yok
                // Aşama var ama başka bir aşamadan söz ediliyorsa (ör. "Lig maçı" iken
                // "çeyrek final") iddia pakette yazmıyordur.
                var stage = Services.News.Intelligence.NewsTextNormalizer.Fold(f.StageLabel);
                var claimed = Services.News.Intelligence.NewsTextNormalizer.Fold(
                    StageClaim.Match(sentence).Value);
                if (claimed.Length > 0 && !stage.Contains(claimed[..Math.Min(4, claimed.Length)],
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static int OrdinalWordToNumber(string word) => word.ToLowerInvariant() switch
        {
            "birinci" => 1, "ikinci" => 2, "üçüncü" => 3, "dördüncü" => 4, "beşinci" => 5,
            "altıncı" => 6, "yedinci" => 7, "sekizinci" => 8,
            _ => -1
        };

        /// <summary>
        /// DENETİM — KADRO DURUMU. Sakat, cezalı ve şüpheli AYRI durumlardır: birleştiren
        /// cümle atılır ("sakat ve şüpheli oyuncu sayısı"). Sayı iddiaları da pakette YAZAN
        /// değerlerle eşleşmek zorundadır — "3 sakat + 1 şüpheli" tablosu "4 oyuncu sakat"
        /// diye anlatılamaz.
        /// </summary>
        /// <summary>
        /// ZAMANSAL KESİNLİK — yalnız YETERLİ ve TAZE form verisiyle kurulabilir.
        ///
        /// Ölçüldü (Celtic–Lask Linz, 19.08.2026): "Lask Linz'in galibiyet hasreti sürüyor"
        /// cümlesi doğru sayılara (0G/2B/3M) dayanıyordu ama o beş maç 2024 Ekim–Aralık
        /// tarihliydi (607 gün) ve yalnız Avrupa kupasındandı — takımın ulusal ligi kapsam
        /// dışı olduğu için aradaki tam sezon sistemde yok. Sayı doğru, zaman yanlış.
        ///
        /// Kural: iddianın SAHİBİ olan tarafın Son5Dayanak.GuncelFormSayilir bayrağı false
        /// ise cümle düşer. Sahibi çözülemiyorsa (takım adı geçmiyorsa) ve HİÇBİR tarafta
        /// izin yoksa yine düşer — izin verilmemiş bir zemine yaslanamaz.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex TrendClaim = new(
            @"(hasret|uzun\s*süredir|uzun\s*zamandır|son\s*dönemde|son\s*zamanlarda|"
            + @"bir\s*türlü|hâlâ\s|hala\s|serisi\w*\s*(sürüyor|devam)|serisini\s*(sürdür|koru)|"
            + @"yükselişte|düşüşte|formda|formsuz|form\s*(grafiği|çizgisi)|"
            + @"kazanamıyor|kazanamamakta|galibiyet\s*(özlemi|bekliyor)|"
            + @"art\s*arda|üst\s*üste|ivme|momentum)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        private static bool ClaimsUnsupportedTrend(string sentence, PackFacts f)
        {
            var m = TrendClaim.Match(sentence);
            if (!m.Success) return false;

            var side = OwnerSide(sentence, f, m.Index);
            if (side is bool isHome)
                return !(isHome ? f.HomeTrendAllowed : f.AwayTrendAllowed);

            // Özne çözülemedi: iki taraftan biri bile izinliyse cümleye dokunma
            // (karşılaştırmalı cümleler tek tarafa yazılamaz), ikisi de değilse düşer.
            return !f.HomeTrendAllowed && !f.AwayTrendAllowed;
        }

        /// <summary>
        /// "SON BEŞ MAÇ" DENİYORSA GERÇEKTEN BEŞ MAÇ OLMALI. Ölçüldü: örneklemde 3–4 maçlık
        /// takımlar için model doğru davranıp "son üç maçında" yazdı; ama bu davranış prompt'a
        /// bağlıydı, garantisi yoktu. Sayı, pakette YAZAN maç sayısıyla uyuşmuyorsa cümle düşer.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex SampleSizeClaim = new(
            @"son\s+(bir|iki|üç|dört|beş|altı|yedi|sekiz|dokuz|on|\d+)\s+"
            + @"(maç|müsabaka|karşılaşma|mücadele)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>
        /// GEÇMİŞ KARŞILAŞMA BAĞLAMI — "aralarındaki son dört karşılaşma" bir FORM örneklemi
        /// DEĞİLDİR; iki takımın birbiriyle oynadığı maçları sayar ve kendi kaynağından
        /// (GecmisKarsilasmalar) gelir. Ölçüldü: örneklem denetimi bu cümleleri form dökümü
        /// sanıp DOĞRU H2H bilgisini siliyordu (Rayo Vallecano–Alaves, 4/4 galibiyet).
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex HeadToHeadContext = new(
            @"(aralarında|birbirleriyle|karşılıklı\s*(maç|randevu)|geçmiş\s*(karşılaşma|randevu|maç)|"
            + @"iki\s*(takım|ekip)\s*arasında|rakiple(ri)?\s*oynadı|randevu\w*)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        private static bool MisstatesSampleSize(string sentence, PackFacts f)
        {
            // H2H cümlesi form örneklemine tabi değildir.
            if (HeadToHeadContext.IsMatch(sentence)) return false;

            foreach (System.Text.RegularExpressions.Match m in SampleSizeClaim.Matches(sentence))
            {
                var n = WordToNumber(m.Groups[1].Value);
                if (n <= 0) continue;

                var side = OwnerSide(sentence, f, m.Index);
                if (side is not bool isHome)
                {
                    // Özne belirsiz: iki taraftan HERHANGİ biri bu sayıyı destekliyorsa geç.
                    if (n != f.HomeSample && n != f.AwaySample) return true;
                    continue;
                }

                var sample = isHome ? f.HomeSample : f.AwaySample;
                if (sample > 0 && n != sample) return true;
            }
            return false;
        }

        /// <summary>
        /// OYUNCU DURUMU ≠ KADRO KARARI — son savunma hattı.
        ///
        /// Pakette bir oyuncunun BU MAÇIN kadrosunda olup olmadığına dair TEK BİR alan
        /// yoktur: Availability yalnız SAYI taşır (kaç sakat / cezalı / şüpheli), haber
        /// notları ise sakatlık DURUMUNU bildirir. Dolayısıyla adı geçen bir futbolcu
        /// için ""kadroda yok"", ""forma giyemeyecek"", ""bu maçı kaçıracak"", ""bir süre
        /// yok"" gibi bir KADRO KARARI cümlesi pakette HİÇBİR ZAMAN dayanağı olmayan bir
        /// iddiadır — prompt kuralı (6b) delinse bile burada düşer.
        ///
        /// Ölçüldü (Fenerbahçe–Lyon, 18.08.2026): elde yalnız ""Mert Günok sakat"" bilgisi
        /// varken model ""sakatlığı nedeniyle bir süre kadroda yer alamayacağı bildirildi""
        /// yazdı; oyuncu UEFA'ya bildirilen kadroda yer alıyordu.
        ///
        /// SAYISAL kadro cümleleri (""iki oyuncu sakat"") bu denetime GİRMEZ; onları
        /// <see cref=""MisstatesAvailability""/> ayrıca doğrular.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex UnavailabilityClaim = new(
            @"(kadro\s*d[ıi]\s*[şs][ıi]|kadroda\s*(yer\s*al[a-zçğıöşü]*|yok)|"
            + @"kadrodan\s*[çc][ıi]kar[ıi]l|kadrosuna\s*al[ıi]nma|kadroya\s*al[ıi]nma|"
            + @"forma\s*giy[ea]me|oynayama|sahaya\s*[çc][ıi]kama|"
            + @"ma[çc]\s*[a-zçğıöşü]*\s*ka[çc][ıi]r|ka[çc][ıi]r[a-zçğıöşü]*\s*ma[çc]|ilk\s*11'?[a-zçğıöşü]*\s*yok|"
            + @"g[öo]rev\s*alama)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>Cümle ADI GEÇEN bir oyuncu için kadro kararı iddia ediyor mu?</summary>
        private static bool ClaimsPlayerUnavailable(string sentence, PackFacts f)
        {
            if (!UnavailabilityClaim.IsMatch(sentence)) return false;

            // İddia bir KİŞİYE bağlanmışsa düşer. Kişi anılmıyorsa cümle SAYISAL kadro
            // cümlesidir (""iki oyuncu sakat"") ve kendi denetimine bırakılır.
            foreach (var name in f.PersonNames)
            {
                if (name.Length < 4) continue;
                if (sentence.Contains(name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool MisstatesAvailability(string sentence, PackFacts f)
        {
            if (!f.SquadAllowed)
                return AvailabilityClaim.IsMatch(sentence) || AvailabilityMerge.IsMatch(sentence);

            if (AvailabilityMerge.IsMatch(sentence)) return true;

            // CEZA, SAYISIZ DA OLSA DOĞRULANIR. Ölçüldü (Gençlerbirliği–Fenerbahçe):
            // pakette ŞÜPHELİ olan bir oyuncu için "Ederson'un cezası" yazıldı. Kadro
            // bloğunda cezalı yoksa VE hiçbir haber olayı cezadan söz etmiyorsa, ceza
            // pakette hiç yok demektir.
            if (System.Text.RegularExpressions.Regex.IsMatch(
                    sentence, @"\b(ceza|cezası|cezalı|cezali|men\s*cezası|kırmızı\s*kart)\w*\b", Rx)
                && f.Suspended.All(n => n == 0)
                && !f.PackText.Contains("ceza", StringComparison.Ordinal)
                && !f.PackText.Contains("suspend", StringComparison.Ordinal))
                return true;

            // SAYISIZ STATÜ İDDİASI DA TARAFA BAĞLIDIR. Ölçüldü (Beşiktaş–Eyüpspor):
            // pakette Beşiktaş'ta HİÇ sakat yokken (iki oyuncu şüpheli) model
            // "Beşiktaş'ın stoperindeki sakatlık" yazdı; sayı geçmediği için sayı denetimi
            // devreye girmiyordu. Haberlerde sakatlık varsa (pakette geçiyorsa) serbesttir.
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(sentence, @"\bsakat\w*", Rx))
            {
                if (f.PackText.Contains("sakat", StringComparison.Ordinal) ||
                    f.PackText.Contains("injur", StringComparison.Ordinal)) break;

                var side = OwnerSide(sentence, f, m.Index);
                if (side is bool isHomeSide && (isHomeSide ? f.HomeInjured : f.AwayInjured) == 0)
                    return true;
            }

            foreach (System.Text.RegularExpressions.Match m in AvailabilityClaim.Matches(sentence))
            {
                var n = WordToNumber(m.Groups[1].Value);
                if (n < 0) continue;

                var kind = Services.News.Intelligence.NewsTextNormalizer.Fold(m.Groups[2].Value);

                // "durumu belirsiz / net değil" = ŞÜPHELİ'nin doğal dildeki karşılığıdır;
                // sakatla TOPLANAMAZ. Aynı sayı denetimine sokulur ki "üç sakat + üç şüpheli"
                // birleştirilip "altı oyuncunun durumu belirsiz" diye yazılamasın.
                var isDoubtfulPhrase = kind.StartsWith("supheli", StringComparison.Ordinal)
                                    || kind.StartsWith("durum", StringComparison.Ordinal);

                var allowed = kind.StartsWith("sakat", StringComparison.Ordinal) ? f.Injured
                            : kind.StartsWith("cezal", StringComparison.Ordinal) ? f.Suspended
                            : isDoubtfulPhrase ? f.Doubtful
                            : null;

                // "kadro dışı" pakette bir durum DEĞİLDİR → hiç kullanılamaz.
                if (allowed == null) return true;
                if (!allowed.Contains(n)) return true;

                // SAYI DOĞRU AMA TARAF YANLIŞ OLABİLİR. Ölçüldü (Utrecht–AZ): pakette
                // Utrecht 5, AZ 3 sakatken model "AZ Alkmaar kadrosunda beş sakat" yazdı;
                // 5 pakette geçen bir değer olduğu için sayı denetimi bunu göremiyordu.
                // İddiadan ÖNCE anılan takım, iddianın sahibidir (form denetimiyle aynı kural).
                var side = OwnerSide(sentence, f, m.Index);
                if (side is bool isHome)
                {
                    var expected = kind.StartsWith("sakat", StringComparison.Ordinal)
                            ? (isHome ? f.HomeInjured : f.AwayInjured)
                        : kind.StartsWith("cezal", StringComparison.Ordinal)
                            ? (isHome ? f.HomeSuspended : f.AwaySuspended)
                            : (isHome ? f.HomeDoubtful : f.AwayDoubtful);   // şüpheli + parafraz
                    if (expected != n) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// DENETİM — PAKET DIŞI ÖZEL AD. Model kendi dünya bilgisinden stat/şehir/kurum adı
        /// ekleyebiliyor ("The Valley'de"). Adın ilk sözcüğü pakette hiç geçmiyorsa o bilgi
        /// pakette yoktur; doğru olması da hatayı değiştirmez.
        /// </summary>
        private static bool UsesUnknownProperNoun(string sentence, PackFacts f)
        {
            // YER ADI GİBİ ÇEKİM EKİ ALMIŞ İKİ SÖZCÜKLÜ ÖZEL AD: yalnız BU MAÇIN takımları
            // ve ligi kabul edilir. Ölçüldü (Sheffield–Birmingham): stadyum adı bir haber
            // başlığında geçtiği için "pakette var" sayılıp "Bramall Lane'de ağırlıyor"
            // cümlesi kullanıcıya çıktı — oysa mekân FORMAX anlatısının konusu değildir.
            foreach (System.Text.RegularExpressions.Match m in ProperNounLocative.Matches(sentence))
            {
                var first = Services.News.Intelligence.NewsTextNormalizer.Fold(m.Groups[1].Value);
                if (first.Length < 2) continue;
                if (!f.KnownNames.Contains(first)) return true;
            }

            foreach (System.Text.RegularExpressions.Match m in SingleProperNounLocative.Matches(sentence))
            {
                if (m.Index == 0) continue;   // cümle başındaki sözcük ad sayılmaz
                var word = Services.News.Intelligence.NewsTextNormalizer.Fold(m.Groups[1].Value);
                if (word.Length < 4) continue;
                if (!f.PackText.Contains(word, StringComparison.Ordinal)) return true;
            }

            // KİŞİ ADI: her iki parçası da pakette geçmiyorsa bu kişi pakette YOKTUR.
            // Denetim YALNIZ kişiden söz eden cümlelerde çalışır (sakatlık, ceza, transfer,
            // kadro, açıklama, pozisyon). Böylece "Sarı Lacivertliler" gibi takım lakapları
            // ya da başka çok sözcüklü öbekler kişi sanılıp anlatı gereksiz yere kısalmaz.
            if (!PersonContext.IsMatch(sentence)) return false;

            // Cümle başındaki sözcük büyük harfle başladığı için ad sanılmasın diye
            // eşleşme cümlenin İLK sözcüğünden başlamaz.
            foreach (System.Text.RegularExpressions.Match m in PersonName.Matches(sentence))
            {
                if (m.Index == 0) continue;

                var a = Services.News.Intelligence.NewsTextNormalizer.Fold(m.Groups[1].Value);
                var b = Services.News.Intelligence.NewsTextNormalizer.Fold(m.Groups[2].Value);
                if (a.Length < 3 || b.Length < 3) continue;

                // Parçalardan biri bile pakette geçiyorsa ad tanıdıktır (takım adları,
                // lig adı ve haber olaylarındaki kişiler bu yolla geçer).
                if (f.PackText.Contains(a, StringComparison.Ordinal)) continue;
                if (f.PackText.Contains(b, StringComparison.Ordinal)) continue;

                // Yalnız GERÇEK kişi adı görünümündekiler elenir: iki sözcük de sözlük
                // dışı özel ad olmalı. Türkçe cümlelerde art arda iki büyük harfli sözcük
                // ancak özel adda görülür.
                return true;
            }
            return false;
        }

        /// <summary>Bir haber olayının backend'ce belirlenmiş öznesi (takım) ve rakibi.</summary>
        private sealed record SubjectRule(string Team, string Opponent, string Player);

        /// <summary>Backend'in verdiği, takıma BAĞLI form gerçekleri.</summary>
        private sealed record FormFacts(
            string HomeTeam, string AwayTeam,
            int HomeWins, int HomeDraws, int HomeLosses,
            int AwayWins, int AwayDraws, int AwayLosses);

        private static readonly System.Text.RegularExpressions.Regex ScorePattern =
            new(@"\b\d{1,2}\s*[-–:]\s*\d{1,2}\b", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>Metindeki skorları normalize ederek toplar ("2 - 1" ve "2-1" aynıdır).</summary>
        private static HashSet<string> CollectScores(string? text)
        {
            var set = new HashSet<string>(System.StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return set;

            foreach (System.Text.RegularExpressions.Match m in ScorePattern.Matches(text))
                set.Add(NormalizeScore(m.Value));
            return set;
        }

        private static string NormalizeScore(string raw) =>
            new string(raw.Where(char.IsDigit).ToArray()) is var digits && digits.Length >= 2
                ? raw.Replace(" ", "").Replace("–", "-").Replace(":", "-")
                : raw.Trim();

        /// <summary>
        /// DENETİM A — cümlede PAKETTE BULUNMAYAN bir skor var mı? Model geçmiş maç skorunu
        /// kendi belleğinden üretemez; skor ancak H2H kaydında ya da haber metninde geçiyorsa
        /// kullanılabilir. Ölçüldü: Utrecht–AZ anlatısında pakette hiç geçmeyen "1-0" çıktı
        /// (gerçek sonuç 1-2 idi ve zaten pakete skor olarak girmiyor).
        /// </summary>
        private static bool HasUnknownScore(string sentence, HashSet<string>? allowedScores)
        {
            foreach (System.Text.RegularExpressions.Match m in ScorePattern.Matches(sentence))
            {
                var score = NormalizeScore(m.Value);
                if (allowedScores == null || !allowedScores.Contains(score)) return true;
            }
            return false;
        }

        /// <summary>Cümlede geçen "N galibiyet/beraberlik/mağlubiyet" iddiaları (konumlarıyla).</summary>
        private static readonly System.Text.RegularExpressions.Regex FormClaimPattern =
            new(@"(\d+|bir|iki|üç|dört|beş|tek|hiç)\s+(?:kez\s+)?(galibiyet|beraberlik|mağlubiyet|berabere)", Rx);

        /// <summary>Cümle iki takımı TEK bir form ifadesinde birleştiriyor mu?</summary>
        private static readonly System.Text.RegularExpressions.Regex CollectivePattern =
            new(@"\b(iki\s*takım\s*da|iki\s*ekip\s*de|ikisi\s*de|her\s*iki\s*takım|her\s*iki\s*ekip|" +
                @"iki\s*tarafın\s*da|her\s*ikisi\s*de)\b", Rx);

        /// <summary>
        /// DENETİM B — form ifadesi AİT OLDUĞU takımla uyuşuyor mu?
        ///
        /// Üç durum:
        ///   • Ortaklaştıran cümle ("ikisi de iki galibiyet…") → iddia HER İKİ takım için de
        ///     doğru olmalıdır; değilse cümle atılır.
        ///   • Tek takım anılıyor → iddia o takımın değerleriyle birebir uyuşmalıdır.
        ///   • İki takım da anılıyor → her iddia, kendisinden ÖNCE geçen en yakın takım adına
        ///     yazılır ("Utrecht üç galibiyet …, AZ tek galibiyet …") ve o takımla doğrulanır.
        /// Takım adı hiç geçmiyorsa bu denetim uygulanmaz (mevcut sayı denetimi devrededir).
        /// </summary>
        private static bool MisattributesForm(string sentence, FormFacts? f)
        {
            if (f == null) return false;
            if (string.IsNullOrWhiteSpace(f.HomeTeam) || string.IsNullOrWhiteSpace(f.AwayTeam)) return false;

            var claims = FormClaimPattern.Matches(sentence);
            if (claims.Count == 0) return false;

            var collective = CollectivePattern.IsMatch(sentence);
            var homeIdx = sentence.IndexOf(f.HomeTeam, System.StringComparison.OrdinalIgnoreCase);
            var awayIdx = sentence.IndexOf(f.AwayTeam, System.StringComparison.OrdinalIgnoreCase);

            foreach (System.Text.RegularExpressions.Match c in claims)
            {
                var n = WordToNumber(c.Groups[1].Value);
                if (n < 0) continue;
                var kind = c.Groups[2].Value.ToLowerInvariant();

                if (collective)
                {
                    // Ortak iddia: her iki takım için de doğru olmak zorunda.
                    if (Expected(f, kind, home: true) != n || Expected(f, kind, home: false) != n)
                        return true;
                    continue;
                }

                bool? isHome = null;
                if (homeIdx >= 0 && awayIdx < 0) isHome = true;
                else if (awayIdx >= 0 && homeIdx < 0) isHome = false;
                else if (homeIdx >= 0 && awayIdx >= 0)
                {
                    // İddiadan ÖNCE gelen en yakın takım adı sahibidir.
                    var before = c.Index;
                    var lastHome = sentence.LastIndexOf(f.HomeTeam, System.Math.Max(0, before - 1),
                        System.StringComparison.OrdinalIgnoreCase);
                    var lastAway = sentence.LastIndexOf(f.AwayTeam, System.Math.Max(0, before - 1),
                        System.StringComparison.OrdinalIgnoreCase);
                    if (lastHome < 0 && lastAway < 0) continue;
                    isHome = lastHome > lastAway;
                }

                if (isHome == null) continue;   // takım belirsiz → bu denetim uygulanmaz
                if (Expected(f, kind, isHome.Value) != n) return true;
            }
            return false;
        }

        private static int Expected(FormFacts f, string kind, bool home) => kind switch
        {
            "galibiyet" => home ? f.HomeWins : f.AwayWins,
            "mağlubiyet" => home ? f.HomeLosses : f.AwayLosses,
            _ => home ? f.HomeDraws : f.AwayDraws     // beraberlik / berabere
        };

        private static int WordToNumber(string word) => word.ToLowerInvariant() switch
        {
            "bir" or "tek" => 1,
            "iki" => 2,
            "üç" => 3,
            "dört" => 4,
            "beş" => 5,
            "altı" => 6,
            "yedi" => 7,
            "sekiz" => 8,
            "dokuz" => 9,
            "on" => 10,
            "hiç" => 0,
            _ => int.TryParse(word, out var v) ? v : -1
        };

        /// <summary>
        /// Pakete giren haber olaylarından özne kuralları çıkarır. Yalnız ÖZNESİ ÇÖZÜLMÜŞ ve
        /// oyuncusu belli olaylar kural üretir — belirsiz olayda kural yoktur (zaten
        /// anlatılmaması gerekir, uydurma kural üretilmez).
        /// </summary>
        private static IReadOnlyList<SubjectRule> BuildSubjectRules(IntelligencePack pack)
        {
            var notes = pack.FactualEvidence?.Notes;
            if (notes == null || notes.Count == 0) return System.Array.Empty<SubjectRule>();

            // Kişi adı oyuncu ya da teknik direktör olabilir; ikisi de olayın öznesidir ve
            // yanlış takıma yazılamaz.
            return notes
                .Where(n => !string.IsNullOrWhiteSpace(n.Team)
                         && !string.IsNullOrWhiteSpace(n.Opponent)
                         && (!string.IsNullOrWhiteSpace(n.Player) || !string.IsNullOrWhiteSpace(n.Coach)))
                .Select(n => new SubjectRule(
                    n.Team!.Trim(), n.Opponent!.Trim(),
                    (string.IsNullOrWhiteSpace(n.Player) ? n.Coach : n.Player)!.Trim()))
                .ToList();
        }

        /// <summary>
        /// Cümle, bir haber olayını YANLIŞ takıma mı yazıyor? Kural dar ve kesindir:
        /// oyuncunun adı RAKİPLE birlikte geçiyor ve gerçek takımı hiç anılmıyorsa,
        /// o cümle olayın öznesini değiştirmiştir → atılır.
        /// </summary>
        private static bool MisattributesSubject(string sentence, IReadOnlyList<SubjectRule>? rules)
        {
            if (rules == null || rules.Count == 0) return false;

            foreach (var r in rules)
            {
                // ADIN HER PARÇASI ARANIR. Ölçüldü: yalnız EN UZUN parça aranınca
                // "Muhammed Salah" kaydı için "Muhammed" aranıyor, metinde ise "Mohamed
                // Salah" geçtiği için kural hiç çalışmıyor ve yanlış atıf metinde kalıyordu.
                var parts = r.Player.Split(new[] { ' ', '.', '\'' },
                                           System.StringSplitOptions.RemoveEmptyEntries)
                                    .Where(p => p.Length >= 4)
                                    .ToList();
                if (parts.Count == 0) continue;
                if (!parts.Any(p => sentence.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;

                var playerIdx = parts
                    .Select(p => sentence.IndexOf(p, System.StringComparison.OrdinalIgnoreCase))
                    .Where(i => i >= 0)
                    .DefaultIfEmpty(-1).Min();

                var opponentIdx = sentence.IndexOf(r.Opponent, System.StringComparison.OrdinalIgnoreCase);
                var teamIdx = sentence.IndexOf(r.Team, System.StringComparison.OrdinalIgnoreCase);

                // Rakip hiç anılmıyorsa yanlış atıf yoktur.
                if (opponentIdx < 0) continue;

                // KURAL: gelişmenin GERÇEK sahibi, cümlede oyuncudan ÖNCE anılmış olmalıdır
                // (eylemi yapan taraf odur). Oyuncu ile rakip yan yana geçip gerçek takım
                // hiç anılmıyorsa ya da yalnızca CÜMLENİN SONUNDA başka bir bağlamda
                // anılıyorsa, olay rakibe mal edilmiştir.
                //   ✔ "Trabzonspor, Kasımpaşa kamp kadrosuna Mohamed Salah'ı ekledi"  (takım < oyuncu)
                //   ✘ "Mohamed Salah'ın Kasımpaşa kadrosuna eklenmesi ve Trabzonspor'un …" (oyuncu < takım)
                if (teamIdx < 0 || (playerIdx >= 0 && teamIdx > playerIdx)) return true;

                // EK KURAL — OYUNCU RAKİBİN HEMEN YANINDA. Ölçüldü (Watford–Southampton):
                // "Watford kadrosunda bir şüpheli oyuncu bulunurken, Southampton'da Caspar
                // Jander maç öncesi şüpheli durumda" cümlesinde gerçek takım (Watford)
                // cümlenin BAŞINDA geçtiği için sıra kuralı sağlanıyor, ama oyuncu bir
                // önceki sözcükte RAKİBE bağlanmış oluyor. Ad ile rakip bitişikse atıf
                // değişmiştir.
                if (playerIdx > opponentIdx && playerIdx - (opponentIdx + r.Opponent.Length) <= 12)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Cümlede geçen "… galibiyet / beraberlik / mağlubiyet" sayısı backend'in verdiği
        /// değerlerden biri değilse cümle atılır (ör. pakette 0 ve 2 varken "bir galibiyet").
        /// </summary>
        private static bool HasWrongResultCount(string sentence, HashSet<int> allowed)
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                         sentence, @"(\d+|bir|iki|üç|dört|beş)\s+(?:kez\s+)?(galibiyet|beraberlik|mağlubiyet|berabere)", Rx))
            {
                var word = m.Groups[1].Value.ToLowerInvariant();
                var n = word switch
                {
                    "bir" => 1, "iki" => 2, "üç" => 3, "dört" => 4, "beş" => 5,
                    _ => int.TryParse(word, out var v) ? v : -1
                };
                if (n >= 0 && !allowed.Contains(n)) return true;
            }
            return false;
        }

        // ── LLM JSON çıktısını parse et + guard uygula ─────────────────────────
        private RadarNarrativeResult? ParseAndGuard(string raw, RadarSurface surface)
        {
            var json = ExtractJson(raw);
            if (json == null) return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var r = new RadarNarrativeResult { IsAiGenerated = true };

                if (surface == RadarSurface.AiIncele)
                {
                    r.AiIncele = Clean(Str(root, "aiIncele"), 900);
                    r.KeyInsights = CleanList(Arr(root, "keyInsights"), 130, 3);
                    if (!_guard.IsAcceptable(r.AiIncele)) return null;
                }
                else if (surface == RadarSurface.Discover)
                {
                    // SINIRLAR ŞEMADAKİ HEDEFİN ÜSTÜNDE: prompt 500 karakterlik özet ve 100
                    // karakterlik başlık ister; buradaki tavan biraz yukarıda durur ki normal
                    // bir üretim hiç kesilmesin, kesme yalnız gerçekten taşan çıktıda devreye girsin.
                    r.RadarSummary = Clean(Str(root, "radarSummary"), 560);
                    // Radarın Öne Çıkardıkları — 2-4 dinamik bulgu (kısa cümleler).
                    r.Highlights = CleanList(Arr(root, "highlights"), 120, 4);
                    r.ScenarioReasons = Reasons(root, "scenarioReasons");
                    if (!_guard.IsAcceptable(r.RadarSummary)) return null;
                }
                else
                {
                    r.MatchReport = Clean(Str(root, "matchReport"), 1100);
                    r.WhyThisMatch = Clean(Str(root, "whyThisMatch"), 280);
                    r.ReasoningSummary = Clean(Str(root, "reasoningSummary"), 320);
                    r.NewsSummary = Clean(Str(root, "newsSummary"), 320);
                    r.SocialSummary = Clean(Str(root, "socialSummary"), 280);
                    r.StatisticalSummary = Clean(Str(root, "statisticalSummary"), 320);
                    r.KeyInsights = CleanList(Arr(root, "keyInsights"), 130, 3);
                    r.ScenarioExplanations = Reasons(root, "scenarioExplanations");
                    r.EvidenceSummary = Clean(Str(root, "evidenceSummary"), 320);
                    if (!_guard.IsAcceptable(r.MatchReport)) return null;
                }

                return r;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RADAR] LLM JSON parse hatası");
                return null;
            }
        }

        private string Clean(string? s, int max)
        {
            if (_guard.ContainsHardBan(s)) return string.Empty;
            return _guard.Sanitize(s, max);
        }

        private List<string> CleanList(IEnumerable<string> items, int maxLen, int maxCount) =>
            items.Select(x => Clean(x, maxLen))
                 .Where(x => _guard.IsAcceptable(x))
                 .Take(maxCount)
                 .ToList();

        private List<RadarScenarioReason> Reasons(JsonElement root, string prop)
        {
            var list = new List<RadarScenarioReason>();
            if (!root.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var el in arr.EnumerateArray())
            {
                var market = Clean(el.TryGetProperty("market", out var m) ? m.GetString() : null, 40);
                var reason = Clean(el.TryGetProperty("reason", out var rs) ? rs.GetString() : null, 160);
                if (!string.IsNullOrWhiteSpace(market) && _guard.IsAcceptable(reason))
                    list.Add(new RadarScenarioReason { Market = market, Reason = reason });
            }
            return list;
        }

        // ── Deterministik fallback (LLM yokken) — Reasoning Pack'ten güvenli üretim ──
        private static RadarNarrativeResult Fallback(
            MatchIntelligenceContext ctx, IntelligencePack pack, RadarSurface surface)
        {
            var r = new RadarNarrativeResult { IsAiGenerated = false };
            var stronger = ctx.Form.HomeFormScore >= ctx.Form.AwayFormScore ? ctx.HomeTeam : ctx.AwayTeam;

            // FALLBACK METNİ DE KULLANICIYA GİDER: teknik terim, yüzde ve iç alan adı içermez;
            // yalnız elde kesin olan bilgiyle (takımlar, son 5 maç dökümü) sakin bir cümle kurar.
            var homeForm = Son5(ctx.Form.HomeRecent);
            var awayForm = Son5(ctx.Form.AwayRecent);

            // FALLBACK DA ROBOTİK OLMAMALI ve üç yüzeyde AYNI metin çıkmamalı: açılış
            // cümlesi yüzeye göre değişir, form dökümü ise düz liste yerine akıcı bir
            // cümleye yedirilir. Sayılar aynıdır — yalnız ifade doğallaşır.
            var acilis = surface switch
            {
                RadarSurface.Discover => $"{ctx.HomeTeam}, {ctx.AwayTeam} ile karşılaşıyor.",
                RadarSurface.AiIncele => $"{ctx.HomeTeam} ve {ctx.AwayTeam} sahaya çıkmaya hazırlanıyor.",
                _ => $"{ctx.HomeTeam} ev sahibi olarak {ctx.AwayTeam}'ı ağırlıyor."
            };

            var summary = acilis +
                (homeForm.Length > 0 && awayForm.Length > 0
                    ? $" Son beş maçın dökümüne bakıldığında {ctx.HomeTeam} {homeForm}, " +
                      $"{ctx.AwayTeam} tarafında ise {awayForm} görünüyor."
                    : "") +
                " Maç öncesi tabloyu iki takımın son dönemdeki görüntüsü şekillendiriyor.";

            var findings = DeriveFindings(ctx, stronger);

            var scenarioReasons = ctx.Scenarios
                .Select(s => new RadarScenarioReason
                {
                    Market = s.Market,
                    Reason = s.EvidenceTags.Count > 0
                        ? $"{string.Join(", ", s.EvidenceTags.Take(2))} bu yönü destekliyor."
                        : "Güncel tablo bu yönü destekliyor."
                }).ToList();

            if (surface == RadarSurface.AiIncele)
            {
                // Yüzde ve teknik market adı KULLANICI METNİNE girmez; yüzdeler zaten ayrı
                // alanda (Probabilities) UI'a gidiyor.
                r.AiIncele = summary + " Sahadaki dengeyi iki takımın son dönemdeki görüntüsü belirleyecek.";
                r.KeyInsights = findings.Take(3).ToList();
            }
            else if (surface == RadarSurface.Discover)
            {
                r.RadarSummary = summary;
                r.Highlights = findings.Take(4).ToList();
                r.ScenarioReasons = scenarioReasons;
            }
            else
            {
                r.MatchReport = summary +
                    " İki takımın son dönemdeki görüntüsü ve karşılıklı denge, maçın seyri hakkında ipucu veriyor.";
                r.WhyThisMatch = $"{stronger} tarafı bir adım önde görünse de dengenin belirleyici olması bekleniyor.";
                r.ReasoningSummary = BuildReasoningFallback(pack);
                // Haber yoksa haber alanı BOŞ kalır; ""haber yok"" cümlesi kurulmaz.
                r.NewsSummary = "";
                // Dürüst: yalnız FORMAX iç etkileşimine dayanır; sinyal yoksa üretme.
                r.SocialSummary = ctx.Social.CommunityInterest <= 0
                    ? ""
                    : ctx.Social.Level == "Yüksek"
                        ? "FORMAX kullanıcılarının bu maça ilgisi belirgin biçimde yüksek."
                        : ctx.Social.Level == "Orta"
                            ? "FORMAX kullanıcılarının ilgisi ortalama seviyede."
                            : "FORMAX kullanıcılarından sınırlı bir ilgi var.";
                r.StatisticalSummary =
                    $"Gol üretimi ve savunma tarafındaki görüntü, {stronger} lehine sınırlı bir üstünlüğe işaret ediyor.";
                r.KeyInsights = findings.Take(3).ToList();
                r.ScenarioExplanations = scenarioReasons;
                r.EvidenceSummary = "";
            }

            return r;
        }

        /// <summary>Son 5 maçın hazır ifadesi — fallback metninde de sayılar yeniden yorumlanmaz.</summary>
        private static string Son5(string? recent)
        {
            if (string.IsNullOrWhiteSpace(recent)) return "";
            int g = recent.Count(c => c == 'G'), b = recent.Count(c => c == 'B'), m = recent.Count(c => c == 'M');
            var parts = new List<string>();
            if (g > 0) parts.Add($"{g} galibiyet");
            if (b > 0) parts.Add($"{b} beraberlik");
            if (m > 0) parts.Add($"{m} mağlubiyet");
            return parts.Count == 0 ? "" : "son beş maçta " + string.Join(", ", parts);
        }

        // Maç okuması (fallback) — teknik ad yok, yalnız Türkçe okuma cümleleri.
        private static string BuildReasoningFallback(IntelligencePack pack)
        {
            var basis = pack.Signals.Count == 0
                ? "Eldeki tablo sınırlı bir görüntü sunuyor."
                : string.Join("; ", pack.Signals.Take(2).Select(s => s.Evidence)) + ".";
            if (pack.Contradictions.Count > 0)
                basis += " " + pack.Contradictions[0];
            return basis;
        }

        // Kanıt özeti (fallback) — Evidence Pack'in derli toplu hâli.
        private static string BuildEvidenceFallback(IntelligencePack pack)
        {
            if (pack.Evidence.Count == 0) return "";
            return "Öne çıkan kanıtlar — " +
                   string.Join(", ", pack.Evidence.Take(4).Select(e => $"{e.Label}: {e.Value}")) + ".";
        }

        // "Radarın Öne Çıkardıkları" — context sinyallerinden türeyen dinamik bulgular.
        private static List<string> DeriveFindings(MatchIntelligenceContext ctx, string stronger)
        {
            var f = new List<string>();

            // FALLBACK METNİ DE KULLANICIYA GİDER → iç ölçüm dili ("haber hacmi",
            // "form göstergesi") burada da kullanılmaz; aynı gerçek futbol diliyle söylenir.
            if (ctx.News.Volume24h >= 8)
                f.Add("Bu maç son bir gündür gündemin üst sıralarında");
            else if (ctx.News.Volume24h > 0)
                f.Add("Maç çevresinde konuşulacak gelişmeler var");

            var attack = (ctx.Stats.HomeGoalScoringRate + ctx.Stats.AwayGoalScoringRate) / 2;
            var defense = (ctx.Stats.HomeCleanSheetRate + ctx.Stats.AwayCleanSheetRate) / 2;
            if (attack > defense + 5)
                f.Add("Hücum tarafı savunmadan daha güçlü görünüyor");

            if (ctx.Form.HomeFormScore >= 60 && ctx.Form.AwayFormScore >= 60)
                f.Add("İki takım da son haftalarda yükselen bir grafik çiziyor");
            else
                f.Add($"{stronger} son dönemde daha derli toplu görünüyor");

            if (ctx.Social.Level == "Yüksek")
                f.Add("Taraftar ilgisi lig ortalamasının üzerinde");

            return f;
        }

        // ── Yardımcılar ────────────────────────────────────────────────────────
        private static string? Str(JsonElement root, string prop) =>
            root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

        private static IEnumerable<string> Arr(JsonElement root, string prop)
        {
            if (!root.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
                yield break;
            foreach (var el in arr.EnumerateArray())
                if (el.ValueKind == JsonValueKind.String)
                    yield return el.GetString() ?? "";
        }

        private static string? ExtractJson(string raw)
        {
            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            return (start >= 0 && end > start) ? raw[start..(end + 1)] : null;
        }

        private static string Hash(string s)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(bytes, 0, 6);
        }
    }
}
