namespace Formax.Domain.Entities
{
    /// <summary>
    /// FORMAX MAÇI ↔ RESMÎ KAYNAKTAKİ MAÇ — kimlik çözümünün kalıcı sonucu.
    ///
    /// Bir kez doğrulanan eşleme her turda yeniden kurulmaz; ama her tur kaynaktaki ev/
    /// deplasman sırası ve tarih yeniden sınanır (eşleme körü körüne güvenilmez).
    /// Aynı kaynak maçı iki FORMAX maçına, aynı FORMAX maçı aynı kaynakta iki maça bağlanamaz.
    /// </summary>
    public class OfficialMatchLink
    {
        public int Id { get; set; }

        public int MatchId { get; set; }

        public string SourceKey { get; set; } = string.Empty;

        /// <summary>Kaynağın kendi maç kimliği (ör. "serie-a::Football_Match::e32f…").</summary>
        public string OfficialMatchId { get; set; } = string.Empty;

        /// <summary>Kaynaktaki herkese açık maç sayfası (varsa).</summary>
        public string? OfficialUrl { get; set; }

        /// <summary>Kaynağın yazdığı takım adları — eşlemenin kanıtı.</summary>
        public string OfficialHomeName { get; set; } = string.Empty;
        public string OfficialAwayName { get; set; } = string.Empty;

        /// <summary>Kaynağın bildirdiği başlama anı (UTC).</summary>
        public DateTime? OfficialKickoffUtc { get; set; }

        /// <summary>Kaynağın en son bildirdiği stat adı — stat değişikliği yalnız iki resmî gözlem arasında aranır.</summary>
        public string? OfficialVenue { get; set; }

        /// <summary>Kaynağın en son bildirdiği durum (Scheduled/Live/Finished/Postponed/Cancelled/Suspended).</summary>
        public string? OfficialStatus { get; set; }

        public DateTime LinkedAtUtc { get; set; }

        /// <summary>Eşlemenin kaynağa karşı en son yeniden doğrulandığı an.</summary>
        public DateTime VerifiedAtUtc { get; set; }
    }
}
