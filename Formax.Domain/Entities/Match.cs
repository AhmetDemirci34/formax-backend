namespace Formax.Domain.Entities
{
    public class Match
    {
        public int Id { get; set; }

        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }

        public DateTime MatchDate { get; set; }

        /// <summary>
        /// İlk yarı skoru — sağlayıcının `score.halftime` değeri. Nullable ZORUNLU:
        /// null = sağlayıcı vermedi (ör. henüz oynanmadı / eski kayıt), 0 = GERÇEK sıfır.
        /// MS skorundan türetilmez, tahmin edilmez.
        /// </summary>
        public int? HalfTimeHomeScore { get; set; }
        public int? HalfTimeAwayScore { get; set; }

        public int HomeScore { get; set; }
        public int AwayScore { get; set; }

        public string? MatchMinute { get; set; }
        public string Status { get; set; } = "NotStarted";

        public string League { get; set; } = string.Empty;
        public int LeagueId { get; set; }

        // Kanonik Competition bağı (Team gibi FK ile; GDP fikstüründen çözülür). Opsiyonel.
        public int? CompetitionId { get; set; }

        // Kanonik Venue bağı (Competition ile aynı pattern; GDP fikstüründen çözülür). Opsiyonel.
        public int? VenueId { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? LastEmittedEventType { get; set; }

        /// <summary>
        /// External ID used by the sports data provider (e.g. api-football fixture id).
        /// Null when the match has not been mapped to an external source.
        /// </summary>
        public string? ExternalMatchId { get; set; }

        /// <summary>Referee name as provided by the sports data provider. Null until synced.</summary>
        public string? Referee { get; set; }

        /// <summary>Venue / stadium name as provided by the sports data provider. Null until synced.</summary>
        public string? Venue { get; set; }

        /// <summary>
        /// Sağlayıcının verdiği GERÇEK tur/aşama adı ("Regular Season - 1",
        /// "3rd Qualifying Round", "Play-offs", "Round of 16", "Final" …).
        /// Maçın TÜRÜNÜ (lig maçı / eleme / final) bu alan belirler; tahmin edilmez.
        /// Sağlayıcı vermediyse null kalır ve UI tür satırını göstermez.
        /// </summary>
        public string? Round { get; set; }

        /// <summary>
        /// KESİN SONUCUN DB'YE YAZILDIĞI AN (UTC). null = bu maç için sağlayıcıdan
        /// kesinleşmiş bir sonuç HİÇ alınmadı. Skorun 0-0 olması bir sonuç DEĞİLDİR;
        /// "sonucu var mı" sorusunun tek dürüst cevabı bu alandır.
        /// </summary>
        public DateTime? ResultUpdatedAtUtc { get; set; }

        /// <summary>
        /// Sonucun geldiği sağlayıcı sorgusu ("api-football:fixtures?date=2026-08-30").
        /// Tazelik denetimi ve teşhis için; uydurulmaz, yalnız gerçek çağrıdan yazılır.
        /// </summary>
        public string? ResultSource { get; set; }

        /// <summary>
        /// Resmî sonuç doğrulaması: "Verified" (resmî kaynak maçı bitmiş gösterdi ve teyit geçti) |
        /// "VerificationPending" (aynı resmî kaynağın iki yüzü çelişti — skor KESİNLEŞTİRİLMEDİ).
        /// Eski kayıtlarda null.
        /// </summary>
        public string? ResultVerificationStatus { get; set; }

        /// <summary>
        /// Kesin sonucun biçimi — resmî kaynaktan: "FT" (normal süre), "AET" (uzatmalar), "PEN" (seri penaltılar).
        /// Status "Finished" kalır (Sonuçlar listesi ve settlement bu durumu okur); bu alan ayrıntıyı taşır.
        /// Eski kayıtlarda null.
        /// </summary>
        public string? ResultDetail { get; set; }

        /// <summary>Seri penaltı skoru — yalnız kaynak yayımladıysa (PEN); aksi hâlde null.</summary>
        public int? PenaltyHomeScore { get; set; }
        public int? PenaltyAwayScore { get; set; }

        // ── TAKVİM GÜVENİLİRLİĞİ (01.09.2026) ───────────────────────────────────

        /// <summary>
        /// Kickoff kesin mi, yoksa turun nominal tarihi mi —
        /// <see cref="Constants.KickoffPrecisions"/>. Geçici saat kullanıcıya KESİN
        /// saatmiş gibi gösterilemez ve o maç doğrulama adayıdır.
        /// </summary>
        public string KickoffPrecision { get; set; } = Constants.KickoffPrecisions.Confirmed;

        /// <summary>Kickoff'un sağlayıcıdan KESİN olarak doğrulandığı an. null = hiç doğrulanmadı.</summary>
        public DateTime? ScheduleVerifiedAtUtc { get; set; }

        /// <summary>
        /// Başlama saatinin kaynağı. "official:{kaynak}" ise saat resmî maç merkezinden doğrulanmıştır
        /// ve lisanslı sağlayıcının takvim senkronu bu saati GERİ ALMAZ. Eski kayıtlarda null.
        /// </summary>
        public string? ScheduleSource { get; set; }

        /// <summary>
        /// Takvim tazeleme için sağlayıcıya EN SON ne zaman soruldu (başarısız denemeler
        /// dâhil). Soğuma penceresi buradan değil kalıcı deneme defterinden hesaplanır;
        /// bu alan teşhis/görünürlük içindir.
        /// </summary>
        public DateTime? ScheduleRefreshAttemptedAtUtc { get; set; }

        // 🔥 GERİ EKLENDİ
        public Match CloneForComparison()
        {
            return (Match)this.MemberwiseClone();
        }

        // 🔥 NEW NAV
        public Team? HomeTeam { get; set; }
        public Team? AwayTeam { get; set; }

        public Competition? Competition { get; set; }

        // GDP kanonik Venue entity bağı. (main'in string? Venue = provider ham ad; bu = canonical FK nav.)
        public Venue? CanonicalVenue { get; set; }
    }
}