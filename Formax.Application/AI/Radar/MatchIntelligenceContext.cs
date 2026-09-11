using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Formax.Application.AI.Radar
{
    /// <summary>
    /// FORMAX Radar v2 — LLM'in gördüğü TEK, SİNDİRİLMİŞ bağlam.
    ///
    /// Tasarım kuralı: Buraya ham haber gövdesi, ham istatistik tablosu veya ham
    /// sosyal veri KONULMAZ. Yalnız önceden hesaplanmış sinyaller, oranlar ve kısa
    /// tema başlıkları bulunur. LLM bunları "tekrar etmez", "anlamlandırır".
    ///
    /// Tüm yüzdeler/skorlar deterministik motorlardan gelir — LLM hesaplamaz.
    /// </summary>
    public sealed class MatchIntelligenceContext
    {
        public int MatchId { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string League { get; set; } = "";
        public string? Round { get; set; }

        /// <summary>
        /// Lig haftası — backend'in bildiği GERÇEK numara ("Regular Season - 1" → 1).
        /// Çözülemiyorsa null; LLM hafta numarası uyduramaz.
        /// </summary>
        public int? WeekNumber { get; set; }

        /// <summary>
        /// Sezon yılı — backend'in ZATEN kullandığı kural (Temmuz'dan itibaren yeni sezon).
        /// Yeni hesap değildir; puan durumu sorgusunda kullanılan değerin aynısıdır.
        /// Puan durumu satırı yoksa null kalır ve LLM'e hiç gitmez.
        /// </summary>
        public int? SeasonYear { get; set; }

        public string Status { get; set; } = "";
        public string KickoffUtc { get; set; } = "";

        public ImportanceBlock Importance { get; set; } = new();
        public FormBlock Form { get; set; } = new();
        public StatsBlock Stats { get; set; } = new();
        public H2HBlock H2H { get; set; } = new();

        /// <summary>
        /// Puan durumu — MatchDetailDto.Standing'den BİREBİR taşınır (yeni hesap yok).
        /// Veri yoksa null kalır; tahmin üretilmez.
        /// </summary>
        public StandingsBlock? Standings { get; set; }
        public NewsBlock News { get; set; } = new();
        public SocialBlock Social { get; set; } = new();

        /// <summary>Kadro eksikleri — MatchDetailDto.PlayerStatus'tan BİREBİR taşınır (yeni hesap yok).</summary>
        public AvailabilityBlock Availability { get; set; } = new();

        /// <summary>Takımların gerçek sezon istatistikleri (ev, deplasman). Veri yoksa boş.</summary>
        public List<TeamStatsRow> TeamStats { get; set; } = new();

        /// <summary>Deterministik motorun ürettiği, sıralı top senaryolar. LLM dokunmaz.</summary>
        public List<ScenarioBlock> Scenarios { get; set; } = new();

        /// <summary>WorldPerceptionProvider üst-bağlamı (günün futbol havası).</summary>
        public string? WorldHeadline { get; set; }

        /// <summary>
        /// Takım başına puan durumu satırı. Değerler backend'in çektiği gerçek tablo satırıdır.
        /// </summary>
        public sealed class StandingRow
        {
            public string TeamName { get; set; } = "";
            public int Position { get; set; }
            public int Played { get; set; }
            public int Won { get; set; }
            public int Drawn { get; set; }
            public int Lost { get; set; }
            public int GoalsFor { get; set; }
            public int GoalsAgainst { get; set; }
            public int Points { get; set; }
        }

        public sealed class StandingsBlock
        {
            public int TeamCount { get; set; }
            public StandingRow? Home { get; set; }
            public StandingRow? Away { get; set; }
        }

        public sealed class ImportanceBlock
        {
            public string Level { get; set; } = "";       // sapma bölgesi / önem etiketi
            public int WatchersCount { get; set; }
            public string? Note { get; set; }              // kısa sapma metni

            /// <summary>
            /// SPORTİF ÖNEM — kullanıcı ilgisinden (WatchersCount) AYRI tutulur. Ölçüldü (14.08):
            /// "maç önemi" etiketi yalnız WatchersCount'tan üretiliyordu; kullanıcı sayısı düşük
            /// olduğu için UEFA play-off'u dâhil HER maç "Düşük" çıkıyordu. Bu alan yalnız
            /// müsabaka kimliği ve gerçek tur/aşama verisinden doldurulur; veri yoksa boş kalır.
            /// </summary>
            public string? SportingLevel { get; set; }

            /// <summary>Aşamanın futbolca karşılığı (ör. "Play-off turu", "Lig maçı"). Veri yoksa null.</summary>
            public string? StageLabel { get; set; }

            /// <summary>Backend'in ürettiği GÜÇ SKORU (0–100). Formül değişmedi; değer aynen taşınır.</summary>
            public int GucSkoru { get; set; }
            /// <summary>Backend'in ürettiği OYNANMA SKORU (0–100) — sapmanın diğer ayağı.</summary>
            public int OynanmaSkoru { get; set; }
        }

        /// <summary>
        /// Kadro durumu. Sayılar backend'in ZATEN hesapladığı <c>UnifiedMatchAiContext.Availability</c>
        /// değerinden BİREBİR gelir (MatchPlayerStatuses + canonical/external takım kimliği çözümü
        /// orada yapılır). Burada yeni sayım/eşleştirme YAPILMAZ. Veri yoksa HasData=false.
        /// </summary>
        public sealed class AvailabilityBlock
        {
            public bool HasData { get; set; }
            public bool LineupsAnnounced { get; set; }
            public int HomeOut { get; set; }   // key absence (Injured/Suspended/Out) — ev sahibi
            public int AwayOut { get; set; }

            /// <summary>Eksiğin nedeni — sakat/cezalı/şüpheli ayrı ayrı (motor okumaz, anlatı okur).</summary>
            public int HomeInjured { get; set; }
            public int HomeSuspended { get; set; }
            public int HomeDoubtful { get; set; }
            public int AwayInjured { get; set; }
            public int AwaySuspended { get; set; }
            public int AwayDoubtful { get; set; }
        }

        public sealed class FormBlock
        {
            public string HomeRecent { get; set; } = "";   // ör. "G G B M G" (son 5)
            public string AwayRecent { get; set; } = "";
            public int HomeFormScore { get; set; }
            public int AwayFormScore { get; set; }

            /// <summary>
            /// FORM KANITININ KALİTESİ (kaç maç / ne kadar taze / hangi turnuvalar).
            /// Sayının kendisi kadar ona ne kadar yaslanılabileceği de taşınır; zamansal
            /// kesinlik izni buradan okunur (bkz. FormEvidencePolicy).
            /// </summary>
            public Services.Matches.FormEvidence HomeEvidence { get; set; }
                = Services.Matches.FormEvidence.None;
            public Services.Matches.FormEvidence AwayEvidence { get; set; }
                = Services.Matches.FormEvidence.None;

            /// <summary>
            /// MEVCUT SEZON LİG FORMU — "bu sezon" ifadesinin arkasındaki gerçek özet.
            /// (Aynı lig + aynı sezon + kickoff öncesi + tamamlanmış maçlar.) Bu blok
            /// geldiğinde anlatı form cümlesini BU özetten kurar; G/B/M dizisini kendisi
            /// yorumlamaz. Sezon çözülemezse null kalır ve form konusu açılmaz.
            /// </summary>
            public DTOs.Matches.TeamSeasonFormDto? HomeSeason { get; set; }
            public DTOs.Matches.TeamSeasonFormDto? AwaySeason { get; set; }
        }

        public sealed class StatsBlock
        {
            public double HomeAvgGoalsFor { get; set; }
            public double AwayAvgGoalsFor { get; set; }
            public int HomeGoalScoringRate { get; set; }
            public int AwayGoalScoringRate { get; set; }
            public int HomeCleanSheetRate { get; set; }
            public int AwayCleanSheetRate { get; set; }
            public int? HomeRank { get; set; }
            public int? AwayRank { get; set; }
        }

        public sealed class H2HBlock
        {
            public int Total { get; set; }
            public int HomeWins { get; set; }
            public int AwayWins { get; set; }
            public int Draws { get; set; }

            /// <summary>
            /// Geçmiş karşılaşmaların GERÇEK sonuçları (en yeniden eskiye, en çok 3).
            /// Toplamlar "ne kadar" der; bu liste "ne oldu" der. Skorlar backend kaydından
            /// birebir gelir — model skor uyduramaz, yalnız buradakini okuyabilir.
            /// </summary>
            public List<H2HResult> RecentResults { get; set; } = new();
        }

        /// <summary>
        /// Tek bir geçmiş karşılaşma — backend kaydından birebir. O MAÇIN ev sahibi ve
        /// deplasmanı, BUGÜNKÜ maçınkinden farklı olabilir; bu yüzden alanlar ayrı adlarla
        /// taşınır ve gol sayıları ayrı ayrı verilir (skor metni yönü gizliyordu).
        /// </summary>
        public sealed class H2HResult
        {
            public string Date { get; set; } = "";
            public string H2HHomeTeam { get; set; } = "";
            public string H2HAwayTeam { get; set; } = "";
            public int HomeGoals { get; set; }
            public int AwayGoals { get; set; }
            public string Competition { get; set; } = "";
        }

        /// <summary>Takımın gerçek sezon istatistikleri (backend hesabı, birebir).</summary>
        public sealed class TeamStatsRow
        {
            public string TeamName { get; set; } = "";
            public double AvgGoalsFor { get; set; }
            public double AvgGoalsAgainst { get; set; }
            public int GoalScoringRate { get; set; }
            public int CleanSheetRate { get; set; }
        }

        public sealed class NewsBlock
        {
            public int Volume24h { get; set; }
            public string? TopType { get; set; }           // baskın sinyal/tip (Transfer/Injury...)
            public List<string> Themes { get; set; } = new(); // kısa başlık temaları (max 3)
            /// <summary>v2.1 Evidence Store sinyalleri (Transfer, Injury, Derby...). Boşsa eski kaynak.</summary>
            public List<string> Signals { get; set; } = new();
            /// <summary>Evidence kaynağı kullanıldı mı (Reasoning bunu bilir).</summary>
            public bool FromEvidence { get; set; }

            /// <summary>
            /// Okuma kapılarından geçmiş kanıtların TEKİL kayıtları (yalnız FromEvidence=true iken
            /// dolar). Ham gövde değil; kategori + kalite + zaman + kısa başlık taşır. Metin
            /// güvenliği (enjeksiyon/skor/bahis süzgeci) pack'e kopyalanırken uygulanır.
            /// </summary>
            public List<EvidenceItem> Items { get; set; } = new();
        }

        /// <summary>Tek bir güvenilir kanıt. Kaynak ADI taşınmaz (LLM yayıncı anmamalı), kalite taşınır.</summary>
        public sealed class EvidenceItem
        {
            public string Category { get; set; } = "";   // Injury / Transfer / Lineup / Coach …
            public int SourceQuality { get; set; }
            public int Confidence { get; set; }
            public string PublishedUtc { get; set; } = "";
            public string Headline { get; set; } = "";

            /// <summary>Bu OLAYI kaç farklı yayıncı doğruladı (tek olay + çoklu kaynak).</summary>
            public int SourceCount { get; set; } = 1;

            /// <summary>Maçla ilişkisi — "Maç" (iki takım) veya "Takım" (tek takım gelişmesi).</summary>
            public string Relation { get; set; } = "";

            /// <summary>Zaman konumu: MaçÖncesi / MaçGünü / MaçSonrası.</summary>
            public string Timing { get; set; } = "";

            /// <summary>Gelişme hangi takımla ilgili (tek takım haberiyse dolu).</summary>
            public string RelatedTeam { get; set; } = "";

            /// <summary>O takımın bu maçtaki rakibi.</summary>
            public string OpponentTeam { get; set; } = "";

            /// <summary>Olayın öznesi olan oyuncu (yalnız bilinen kadro adıyla eşleştiyse).</summary>
            public string Player { get; set; } = "";

            /// <summary>Olayın öznesi teknik direktörse adı.</summary>
            public string Coach { get; set; } = "";

            /// <summary>Futbol olay türü (Transfer / Sakatlık / Kadro / …).</summary>
            public string EventType { get; set; } = "";

            /// <summary>Olayın maç açısından önemi (Yüksek / Orta / Düşük).</summary>
            public string Importance { get; set; } = "";

            /// <summary>
            /// Haberin gerçek kısa içeriği (varsa). Boşsa elde yalnız başlık vardır ve
            /// anlatıda haberden söz edilmez — başlıktan içerik üretilmez.
            /// </summary>
            public string Summary { get; set; } = "";
        }

        // Topluluk/taraftar ilgisi — iç sinyal proxy'si. Dış sosyal (X/Reddit) henüz yok.
        public sealed class SocialBlock
        {
            public int CommunityInterest { get; set; }   // takip eden kullanıcı sayısı
            public string Level { get; set; } = "";       // Düşük / Orta / Yüksek
        }

        public sealed class ScenarioBlock
        {
            public string Market { get; set; } = "";
            public int Probability { get; set; }
            public string Confidence { get; set; } = "";
            /// <summary>Bu senaryoyu destekleyen deterministik kanıt etiketleri (LLM nedeni buna dayandırır).</summary>
            public List<string> EvidenceTags { get; set; } = new();
        }

        private static readonly JsonSerializerOptions PromptJson = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        /// <summary>Prompt'a gömülecek kompakt JSON gösterimi.</summary>
        public string ToPromptJson() => JsonSerializer.Serialize(this, PromptJson);
    }
}
