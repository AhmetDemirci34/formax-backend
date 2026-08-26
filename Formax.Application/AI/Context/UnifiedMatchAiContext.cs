namespace Formax.Application.AI.Context
{
    /// <summary>
    /// FORMAX AI Evolution — <see cref="Formax.Application.Services.Radar.Intelligence.Scenarios.MarketProbabilityEngine"/>'in
    /// okuduğu TEK, read-only, GDP-türevli sinyal paketi.
    ///
    /// Motor bundan BAŞKA hiçbir kaynağa dokunmaz: repository yok, provider yok, ham
    /// istatistik hesabı yok. Yeni provider/sinyal eklendiğinde yalnız bu context'i
    /// dolduran <see cref="IMatchAiContextBuilder"/> değişir; motor DEĞİŞMEZ (OCP/SOLID).
    ///
    /// FAZ 1 (davranış-koruyan): bugün motorun kullandığı sinyalleri (TeamComparison +
    /// H2H + GücSkoru) taşır → çıktı birebir aynı kalır. Sonraki fazlarda bloklar zenginleşir
    /// (Attack/Defence index, Standings, Player Availability, Injuries, Evidence/News sinyalleri,
    /// xG, Weather...) — motor bu alanları okumaya BAŞLADIKÇA kalite artar, imza değişmez.
    /// </summary>
    public sealed class UnifiedMatchAiContext
    {
        public int MatchId { get; init; }

        public string HomeName { get; init; } = "Ev sahibi";
        public string AwayName { get; init; } = "Deplasman";

        /// <summary>Ev sahibi takım sinyalleri (GDP-türevli).</summary>
        public TeamAiSignals Home { get; init; } = new();

        /// <summary>Deplasman takım sinyalleri (GDP-türevli).</summary>
        public TeamAiSignals Away { get; init; } = new();

        /// <summary>Karşılıklı maç geçmişi.</summary>
        public H2HAiSignals H2H { get; init; } = new();

        /// <summary>Maç güç dengesi (0-100, 50=denge). GucSkoruCalculator çıktısı.</summary>
        public int GucSkoru { get; init; } = 50;

        /// <summary>
        /// FAZ 2 — gerçek gol verisinden türetilen güç/tempo sinyalleri. Motor bunları
        /// beklenen-gol ve lig-uyarlı fallback için okur (ham istatistik hesaplamaz).
        /// </summary>
        public MatchStrengthSignals Strength { get; init; } = new();

        /// <summary>
        /// Bağlam veri tamlığı (0..1). Builder gerçek sinyal doluluğundan hesaplar;
        /// düşükse motor güven etiketlerini dürüstçe kısar (açıklanabilir AI).
        /// </summary>
        public double DataQuality { get; init; } = 1.0;

        // ── İLERİ SİNYAL BLOKLARI (GDP dolunca builder doldurur; motor OCP ile okur) ──
        // Coverage olan liglerde GERÇEK provider verisiyle dolar; olmayanlarda HasData=false.
        /// <summary>Kadro/sakatlık uygunluğu (MatchPlayerStatuses — FAZ 3).</summary>
        /// <summary>
        /// MOTORUN GÖRDÜĞÜ kadro sinyali. Kapsam bilerek DARDIR: yalnız kadroların
        /// açıklandığı pencerede (kickoff'a &lt;= 60 dk) dolar. Sebep: MarketProbabilityEngine
        /// bu değeri okuyup olasılığa ceza/kenar uyguluyor; sağlayıcıdan günler öncesinden
        /// gelen sakatlık listesi buraya bağlanırsa mevcut Olası Sonuçlar sessizce değişir.
        /// </summary>
        public AvailabilityAiSignals Availability { get; init; } = new();

        /// <summary>
        /// ANLATI/UI'ın gördüğü TAM kadro sinyali — kickoff uzaklığından bağımsız, sağlayıcının
        /// fikstür-bazlı (/injuries?fixture=) gerçek kaydı. MOTOR BU ALANI OKUMAZ; yalnız
        /// Match Intelligence'a taşınır. İki akış burada ayrılır.
        /// </summary>
        public AvailabilityAiSignals AvailabilityFull { get; init; } = new();

        /// <summary>Lig sıralaması sinyalleri (LeagueStandings — Phase 4). Coverage yoksa HasData=false.</summary>
        public StandingsAiSignals Standings { get; init; } = new();

        /// <summary>Haber/kanıt sinyalleri (MatchEvidenceRecords — Phase 5). Kanıt yoksa HasData=false.</summary>
        public NewsEvidenceSignals News { get; init; } = new();

        /// <summary>
        /// Takım sezon istatistikleri (api-football /teams/statistics — Phase 6). GERÇEK sezon
        /// toplamları: ev/deplasman gol ortalamaları, clean-sheet, gol atamama, form. Coverage
        /// yoksa HasData=false. Builder Strength'i bu daha zengin veriyle iyileştirir (motor imzası sabit).
        /// </summary>
        public TeamStatsAiSignals TeamStats { get; init; } = new();

        /// <summary>
        /// Provider öngörü sinyali (api-football /predictions — Phase 6 / Slice 2). YALNIZ AI için
        /// EK bir sinyaldir; kullanıcıya ASLA gösterilmez ve motor davranışını değiştirmez (motor
        /// bu bloğu bu fazda OKUMAZ, hazır bulunur). Coverage yoksa HasData=false.
        /// </summary>
        public PredictionAiSignals Prediction { get; init; } = new();

        /// <summary>
        /// Takım profili sinyalleri (api-football coach/venue/squad/transfers — Phase 6 Final).
        /// AI/GDP-only; kullanıcıya gösterilmez. Coverage yoksa HasData=false.
        /// </summary>
        public TeamProfileAiSignals TeamProfile { get; init; } = new();

        /// <summary>
        /// Hakem kimlik sinyali (mevcut Match.Referee'den — Phase 6 Final). AI-only.
        /// Hakem atanmadıysa HasData=false (fake YOK).
        /// </summary>
        public RefereeAiSignals Referee { get; init; } = new();

        /// <summary>
        /// Resmi sosyal medya sinyalleri (Canonical SocialPost — Phase 7). GERÇEK, doğrulanmış
        /// resmi hesaplardan. Coverage yoksa HasData=false. Motor bu fazda okumaz (hazır bulunur).
        /// </summary>
        public SocialAiSignals Social { get; init; } = new();

        // ── v2.5 READ-ONLY BAĞLAM BLOKLARI (additive) ────────────────────────────────
        // ZATEN mevcut canonical kaynaklardan (Match entity + CompetitionContext + LeagueStandings)
        // türetilir; yeni provider/API/veri YOK. Kaynak boş/eksikse HasData=false (fake YOK).
        // Motor bu blokları okur; mevcut field'lar korunur (backward-compat).

        /// <summary>Maçın önemi/türü (League/Cup/Knockout, stage, round, importance) — CompetitionContext'ten.</summary>
        public CompetitionContextSignals Competition { get; init; } = new();

        /// <summary>Eleme/turnuva bağlamı (ilk maç/rövanş/aggregate/uzatma/penaltı) — CompetitionContext bracket'ten.</summary>
        public TournamentContextSignals Tournament { get; init; } = new();

        /// <summary>Sezon bağlamı (sezon/faz/ay) — Match.MatchDate'ten (her zaman gerçek).</summary>
        public SeasonContextSignals Season { get; init; } = new();

        /// <summary>Lig bağlamı (lider farkı/title-race) — tam LeagueStandings tablosundan. Kısmi kapsamda dürüst işaretlenir.</summary>
        public StandingsContextSignals StandingsContext { get; init; } = new();

        /// <summary>Canlı maç durumu (skor/dakika/istatistik) — Match + MEVCUT MatchLiveStats'ten. In-play değilse HasData=false.</summary>
        public LiveStateSignals LiveState { get; init; } = new();

        /// <summary>
        /// v2 — TIMELINE INTELLIGENCE: takımın geçmiş+gelecek fikstüründen türetilen sinyaller (dinlenme,
        /// sonraki maç, yoğunluk, rotasyon riski). YALNIZ mevcut Matches tablosundan (yeni provider/API/
        /// migration YOK). Komşu maç yoksa HasData=false (uydurma YOK). Motor bunu editoryal yorum için okur;
        /// olasılık matematiğine GİRMEZ (hash sabit).
        /// </summary>
        public TimelineSignals Timeline { get; init; } = new();

        /// <summary>
        /// Football Intelligence v1.0 — PLAYER/SQUAD INTELLIGENCE: takım oyuncu-düzeyi zekâsı
        /// (en skorer, asist lideri, kilit oyuncu rating, süre lideri, isimli sakatlar, pozisyon
        /// dağılımı) GERÇEK api-football verisinden (/players + /injuries), TeamPlayerIntelligence
        /// deposundan okunur. Coverage yoksa HasData=false (uydurma YOK). Motor bunu YALNIZ Football
        /// Intelligence editoryal bloğu için okur; olasılık/gol modeline GİRMEZ (hash sabit).
        /// </summary>
        public PlayerIntelligenceAiSignals PlayerIntelligence { get; init; } = new();

        /// <summary>
        /// AI Signal Factory çıktısı: yukarıdaki tüm alt-sinyaller futbol anlamına dönüştürülmüş,
        /// çok-kaynak füzyonlu, çelişki-çözümlü, standart zarflı TEK sinyal listesi (Signals.AiSignal).
        /// Builder tüm bloklar dolduktan SONRA doldurur. Motor gelecekte YALNIZ bunu okuyacak.
        /// internal set → dışarıya (motor/LLM) READ-ONLY (immutable tasarım); yalnız GDP builder yazar.
        /// </summary>
        public IReadOnlyList<Formax.Application.AI.Signals.AiSignal> Signals { get; internal set; }
            = new List<Formax.Application.AI.Signals.AiSignal>();

        /// <summary>Unified AI Context şema sürümü (versionlanabilir). Yeni sinyal eklense de motor değişmez.</summary>
        public string Version { get; init; } = "unified-ai-context/v1";

        /// <summary>
        /// Context-düzeyi kalite/özet paketi (agregat güven, conflict özeti, reasoning ipuçları,
        /// ilişkili varlıklar). Builder Signals'tan sonra yazar (internal set → dışarıya read-only).
        /// </summary>
        public UnifiedContextQuality Quality { get; internal set; } = new();

        // ── TEK GİRİŞ NOKTASI — null-safe, extensible accessor'lar (motorun kullanacağı yüzey) ──

        /// <summary>Ada göre sinyal (null-safe). Yeni sinyal eklendiğinde motor bu yüzeyi kullanır, değişmez.</summary>
        public Formax.Application.AI.Signals.AiSignal? Signal(string name)
        {
            if (Signals == null || string.IsNullOrWhiteSpace(name)) return null;
            foreach (var s in Signals)
                if (string.Equals(s.Name, name, System.StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        /// <summary>Yalnız gerçek veriyle dolu (HasData) sinyaller. Asla null döndürmez.</summary>
        public System.Collections.Generic.IReadOnlyList<Formax.Application.AI.Signals.AiSignal> ActiveSignals()
        {
            var list = new System.Collections.Generic.List<Formax.Application.AI.Signals.AiSignal>();
            if (Signals == null) return list;
            foreach (var s in Signals) if (s.HasData) list.Add(s);
            return list;
        }
    }

    /// <summary>
    /// Unified AI Context kalite/özet paketi — context-düzeyi agregatlar + Conflict Management özeti.
    /// Tüm alanlar Signals'tan deterministik türetilir (mock YOK). Immutable (init).
    /// </summary>
    public sealed class UnifiedContextQuality
    {
        /// <summary>Aktif (HasData) sinyallerin ortalama güveni (0-100).</summary>
        public int OverallConfidence { get; init; }
        /// <summary>Aktif sinyallerin ortalama veri tamlığı (0..1).</summary>
        public double OverallDataQuality { get; init; }
        /// <summary>Aktif sinyallerin ortalama tazeliği (0..1).</summary>
        public double OverallFreshness { get; init; }
        /// <summary>Aktif sinyallerdeki en yüksek kaynak güveni (0-100).</summary>
        public int OverallSourceTrust { get; init; }
        /// <summary>Aktif sinyallerin ortalama kanıt gücü (0-100).</summary>
        public int OverallEvidenceScore { get; init; }

        public int TotalSignalCount { get; init; }
        public int ActiveSignalCount { get; init; }

        /// <summary>Conflict Management özeti: durum → adet (Merged/Resolved/Unresolved/Suppressed/None).</summary>
        public System.Collections.Generic.Dictionary<string, int> ConflictSummary { get; init; } = new();

        /// <summary>Açıklanabilir kısa reasoning ipuçları (en güçlü aktif sinyallerden).</summary>
        public System.Collections.Generic.List<string> ReasoningHints { get; init; } = new();

        /// <summary>Context-düzeyi ilişkili varlıklar (takım/lig birleşimi).</summary>
        public System.Collections.Generic.List<string> RelatedEntities { get; init; } = new();
    }

    /// <summary>
    /// Resmi sosyal medya AI sinyalleri (Canonical SocialPost'tan türetilir). Yalnız verified
    /// resmi hesaplar. Coverage yoksa HasData=false (fake YOK).
    /// </summary>
    public sealed class SocialAiSignals
    {
        public bool HasData { get; init; }

        // Resmi açıklama sinyalleri (SignalType → Official* eşlemesi).
        public int OfficialAnnouncement { get; init; }
        public int OfficialSquadAnnouncement { get; init; }
        public int OfficialLineupAnnouncement { get; init; }
        public int OfficialInjuryAnnouncement { get; init; }
        public int OfficialTransferAnnouncement { get; init; }
        public int OfficialCoachStatement { get; init; }
        public int OfficialClubStatement { get; init; }
        public int OfficialCompetitionStatement { get; init; }
        public int OfficialFederationStatement { get; init; }
        public int OfficialPlayerStatement { get; init; }

        /// <summary>Son 6 saatte resmi hesaptan paylaşım (breaking).</summary>
        public bool BreakingOfficialNews { get; init; }

        public int SourceTrust { get; init; }
        public int EvidenceScore { get; init; }
        public int Confidence { get; init; }

        /// <summary>En yeni resmi paylaşımın zamanı (ISO-8601 UTC). Yoksa null.</summary>
        public string? LatestPublishedUtc { get; init; }

        public bool HasOfficialSource { get; init; }
        public bool HasSocialSource { get; init; }
    }

    /// <summary>Maç düzeyi takım profili sinyalleri (coach/venue/squad/transfers).</summary>
    public sealed class TeamProfileAiSignals
    {
        public TeamProfileTeamSignals Home { get; init; } = new();
        public TeamProfileTeamSignals Away { get; init; } = new();
        /// <summary>İki takımın da profil kaydı bulunduysa true.</summary>
        public bool HasData { get; init; }
    }

    /// <summary>Tek takımın profil sinyalleri (her alt-blok bağımsız coverage-gated).</summary>
    public sealed class TeamProfileTeamSignals
    {
        public bool HasCoach { get; init; }
        public string CoachName { get; init; } = "";
        public int CoachAge { get; init; }

        public bool HasVenue { get; init; }
        public string VenueName { get; init; } = "";
        public string VenueCity { get; init; } = "";
        public int VenueCapacity { get; init; }
        public string VenueSurface { get; init; } = "";

        public bool HasSquad { get; init; }
        public int SquadSize { get; init; }
        public double SquadAvgAge { get; init; }

        public bool HasTransfers { get; init; }
        public int RecentTransfersIn { get; init; }
        public int RecentTransfersOut { get; init; }

        /// <summary>En az bir alt-blok doldu mu.</summary>
        public bool HasData { get; init; }
    }

    /// <summary>Hakem kimlik sinyali (Match.Referee'den).</summary>
    public sealed class RefereeAiSignals
    {
        public bool HasData { get; init; }
        public string RefereeName { get; init; } = "";
    }

    /// <summary>
    /// Provider (api-football) öngörü sinyalleri — YALNIZ AI değerlendirmesi için EK sinyal.
    /// Kullanıcıya gösterilmez. Motor bu fazda okumaz (OCP: ileride okuyabilir, imza sabit).
    /// Coverage yoksa HasData=false (fake YOK).
    /// </summary>
    public sealed class PredictionAiSignals
    {
        /// <summary>Bu maça bağlı provider öngörüsü var mı.</summary>
        public bool HasData { get; init; }

        /// <summary>Provider'ın en yüksek olasılığı (0-100) = öngörü kararlılığı/güveni.</summary>
        public int PredictionConfidence { get; init; }

        /// <summary>Provider'ın öne çıkardığı sonuç: "Home" | "Draw" | "Away" | "".</summary>
        public string ProviderPrediction { get; init; } = "";

        /// <summary>Provider tavsiyesi (advice) — ProviderRecommendation.</summary>
        public string ProviderRecommendation { get; init; } = "";

        /// <summary>Provider karşılaştırma güç eğilimi (comparison.total, 0-100).</summary>
        public int ProviderTrendHome { get; init; }
        public int ProviderTrendAway { get; init; }

        /// <summary>Ham olasılık sinyalleri (0-100).</summary>
        public int PercentHome { get; init; }
        public int PercentDraw { get; init; }
        public int PercentAway { get; init; }

        /// <summary>Provider "kazanır ya da berabere" işareti.</summary>
        public bool WinOrDraw { get; init; }

        /// <summary>Üst/Alt öngörüsü (ör. "-3.5").</summary>
        public string UnderOver { get; init; } = "";

        /// <summary>Sinyal veri tamlığı (0..1): yüzdeler ~100 toplanıyor + karşılaştırma var mı.</summary>
        public double DataQuality { get; init; }
    }

    /// <summary>Maç düzeyi takım sezon istatistik sinyalleri — gerçek /teams/statistics verisinden.</summary>
    public sealed class TeamStatsAiSignals
    {
        public TeamSeasonAiSignals Home { get; init; } = new();
        public TeamSeasonAiSignals Away { get; init; } = new();
        /// <summary>İki takımın da sezon istatistiği bulunduysa true; yoksa false (fake yok).</summary>
        public bool HasData { get; init; }
    }

    /// <summary>Tek takımın sezon istatistikleri (gerçek provider verisi; ev/deplasman ayrımlı).</summary>
    public sealed class TeamSeasonAiSignals
    {
        public int Played { get; init; }
        public int Wins { get; init; }
        public int Draws { get; init; }
        public int Loses { get; init; }
        public double GoalsForAvgTotal { get; init; }
        public double GoalsForAvgHome { get; init; }
        public double GoalsForAvgAway { get; init; }
        public double GoalsAgainstAvgTotal { get; init; }
        public double GoalsAgainstAvgHome { get; init; }
        public double GoalsAgainstAvgAway { get; init; }
        public int CleanSheets { get; init; }
        public int FailedToScore { get; init; }
        public string Form { get; init; } = "";
        public bool HasData { get; init; }
    }

    /// <summary>
    /// Phase 5 — GDP haber/kanıt katmanı sinyalleri. GERÇEK, kaynak-skorlu, maça bağlı
    /// MatchEvidence'ten türetilir (Data Engine v2.1). Fake haber/dedikodu YOK; kaynak
    /// kalitesi (SourceTrust) taşınır. Motor bunu okumaya BAŞLAYABİLİR ama bu fazda okumaz.
    /// </summary>
    public sealed class NewsEvidenceSignals
    {
        /// <summary>Bu maça bağlı en az bir doğrulanmış kanıt var mı.</summary>
        public bool HasData { get; init; }

        public int TotalEvidence { get; init; }

        /// <summary>0-100 kanıt güveni (kaynak kalitesiyle ağırlıklı — MatchEvidence.Confidence).</summary>
        public int EvidenceScore { get; init; }

        /// <summary>0-100 en güvenilir kaynağın kalite skoru (SourceQuality).</summary>
        public int SourceTrust { get; init; }

        // Kategori bazlı sinyal adetleri (signal type → kategori eşlemesi).
        public int PlayerNews { get; init; }
        public int CoachNews { get; init; }
        public int ClubNews { get; init; }

        // Global News Platform — granular kategori sinyalleri (evidence Type histogramından).
        public int InjuryNews { get; init; }
        public int SuspensionNews { get; init; }
        public int TransferNews { get; init; }
        public int CompetitionNews { get; init; }

        /// <summary>Resmi açıklama (Club Statement) kanıt adedi.</summary>
        public int OfficialAnnouncements { get; init; }

        /// <summary>Haber katmanının toplam güveni (0-100): EvidenceScore + hacim harmanı.</summary>
        public int NewsConfidence { get; init; }

        /// <summary>Son 6 saatte yüksek-kaliteli kaynaktan kanıt var mı (breaking).</summary>
        public bool HasBreakingNews { get; init; }

        /// <summary>Tier-1 resmi kaynak (federasyon/resmi kulüp, SourceQuality≥95) var mı.</summary>
        public bool HasOfficialSource { get; init; }

        /// <summary>Resmi sosyal medya kaynağı var mı (SocialDiscoveryJob — şimdilik false).</summary>
        public bool HasSocialSource { get; init; }

        /// <summary>Yüksek kaliteli (resmi/güvenilir) kaynaktan kanıt var mı.</summary>
        public bool HasOfficialNews { get; init; }

        /// <summary>Resmi kulüp açıklaması (Club Statement) var mı.</summary>
        public bool HasOfficialAnnouncement { get; init; }

        /// <summary>Baskın sinyal türü (General hariç).</summary>
        public string TopSignal { get; init; } = "";

        /// <summary>Aktif sinyal türleri (General hariç).</summary>
        public List<string> NewsSignals { get; init; } = new();

        /// <summary>En yeni kanıtın yayın zamanı (ISO-8601 UTC). Yoksa null.</summary>
        public string? LatestPublishedUtc { get; init; }
    }

    /// <summary>Maç düzeyi sıralama sinyalleri — gerçek LeagueStandings'ten (canonical TeamId).</summary>
    public sealed class StandingsAiSignals
    {
        public TeamStandingSignals Home { get; init; } = new();
        public TeamStandingSignals Away { get; init; } = new();
        /// <summary>İki takımın da sıralama satırı bulunduysa true; yoksa false (fake yok).</summary>
        public bool HasData { get; init; }
    }

    /// <summary>Tek takımın sıralama satırı (gerçek provider verisi).</summary>
    public sealed class TeamStandingSignals
    {
        public int Position { get; init; }
        public int Played { get; init; }
        public int Points { get; init; }
        public int GoalsFor { get; init; }
        public int GoalsAgainst { get; init; }
        public int GoalDifference { get; init; }
        public string Form { get; init; } = "";
        public bool HasData { get; init; }
    }

    /// <summary>Maç düzeyi güç/tempo sinyalleri — gerçek gol geçmişinden türetilir.</summary>
    public sealed class MatchStrengthSignals
    {
        /// <summary>Maç bağlamının beklenen takım-başı gol seviyesi (lig-uyarlı fallback). 0=bilinmiyor.</summary>
        public double LeagueGoalBaseline { get; init; }

        public double HomeAttackIndex { get; init; }
        public double AwayAttackIndex { get; init; }
        public double HomeDefenceIndex { get; init; }
        public double AwayDefenceIndex { get; init; }
    }

    /// <summary>Kadro uygunluğu sinyalleri. GDP dolunca değerlenir; boşken motor davranışı değişmez.</summary>
    public sealed class AvailabilityAiSignals
    {
        public int HomeKeyAbsences { get; init; }
        public int AwayKeyAbsences { get; init; }
        public bool LineupConfirmed { get; init; }
        /// <summary>Bu blok gerçek veriyle doldu mu? false → motor bu sinyali yok sayar.</summary>
        public bool HasData { get; init; }

        // ── EKSİĞİN NEDENİ (anlatı için; MOTOR OKUMAZ) ──────────────────────────────
        // Motor yalnız HomeKeyAbsences/AwayKeyAbsences okur; aşağıdaki kırılım anlatının
        // "kim neden yok" sorusunu cevaplaması içindir. Olası Sonuçlar etkilenmez.
        public int HomeInjured { get; init; }
        public int HomeSuspended { get; init; }
        public int AwayInjured { get; init; }
        public int AwaySuspended { get; init; }

        /// <summary>Şüpheli (Doubtful) — SAKAT DEĞİLDİR, kadro dışı da değildir. Ayrı tutulur.</summary>
        public int HomeDoubtful { get; init; }
        public int AwayDoubtful { get; init; }
    }

    /// <summary>Tek takımın karar-ilgili sinyalleri. Hepsi GDP/canonical veriden türetilir.</summary>
    public sealed class TeamAiSignals
    {
        public double AvgGoalsFor { get; init; }
        public double AvgGoalsAgainst { get; init; }
        public int GoalScoringRate { get; init; }
        public int CleanSheetRate { get; init; }

        // ── Motor bugün OKUMUYOR; ileride zenginleştirme için hazır (davranışı etkilemez) ──
        public int FormScore { get; init; }
        public int LeagueRank { get; init; }
        public double HomeAwayAvgGoals { get; init; }
    }

    /// <summary>
    /// Timeline Intelligence — takımın geçmiş+gelecek fikstür sinyalleri (mevcut Matches tablosundan).
    /// Ham maç listesi taşımaz; editoryal olarak anlamlı TÜRETİLMİŞ sinyaller taşır. Komşu maç yoksa HasData=false.
    /// </summary>
    public sealed class TimelineSignals
    {
        public TeamTimelineSignals Home { get; init; } = new();
        public TeamTimelineSignals Away { get; init; } = new();
        /// <summary>En az bir takım için komşu fikstür bulunduysa true.</summary>
        public bool HasData { get; init; }
    }

    /// <summary>Tek takımın zaman-çizgisi sinyalleri (dinlenme/sonraki maç/yoğunluk/rotasyon).</summary>
    public sealed class TeamTimelineSignals
    {
        public bool HasData { get; init; }

        // ── Geçmiş ──
        public bool HasPrevMatch { get; init; }
        /// <summary>Bu maçtan önce kaç gün dinlendi (önceki resmi maçtan bu yana).</summary>
        public int RestDaysBefore { get; init; }

        // ── Gelecek ──
        public bool HasNextMatch { get; init; }
        /// <summary>Bu maçtan sonra bir sonraki maça kaç gün var.</summary>
        public int DaysToNextMatch { get; init; }
        /// <summary>Sonraki maç bu maçtan FARKLI bir turnuvada/ligde mi (kupa/Avrupa ihtimali).</summary>
        public bool NextIsDifferentCompetition { get; init; }

        // ── Yoğunluk ──
        /// <summary>Bu maçtan önceki 14 günde oynanan resmi maç sayısı.</summary>
        public int MatchesLast14 { get; init; }
        /// <summary>Bu maçtan sonraki 14 günde planlı resmi maç sayısı.</summary>
        public int MatchesNext14 { get; init; }

        // ── Türetilmiş (builder hesaplar; motor yorumlar) ──
        /// <summary>≤3 gün dinlenme (kısa dinlenme/fiziksel dezavantaj).</summary>
        public bool ShortRest { get; init; }
        /// <summary>≥6 gün dinlenme (uzun dinlenme avantajı).</summary>
        public bool LongRestAdvantage { get; init; }
        /// <summary>±14 günde yoğun fikstür.</summary>
        public bool CongestedSchedule { get; init; }
        /// <summary>Yakında (≤4 gün) farklı-turnuva maçı — öncelik/rotasyon sinyali.</summary>
        public bool UpcomingPriorityMatch { get; init; }
        /// <summary>Yoğunluk + yakın öncelikli maç → rotasyon ihtimali (kesinlik DEĞİL).</summary>
        public bool RotationRisk { get; init; }
    }

    /// <summary>Football Intelligence v1.0 — iki takımın oyuncu-düzeyi zekâsı (GERÇEK api-football).</summary>
    public sealed class PlayerIntelligenceAiSignals
    {
        public TeamPlayerSignals Home { get; init; } = new();
        public TeamPlayerSignals Away { get; init; } = new();
        public bool HasData { get; init; }
    }

    /// <summary>Tek takımın oyuncu/kadro zekâsı — coverage varsa gerçek, yoksa HasData=false.</summary>
    public sealed class TeamPlayerSignals
    {
        public bool HasData { get; init; }

        // Kadro / pozisyon dağılımı
        public int SquadPlayerCount { get; init; }
        public int GkCount { get; init; }
        public int DefCount { get; init; }
        public int MidCount { get; init; }
        public int AttCount { get; init; }

        // En skorer
        public string TopScorerName { get; init; } = "";
        public int TopScorerGoals { get; init; }
        public int TopScorerAssists { get; init; }
        public double TopScorerRating { get; init; }

        // Asist lideri
        public string TopAssistName { get; init; } = "";
        public int TopAssistCount { get; init; }

        // Kilit oyuncu (rating)
        public string KeyPlayerName { get; init; } = "";
        public double KeyPlayerRating { get; init; }

        // Süre lideri
        public string MinutesLeaderName { get; init; } = "";
        public int MinutesLeaderMinutes { get; init; }

        // Sakat/cezalı (isimli + bölge)
        public int InjuredCount { get; init; }
        public IReadOnlyList<string> InjuredNames { get; init; } = new List<string>();
        public int InjuredDefCount { get; init; }
        public int InjuredMidCount { get; init; }
        public int InjuredAttCount { get; init; }

        // v2 Deep Intelligence
        public int TeamTotalGoals { get; init; }
        public int TopScorerGoalSharePct { get; init; }
        public int Top2GoalSharePct { get; init; }
        public bool OneManDependency { get; init; }
        public string DefenseLeaderName { get; init; } = "";
        public double DefenseLeaderRating { get; init; }
        public string MidfieldBrainName { get; init; } = "";
        public int MidfieldBrainAssists { get; init; }
        public double MidfieldBrainRating { get; init; }
        public string ShotsLeaderName { get; init; } = "";
        public int ShotsLeaderCount { get; init; }
        public string KeyPassLeaderName { get; init; } = "";
        public int KeyPassLeaderCount { get; init; }
        public string CardRiskName { get; init; } = "";
        public int CardRiskYellows { get; init; }
    }

    /// <summary>Karşılıklı geçmiş özeti.</summary>
    public sealed class H2HAiSignals
    {
        public int TotalMatches { get; init; }
        public int HomeWins { get; init; }
        public int AwayWins { get; init; }
        public int Draws { get; init; }
    }

    // ══════════════════════ v2.5 READ-ONLY BAĞLAM BLOKLARI ══════════════════════

    /// <summary>
    /// Maçın önemi/türü — CompetitionContext (canonical, mevcut) satırından. Coverage yoksa
    /// (tablo boş) HasData=false; ilgili job doldurunca otomatik aktif olur (OCP). Fake YOK.
    /// </summary>
    public sealed class CompetitionContextSignals
    {
        public bool HasData { get; init; }
        /// <summary>"League" | "Cup" | "Knockout".</summary>
        public string CompetitionType { get; init; } = "";
        /// <summary>Ham stage/round etiketi (ör. "Regular Season - 25", "Quarter-Final").</summary>
        public string StageName { get; init; } = "";
        /// <summary>Türetilmiş stage: RegularSeason|Group|RoundOf16|QuarterFinal|SemiFinal|Final|ThirdPlace|Playoff|Qualification|Unknown.</summary>
        public string Stage { get; init; } = "";
        /// <summary>StageName'den ayrıştırılan tur numarası (0 = bilinmiyor).</summary>
        public int CurrentRound { get; init; }
        /// <summary>Türetilmiş önem: Normal|High|Critical|Elimination.</summary>
        public string Importance { get; init; } = "";
        /// <summary>Eleme (tek maç knockout) mu.</summary>
        public bool IsElimination { get; init; }
        public string Headline { get; init; } = "";
        public string Summary { get; init; } = "";
    }

    /// <summary>Turnuva/eleme bağlamı — CompetitionContext türü + bracket'ten. Coverage yoksa HasData=false.</summary>
    public sealed class TournamentContextSignals
    {
        public bool HasData { get; init; }
        public bool IsTwoLegged { get; init; }
        public bool IsFirstLeg { get; init; }
        public bool IsSecondLeg { get; init; }
        /// <summary>Toplam skor (aggregate) belirleyici mi.</summary>
        public bool AggregateMatters { get; init; }
        public bool ExtraTimePossible { get; init; }
        public bool PenaltiesPossible { get; init; }
        public string Summary { get; init; } = "";
    }

    /// <summary>Sezon bağlamı — Match.MatchDate'ten (her zaman gerçek). Round bazlı ilerleme CompetitionContext'e bağlı.</summary>
    public sealed class SeasonContextSignals
    {
        public bool HasData { get; init; }
        public int SeasonYear { get; init; }
        /// <summary>Start | Middle | End (maç ayına göre; sezon Temmuz'da başlar kabulü).</summary>
        public string SeasonPhase { get; init; } = "";
        /// <summary>Maç tarihi (ISO-8601 UTC).</summary>
        public string MatchDateUtc { get; init; } = "";
        /// <summary>Bağlamsal oynanan maç sayısı (standings'ten; 0 = bilinmiyor).</summary>
        public int MatchesPlayedContext { get; init; }
        public string Summary { get; init; } = "";
    }

    /// <summary>
    /// Lig bağlamı — TAM LeagueStandings tablosundan (lider farkı, title-race). Tablo kısmi ise
    /// (ör. yalnız üst sıralar) RelegationDataAvailable=false ile dürüstçe işaretlenir. Fake YOK.
    /// </summary>
    public sealed class StandingsContextSignals
    {
        public bool HasData { get; init; }
        /// <summary>Elimizdeki standings satır sayısı (kısmi kapsam göstergesi).</summary>
        public int KnownRows { get; init; }
        public int LeaderPoints { get; init; }
        public string LeaderName { get; init; } = "";
        public int HomeGapToLeader { get; init; }
        public int AwayGapToLeader { get; init; }
        /// <summary>İki takımdan en az biri lidere yakın (şampiyonluk yarışı) mı.</summary>
        public bool TitleRace { get; init; }
        /// <summary>Tam tablo (küme hattı dahil) mevcut mu; kısmi kapsamda false.</summary>
        public bool RelegationDataAvailable { get; init; }
        public string Summary { get; init; } = "";
    }

    /// <summary>
    /// Canlı maç durumu — Match (skor/dakika/status) + MEVCUT MatchLiveStats'ten (yeni API/job YOK).
    /// In-play değilse (Status != Live) veya canlı satır yoksa HasData=false. Zengin istatistik
    /// (şut/xG/topa sahip olma) ingest'te dolmadıysa HasRichStats=false (skor/dakika yine gerçek).
    /// </summary>
    public sealed class LiveStateSignals
    {
        public bool HasData { get; init; }
        public bool IsLive { get; init; }
        public int Minute { get; init; }
        public string Phase { get; init; } = "";
        public int HomeScore { get; init; }
        public int AwayScore { get; init; }

        public bool HasRichStats { get; init; }
        public int PossessionHome { get; init; }
        public int PossessionAway { get; init; }
        public int ShotsHome { get; init; }
        public int ShotsAway { get; init; }
        public int ShotsOnTargetHome { get; init; }
        public int ShotsOnTargetAway { get; init; }
        public int CornersHome { get; init; }
        public int CornersAway { get; init; }
        public int DangerousAttacksHome { get; init; }
        public int DangerousAttacksAway { get; init; }
        public int YellowHome { get; init; }
        public int YellowAway { get; init; }
        public int RedHome { get; init; }
        public int RedAway { get; init; }
        public double XgHome { get; init; }
        public double XgAway { get; init; }
    }
}
