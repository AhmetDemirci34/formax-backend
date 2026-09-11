using System;

namespace Formax.Application.DTOs.Picks
{
    /// <summary>Seçimin yaşam durumu — <c>UserPick.SelectionStatus</c> değerleri.</summary>
    public static class PickSelectionStatuses
    {
        /// <summary>Maç henüz başlamadı; seçim değiştirilebilir.</summary>
        public const string Active = "Active";

        /// <summary>Maç başladı/oynanıyor; sonuç henüz kesinleşmedi.</summary>
        public const string Pending = "Pending";

        /// <summary>Sonuç hesaplandı (doğru veya yanlış).</summary>
        public const string Settled = "Settled";

        /// <summary>Bu market için sonuç hesaplanamıyor — uydurma settlement YOK.</summary>
        public const string Unsettleable = "Unsettleable";
    }

    /// <summary>
    /// SEÇİM İSTEĞİ — arayüzün gönderdiği anlık görüntü.
    ///
    /// Etiket ve yüzde BACKEND'İN ürettiği "Olası Sonuçlar" listesinden gelir; arayüz
    /// yeni bir olasılık HESAPLAMAZ. Backend, etiketi bilinen market anahtarlarına
    /// çözemezse isteği reddeder — böylece istemci uydurma bir market kaydedemez.
    /// </summary>
    public sealed class UserPickRequest
    {
        public int MatchId { get; set; }

        /// <summary>Kullanıcıya gösterilen market etiketi ("2.5 Alt").</summary>
        public string MarketLabel { get; set; } = string.Empty;

        /// <summary>Seçim anındaki model olasılığı (0-100).</summary>
        public int ProbabilityPercent { get; set; }

        /// <summary>Seçim anındaki gerçek oran; sağlayıcıda yoksa null.</summary>
        public decimal? Odd { get; set; }

        public string? ModelVersions { get; set; }
        public string? ModelFingerprint { get; set; }
    }

    /// <summary>Kaydedilmiş bir seçim — arayüz durumunu bundan geri yükler.</summary>
    public sealed class UserPickDto
    {
        public Guid Id { get; set; }
        public int MatchId { get; set; }

        /// <summary>Normalize market anahtarı ("ALT_2_5") — arayüz satır eşlemesini bununla yapar.</summary>
        public string MarketKey { get; set; } = string.Empty;

        /// <summary>Çakışma grubu ("TOTAL_2_5"); grupsuz markette null.</summary>
        public string? MarketGroup { get; set; }

        /// <summary>Kullanıcıya gösterilen etiket.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>SEÇİM ANINDAKİ olasılık — model sonradan değişse de bu sayı sabittir.</summary>
        public int ProbabilityPercent { get; set; }

        public decimal? Odd { get; set; }

        /// <summary><see cref="PickSelectionStatuses"/>.</summary>
        public string SelectionStatus { get; set; } = PickSelectionStatuses.Active;

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? MatchKickoffUtc { get; set; }

        // ── SONUÇ (yalnız hesaplanabildiyse dolar) ──────────────────────────────

        /// <summary>true = doğru, false = yanlış, null = hesaplanamadı (uydurma YOK).</summary>
        public bool? IsCorrect { get; set; }

        /// <summary>Sonucun dayanağı ("MS 1-2"). Hesaplanamadıysa null.</summary>
        public string? SettlementNote { get; set; }

        /// <summary>Sonucun kalıcı yazıldığı an (UTC) — DB'den olduğu gibi. Sonuçlanmadıysa null.</summary>
        public DateTime? SettledAtUtc { get; set; }
    }

    /// <summary>
    /// TAHMİNLERİM ekranının kart modeli — maç + o maçtaki kullanıcı seçimleri.
    ///
    /// Kart bir MAÇI temsil eder; kullanıcının o maçtaki bir veya birden fazla seçimi
    /// altında listelenir. Karta tıklandığında <c>/match/{matchId}</c> açılır.
    /// </summary>
    public sealed class UserPredictionCardDto
    {
        public int MatchId { get; set; }
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;
        public string? HomeTeamLogoUrl { get; set; }
        public string? AwayTeamLogoUrl { get; set; }
        public string League { get; set; } = string.Empty;
        public DateTime MatchDateUtc { get; set; }

        /// <summary>Maçın gerçek durumu (NotStarted / Live / Finished).</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Bitmiş maçta kesin skor; aksi hâlde null — 0-0 UYDURULMAZ.</summary>
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public int? HalfTimeHomeScore { get; set; }
        public int? HalfTimeAwayScore { get; set; }

        /// <summary>
        /// 2. yarı skoru = MS − İY. YALNIZ ikisi de gerçekten varsa ve sonuç negatif
        /// değilse dolar; frontend bu çıkarmayı yapmaz.
        /// </summary>
        public int? SecondHalfHomeScore { get; set; }
        public int? SecondHalfAwayScore { get; set; }

        /// <summary>Kartın durumu: "Active" | "Pending" | "Settled" — seçimlerin özeti.</summary>
        public string CardStatus { get; set; } = PickSelectionStatuses.Active;

        public List<UserPickDto> Selections { get; set; } = new();
    }
}
