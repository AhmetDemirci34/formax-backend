using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Outcomes
{
    /// <summary>Olası sonuç modelinin sürümü — snapshot ve koşu kayıtlarına yazılır.</summary>
    public static class OutcomeModelVersion
    {
        /// <summary>
        /// 4.0 (18.09.2026): PARSİMONİ KAPILI PARAMETRE SEÇİMİ. 3.0 reyting parametrelerini (sezon daraltması, ligler arası takım
        /// payı, lig ofseti oranı) seçim penceresindeki argmin ile alıyordu; ölçüldü ki bu farklar örneklem gürültüsünün altında
        /// (sezon daraltması: −0,0008 fark, %95 eşli aralık [−0,0029, +0,0013]; ligler arası takım payı: yalnız 50 maç, aralık
        /// [−0,045, +0,028]). 4.0 bir parametreyi nötr değerinden ancak eşli %95 aralık tamamen 0'ın altındaysa ayırır.
        /// Ölçülen etki (18.09.2026, 5.328 zamansal test maçı): 1X2 log loss 1,0087 → 1,0059 (eşli %95 aralık [−0,0043, −0,0011]),
        /// Brier 0,6028 → 0,6011, ligler arası 0,9877 → 0,9830.
        /// 3.0 (17.09.2026): ligler arası ORTAK GÜÇ ÖLÇEĞİ (ligler arası maçlardan öğrenilen lig güç ofseti; takım lig değiştirince
        /// reyting ölçek farkıyla taşınır), sezon arası daraltma, bağımsız Elo yön kontrolü, maç düzeyi güvenlik kapıları.
        /// 2.0: yalnız lig içi göreli reyting (UEFA maçlarında Kıbrıs lideri La Liga takımıyla doğrudan kıyaslanıyordu).
        /// </summary>
        public const string Current = "formax-outcome-4.0";
        /// <summary>Bir önceki ÜRETİM sürümü — koşu kaydında aynı pencerede yeniden kurulup karşılaştırılır.</summary>
        public const string Previous = "formax-outcome-3.0";
        /// <summary>Ligler arası ortak ölçekten ÖNCEKİ sürüm — ligler arası gerileme denetimi için korunur.</summary>
        public const string NoCrossScale = "formax-outcome-2.0";
    }

    /// <summary>Tarihsel (bitmiş) maç — modelin tek girdisi. Maç sonrası başka veri modele girmez.</summary>
    public readonly record struct HistoricalMatch(int MatchId, DateTime KickoffUtc, int LeagueId, int HomeTeamId, int AwayTeamId, int HomeGoals, int AwayGoals);

    /// <summary>
    /// ORGANİZASYON SINIFLAMASI — hangi organizasyon "lig" (takımın ev ligi, güç ölçeğinin düğümü), hangisi lig dışı (kupa, kıta
    /// turnuvası) ve hangisi modele hiç girmez (hazırlık maçları: rotasyonlu kadro, rekabet yok).
    ///
    /// Lig ölçütü veriden ölçülür: en iyi kapsanan yılda takım başına maç ≥ <see cref="LeagueMatchesPerTeamYear"/> ve yıllık farklı
    /// takım medyanı ≤ <see cref="MaxLeagueTeams"/> (lig ~8–38 maç/takım ve ≤ 40 takım; kupa ~1–2; UEFA ≤ 6 ve 80+ takım).
    /// Organizasyon türü statik üst veridir (sonuç değildir); geçmiş tahmine sızıntı üretmez.
    /// </summary>
    public sealed class CompetitionCatalog
    {
        public const double LeagueMatchesPerTeamYear = 7;
        public const int MaxLeagueTeams = 48;

        private readonly HashSet<int> _leagues;
        private readonly HashSet<int> _excluded;

        public CompetitionCatalog(IEnumerable<int> leagues, IEnumerable<int>? excluded = null)
        {
            _leagues = leagues.ToHashSet();
            _excluded = (excluded ?? Array.Empty<int>()).ToHashSet();
        }

        /// <summary>Sınıflama bilgisi olmayan (eski) kullanım: her organizasyon kendi başına lig sayılır — ligler arası katman kapalı.</summary>
        public static CompetitionCatalog Unclassified { get; } = new(Array.Empty<int>()) { TreatAllAsLeague = true };

        private bool TreatAllAsLeague { get; init; }

        public bool IsLeague(int competitionId) => TreatAllAsLeague || _leagues.Contains(competitionId);
        public bool IsExcluded(int competitionId) => _excluded.Contains(competitionId);
        public int LeagueCount => _leagues.Count;

        /// <summary>
        /// Tarihsel maç listesinden sınıflama. <paramref name="names"/> yalnız hazırlık maçı organizasyonlarını ayıklamak için
        /// kullanılır ("Friendlies"); lig/kupa ayrımı maç sayısından gelir.
        /// </summary>
        public static CompetitionCatalog Build(IReadOnlyList<HistoricalMatch> history, IReadOnlyDictionary<int, string>? names = null)
        {
            var excluded = names == null
                ? new HashSet<int>()
                : names.Where(n => n.Value.Contains("Friendl", StringComparison.OrdinalIgnoreCase)).Select(n => n.Key).ToHashSet();
            var leagues = new List<int>();
            foreach (var g in history.GroupBy(m => m.LeagueId))
            {
                if (excluded.Contains(g.Key)) continue;
                // Takım-yıl başına maç: her yılda (maç × 2) / farklı takım. Veri bazı yıllarda eksik içe aktarıldığı için en iyi
                // kapsanan yıl esas alınır (ölçüm 17.09.2026: Kıbrıs 318 → 2024: 3,2 / 2025: 11,2; UEFA 2/3/848 en fazla 6,0).
                var years = g.GroupBy(m => m.KickoffUtc.Year).Select(y =>
                {
                    var teams = y.Select(m => m.HomeTeamId).Concat(y.Select(m => m.AwayTeamId)).Distinct().Count();
                    return (PerTeam: teams == 0 ? 0 : y.Count() * 2.0 / teams, Teams: teams);
                }).ToList();
                var bestPerTeam = years.Max(y => y.PerTeam);
                var medianTeams = years.Select(y => y.Teams).OrderBy(x => x).ElementAt(years.Count / 2);
                if (bestPerTeam >= LeagueMatchesPerTeamYear && medianTeams <= MaxLeagueTeams) leagues.Add(g.Key);
            }
            return new CompetitionCatalog(leagues, excluded);
        }
    }

    /// <summary>
    /// MODEL PARAMETRELERİ — öğrenme oranları eğitim penceresinde, kalibrasyon parametreleri kalibrasyon penceresinde seçilir
    /// (test penceresine bakılarak SEÇİLMEZ).
    /// </summary>
    public sealed class OutcomeModelParameters
    {
        /// <summary>Log-hücum/savunma reytinglerinin maç başına öğrenme oranı (Poisson log-bağ gradyanı).</summary>
        public double LearningRate { get; set; } = 0.05;
        /// <summary>Lig ev/deplasman gol ortalamasının üstel güncelleme oranı.</summary>
        public double LeagueAlpha { get; set; } = 0.02;
        public double DefaultHomeGoals { get; set; } = 1.45;
        public double DefaultAwayGoals { get; set; } = 1.15;
        /// <summary>Kapsamın 1 sayıldığı örneklem (iki takımın daha az maçlısı).</summary>
        public int FullCoverageSample { get; set; } = 12;
        /// <summary>Bu örneklemin altında tahmin ÜRETİLMEZ (yetersiz veri).</summary>
        public int MinSample { get; set; } = 4;
        /// <summary>Son maçı bu kadar günden eskiyse kapsam düşürülür (bayat reyting).</summary>
        public int StaleDays { get; set; } = 150;

        // ── Ligler arası ortak güç ölçeği (3.0) ──
        /// <summary>Ligler arası katman açık mı? (false = 2.0 davranışı: yalnız lig içi göreli reyting).</summary>
        public bool CrossLeagueAware { get; set; } = true;
        /// <summary>Lig güç ofsetinin ligler arası maç başına öğrenme oranı.</summary>
        public double StrengthLearningRate { get; set; } = 0.02;
        /// <summary>Bir ligin güç ofseti bu kadar ligler arası maçla bağlanmadıysa o ligin takımları için ligler arası tahmin YAYIMLANMAZ.</summary>
        public int MinLeagueLinks { get; set; } = 20;
        /// <summary>
        /// Lig gücü önsel standart sapması (log-gol ölçeği). Toplu çözümde ridge cezası 1/σ² olarak girer; az bağlantılı lig 0'a
        /// (ortalamaya) daraltılır. Önsel varsayımdır — test penceresine bakılarak seçilmez.
        /// </summary>
        public double LeagueStrengthPriorSd { get; set; } = 0.5;
        /// <summary>Lig güçlerinin ligler arası maç grafiğinde toplu yeniden çözülme aralığı (gün).</summary>
        public int StrengthRefitDays { get; set; } = 14;
        /// <summary>Toplu çözümde eski ligler arası maçların yarı ömrü (gün).</summary>
        public int StrengthHalfLifeDays { get; set; } = 730;
        /// <summary>
        /// Ligler arası maçta hatanın takım reytingine giden payı (kalan açıklama lig ofsetine kalır).
        /// Nötr değer 1,0'dır (4.0): <see cref="OutcomeRatingModel.Update"/> hatayı zaten lig ofseti DÜŞÜLDÜKTEN sonra hesaplar,
        /// lig gücü ayrıca bütün ligler arası maç grafiğinden toplu çözülür — çift sayım yoktur. Bu yüzden ligler arası maçın
        /// takım reytingine katkısı KANIT OLMADAN kısılmaz.
        /// </summary>
        public double CrossLeagueTeamWeight { get; set; } = 1.0;
        /// <summary>Uzun aradan (sezon arası) sonra takım reytinginin korunan payı (1 = daraltma yok).</summary>
        public double SeasonCarry { get; set; } = 1.0;
        public int SeasonBreakDays { get; set; } = 50;

        // ── Takım düzeyi iç saha avantajı — ÖLÇÜLDÜ, KANITLANMADI (18.09.2026). Varsayılan KAPALI. ──
        /// <summary>
        /// TAKIM İÇ SAHA AVANTAJI η — 3.0/4.0'da iç saha avantajı YALNIZ lig tabanındaki ev/deplasman gol farkıdır; bütün takımlar
        /// aynı kabul edilir. η_t takımın kendi sahasındaki artık üstünlüğüdür: λ_ev ×= e^{η_ev}, λ_dep ×= e^{−η_ev}.
        ///
        /// ÖLÇÜM SONUCU (18.09.2026, 5.328 zamansal test maçı): η eğitim ve kalibrasyon pencerelerinde kaybı DÜŞÜRÜYOR
        /// (2,38236 → 2,38160 / 2,40384 → 2,40052) ama TEST penceresinde BOZUYOR: 1X2 log loss 1,0061 → 1,0063 → 1,0072 → 1,0101,
        /// ECE 0,0100 → 0,0182, ev sapması +0,003 → +0,012. Nedeni: iç saha üstünlüğü uydurma penceresi ile test penceresi
        /// arasında GERİLEDİ; iç sahayı büyüten her katman geçmişe uyup geleceği şişiriyor. Aynı sonuç lig bazlı
        /// <see cref="HomeTilt"/> için de çıktı. Bu yüzden 0 (kapalı) bırakıldı — kanıt gelmeden açılmaz.
        /// </summary>
        public double TeamHomeEdgeRate { get; set; } = 0;
        /// <summary>η'nin her güncellemede 0'a (lig ortalamasına) çekilme payı — küçük örneklemde takım üstünlüğü uydurulmaz.</summary>
        public double TeamHomeEdgeDecay { get; set; } = 0.01;
        public double TeamHomeEdgeCap { get; set; } = 0.35;

        // ── Kalibrasyon (dağılımın kendisine uygulanır: bütün marketler aynı matristen türemeye devam eder) ──
        /// <summary>Beklenen gollerin ölçeği (toplam gol yanlılığını düzeltir).</summary>
        public double GoalScale { get; set; } = 1.0;
        /// <summary>Köşegen (beraberlik) ağırlığı; bağımsız Poisson'un beraberlik yanlılığını düzeltir.</summary>
        public double DrawInflation { get; set; } = 1.0;
        /// <summary>
        /// Toplam gol etkisinin korunan payı γ: log λ sapmaları fark (q) ve toplam (t) bileşenine ayrılır, λ_ev = taban·e^(γt+q),
        /// λ_dep = taban·e^(γt−q). γ &lt; 1 takım düzeyindeki gürültülü toplam gol sinyalini lig ortalamasına daraltır, 1X2 farkını korur;
        /// bütün marketler yine TEK matristen türer.
        /// </summary>
        public double TotalGoalShrink { get; set; } = 1.0;
        /// <summary>Lig taban dağılımıyla sabit karışım (aşırı güveni tavlar).</summary>
        public double BaselineMix { get; set; } = 0.0;
        /// <summary>Veri kapsamı düştükçe taban dağılıma eklenen karışım (belirsizlik artar).</summary>
        public double UncertaintyMix { get; set; } = 0.35;
        /// <summary>Lig başına gol ölçeği (küçük örneklemde global değere daraltılmış).</summary>
        public Dictionary<int, double> LeagueGoalScale { get; set; } = new();

        // ── SINANMIŞ ama KANITLANMAMIŞ dağılım katmanları (18.09.2026) — hepsi varsayılan olarak ETKİSİZ. ──
        // Aramaya DAHİL EDİLMEZ: ölçümleri aşağıda; yeniden denenecekse önce bu ölçümler çürütülmelidir.
        /// <summary>
        /// DÜŞÜK SKOR BAĞIMLILIĞI (Dixon–Coles ρ) — bağımsız Poisson'un 0-0, 1-0, 0-1 ve 1-1 hücrelerindeki sistematik hatasını
        /// düzeltir. ρ &lt; 0 beraberlik hücrelerini büyütür. <see cref="DrawInflation"/>'dan farkı: köşegenin TAMAMINI (3-3, 4-4)
        /// değil yalnız düşük skor hücrelerini değiştirir.
        ///
        /// ÖLÇÜM SONUCU: kalibrasyon penceresinde en iyi değer ρ = 0 çıktı (ρ=−0,04 → 2,40077, ρ=−0,08 → 2,40151, ρ=0 → 2,40054);
        /// test penceresinde de 1X2 log loss ρ ile KÖTÜLEŞİYOR (1,0087 → 1,0091 → 1,0100 → 1,0131). Mevcut <see cref="DrawInflation"/>
        /// (1,08) ile ρ birbirinin YERİNE geçiyor (ρ=−0,08 + draw=1,00 ≈ ρ=0 + draw=1,08); yani DC bu veride EK bilgi getirmiyor.
        /// </summary>
        public double LowScoreRho { get; set; } = 0;
        /// <summary>
        /// İÇ SAHA EĞİMİ δ — λ_ev ×= e^{δ}, λ_dep ×= e^{−δ}. Toplam gol beklentisini değiştirmeden ev/deplasman dengesini kaydırır.
        ///
        /// ÖLÇÜM SONUCU: kalibrasyon penceresinde her lig için POZİTİF seçiliyor (+0,006 … +0,056) ama test penceresinde ev sapmasını
        /// −0,002'den +0,013'e çıkarıyor ve 1X2 log loss'u 1,0087 → 1,0097 kötüleştiriyor. <see cref="TeamHomeEdgeRate"/> ile aynı
        /// kök neden: iç saha avantajı uydurma penceresinden test penceresine geriledi.
        /// </summary>
        public double HomeTilt { get; set; } = 0;
        /// <summary>Lig başına beraberlik ağırlığı (boşsa global <see cref="DrawInflation"/>). ÖLÇÜM: lig bazlı kalibrasyon
        /// örneklemi 73–260 maç; K=200 daraltmasıyla bile UEFA Avrupa Ligi'ndeki +0,082 beraberlik sapmasını kapatamadı (+0,079).</summary>
        public Dictionary<int, double> LeagueDrawInflation { get; set; } = new();
        /// <summary>Lig başına iç saha eğimi (boşsa global <see cref="HomeTilt"/>). ÖLÇÜM: bkz. <see cref="HomeTilt"/> — reddedildi.</summary>
        public Dictionary<int, double> LeagueHomeTilt { get; set; } = new();
        /// <summary>Lig başına düşük skor bağımlılığı (boşsa global <see cref="LowScoreRho"/>).</summary>
        public Dictionary<int, double> LeagueLowScoreRho { get; set; } = new();

        // ── Kalibrasyon penceresinden ölçülen güvenlik sınırları (keyfî sabit değil) ──
        /// <summary>Kalibrasyon penceresinde güvenilirliği doğrulanmış en yüksek 1X2 olasılığı; üstü "kanıtsız aşırı olasılık".</summary>
        public double MaxSupportedProbability { get; set; } = 0.80;
        /// <summary>Model ile bağımsız Elo arasındaki beklenen puan farkının kalibrasyon penceresi 99. yüzdeliği.</summary>
        public double EloConflictThreshold { get; set; } = 0.25;
        /// <summary>Gol artığının standart sapması (snapshot değişim sınırında kullanılır).</summary>
        public double GoalResidualStd { get; set; } = 1.15;

        public OutcomeModelParameters Clone() => new()
        {
            LearningRate = LearningRate, LeagueAlpha = LeagueAlpha, DefaultHomeGoals = DefaultHomeGoals, DefaultAwayGoals = DefaultAwayGoals,
            FullCoverageSample = FullCoverageSample, MinSample = MinSample, StaleDays = StaleDays,
            CrossLeagueAware = CrossLeagueAware, StrengthLearningRate = StrengthLearningRate, MinLeagueLinks = MinLeagueLinks, CrossLeagueTeamWeight = CrossLeagueTeamWeight,
            LeagueStrengthPriorSd = LeagueStrengthPriorSd, StrengthRefitDays = StrengthRefitDays, StrengthHalfLifeDays = StrengthHalfLifeDays,
            SeasonCarry = SeasonCarry, SeasonBreakDays = SeasonBreakDays,
            TeamHomeEdgeRate = TeamHomeEdgeRate, TeamHomeEdgeDecay = TeamHomeEdgeDecay, TeamHomeEdgeCap = TeamHomeEdgeCap,
            GoalScale = GoalScale, TotalGoalShrink = TotalGoalShrink, DrawInflation = DrawInflation, BaselineMix = BaselineMix, UncertaintyMix = UncertaintyMix,
            LeagueGoalScale = new Dictionary<int, double>(LeagueGoalScale),
            LowScoreRho = LowScoreRho, HomeTilt = HomeTilt,
            LeagueDrawInflation = new Dictionary<int, double>(LeagueDrawInflation),
            LeagueHomeTilt = new Dictionary<int, double>(LeagueHomeTilt),
            LeagueLowScoreRho = new Dictionary<int, double>(LeagueLowScoreRho),
            MaxSupportedProbability = MaxSupportedProbability, EloConflictThreshold = EloConflictThreshold, GoalResidualStd = GoalResidualStd
        };

        /// <summary>Bu maçın organizasyonu için geçerli kalibrasyon sabitleri (lig değeri yoksa global değer).</summary>
        public (double Scale, double Draw, double Tilt, double Rho) ForLeague(int leagueId) => (
            LeagueGoalScale.TryGetValue(leagueId, out var s) ? s : GoalScale,
            LeagueDrawInflation.TryGetValue(leagueId, out var d) ? d : DrawInflation,
            LeagueHomeTilt.TryGetValue(leagueId, out var t) ? t : HomeTilt,
            LeagueLowScoreRho.TryGetValue(leagueId, out var r) ? r : LowScoreRho);

        /// <summary>2.0 davranışı (karşılaştırma için): ligler arası katman ve sezon daraltması kapalı.</summary>
        public static OutcomeModelParameters Legacy() => new() { CrossLeagueAware = false, StrengthLearningRate = 0, SeasonCarry = 1.0 };
    }

    /// <summary>Takım reytingi — yalnız geçmiş maçlardan, sırayla güncellenir (zamansal sızıntı yok).</summary>
    public sealed class TeamRating
    {
        public double LogAttack;
        public double LogDefence;
        public int Matches;
        public DateTime? LastMatchUtc;
        /// <summary>Takımın ev ligi (en son oynadığı lig türü organizasyon).</summary>
        public int? HomeLeague;
        public DateTime? HomeLeagueSeenUtc;
        /// <summary>Bağımsız kontrol reytingi (sonuç tabanlı Elo, lig ofsetsiz takım değeri).</summary>
        public double Elo = 1500;
        /// <summary>Takımın kendi sahasındaki artık üstünlüğü (log ölçek; 0 = lig ortalaması kadar).</summary>
        public double HomeEdge;
        public int HomeMatches;
        /// <summary>Son 10 maç (attığı, yediği, iç saha mı).</summary>
        public readonly Queue<(int For, int Against, bool Home)> Recent = new();
    }

    public sealed class LeagueGoalState
    {
        public double Home;
        public double Away;
        public int Matches;
    }

    /// <summary>Ligin ortak ölçekteki gücü — ligler arası maçlardan öğrenilir.</summary>
    public sealed class LeagueStrengthState
    {
        /// <summary>Log-gol ölçeğinde güç ofseti (0 = başlangıç; fark önemlidir).</summary>
        public double Strength;
        /// <summary>Elo ölçeğinde lig ofseti (bağımsız kontrol).</summary>
        public double EloOffset;
        /// <summary>Bu ligin takımlarının oynadığı ligler arası maç sayısı (grafik bağlantısı).</summary>
        public int Links;
        public HashSet<int> LinkedLeagues { get; } = new();
    }

    /// <summary>Tek maç için modelin girdileri (sonuç bilinmeden hesaplanır).</summary>
    public sealed record OutcomeExpectation(
        double LambdaHome, double LambdaAway, double LeagueHome, double LeagueAway,
        int HomeSample, int AwaySample, double Coverage, bool Sufficient,
        double HomeRecentFor, double HomeRecentAgainst, double AwayRecentFor, double AwayRecentAgainst, int HomeRecentCount, int AwayRecentCount,
        DateTime? HomeLastMatchUtc, DateTime? AwayLastMatchUtc)
    {
        /// <summary>İki takımın ev ligi farklı mı (ligler arası maç)?</summary>
        public bool CrossLeague { get; init; }
        public int? HomeLeagueId { get; init; }
        public int? AwayLeagueId { get; init; }
        public double HomeLeagueStrength { get; init; }
        public double AwayLeagueStrength { get; init; }
        public int HomeLeagueLinks { get; init; }
        public int AwayLeagueLinks { get; init; }
        public double HomeLogAttack { get; init; }
        public double HomeLogDefence { get; init; }
        public double AwayLogAttack { get; init; }
        public double AwayLogDefence { get; init; }
        /// <summary>Bağımsız Elo'ya göre ev sahibinin beklenen puan payı (0..1; beraberlik yarım).</summary>
        public double EloHomeExpectation { get; init; } = 0.5;
        public double HomeClubRating { get; init; }
        public double AwayClubRating { get; init; }
        /// <summary>Tahmin YAYIMLANMAMA nedenleri (boşsa kapı açık).</summary>
        public IReadOnlyList<string> GateReasons { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// ONLINE POISSON REYTİNG MODELİ (3.0) — her takım için ev ligine göreli log-hücum ve log-savunma, her lig için ortak ölçekte
    /// güç ofseti S:
    ///   λ_ev  = taban_ev  × e^(hücum_ev + savunma_dep + (S_evLig − S_depLig)/2)
    ///   λ_dep = taban_dep × e^(hücum_dep + savunma_ev − (S_evLig − S_depLig)/2)
    /// Lig içi maçta ofsetler sadeleşir (2.0 ile aynı). Ligler arası maçta S, Poisson log-olabilirlik gradyanıyla güncellenir.
    /// Takım lig değiştirirse (yükselme/düşme) mutlak gücü korunacak biçimde reytingi ölçek farkı kadar kaydırılır. Her maçın
    /// tahmini yalnız o maçtan önce bitmiş maçları görür.
    /// </summary>
    public sealed class OutcomeRatingModel
    {
        public const double EloHomeAdvantage = 60;
        public const double EloK = 20;
        public const double EloLeagueK = 20;

        private readonly Dictionary<int, TeamRating> _teams = new();
        private readonly Dictionary<int, LeagueGoalState> _leagues = new();
        private readonly Dictionary<int, LeagueStrengthState> _strength = new();
        private readonly List<(int HomeLeague, int AwayLeague, double BaseLogHome, double BaseLogAway, int HomeGoals, int AwayGoals, DateTime At)> _crossRecords = new();
        private DateTime? _lastRefitUtc;
        public OutcomeModelParameters Parameters { get; }
        public CompetitionCatalog Catalog { get; }
        public DateTime? LastUpdateUtc { get; private set; }
        public int ProcessedMatches { get; private set; }

        public OutcomeRatingModel(OutcomeModelParameters parameters) : this(parameters, CompetitionCatalog.Unclassified) { }

        public OutcomeRatingModel(OutcomeModelParameters parameters, CompetitionCatalog catalog)
        {
            Parameters = parameters;
            Catalog = catalog;
        }

        private TeamRating Team(int id) => _teams.TryGetValue(id, out var t) ? t : _teams[id] = new TeamRating();

        private LeagueGoalState League(int id)
            => _leagues.TryGetValue(id, out var l) ? l : _leagues[id] = new LeagueGoalState { Home = Parameters.DefaultHomeGoals, Away = Parameters.DefaultAwayGoals };

        private LeagueStrengthState Strength(int id) => _strength.TryGetValue(id, out var s) ? s : _strength[id] = new LeagueStrengthState();

        public LeagueStrengthState? GetLeagueStrength(int leagueId) => _strength.GetValueOrDefault(leagueId);
        public TeamRating? GetTeam(int teamId) => _teams.GetValueOrDefault(teamId);
        public IReadOnlyDictionary<int, LeagueStrengthState> LeagueStrengths => _strength;

        private bool CrossLeagueOn => Parameters.CrossLeagueAware && Catalog.LeagueCount > 0;

        /// <summary>Sezon arası daraltma çarpanı (tahmin anında sanal; güncellemede kalıcı uygulanır).</summary>
        private double Carry(TeamRating? t, DateTime at)
            => t?.LastMatchUtc is DateTime last && (at - last).TotalDays > Parameters.SeasonBreakDays ? Parameters.SeasonCarry : 1.0;

        public OutcomeExpectation Expect(int leagueId, int homeTeamId, int awayTeamId, DateTime kickoffUtc)
        {
            var h = _teams.GetValueOrDefault(homeTeamId);
            var a = _teams.GetValueOrDefault(awayTeamId);
            var lg = _leagues.GetValueOrDefault(leagueId) ?? new LeagueGoalState { Home = Parameters.DefaultHomeGoals, Away = Parameters.DefaultAwayGoals };
            var ch = Carry(h, kickoffUtc);
            var ca = Carry(a, kickoffUtc);
            var hAtt = (h?.LogAttack ?? 0) * ch; var hDef = (h?.LogDefence ?? 0) * ch;
            var aAtt = (a?.LogAttack ?? 0) * ca; var aDef = (a?.LogDefence ?? 0) * ca;

            var gates = new List<string>();
            var cross = false;
            double sH = 0, sA = 0;
            int linksH = 0, linksA = 0;
            // Maçın oynandığı organizasyon bir lig ise ev ligi o lig kabul edilir (lig maçı lig içidir).
            var isLeague = Catalog.IsLeague(leagueId);
            int? hl = isLeague ? leagueId : h?.HomeLeague;
            int? al = isLeague ? leagueId : a?.HomeLeague;
            if (CrossLeagueOn && !isLeague)
            {
                if (hl == null || al == null) gates.Add("TEAM_LEAGUE_UNKNOWN");
                else if (hl != al)
                {
                    cross = true;
                    var shs = _strength.GetValueOrDefault(hl.Value);
                    var sas = _strength.GetValueOrDefault(al.Value);
                    sH = shs?.Strength ?? 0; sA = sas?.Strength ?? 0;
                    linksH = shs?.Links ?? 0; linksA = sas?.Links ?? 0;
                    if (Math.Min(linksH, linksA) < Parameters.MinLeagueLinks) gates.Add("CROSS_LEAGUE_UNLINKED");
                }
            }
            var off = (sH - sA) / 2;
            // Takım düzeyi iç saha üstünlüğü (kapalıyken 0): ev sahibinin kendi sahasındaki artık üstünlüğü.
            var edge = (h?.HomeEdge ?? 0) * ch;
            var lh = Clamp(lg.Home * Math.Exp(hAtt + aDef + off + edge), 0.15, 4.5);
            var la = Clamp(lg.Away * Math.Exp(aAtt + hDef - off - edge), 0.15, 4.5);
            var nh = h?.Matches ?? 0;
            var na = a?.Matches ?? 0;
            var coverage = Math.Clamp(Math.Min(nh, na) / (double)Parameters.FullCoverageSample, 0, 1);
            if (IsStale(h, kickoffUtc) || IsStale(a, kickoffUtc)) coverage *= 0.6;
            if (cross) coverage *= Math.Clamp(Math.Min(linksH, linksA) / (2.0 * Math.Max(1, Parameters.MinLeagueLinks)), 0, 1);
            if (Math.Min(nh, na) < Parameters.MinSample) gates.Insert(0, "INSUFFICIENT_SAMPLE");

            var (hf, hg, hc) = RecentAverages(h);
            var (af, ag, ac) = RecentAverages(a);
            var eloH = (h?.Elo ?? 1500) + (cross ? _strength.GetValueOrDefault(hl!.Value)?.EloOffset ?? 0 : 0);
            var eloA = (a?.Elo ?? 1500) + (cross ? _strength.GetValueOrDefault(al!.Value)?.EloOffset ?? 0 : 0);
            var eloExp = 1.0 / (1 + Math.Pow(10, -(eloH - eloA + EloHomeAdvantage) / 400));

            return new OutcomeExpectation(lh, la, lg.Home, lg.Away, nh, na, coverage, gates.Count == 0,
                hf, hg, af, ag, hc, ac, h?.LastMatchUtc, a?.LastMatchUtc)
            {
                CrossLeague = cross, HomeLeagueId = hl, AwayLeagueId = al,
                HomeLeagueStrength = Math.Round(sH, 4), AwayLeagueStrength = Math.Round(sA, 4),
                HomeLeagueLinks = linksH, AwayLeagueLinks = linksA,
                HomeLogAttack = hAtt, HomeLogDefence = hDef, AwayLogAttack = aAtt, AwayLogDefence = aDef,
                EloHomeExpectation = eloExp, HomeClubRating = Math.Round(eloH), AwayClubRating = Math.Round(eloA),
                GateReasons = gates
            };
        }

        private bool IsStale(TeamRating? t, DateTime kickoffUtc)
            => t?.LastMatchUtc is DateTime last && (kickoffUtc - last).TotalDays > Parameters.StaleDays;

        private static (double For, double Against, int Count) RecentAverages(TeamRating? t)
            => t == null || t.Recent.Count == 0 ? (0, 0, 0) : (t.Recent.Average(r => r.For), t.Recent.Average(r => r.Against), t.Recent.Count);

        /// <summary>Bitmiş maçı modele işler (tahmin YAPILDIKTAN sonra çağrılır).</summary>
        public void Update(HistoricalMatch m)
        {
            if (Catalog.IsExcluded(m.LeagueId)) return; // hazırlık maçı: reytinge girmez
            var h = Team(m.HomeTeamId);
            var a = Team(m.AwayTeamId);
            ApplyCarry(h, m.KickoffUtc);
            ApplyCarry(a, m.KickoffUtc);

            var isLeague = Catalog.IsLeague(m.LeagueId);
            if (CrossLeagueOn && isLeague)
            {
                MoveToLeague(h, m.LeagueId, m.KickoffUtc);
                MoveToLeague(a, m.LeagueId, m.KickoffUtc);
            }

            var cross = CrossLeagueOn && !isLeague && h.HomeLeague != null && a.HomeLeague != null && h.HomeLeague != a.HomeLeague;
            LeagueStrengthState? sh = cross ? Strength(h.HomeLeague!.Value) : null;
            LeagueStrengthState? sa = cross ? Strength(a.HomeLeague!.Value) : null;
            var off = cross ? (sh!.Strength - sa!.Strength) / 2 : 0;

            var lg = League(m.LeagueId);
            var lh = Clamp(lg.Home * Math.Exp(h.LogAttack + a.LogDefence + off + h.HomeEdge), 0.15, 4.5);
            var la = Clamp(lg.Away * Math.Exp(a.LogAttack + h.LogDefence - off - h.HomeEdge), 0.15, 4.5);
            var eta = Parameters.LearningRate * (cross ? Parameters.CrossLeagueTeamWeight : 1.0);
            var errH = Math.Clamp(m.HomeGoals - lh, -4, 4);
            var errA = Math.Clamp(m.AwayGoals - la, -4, 4);
            if (Parameters.TeamHomeEdgeRate > 0)
            {
                h.HomeEdge = Clamp(h.HomeEdge * (1 - Parameters.TeamHomeEdgeDecay) + Parameters.TeamHomeEdgeRate * (errH - errA) / 2,
                    -Parameters.TeamHomeEdgeCap, Parameters.TeamHomeEdgeCap);
                h.HomeMatches++;
            }
            h.LogAttack = Clamp(h.LogAttack + eta * errH, -1.6, 1.6);
            a.LogDefence = Clamp(a.LogDefence + eta * errH, -1.6, 1.6);
            a.LogAttack = Clamp(a.LogAttack + eta * errA, -1.6, 1.6);
            h.LogDefence = Clamp(h.LogDefence + eta * errA, -1.6, 1.6);

            // Bağımsız Elo (sonuç tabanlı) — ligler arası maçta lig ofseti de güncellenir.
            var eloH = h.Elo + (cross ? sh!.EloOffset : 0);
            var eloA = a.Elo + (cross ? sa!.EloOffset : 0);
            var we = 1.0 / (1 + Math.Pow(10, -(eloH - eloA + EloHomeAdvantage) / 400));
            var w = m.HomeGoals > m.AwayGoals ? 1.0 : m.HomeGoals == m.AwayGoals ? 0.5 : 0.0;
            var mult = 1 + Math.Log(1 + Math.Abs(m.HomeGoals - m.AwayGoals));
            var teamK = EloK * (cross ? Parameters.CrossLeagueTeamWeight : 1.0);
            h.Elo += teamK * mult * (w - we);
            a.Elo -= teamK * mult * (w - we);

            if (cross)
            {
                _crossRecords.Add((h.HomeLeague!.Value, a.HomeLeague!.Value, Math.Log(lg.Home) + h.LogAttack - eta * errH + a.LogDefence - eta * errH,
                    Math.Log(lg.Away) + a.LogAttack - eta * errA + h.LogDefence - eta * errA, m.HomeGoals, m.AwayGoals, m.KickoffUtc));
                var g = Parameters.StrengthLearningRate * (errH - errA) / 2;
                sh!.Strength = Clamp(sh.Strength + g, -3, 3);
                sa!.Strength = Clamp(sa.Strength - g, -3, 3);
                sh.EloOffset += EloLeagueK * mult * (w - we);
                sa.EloOffset -= EloLeagueK * mult * (w - we);
                sh.Links++; sa.Links++;
                sh.LinkedLeagues.Add(a.HomeLeague!.Value);
                sa.LinkedLeagues.Add(h.HomeLeague!.Value);
            }

            var alpha = lg.Matches < 50 ? Math.Max(Parameters.LeagueAlpha, 1.0 / (lg.Matches + 2)) : Parameters.LeagueAlpha;
            lg.Home += alpha * (m.HomeGoals - lg.Home);
            lg.Away += alpha * (m.AwayGoals - lg.Away);
            lg.Matches++;
            Push(h, m.HomeGoals, m.AwayGoals, true, m.KickoffUtc);
            Push(a, m.AwayGoals, m.HomeGoals, false, m.KickoffUtc);
            LastUpdateUtc = m.KickoffUtc;
            ProcessedMatches++;
            if (cross && (_lastRefitUtc == null || (m.KickoffUtc - _lastRefitUtc.Value).TotalDays >= Parameters.StrengthRefitDays))
                RefitLeagueStrengths(m.KickoffUtc);
        }

        /// <summary>
        /// LİG GÜÇLERİNİN TOPLU ÇÖZÜMÜ — o ana kadarki bütün ligler arası maçlarda (takım göreli reytingleri maç anındaki hâliyle sabit)
        /// Poisson log-olabilirlik + ridge önseli (S ~ N(0, σ²)) en büyüklenir; zaman ağırlığı yarı ömürle azalır. Jacobi-Newton
        /// yinelemesi. Çevrimiçi adımlar bağlantı zinciri boyunca farkı çok yavaş taşıdığı için (ölçüm 17.09.2026: Kıbrıs−La Liga
        /// 0,33'te kaldı) güç ölçeği grafiğin tamamından birlikte çözülür. Yalnız GEÇMİŞ maçlar kullanılır.
        /// </summary>
        public void RefitLeagueStrengths(DateTime asOfUtc)
        {
            _lastRefitUtc = asOfUtc;
            if (_crossRecords.Count == 0) return;
            var leagues = _crossRecords.SelectMany(r => new[] { r.HomeLeague, r.AwayLeague }).Distinct().ToList();
            var S = leagues.ToDictionary(l => l, l => _strength.GetValueOrDefault(l)?.Strength ?? 0.0);
            var prior = 1.0 / (Parameters.LeagueStrengthPriorSd * Parameters.LeagueStrengthPriorSd);
            var weights = _crossRecords.Select(r => Math.Pow(0.5, Math.Max(0, (asOfUtc - r.At).TotalDays) / Math.Max(1, Parameters.StrengthHalfLifeDays))).ToArray();
            var grad = new Dictionary<int, double>();
            var hess = new Dictionary<int, double>();
            for (var iter = 0; iter < 40; iter++)
            {
                foreach (var l in leagues) { grad[l] = -prior * S[l]; hess[l] = prior; }
                for (var i = 0; i < _crossRecords.Count; i++)
                {
                    var r = _crossRecords[i];
                    var w = weights[i];
                    var d = (S[r.HomeLeague] - S[r.AwayLeague]) / 2;
                    var lh = Math.Exp(Math.Clamp(r.BaseLogHome + d, -3, 2));
                    var la = Math.Exp(Math.Clamp(r.BaseLogAway - d, -3, 2));
                    var g = w * ((r.HomeGoals - lh) - (r.AwayGoals - la)) / 2;
                    var hh = w * (lh + la) / 4;
                    grad[r.HomeLeague] += g; hess[r.HomeLeague] += hh;
                    grad[r.AwayLeague] -= g; hess[r.AwayLeague] += hh;
                }
                double maxStep = 0;
                foreach (var l in leagues)
                {
                    var step = grad[l] / hess[l];
                    S[l] = Clamp(S[l] + 0.7 * step, -3, 3);
                    maxStep = Math.Max(maxStep, Math.Abs(step));
                }
                if (maxStep < 1e-4) break;
            }
            foreach (var l in leagues) Strength(l).Strength = Math.Round(S[l], 5);
        }

        private void ApplyCarry(TeamRating t, DateTime at)
        {
            var c = Carry(t, at);
            if (c >= 1) return;
            t.LogAttack *= c;
            t.LogDefence *= c;
            t.HomeEdge *= c;
            t.Elo = 1500 + (t.Elo - 1500) * c;
        }

        /// <summary>
        /// Takım lig değiştirirse mutlak gücü korunur: mutlak hücum = hücum + S/2, mutlak savunma = savunma − S/2.
        /// Elo tarafında da takım değeri lig ofseti farkı kadar kaydırılır.
        /// </summary>
        private void MoveToLeague(TeamRating t, int leagueId, DateTime at)
        {
            if (t.HomeLeague == leagueId) { t.HomeLeagueSeenUtc = at; return; }
            if (t.HomeLeague is int old)
            {
                var so = _strength.GetValueOrDefault(old);
                var sn = _strength.GetValueOrDefault(leagueId);
                var d = (so?.Strength ?? 0) - (sn?.Strength ?? 0);
                t.LogAttack = Clamp(t.LogAttack + d / 2, -1.6, 1.6);
                t.LogDefence = Clamp(t.LogDefence - d / 2, -1.6, 1.6);
                t.Elo += (so?.EloOffset ?? 0) - (sn?.EloOffset ?? 0);
            }
            t.HomeLeague = leagueId;
            t.HomeLeagueSeenUtc = at;
        }

        private static void Push(TeamRating t, int gf, int ga, bool home, DateTime at)
        {
            t.Matches++;
            t.LastMatchUtc = at;
            t.Recent.Enqueue((gf, ga, home));
            while (t.Recent.Count > 10) t.Recent.Dequeue();
        }

        private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
    }

    /// <summary>
    /// TUTARLI SKOR DAĞILIMI — P(Ev = h, Dep = a), 0..10 gol. Bütün marketler (1X2, çifte şans, alt/üst, KG, takım golü,
    /// temiz kale, gol aralığı, en olası skorlar) YALNIZ bu matristen türetilir; toplamlar yapısal olarak tutarlıdır.
    /// </summary>
    public sealed class ScoreDistribution
    {
        public const int MaxGoals = 10;
        private readonly double[,] _p = new double[MaxGoals + 1, MaxGoals + 1];

        private ScoreDistribution() { }

        public double this[int h, int a] => _p[h, a];

        public static ScoreDistribution Poisson(double lambdaHome, double lambdaAway, double drawInflation = 1.0)
            => Poisson(lambdaHome, lambdaAway, drawInflation, 0.0);

        /// <summary>
        /// <paramref name="lowScoreRho"/> — Dixon–Coles düşük skor düzeltmesi: yalnız (0,0), (0,1), (1,0) ve (1,1) hücrelerini
        /// çarpanla değiştirir (τ). ρ = 0 bağımsız Poisson'dur. Hücreler 0'ın altına düşmeyecek biçimde sınırlanır; dağılım
        /// her hâlükârda normalize edilir, bu yüzden bütün market toplamları %100 kalır.
        /// </summary>
        public static ScoreDistribution Poisson(double lambdaHome, double lambdaAway, double drawInflation, double lowScoreRho)
        {
            var d = new ScoreDistribution();
            var hp = Pmf(lambdaHome);
            var ap = Pmf(lambdaAway);
            var lh = Math.Max(0.01, lambdaHome);
            var la = Math.Max(0.01, lambdaAway);
            // ρ'nun geçerli aralığı λ'lara bağlıdır; dışına çıkılırsa negatif olasılık üretilirdi.
            var rho = Math.Clamp(lowScoreRho, Math.Max(-1.0 / (lh * la), -1.0), Math.Min(1.0 / (lh * la), 1.0));
            double total = 0;
            for (var i = 0; i <= MaxGoals; i++)
                for (var j = 0; j <= MaxGoals; j++)
                {
                    var tau = rho == 0 ? 1.0
                        : i == 0 && j == 0 ? 1 - lh * la * rho
                        : i == 0 && j == 1 ? 1 + lh * rho
                        : i == 1 && j == 0 ? 1 + la * rho
                        : i == 1 && j == 1 ? 1 - rho
                        : 1.0;
                    var v = hp[i] * ap[j] * (i == j ? drawInflation : 1.0) * Math.Max(0, tau);
                    d._p[i, j] = v;
                    total += v;
                }
            d.Normalize(total);
            return d;
        }

        /// <summary>İki dağılımın karışımı: (1−w)·this + w·other.</summary>
        public ScoreDistribution Mix(ScoreDistribution other, double w)
        {
            w = Math.Clamp(w, 0, 1);
            var d = new ScoreDistribution();
            for (var i = 0; i <= MaxGoals; i++)
                for (var j = 0; j <= MaxGoals; j++)
                    d._p[i, j] = (1 - w) * _p[i, j] + w * other._p[i, j];
            return d;
        }

        /// <summary>
        /// SONUÇ SINIFI YENİDEN AĞIRLIKLANDIRMA — skor matrisini, 1X2 marjinalleri verilen (ev, beraberlik, deplasman) olacak biçimde
        /// sınıf içinde orantılı ölçekler. Her sınıfın kendi skor şekli korunur; çifte şans ve gol marketleri aynı matristen
        /// tutarlı türetilir, toplam 1 kalır. Hedef ≤ 0 ya da sınıf boşsa matris değişmeden döner.
        /// </summary>
        public ScoreDistribution ReweightResult(double home, double draw, double away)
        {
            double h = HomeWin, x = Draw, a = AwayWin;
            if (home <= 0 || draw <= 0 || away <= 0 || h <= 0 || x <= 0 || a <= 0) return this;
            var z = home + draw + away;
            double fh = home / z / h, fx = draw / z / x, fa = away / z / a;
            var d = new ScoreDistribution();
            for (var i = 0; i <= MaxGoals; i++)
                for (var j = 0; j <= MaxGoals; j++)
                    d._p[i, j] = _p[i, j] * (i > j ? fh : i == j ? fx : fa);
            return d;
        }

        private void Normalize(double total)
        {
            if (total <= 0) return;
            for (var i = 0; i <= MaxGoals; i++)
                for (var j = 0; j <= MaxGoals; j++)
                    _p[i, j] /= total;
        }

        private static double[] Pmf(double lambda)
        {
            lambda = Math.Max(0.01, lambda);
            var p = new double[MaxGoals + 1];
            p[0] = Math.Exp(-lambda);
            for (var k = 1; k <= MaxGoals; k++) p[k] = p[k - 1] * lambda / k;
            return p;
        }

        private double Sum(Func<int, int, bool> pred)
        {
            double s = 0;
            for (var i = 0; i <= MaxGoals; i++)
                for (var j = 0; j <= MaxGoals; j++)
                    if (pred(i, j)) s += _p[i, j];
            return s;
        }

        public double Total => Sum((_, _) => true);
        public double HomeWin => Sum((h, a) => h > a);
        public double Draw => Sum((h, a) => h == a);
        public double AwayWin => Sum((h, a) => h < a);
        public double Over(double line) => Sum((h, a) => h + a > line);
        public double Under(double line) => Sum((h, a) => h + a < line);
        public double BttsYes => Sum((h, a) => h > 0 && a > 0);
        public double BttsNo => Sum((h, a) => h == 0 || a == 0);
        public double HomeScores => Sum((h, _) => h > 0);
        public double AwayScores => Sum((_, a) => a > 0);
        public double HomeCleanSheet => Sum((_, a) => a == 0);
        public double AwayCleanSheet => Sum((h, _) => h == 0);
        public double TotalBetween(int min, int max) => Sum((h, a) => h + a >= min && h + a <= max);
        public double ExpectedTotalGoals => ExpectedHome + ExpectedAway;
        public double ExpectedHome { get { double s = 0; for (var i = 0; i <= MaxGoals; i++) for (var j = 0; j <= MaxGoals; j++) s += i * _p[i, j]; return s; } }
        public double ExpectedAway { get { double s = 0; for (var i = 0; i <= MaxGoals; i++) for (var j = 0; j <= MaxGoals; j++) s += j * _p[i, j]; return s; } }

        public IReadOnlyList<(int Home, int Away, double P)> TopScores(int count)
        {
            var list = new List<(int, int, double)>();
            for (var i = 0; i <= MaxGoals; i++)
                for (var j = 0; j <= MaxGoals; j++)
                    list.Add((i, j, _p[i, j]));
            return list.OrderByDescending(x => x.Item3).ThenBy(x => x.Item1 + x.Item2).Take(count).ToList();
        }
    }

    /// <summary>Tek maç tahmini — ham ve kalibre dağılım + taban dağılım.</summary>
    public sealed record OutcomePrediction(
        OutcomeExpectation Expectation, ScoreDistribution Raw, ScoreDistribution Calibrated, ScoreDistribution Baseline, double UncertaintyWeight);

    /// <summary>Beklenti → dağılımlar. Kalibrasyon parametreleri dağılımın kendisine uygulanır.</summary>
    public static class OutcomePredictor
    {
        public static OutcomePrediction Predict(OutcomeExpectation e, int leagueId, OutcomeModelParameters p)
        {
            var raw = ScoreDistribution.Poisson(e.LambdaHome, e.LambdaAway);
            var (scale, draw, tilt, rho) = p.ForLeague(leagueId);
            // İç saha eğimi toplam gol beklentisini değiştirmez: e^{+δ} ve e^{−δ} çarpımı 1'dir.
            var th = Math.Exp(tilt);
            var ta = Math.Exp(-tilt);
            var baseline = ScoreDistribution.Poisson(e.LeagueHome * scale * th, e.LeagueAway * scale * ta, draw, rho);
            var (lh, la) = Shrink(e, p.TotalGoalShrink);
            var model = ScoreDistribution.Poisson(lh * scale * th, la * scale * ta, draw, rho);
            var w = Math.Clamp(p.BaselineMix + p.UncertaintyMix * (1 - e.Coverage), 0, 0.85);
            return new OutcomePrediction(e, raw, model.Mix(baseline, w), baseline, w);
        }

        /// <summary>Toplam gol bileşenini γ ile daraltılmış beklenen goller (fark bileşeni aynen korunur).</summary>
        public static (double Home, double Away) Shrink(OutcomeExpectation e, double gamma)
        {
            if (Math.Abs(gamma - 1) < 1e-9 || e.LeagueHome <= 0 || e.LeagueAway <= 0) return (e.LambdaHome, e.LambdaAway);
            var uh = Math.Log(e.LambdaHome / e.LeagueHome);
            var ua = Math.Log(e.LambdaAway / e.LeagueAway);
            var t = (uh + ua) / 2;
            var q = (uh - ua) / 2;
            return (e.LeagueHome * Math.Exp(gamma * t + q), e.LeagueAway * Math.Exp(gamma * t - q));
        }

        /// <summary>
        /// MAÇ DÜZEYİ GÜVENLİK KAPISI — model çıktısı hesaplandıktan sonra (kalibre dağılım üzerinde):
        ///  • OUTLIER_PROBABILITY: en yüksek 1X2 olasılığı kalibrasyon penceresinde doğrulanmış tavanın üstünde;
        ///  • RATING_DIRECTION_CONFLICT: bağımsız Elo beklentisi ile modelin beklenen puanı kalibrasyon 99. yüzdeliğinden fazla ayrışıyor.
        /// </summary>
        public static IReadOnlyList<string> OutputGates(OutcomePrediction pr, OutcomeModelParameters p)
        {
            var list = new List<string>();
            var cal = pr.Calibrated;
            var max = Math.Max(cal.HomeWin, Math.Max(cal.Draw, cal.AwayWin));
            if (max > p.MaxSupportedProbability + 1e-9) list.Add("OUTLIER_PROBABILITY");
            var modelPoints = cal.HomeWin + 0.5 * cal.Draw;
            if (Math.Abs(modelPoints - pr.Expectation.EloHomeExpectation) > p.EloConflictThreshold) list.Add("RATING_DIRECTION_CONFLICT");
            return list;
        }
    }
}
