using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Formax.Domain.Constants;

namespace Formax.Application.Services.Outcomes
{
    /// <summary>Olasılık aileleri.</summary>
    public static class OutcomeFamilies
    {
        public const string Result = "MatchResult";
        public const string Goals = "TotalGoals";
        public const string Btts = "BothTeamsScore";
        public const string Other = "Other";

        public static string Title(string family) => family switch
        {
            Result => "Maç Sonucu",
            Goals => "Gol Beklentisi",
            Btts => "İki Takımın Gol Durumu",
            _ => "Diğer"
        };

        /// <summary>Ana kartlara giremeyen (çifte şans gibi bileşik) marketler.</summary>
        public static bool IsCompound(string marketKey)
            => marketKey is OddsMarketKeys.DoubleChance1X or OddsMarketKeys.DoubleChanceX2 or OddsMarketKeys.DoubleChance12;
    }

    public sealed class OutcomeCandidateDto
    {
        public string Family { get; set; } = string.Empty;
        public string FamilyTitle { get; set; } = string.Empty;
        public string Market { get; set; } = string.Empty;
        /// <summary>Seçim/settlement anahtarı; seçilemeyen market için null.</summary>
        public string? MarketKey { get; set; }
        /// <summary>Kullanıcıya gösterilen yüzde (kalibre, ailede yuvarlama tutarlı).</summary>
        public int Probability { get; set; }
        public double RawProbability { get; set; }
        public double CalibratedProbability { get; set; }
        public double BaselineProbability { get; set; }
        public double InformationLift { get; set; }
        public double EvidenceCoverage { get; set; }
        public string SampleQuality { get; set; } = string.Empty;
        public double Uncertainty { get; set; }
        public double SelectionScore { get; set; }
        public List<string> ReasonCodes { get; set; } = new();
        public string? Reason { get; set; }
        public string? Limitation { get; set; }
        /// <summary>
        /// Bu adayın uygunluğunu belirleyen ÖLÇÜLEN market ailesi (<see cref="MarketFamilies"/>). Görsel aile (kart grubu) ile
        /// karışmasın: "Çifte Şans (1X)" görsel olarak Diğer'de durur ama uygunluğu DoubleChance ailesinden gelir.
        /// </summary>
        public string? MeasuredFamily { get; set; }
        /// <summary>Adayın ait olduğu snapshot ve model sürümü (seçim skoru denetimi için aday düzeyinde taşınır).</summary>
        public string? SnapshotId { get; set; }
        public string ModelVersion { get; set; } = OutcomeModelVersion.Current;
        public string SelectionVersion { get; set; } = OutcomeSnapshotBuilder.SelectionVersion;
    }

    public sealed class OutcomeFamilyDto
    {
        public string Family { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<OutcomeCandidateDto> Items { get; set; } = new();
    }

    public sealed class OutcomeScoreDto
    {
        public int Home { get; set; }
        public int Away { get; set; }
        public int Probability { get; set; }
    }

    public sealed class OutcomeChecksDto
    {
        public double ResultSum { get; set; }
        public double BttsSum { get; set; }
        public double Over15Sum { get; set; }
        public double Over25Sum { get; set; }
        public double Over35Sum { get; set; }
        public double DoubleChance1XError { get; set; }
        public double DoubleChanceX2Error { get; set; }
        public double DoubleChance12Error { get; set; }
        public bool Consistent { get; set; }
    }

    /// <summary>Tek market ailesinin yayın durumu — kullanıcı yüzdesi YALNIZ <see cref="Published"/> olan aileden gider.</summary>
    public sealed class OutcomeMarketStatusDto
    {
        public string Family { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        /// <summary>Organizasyon katmanı kararı (Eligible / Limited / InsufficientSample / WorseThanBaseline / CalibrationFailed / DataQualityFailed).</summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>Makine okunabilir gerekçeler — kullanıcıya teknik metin olarak GÖSTERİLMEZ.</summary>
        public List<string> ReasonCodes { get; set; } = new();
        /// <summary>İki katmanın birleşimi: organizasyon uygun VE maç kapısı yok.</summary>
        public bool Published { get; set; }
        /// <summary>Bu ailenin organizasyon sınavındaki zamansal test maçı sayısı.</summary>
        public int SampleSize { get; set; }
    }

    /// <summary>Snapshot yükü — Keşfet ve Maç Detayı bu nesnenin AYNISINI okur.</summary>
    public sealed class OutcomeSnapshotDto
    {
        /// <summary>Market ailesi bazlı yayın durumu (organizasyon × market × maç).</summary>
        public List<OutcomeMarketStatusDto> Markets { get; set; } = new();
        /// <summary>Full (≥3 kart) | Partial (1–2 kart) | NotEligible (0 kart).</summary>
        public string OverallStatus { get; set; } = OutcomeOverallStatuses.NotEligible;
        public int PublishedCardCount { get; set; }
        public string MarketPolicyVersion { get; set; } = MarketEligibilityPolicy.Version;
        public string? SnapshotId { get; set; }
        public int MatchId { get; set; }
        public string ModelVersion { get; set; } = OutcomeModelVersion.Current;
        public string? CalibrationRunId { get; set; }
        public DateTime? ComputedAtUtc { get; set; }
        public DateTime? InputsCutoffUtc { get; set; }
        /// <summary>Available | InsufficientData | Pending.</summary>
        public string Status { get; set; } = "Pending";
        public double? ExpectedHomeGoals { get; set; }
        public double? ExpectedAwayGoals { get; set; }
        public double EvidenceCoverage { get; set; }
        public string SampleQuality { get; set; } = string.Empty;
        public int HomeSampleSize { get; set; }
        public int AwaySampleSize { get; set; }
        public string? Limitation { get; set; }
        public List<OutcomeCandidateDto> MainCards { get; set; } = new();
        public List<OutcomeFamilyDto> Families { get; set; } = new();
        public List<OutcomeScoreDto> TopScores { get; set; } = new();
        public List<string> ReasonCodes { get; set; } = new();
        public OutcomeChecksDto? Checks { get; set; }
        public string? Notice { get; set; }

        /// <summary>Enabled | Limited | Disabled. Yüzdeler kullanıcıya YALNIZ Enabled'da gider.</summary>
        public string PredictionEligibility { get; set; } = PredictionEligibilities.Disabled;
        public List<string> EligibilityReasons { get; set; } = new();
        public string? TriggerType { get; set; }
        public string? PreviousSnapshotId { get; set; }
        /// <summary>Ligler arası maç bilgisi (ortak güç ölçeği) — teşhis için.</summary>
        public OutcomeStrengthDto? Strength { get; set; }
    }

    public sealed class OutcomeStrengthDto
    {
        public bool CrossLeague { get; set; }
        public int? HomeLeagueId { get; set; }
        public int? AwayLeagueId { get; set; }
        public double HomeLeagueStrength { get; set; }
        public double AwayLeagueStrength { get; set; }
        public int HomeLeagueLinks { get; set; }
        public int AwayLeagueLinks { get; set; }
        public double HomeClubRating { get; set; }
        public double AwayClubRating { get; set; }
        public double LambdaHome { get; set; }
        public double LambdaAway { get; set; }
        public double EloHomeExpectation { get; set; }
        /// <summary>Organizasyonun (maçın oynandığı lig/turnuva) ev/deplasman gol tabanı — değişim kapısı açıklaması için.</summary>
        public double LeagueHome { get; set; }
        public double LeagueAway { get; set; }
    }

    /// <summary>
    /// SNAPSHOT KURUCU — dağılımlardan bütün aileleri, tutarlılık kontrollerini, gerekçe kodlarını ve ana üç kartı üretir.
    ///
    /// ANA KART KURALI: üç kart üç FARKLI aileden (Maç Sonucu / Gol Beklentisi / İki Takımın Gol Durumu). Çifte şans (1X, X2, 12)
    /// bileşik olasılıktır — P(1X)=P(1)+P(X) — ve tek olaylardan yapısal olarak yüksek çıkar; ana kartlara ASLA giremez, yalnız
    /// "Tüm Olasılıklar" içinde gösterilir. Aile içinde seçim ham yüzdeye göre değil: kalibre olasılık + lig tabanına göre bilgi
    /// farkı (standartlaştırılmış) + veri kapsamıyla yapılır. Oran (bookmaker) hiçbir adımda kullanılmaz.
    /// </summary>
    public static class OutcomeSnapshotBuilder
    {
        /// <summary>
        /// Ana kart seçim kuralının sürümü — değişince snapshot'lar yeniden üretilir.
        /// selection-3 (18.09.2026): kartlar YALNIZ yayımlanabilir market ailelerinden seçilir; kart sayısı 0–3 arasında
        /// dinamiktir ve eksik yuva zayıf marketle DOLDURULMAZ. Sıralama bilgi değerine (Kullback–Leibler ayrışması) göredir.
        /// selection-2: üç aileden birer kart, çifte şans fiilen yasak.
        /// </summary>
        public const string SelectionVersion = "selection-3";

        /// <summary>Görsel "Maç Sonucu" kart grubu (denetim ve testler tek yerden okur).</summary>
        public const string ResultFamily = OutcomeFamilies.Result;

        /// <summary>Gol çizgileri — her biri KENDİ market ailesidir; biri zayıf diye diğeri kapanmaz.</summary>
        internal static readonly (double Line, string Family)[] GoalLines =
        {
            (1.5, MarketFamilies.TotalGoals15),
            (2.5, MarketFamilies.TotalGoals25),
            (3.5, MarketFamilies.TotalGoals35)
        };

        /// <summary>
        /// YAYIN İZNİ — iki katmanın birleşimi: organizasyon × market ailesi sınavı (tarihsel) ve bu maçın kapıları (anlık).
        /// Bir aile ancak İKİSİNDEN de geçerse kullanıcıya gider.
        /// </summary>
        public sealed class MarketPublication
        {
            private readonly Dictionary<string, (string Status, List<string> Reasons)> _organization = new();
            private readonly HashSet<string> _matchGated = new();

            /// <summary>Matris olmadan (denetim/test) kurulan görünüm: bütün aileler uygun kabul edilir.</summary>
            public static MarketPublication AllEligible { get; } = new();

            public MarketPublication() { }

            public MarketPublication(IEnumerable<MarketFamilyMetrics> organization, IEnumerable<string>? matchGatedFamilies = null)
            {
                foreach (var m in organization) _organization[m.Family] = (m.Status, m.ReasonCodes.ToList());
                foreach (var f in matchGatedFamilies ?? Array.Empty<string>()) _matchGated.Add(f);
            }

            /// <summary>Organizasyon katmanı — bu ligde bu market tarihsel olarak kanıtlandı mı?</summary>
            public string OrganizationStatus(string family)
                => _organization.Count == 0 ? MarketEligibilityStatuses.Eligible
                 : _organization.TryGetValue(family, out var v) ? v.Status
                 : MarketEligibilityStatuses.DataQualityFailed;

            public IReadOnlyList<string> OrganizationReasons(string family)
                => _organization.TryGetValue(family, out var v) ? v.Reasons : Array.Empty<string>();

            /// <summary>Maç katmanı — bu maçta bu aile için kapı var mı?</summary>
            public bool MatchGated(string family) => _matchGated.Contains(family);

            public bool IsPublished(string family)
                => OrganizationStatus(family) == MarketEligibilityStatuses.Eligible && !MatchGated(family);

            public List<string> ReasonsFor(string family)
            {
                var list = new List<string>(OrganizationReasons(family));
                if (MatchGated(family)) list.Insert(0, "MATCH_LEVEL_GATE");
                return list;
            }
        }

        public const string InsufficientNotice = "Bu maç için olası sonuç üretecek yeterli doğrulanmış veri bulunamadı.";
        /// <summary>Limited / Disabled maçlarda kullanıcıya gösterilen TEK metin (yüzde yok).</summary>
        public const string NotEligibleNotice = "Bu maç için güvenilir AI beklentisi oluşturacak yeterli doğrulanmış veri bulunmuyor.";

        /// <summary>Ev sahibi/deplasman/lig bilgisi olmadan kapanan (yayımlanmayan) kapı kodları — Disabled.</summary>
        public static readonly IReadOnlySet<string> HardGates = new HashSet<string>
        {
            "INSUFFICIENT_SAMPLE", "TEAM_LEAGUE_UNKNOWN", "CROSS_LEAGUE_UNLINKED", "LEAGUE_NOT_EVALUATED", "MATCH_NOT_SCHEDULED"
        };

        /// <summary>
        /// UYGUNLUK BİRLEŞTİRME — maç kapıları + lig sınavı. Sert kapı (veri/kimlik/bağlantı yok, maç ertelendi) → Disabled;
        /// çıktı kapısı (kanıtsız aşırı olasılık, bağımsız reyting çelişkisi) → en fazla Limited; aksi hâlde lig kararı.
        /// </summary>
        public static (string Eligibility, List<string> Reasons) Combine(string? leagueStatus, IEnumerable<string> leagueReasons, IEnumerable<string> matchGates, IEnumerable<string> outputGates)
        {
            var reasons = new List<string>();
            var gates = matchGates.ToList();
            reasons.AddRange(gates);
            if (leagueStatus == null) reasons.Add("LEAGUE_NOT_EVALUATED");
            if (reasons.Any(HardGates.Contains)) return (PredictionEligibilities.Disabled, reasons);
            var outs = outputGates.ToList();
            reasons.AddRange(outs);
            reasons.AddRange(leagueReasons.Select(r => "LEAGUE:" + r));
            if (leagueStatus == PredictionEligibilities.Disabled) return (PredictionEligibilities.Disabled, reasons);
            if (outs.Count > 0 || leagueStatus == PredictionEligibilities.Limited) return (PredictionEligibilities.Limited, reasons);
            return (PredictionEligibilities.Enabled, reasons);
        }

        /// <summary>
        /// MAÇ DÜZEYİ KAPI → MARKET AİLESİ EŞLEMESİ (ikinci katman). Sert kapılar bütün maçı kapatır (ayrıca ele alınır);
        /// çıktı kapıları YALNIZ ilgili aileyi kapatır:
        ///  • OUTLIER_PROBABILITY — kalibrasyon penceresinde doğrulanmamış aşırı 1X2 olasılığı → yalnız MaçSonucu + ÇifteŞans;
        ///  • RATING_DIRECTION_CONFLICT — bağımsız Elo ile yön çelişkisi → yalnız MaçSonucu + ÇifteŞans.
        /// Gol ve KG aileleri bu kapılardan etkilenmez: aşırı 1X2 olasılığı gol dağılımını geçersiz kılmaz.
        /// </summary>
        public static IReadOnlyList<string> MatchGatedFamilies(IEnumerable<string> outputGates)
        {
            var gated = new List<string>();
            foreach (var g in outputGates)
                if (g is "OUTLIER_PROBABILITY" or "RATING_DIRECTION_CONFLICT")
                {
                    if (!gated.Contains(MarketFamilies.MatchResult)) { gated.Add(MarketFamilies.MatchResult); gated.Add(MarketFamilies.DoubleChance); }
                }
            return gated;
        }

        /// <summary>
        /// NİHAİ YAYIN KARARI — kart üretildikten SONRA. Sert kapı → Disabled; hiç yayımlanabilir kart yoksa → Limited
        /// (yüzde taşınmaz); en az bir kart varsa → Enabled (Full ya da Partial). Eski istemciler için
        /// <see cref="OutcomeSnapshotDto.PredictionEligibility"/> sözleşmesi korunur.
        /// </summary>
        public static (string Eligibility, List<string> Reasons) Finalize(OutcomeSnapshotDto dto, IEnumerable<string> matchGates, IEnumerable<string> outputGates)
        {
            var reasons = matchGates.ToList();
            if (reasons.Any(HardGates.Contains)) return (PredictionEligibilities.Disabled, reasons);
            reasons.AddRange(outputGates);
            foreach (var m in dto.Markets.Where(m => !m.Published))
                reasons.AddRange(m.ReasonCodes.Select(r => m.Family + ":" + r));
            if (dto.Status != "Available" || dto.PublishedCardCount == 0)
                return (dto.Status == "Available" ? PredictionEligibilities.Limited : PredictionEligibilities.Disabled, reasons);
            return (PredictionEligibilities.Enabled, reasons);
        }

        /// <summary>
        /// KULLANICI GÖRÜNÜMÜ — Enabled değilse yüzde, aile, skor ve beklenen gol TAŞINMAZ (frontend gösteremez); yalnız durum, gerekçe
        /// kodları, SnapshotId/ModelVersion/hesaplama zamanı ve dürüst metin kalır. Keşfet ve Detay aynı temizlenmiş nesneyi okur.
        /// </summary>
        public static OutcomeSnapshotDto ForUser(OutcomeSnapshotDto s)
        {
            if (s.Status == "Pending") return s;
            if (s.PredictionEligibility == PredictionEligibilities.Enabled)
            {
                // Market bazlı süzgeç: yayımlanmayan ailenin TEK bir yüzdesi bile kullanıcıya gitmez. Ana kartlar zaten
                // yalnız yayımlanabilir ailelerden seçildi; burada "Tüm Olasılıklar" listesi ve türev alanlar temizlenir.
                var published = s.Markets.Where(m => m.Published).Select(m => m.Family).ToHashSet();
                if (published.Count == 0 && s.Markets.Count > 0) return NotEligibleView(s);
                foreach (var f in s.Families) f.Items = f.Items.Where(i => i.MeasuredFamily != null && published.Contains(i.MeasuredFamily)).ToList();
                s.Families = s.Families.Where(f => f.Items.Count > 0).ToList();
                // En olası skorlar maç sonucu dağılımının gösterimidir: 1X2 yayımlanmıyorsa taşınmaz.
                if (!published.Contains(MarketFamilies.MatchResult)) s.TopScores = new List<OutcomeScoreDto>();
                // Beklenen gol yalnız bir gol çizgisi yayımlanıyorsa anlamlıdır.
                if (!published.Contains(MarketFamilies.TotalGoals15) && !published.Contains(MarketFamilies.TotalGoals25)
                    && !published.Contains(MarketFamilies.TotalGoals35))
                { s.ExpectedHomeGoals = null; s.ExpectedAwayGoals = null; }
                return s;
            }
            return NotEligibleView(s);
        }

        private static OutcomeSnapshotDto NotEligibleView(OutcomeSnapshotDto s)
        {
            return new OutcomeSnapshotDto
            {
                Markets = s.Markets,
                OverallStatus = OutcomeOverallStatuses.NotEligible,
                PublishedCardCount = 0,
                MarketPolicyVersion = s.MarketPolicyVersion,
                SnapshotId = s.SnapshotId, MatchId = s.MatchId, ModelVersion = s.ModelVersion, CalibrationRunId = s.CalibrationRunId,
                ComputedAtUtc = s.ComputedAtUtc, InputsCutoffUtc = s.InputsCutoffUtc,
                Status = "NotEligible",
                PredictionEligibility = s.PredictionEligibility,
                EligibilityReasons = s.EligibilityReasons,
                EvidenceCoverage = s.EvidenceCoverage, SampleQuality = s.SampleQuality,
                HomeSampleSize = s.HomeSampleSize, AwaySampleSize = s.AwaySampleSize,
                ReasonCodes = s.ReasonCodes, TriggerType = s.TriggerType, PreviousSnapshotId = s.PreviousSnapshotId,
                Notice = NotEligibleNotice
            };
        }

        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        public static OutcomeSnapshotDto Insufficient(int matchId, OutcomeExpectation e, string homeName, string awayName) => new()
        {
            MatchId = matchId,
            Status = "InsufficientData",
            EvidenceCoverage = Math.Round(e.Coverage, 3),
            SampleQuality = SampleQuality(e),
            HomeSampleSize = e.HomeSample,
            AwaySampleSize = e.AwaySample,
            Limitation = $"Doğrulanmış geçmiş maç sayısı yetersiz: {homeName} {e.HomeSample}, {awayName} {e.AwaySample}.",
            Notice = NotEligibleNotice,
            ReasonCodes = new List<string> { "INSUFFICIENT_SAMPLE" },
            PredictionEligibility = PredictionEligibilities.Disabled,
            EligibilityReasons = e.GateReasons.Count > 0 ? e.GateReasons.ToList() : new List<string> { "INSUFFICIENT_SAMPLE" }
        };

        public static string SampleQuality(OutcomeExpectation e)
            => e.Coverage >= 0.99 ? "Rich" : e.Coverage >= 0.6 ? "Developing" : "Limited";

        public static OutcomeSnapshotDto Build(int matchId, OutcomePrediction p, string homeName, string awayName,
            MarketPublication? publication = null)
        {
            var e = p.Expectation;
            var cal = p.Calibrated;
            var raw = p.Raw;
            var bas = p.Baseline;
            var quality = SampleQuality(e);
            var uncertainty = Math.Round(1 - e.Coverage, 3);
            string? limitation = e.Coverage < 0.6
                ? $"Sınırlı veri: {homeName} için {e.HomeSample}, {awayName} için {e.AwaySample} doğrulanmış maç; yüzdeler lig ortalamasına yaklaştırıldı."
                : null;

            OutcomeCandidateDto C(string family, string market, string? key, double rawP, double calP, double baseP, string? measured = null)
            {
                var lift = calP - baseP;
                return new OutcomeCandidateDto
                {
                    Family = family, FamilyTitle = OutcomeFamilies.Title(family), Market = market, MarketKey = key,
                    MeasuredFamily = measured ?? MarketFamilies.ForMarketKey(key),
                    RawProbability = Math.Round(rawP, 4), CalibratedProbability = Math.Round(calP, 4), BaselineProbability = Math.Round(baseP, 4),
                    InformationLift = Math.Round(lift, 4), EvidenceCoverage = Math.Round(e.Coverage, 3), SampleQuality = quality,
                    Uncertainty = uncertainty, Limitation = limitation
                };
            }

            var result = new List<OutcomeCandidateDto>
            {
                C(OutcomeFamilies.Result, "Ev Sahibi Kazanır", OddsMarketKeys.Ms1, raw.HomeWin, cal.HomeWin, bas.HomeWin),
                C(OutcomeFamilies.Result, "Beraberlik", OddsMarketKeys.MsX, raw.Draw, cal.Draw, bas.Draw),
                C(OutcomeFamilies.Result, "Deplasman Kazanır", OddsMarketKeys.Ms2, raw.AwayWin, cal.AwayWin, bas.AwayWin)
            };
            var goals = new List<OutcomeCandidateDto>();
            foreach (var (line, over, under) in new[] { (1.5, OutcomeMarketKeys.Over15, OutcomeMarketKeys.Under15), (2.5, OddsMarketKeys.Over25, OddsMarketKeys.Under25), (3.5, OutcomeMarketKeys.Over35, OutcomeMarketKeys.Under35) })
            {
                var l = line.ToString("0.0", CultureInfo.InvariantCulture);
                goals.Add(C(OutcomeFamilies.Goals, $"{l} Üst", over, raw.Over(line), cal.Over(line), bas.Over(line)));
                goals.Add(C(OutcomeFamilies.Goals, $"{l} Alt", under, raw.Under(line), cal.Under(line), bas.Under(line)));
            }
            var btts = new List<OutcomeCandidateDto>
            {
                C(OutcomeFamilies.Btts, "Karşılıklı Gol Var", OddsMarketKeys.BttsYes, raw.BttsYes, cal.BttsYes, bas.BttsYes),
                C(OutcomeFamilies.Btts, "Karşılıklı Gol Yok", OddsMarketKeys.BttsNo, raw.BttsNo, cal.BttsNo, bas.BttsNo)
            };
            var other = new List<OutcomeCandidateDto>
            {
                C(OutcomeFamilies.Other, "Çifte Şans (1X)", OddsMarketKeys.DoubleChance1X, raw.HomeWin + raw.Draw, cal.HomeWin + cal.Draw, bas.HomeWin + bas.Draw),
                C(OutcomeFamilies.Other, "Çifte Şans (X2)", OddsMarketKeys.DoubleChanceX2, raw.Draw + raw.AwayWin, cal.Draw + cal.AwayWin, bas.Draw + bas.AwayWin),
                C(OutcomeFamilies.Other, "Çifte Şans (1-2)", OddsMarketKeys.DoubleChance12, raw.HomeWin + raw.AwayWin, cal.HomeWin + cal.AwayWin, bas.HomeWin + bas.AwayWin),
                C(OutcomeFamilies.Other, $"{homeName} Gol Atar", null, raw.HomeScores, cal.HomeScores, bas.HomeScores, MarketFamilies.BothTeamsToScore),
                C(OutcomeFamilies.Other, $"{awayName} Gol Atar", null, raw.AwayScores, cal.AwayScores, bas.AwayScores, MarketFamilies.BothTeamsToScore),
                C(OutcomeFamilies.Other, $"{homeName} Gol Yemez", null, raw.HomeCleanSheet, cal.HomeCleanSheet, bas.HomeCleanSheet, MarketFamilies.BothTeamsToScore),
                C(OutcomeFamilies.Other, $"{awayName} Gol Yemez", null, raw.AwayCleanSheet, cal.AwayCleanSheet, bas.AwayCleanSheet, MarketFamilies.BothTeamsToScore),
                C(OutcomeFamilies.Other, "Toplam Gol 0-1", null, raw.TotalBetween(0, 1), cal.TotalBetween(0, 1), bas.TotalBetween(0, 1), MarketFamilies.TotalGoals15),
                C(OutcomeFamilies.Other, "Toplam Gol 2-3", null, raw.TotalBetween(2, 3), cal.TotalBetween(2, 3), bas.TotalBetween(2, 3), MarketFamilies.TotalGoals25),
                C(OutcomeFamilies.Other, "Toplam Gol 4+", null, raw.TotalBetween(4, 99), cal.TotalBetween(4, 99), bas.TotalBetween(4, 99), MarketFamilies.TotalGoals35)
            };

            // ── Gösterim yuvarlaması — aile içinde tutarlı (1X2 = 100; alt + üst = 100; KG var + yok = 100; çifte şans = bileşenler) ──
            var res = LargestRemainder(result.Select(r => r.CalibratedProbability).ToArray());
            for (var i = 0; i < 3; i++) result[i].Probability = res[i];
            for (var i = 0; i < goals.Count; i += 2)
            {
                goals[i].Probability = Pct(goals[i].CalibratedProbability);
                goals[i + 1].Probability = 100 - goals[i].Probability;
            }
            btts[0].Probability = Pct(btts[0].CalibratedProbability);
            btts[1].Probability = 100 - btts[0].Probability;
            other[0].Probability = res[0] + res[1];
            other[1].Probability = res[1] + res[2];
            other[2].Probability = res[0] + res[2];
            var band = LargestRemainder(new[] { other[7].CalibratedProbability, other[8].CalibratedProbability, other[9].CalibratedProbability });
            other[7].Probability = band[0]; other[8].Probability = band[1]; other[9].Probability = band[2];
            for (var i = 3; i <= 6; i++) other[i].Probability = Pct(other[i].CalibratedProbability);

            // ── Gerekçe kodları (gerçek model girdilerinden) ──
            var codes = ReasonCodes(e, cal);
            // Çifte şans adayları da maç sonucu yuvasına girebildiği için aynı gerekçe kodlarını taşır.
            foreach (var c in result.Concat(other.Take(3))) { c.ReasonCodes = codes.Where(IsResultCode).ToList(); }
            foreach (var c in goals) { c.ReasonCodes = codes.Where(IsGoalCode).ToList(); }
            foreach (var c in btts) { c.ReasonCodes = codes.Where(IsBttsCode).ToList(); }

            // ── Ana kart seçimi — YALNIZ yayımlanabilir market ailelerinden ──
            var pub = publication ?? MarketPublication.AllEligible;
            foreach (var c in result.Concat(goals).Concat(btts).Concat(other.Take(3))) c.SelectionScore = Math.Round(Score(c, e.Coverage), 4);

            var mainCards = new List<OutcomeCandidateDto>();
            // 1) Maç sonucu yuvası: tek sonuçlar; hiçbiri lig ortalamasının üstünde bilgi taşımıyorsa çifte şans adayları.
            if (pub.IsPublished(MarketFamilies.MatchResult))
            {
                // Tek sonuçlar ve çifte şans AYNI yarışa girer; kazanan bilgi değeridir. Çifte şans yasak DEĞİLDİR ama
                // BİLEŞENLERİNİN İKİSİ DE lig ortalamasının üstünde olmalıdır: bileşik kartın anlamı "model bu iki sonucun
                // İKİSİNİ de olağandan olası buluyor"dur. Yalnız bir bileşen yükseliyorsa birleşim o bileşenin bilgisini
                // yüksek bir yüzdenin arkasına gizler — o zaman tek sonuç kartı daha çok şey söyler.
                // Ölçüm 19.09.2026: kural olmadan çifte şans yayımlanan kartların %30,2'sini alıyordu ve seçildiği maçlarda
                // ortalama bilgi değeri en iyi tek sonuçla AYNIYDI (0,0181 / 0,0181) — yani yalnız berabere kalarak kazanıyordu.
                var candidates = result.Where(c => c.InformationLift > 0).ToList();
                if (pub.IsPublished(MarketFamilies.DoubleChance))
                    candidates.AddRange(other.Take(3).Where(c => c.InformationLift > 0 && BothComponentsLifted(c.MarketKey, result)));
                if (candidates.Count > 0)
                {
                    var pick = Best(candidates);
                    if (OutcomeFamilies.IsCompound(pick.MarketKey ?? string.Empty))
                    { pick.Family = OutcomeFamilies.Result; pick.FamilyTitle = OutcomeFamilies.Title(OutcomeFamilies.Result); }
                    pick.Reason = ResultReason(pick, e, cal, homeName, awayName);
                    mainCards.Add(pick);
                }
            }
            // 2) Gol yuvası: yalnız UYGUN gol çizgilerinin adayları yarışır (2.5 zayıfsa 1.5/3.5 otomatik kapanmaz).
            // İKİLİ MARKET KURALI: kart, modelin olması DAHA OLASI gördüğü tarafı söyler (p ≥ 0,5) VE lig ortalamasına göre
            // bilgi taşır (lift > 0). İkisi birden sağlanmıyorsa yuva BOŞ kalır — "1.5 Alt %21" gibi hem düşük olasılıklı hem
            // okunması güç bir kart üretilmez (ölçüm 19.09.2026: Athletic–Alaves).
            var goalCandidates = new List<OutcomeCandidateDto>();
            for (var i = 0; i < GoalLines.Length; i++)
                if (pub.IsPublished(GoalLines[i].Family))
                    goalCandidates.AddRange(new[] { goals[i * 2], goals[i * 2 + 1] }.Where(Informative));
            if (goalCandidates.Count > 0)
            {
                var pick = Best(goalCandidates);
                pick.Reason = GoalsReason(pick, e, cal, homeName, awayName);
                mainCards.Add(pick);
            }
            // 3) KG yuvası.
            if (pub.IsPublished(MarketFamilies.BothTeamsToScore))
            {
                var pick = btts.Where(Informative).ToList();
                if (pick.Count > 0)
                {
                    var b = Best(pick);
                    b.Reason = BttsReason(b, cal, homeName, awayName);
                    mainCards.Add(b);
                }
            }
            // SEÇİM bilgi değerine göredir; GÖSTERİM sırası sabit aile sırasıdır (Maç Sonucu → Gol → KG). Ekran düzeni
            // kart sayısına göre değişmez, yalnız eksik yuvalar çıkarılır.
            mainCards = mainCards.OrderBy(c => FamilyRank(c.Family)).ThenBy(c => c.MarketKey, StringComparer.Ordinal).ToList();

            var checks = new OutcomeChecksDto
            {
                ResultSum = Math.Round(cal.HomeWin + cal.Draw + cal.AwayWin, 6),
                BttsSum = Math.Round(cal.BttsYes + cal.BttsNo, 6),
                Over15Sum = Math.Round(cal.Over(1.5) + cal.Under(1.5), 6),
                Over25Sum = Math.Round(cal.Over(2.5) + cal.Under(2.5), 6),
                Over35Sum = Math.Round(cal.Over(3.5) + cal.Under(3.5), 6),
                DoubleChance1XError = Math.Round(Math.Abs(other[0].CalibratedProbability - (result[0].CalibratedProbability + result[1].CalibratedProbability)), 6),
                DoubleChanceX2Error = Math.Round(Math.Abs(other[1].CalibratedProbability - (result[1].CalibratedProbability + result[2].CalibratedProbability)), 6),
                DoubleChance12Error = Math.Round(Math.Abs(other[2].CalibratedProbability - (result[0].CalibratedProbability + result[2].CalibratedProbability)), 6)
            };
            checks.Consistent = new[] { checks.ResultSum, checks.BttsSum, checks.Over15Sum, checks.Over25Sum, checks.Over35Sum }.All(s => Math.Abs(s - 1) < 0.002)
                                && checks.DoubleChance1XError < 0.001 && checks.DoubleChanceX2Error < 0.001 && checks.DoubleChance12Error < 0.001;

            // ── Market ailesi durum listesi (kullanıcıya ne gittiğinin tek kaydı) ──
            var markets = MarketFamilies.All.Select(f => new OutcomeMarketStatusDto
            {
                Family = f, Title = MarketFamilies.Title(f),
                Status = pub.OrganizationStatus(f), ReasonCodes = pub.ReasonsFor(f), Published = pub.IsPublished(f)
            }).ToList();

            return new OutcomeSnapshotDto
            {
                MatchId = matchId,
                Status = "Available",
                ExpectedHomeGoals = Math.Round(cal.ExpectedHome, 2),
                ExpectedAwayGoals = Math.Round(cal.ExpectedAway, 2),
                EvidenceCoverage = Math.Round(e.Coverage, 3),
                SampleQuality = quality,
                HomeSampleSize = e.HomeSample,
                AwaySampleSize = e.AwaySample,
                Limitation = limitation,
                Markets = markets,
                OverallStatus = mainCards.Count >= 3 ? OutcomeOverallStatuses.Full
                    : mainCards.Count > 0 ? OutcomeOverallStatuses.Partial : OutcomeOverallStatuses.NotEligible,
                PublishedCardCount = mainCards.Count,
                MainCards = mainCards,
                Families = new List<OutcomeFamilyDto>
                {
                    new() { Family = OutcomeFamilies.Result, Title = OutcomeFamilies.Title(OutcomeFamilies.Result), Items = result },
                    new() { Family = OutcomeFamilies.Goals, Title = OutcomeFamilies.Title(OutcomeFamilies.Goals), Items = goals },
                    new() { Family = OutcomeFamilies.Btts, Title = OutcomeFamilies.Title(OutcomeFamilies.Btts), Items = btts },
                    new() { Family = OutcomeFamilies.Other, Title = OutcomeFamilies.Title(OutcomeFamilies.Other), Items = other }
                },
                TopScores = cal.TopScores(5).Select(s => new OutcomeScoreDto { Home = s.Home, Away = s.Away, Probability = Pct(s.P) }).ToList(),
                ReasonCodes = codes,
                Checks = checks
            };
        }

        /// <summary>
        /// BİLGİ DEĞERİ — kartın lig ortalamasına göre taşıdığı bilgi: ikili Kullback–Leibler ayrışması D(p‖p₀).
        /// Serbest katsayısı yoktur ve olayın taban büyüklüğünden BAĞIMSIZ olarak karşılaştırılabilir. selection-2'deki
        /// (fark / √(p₀(1−p₀))) ölçüsü bileşik marketleri yapısal olarak kayırıyordu: çifte şansın farkı iki bileşenin
        /// TOPLAMI kadar büyürken paydası p₀ → 0,7'de küçülüyordu (ölçüm 18.09.2026: serbest bırakılınca maçların %56'sında
        /// çifte şans birinci kart oluyordu). KL'de bu yapısal kayırma yoktur.
        /// </summary>
        public static double InformationValue(OutcomeCandidateDto c)
        {
            var p = Math.Clamp(c.CalibratedProbability, 1e-6, 1 - 1e-6);
            var b = Math.Clamp(c.BaselineProbability, 1e-6, 1 - 1e-6);
            return p * Math.Log(p / b) + (1 - p) * Math.Log((1 - p) / (1 - b));
        }

        /// <summary>
        /// SEÇİM SKORU — bilgi değeri, veri kapsamıyla ağırlıklı. Yalnız yüksek yüzde bir kartı öne çıkarmaz: taban zaten
        /// yüksekse (1.5 Üst, 3.5 Alt) KL küçük kalır.
        /// </summary>
        public static double Score(OutcomeCandidateDto c, double coverage)
            => InformationValue(c) * (0.4 + 0.6 * coverage);

        /// <summary>
        /// İkili market adayı kart olabilir mi? İki koşul birden: modelin daha olası gördüğü taraf (p ≥ 0,5) VE lig
        /// ortalamasının üstünde bilgi (lift &gt; 0). 1X2 ailesi bu kuraldan muaftır: üç şıklı bir bölünmede hiçbir sonuç
        /// %50'ye ulaşmayabilir, orada anlamlı ifade en olası sonuçtur.
        /// </summary>
        private static bool Informative(OutcomeCandidateDto c) => c.CalibratedProbability >= 0.5 && c.InformationLift > 0;

        /// <summary>Deterministik en iyi aday: bilgi değeri, eşitlikte market anahtarı.</summary>
        private static OutcomeCandidateDto Best(IReadOnlyList<OutcomeCandidateDto> candidates)
            => candidates.OrderByDescending(c => c.SelectionScore).ThenBy(c => c.MarketKey, StringComparer.Ordinal).First();

        /// <summary>
        /// Çifte şans kartının bilgi koşulu: birleşimi oluşturan İKİ tek sonucun da lig ortalamasının üstünde olması.
        /// <paramref name="result"/> sırası: [0] ev, [1] beraberlik, [2] deplasman.
        /// </summary>
        private static bool BothComponentsLifted(string? marketKey, IReadOnlyList<OutcomeCandidateDto> result)
        {
            var (i, j) = marketKey switch
            {
                OddsMarketKeys.DoubleChance1X => (0, 1),
                OddsMarketKeys.DoubleChanceX2 => (1, 2),
                OddsMarketKeys.DoubleChance12 => (0, 2),
                _ => (-1, -1)
            };
            if (i < 0) return false;
            if (result[i].InformationLift <= 0 || result[j].InformationLift <= 0) return false;
            // ZİNCİR KURALI — 1X2 dağılımının lig tabanından toplam ayrışması ikiye bölünür:
            //   D_toplam = D_birleşim ("A ya da B" ↔ "C" hakkında bilinen) + D_içeride ("A" ↔ "B" ayrımı hakkında bilinen).
            // Çifte şans kartı ancak model BİRLEŞİMİ, içerideki ayrımdan daha iyi biliyorsa doğru özettir; aksi hâlde
            // birleşim, tek sonucun taşıdığı ayrımı yüksek bir yüzdenin arkasına gizler.
            double Kl(OutcomeCandidateDto c) => Part(c.CalibratedProbability, c.BaselineProbability);
            var total = Kl(result[0]) is var _ ? PartSum(result) : 0;
            var union = InformationValue(new OutcomeCandidateDto
            {
                CalibratedProbability = result[i].CalibratedProbability + result[j].CalibratedProbability,
                BaselineProbability = result[i].BaselineProbability + result[j].BaselineProbability
            });
            return union > total - union;
        }

        /// <summary>Tek terimin KL katkısı: p·ln(p/p₀).</summary>
        private static double Part(double p, double b)
            => Math.Clamp(p, 1e-9, 1) * Math.Log(Math.Clamp(p, 1e-9, 1) / Math.Clamp(b, 1e-9, 1));

        /// <summary>1X2 dağılımının lig tabanına göre toplam KL ayrışması.</summary>
        private static double PartSum(IReadOnlyList<OutcomeCandidateDto> result)
            => result.Take(3).Sum(c => Part(c.CalibratedProbability, c.BaselineProbability));

        /// <summary>Sabit gösterim sırası — kart sayısı 0–3 arasında değişse de ekran düzeni aynı kalır.</summary>
        private static int FamilyRank(string family) => family switch
        {
            OutcomeFamilies.Result => 0,
            OutcomeFamilies.Goals => 1,
            OutcomeFamilies.Btts => 2,
            _ => 3
        };

        private static int Pct(double p) => (int)Math.Round(Math.Clamp(p, 0, 1) * 100, MidpointRounding.AwayFromZero);

        /// <summary>Toplamı 100 olan tamsayı yüzdeler (en büyük kalan yöntemi).</summary>
        public static int[] LargestRemainder(double[] probs)
        {
            var total = probs.Sum();
            if (total <= 0) return probs.Select(_ => 0).ToArray();
            var scaled = probs.Select(p => p / total * 100).ToArray();
            var floors = scaled.Select(s => (int)Math.Floor(s)).ToArray();
            var remaining = 100 - floors.Sum();
            foreach (var i in scaled.Select((s, i) => (r: s - Math.Floor(s), i)).OrderByDescending(x => x.r).ThenBy(x => x.i).Take(remaining).Select(x => x.i))
                floors[i]++;
            return floors;
        }

        private static bool IsResultCode(string c) => c.StartsWith("RESULT_") || c.StartsWith("SAMPLE_") || c == "HOME_ADVANTAGE_LEAGUE" || c == "STALE_RATING";
        private static bool IsGoalCode(string c) => c.StartsWith("GOALS_") || c.StartsWith("SAMPLE_") || c == "STALE_RATING";
        private static bool IsBttsCode(string c) => c.StartsWith("BTTS_") || c.StartsWith("SAMPLE_") || c == "STALE_RATING";

        public static List<string> ReasonCodes(OutcomeExpectation e, ScoreDistribution cal)
        {
            var codes = new List<string>();
            var diff = e.LambdaHome - e.LambdaAway;
            if (diff >= 0.6) codes.Add("RESULT_HOME_CLEAR_FAVOURITE");
            else if (diff >= 0.25) codes.Add("RESULT_HOME_STRONGER");
            else if (diff <= -0.6) codes.Add("RESULT_AWAY_CLEAR_FAVOURITE");
            else if (diff <= -0.25) codes.Add("RESULT_AWAY_STRONGER");
            else codes.Add("RESULT_BALANCED");
            if (e.LeagueHome - e.LeagueAway >= 0.2) codes.Add("HOME_ADVANTAGE_LEAGUE");

            var leagueTotal = e.LeagueHome + e.LeagueAway;
            var total = e.LambdaHome + e.LambdaAway;
            if (total <= leagueTotal * 0.85) codes.Add("GOALS_LOW_EXPECTATION");
            else if (total >= leagueTotal * 1.15) codes.Add("GOALS_HIGH_EXPECTATION");
            else codes.Add("GOALS_NEAR_LEAGUE_AVERAGE");

            if (cal.HomeScores >= 0.7 && cal.AwayScores >= 0.7) codes.Add("BTTS_BOTH_LIKELY_TO_SCORE");
            if (cal.AwayScores < 0.6) codes.Add("BTTS_AWAY_SCORING_DOUBT");
            if (cal.HomeScores < 0.6) codes.Add("BTTS_HOME_SCORING_DOUBT");
            if (!codes.Any(c => c.StartsWith("BTTS_"))) codes.Add("BTTS_BALANCED_SCORING");

            codes.Add(e.Coverage >= 0.99 ? "SAMPLE_RICH" : e.Coverage >= 0.6 ? "SAMPLE_DEVELOPING" : "SAMPLE_LIMITED");
            if (e.Coverage < 1 && Math.Min(e.HomeSample, e.AwaySample) >= 12) codes.Add("STALE_RATING");
            return codes;
        }

        private static string F(double v) => v.ToString("0.0", Tr);

        private static string ResultReason(OutcomeCandidateDto c, OutcomeExpectation e, ScoreDistribution cal, string home, string away)
        {
            var s = $"Model beklenen golü {home} {F(cal.ExpectedHome)}, {away} {F(cal.ExpectedAway)} olarak hesaplıyor";
            // Metin gerekçe KODUNDAN türetilir (kart ↔ kod çelişkisi olmasın): yakın beklentide "üstünlük" denmez.
            var codes = ReasonCodes(e, cal);
            // Kart artık ham yüzdeye değil LİG ORTALAMASINA GÖRE BİLGİ DEĞERİNE göre seçiliyor; bu yüzden gol beklentisi
            // ev sahibi lehine olsa bile deplasman kartı seçilebilir (o ligin ev sahibi taban oranı yüksekse). Metin bu
            // durumda "birbirine yakın" DEMEZ — yalnız denge kodu varken der.
            var balanced = codes.Contains("RESULT_BALANCED");
            s += c.MarketKey switch
            {
                OddsMarketKeys.Ms1 when codes.Any(x => x is "RESULT_HOME_STRONGER" or "RESULT_HOME_CLEAR_FAVOURITE")
                    => "; ev sahibinin reyting üstünlüğü bu sonuca en yüksek payı veriyor.",
                OddsMarketKeys.Ms2 when codes.Any(x => x is "RESULT_AWAY_STRONGER" or "RESULT_AWAY_CLEAR_FAVOURITE")
                    => "; deplasman takımının reyting üstünlüğü bu sonuca en yüksek payı veriyor.",
                OddsMarketKeys.Ms1 when balanced => "; iki takımın gol beklentisi birbirine yakın, ev sahibi sonucu küçük farkla öne çıkıyor.",
                OddsMarketKeys.Ms2 when balanced => "; iki takımın gol beklentisi birbirine yakın, deplasman sonucu küçük farkla öne çıkıyor.",
                OddsMarketKeys.MsX when balanced => "; iki takımın gol beklentisi birbirine yakın, beraberlik payı yüksek.",
                OddsMarketKeys.Ms1 => "; ev sahibi sonucu bu ligin ortalamasının üzerinde çıkıyor.",
                OddsMarketKeys.Ms2 => "; deplasman sonucu bu ligin ortalamasının üzerinde çıkıyor.",
                OddsMarketKeys.MsX => "; beraberlik payı bu ligin ortalamasının üzerinde çıkıyor.",
                OddsMarketKeys.DoubleChance1X => "; tek bir sonuç ayrışmıyor, ancak deplasman galibiyeti dışı seçenek lig ortalamasının üzerinde.",
                OddsMarketKeys.DoubleChanceX2 => "; tek bir sonuç ayrışmıyor, ancak ev sahibi galibiyeti dışı seçenek lig ortalamasının üzerinde.",
                OddsMarketKeys.DoubleChance12 => "; tek bir sonuç ayrışmıyor, ancak beraberlik dışı seçenek lig ortalamasının üzerinde.",
                _ => "; sonuç dağılımı lig ortalamasından bu yönde ayrışıyor."
            };
            if (e.CrossLeague)
                s += " Takımlar farklı liglerden geliyor; güçler ligler arası maçlardan öğrenilen ortak ölçekte karşılaştırıldı.";
            if (e.HomeRecentCount >= 5 && e.AwayRecentCount >= 5)
                s += $" Son {e.HomeRecentCount} maçta {home} maç başına {F(e.HomeRecentFor)} gol attı, {away} son {e.AwayRecentCount} maçta {F(e.AwayRecentFor)}.";
            return s;
        }

        private static string GoalsReason(OutcomeCandidateDto c, OutcomeExpectation e, ScoreDistribution cal, string home, string away)
        {
            var total = cal.ExpectedTotalGoals;
            var league = e.LeagueHome + e.LeagueAway;
            var s = $"Beklenen toplam gol {F(total)}; bu ligin ortalaması {F(league)}.";
            if (e.HomeRecentCount >= 5 && e.AwayRecentCount >= 5)
                s += $" {home} son {e.HomeRecentCount} maçında maç başına {F(e.HomeRecentFor + e.HomeRecentAgainst)}, {away} {F(e.AwayRecentFor + e.AwayRecentAgainst)} gol gördü.";
            return s;
        }

        private static string BttsReason(OutcomeCandidateDto c, ScoreDistribution cal, string home, string away)
            => $"Modele göre {home} için gol bulma olasılığı %{Pct(cal.HomeScores)}, {away} için %{Pct(cal.AwayScores)}.";
    }

    /// <summary>1.5 ve 3.5 çizgileri için seçim/settlement anahtarları (additive).</summary>
    public static class OutcomeMarketKeys
    {
        public const string Over15 = OddsMarketKeys.Over15;
        public const string Under15 = OddsMarketKeys.Under15;
        public const string Over35 = OddsMarketKeys.Over35;
        public const string Under35 = OddsMarketKeys.Under35;
    }
}
