namespace Formax.Application.DTOs.Lineup
{
    // ─── Provider result models (returned by ISportsDataProvider) ─────────────

    /// <summary>Full lineup result from the sports data provider for one fixture.</summary>
    public class SportsLineupResult
    {
        public bool LineupsAnnounced { get; set; }

        /// <summary>Sağlayıcının açıkladığı diziliş ("4-4-2"). Vermezse null — TAHMİN EDİLMEZ.</summary>
        public string? HomeFormation { get; set; }
        public string? AwayFormation { get; set; }

        public List<SportsLineupPlayer> HomeStarters { get; set; } = new();
        public List<SportsLineupPlayer> HomeBench { get; set; } = new();

        public List<SportsLineupPlayer> AwayStarters { get; set; } = new();
        public List<SportsLineupPlayer> AwayBench { get; set; } = new();
    }

    public class SportsLineupPlayer
    {
        public string Name { get; set; } = string.Empty;
        public int ShirtNumber { get; set; }

        /// <summary>Position abbreviation: G, D, M, F</summary>
        public string Position { get; set; } = string.Empty;

        /// <summary>Sağlayıcı saha koordinatı "hat:sıra" (ör. "2:4"). Yoksa null.</summary>
        public string? Grid { get; set; }

        public bool IsCaptain { get; set; }
    }

    /// <summary>Single player status returned by the sports data provider.</summary>
    public class SportsPlayerStatusResult
    {
        /// <summary>api-football oyuncu id'si (0 = sağlayıcı vermedi → ad ile tekilleştirilir).</summary>
        public int PlayerId { get; set; }

        public string PlayerName { get; set; } = string.Empty;
        public int TeamId { get; set; }

        /// <summary>"Injured" | "Suspended" | "Doubtful"</summary>
        public string Status { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
    }

    // ─── MatchDetail DTO section models (consumed by the frontend) ────────────

    public class LineupPlayerDto
    {
        public int ShirtNumber { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;

        /// <summary>
        /// Sağlayıcının açıkladığı saha koordinatı: "hat:sıra" (ör. "1:1", "2:4").
        /// UI dizilişi BUNDAN çizer; yoksa null gelir ve UI konum ÜRETMEZ.
        /// </summary>
        public string? Grid { get; set; }

        public bool IsCaptain { get; set; }
    }

    public class PlayerStatusDto
    {
        public string PlayerName { get; set; } = string.Empty;
        public int TeamId { get; set; }

        /// <summary>"Injured" | "Suspended" | "Doubtful"</summary>
        public string Status { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
    }

    public class LineupSectionDto
    {
        /// <summary>
        /// True when at least one side has officially released their lineup.
        /// </summary>
        public bool LineupsAnnounced { get; set; }

        /// <summary>
        /// Açıklanan diziliş ("4-4-2"). Her takım için AYRIDIR ve sağlayıcıdan gelir;
        /// yoksa null → UI "Diziliş bilgisi mevcut değil" der, tahmin ÜRETMEZ.
        /// </summary>
        public string? HomeFormation { get; set; }
        public string? AwayFormation { get; set; }

        public List<LineupPlayerDto> HomeStartingXI { get; set; } = new();
        public List<LineupPlayerDto> AwayStartingXI { get; set; } = new();

        public List<LineupPlayerDto> HomeBench { get; set; } = new();
        public List<LineupPlayerDto> AwayBench { get; set; } = new();

        // ── DÜRÜST BEKLEME DURUMU (06.09.2026) ──────────────────────────────────
        //
        // Arayüz eskiden sabit bir söz veriyordu: "Kadrolar maçtan 1 saat önce
        // açıklanacak." Bu YANLIŞTI — kadro her zaman tam 1 saat önce yayımlanmaz,
        // yayıncıya ve lige göre değişir. Maça 45 dakika kalmışken bile kullanıcı bu
        // metni okuyup boş ekrana bakıyordu. Artık ekran VAAT ETMEZ, DURUM BİLDİRİR;
        // bunun için gereken üç gerçeği backend taşır.

        /// <summary>
        /// Kadro yoklama penceresi AÇILDI mı? (kickoff'a 90 dakika veya daha az kaldı)
        /// false iken arayüz "maç saatine yaklaşıldığında gösterilecek" der; SAAT VERMEZ.
        /// </summary>
        public bool PollingWindowOpen { get; set; }

        /// <summary>Kickoff geçti mi? Geçtiyse ve veri yoksa arayüz bunu açıkça söyler.</summary>
        public bool KickoffPassed { get; set; }

        /// <summary>
        /// Sağlayıcıya EN SON ne zaman soruldu (UTC)? Hiç sorulmadıysa null.
        /// Arayüz bunu küçük bir "Son kontrol: 18:42" satırı olarak gösterebilir.
        /// Değer <c>MatchLineups.FetchedAt</c>'tir — uydurulmaz.
        /// </summary>
        public DateTime? LastCheckedUtc { get; set; }
    }

    public class PlayerStatusSectionDto
    {
        public List<PlayerStatusDto> Injuries { get; set; } = new();
        public List<PlayerStatusDto> Suspensions { get; set; } = new();
        public List<PlayerStatusDto> Doubtful { get; set; } = new();
    }
}
