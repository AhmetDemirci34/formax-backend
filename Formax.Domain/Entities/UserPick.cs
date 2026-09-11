using Formax.Domain.Enums;
using System;

namespace Formax.Domain.Entities;

/// <summary>
/// KULLANICININ SEÇTİĞİ OLASI SONUÇ — "Senin seçimin".
///
/// PARALEL BİR TAHMİN SİSTEMİ KURULMADI (ürün kararı 06.09.2026): "Tahminlerim"
/// ekranının kalıcılığı zaten bu tablodur. Yeni alanlar ADDITIVE eklendi; eski
/// <see cref="PickLabel"/>, <see cref="Confidence"/> ve <see cref="Status"/>
/// sözleşmesi bozulmadı.
///
/// BU BİR MODEL TAHMİNİ DEĞİLDİR. Kullanıcının kendi seçimidir ve arayüzde asla
/// modelin tahmini gibi etiketlenmez. Kaydedilen olasılık, SEÇİM ANINDAKİ modelin
/// değeridir (dondurulmuş anlık görüntü): model sonradan güncellense bile kullanıcının
/// gördüğü sayı değişmez — aksi hâlde "ben bunu %62'yken seçmiştim" iddiası
/// doğrulanamaz hâle gelirdi.
/// </summary>
public class UserPick
{
    public Guid Id { get; set; }

    /// <summary>Kimlik — mevcut auth akışındaki kullanıcı kimliği (metin).</summary>
    public required string UserId { get; set; }

    public int MatchId { get; set; }

    /// <summary>Kullanıcıya GÖSTERİLEN etiket ("2.5 Alt", "Karşılıklı Gol Var").</summary>
    public required string PickLabel { get; set; }

    /// <summary>
    /// SEÇİM ANINDAKİ model olasılığı (0-100). Ad geriye dönük uyumluluk içindir;
    /// <see cref="ProbabilityPercent"/> aynı değeri açık adıyla taşır.
    /// </summary>
    public int Confidence { get; set; }

    public PickStatus Status { get; set; } = PickStatus.Pending;

    public DateTime CreatedAt { get; set; }

    // ── SEÇİM SÖZLEŞMESİ (06.09.2026 · additive) ────────────────────────────────

    /// <summary>
    /// Normalize market anahtarı (<see cref="Formax.Domain.Constants.OddsMarketKeys"/>),
    /// ör. "ALT_2_5". Etiketten AYRIDIR: etiket dile ve sürüme göre değişebilir,
    /// anahtar değişmez. Çakışma kuralı ve settlement bu alanı okur.
    /// </summary>
    public string? MarketKey { get; set; }

    /// <summary>
    /// Çakışma grubu — uygulama katmanındaki <c>PickMarketGroups</c> çözer, ör. "TOTAL_2_5".
    /// Aynı gruptan yalnız BİR seçim aktif olabilir. Grupsuz seçim hiçbir şeyi dışlamaz.
    /// </summary>
    public string? MarketGroup { get; set; }

    /// <summary>Seçim anındaki model olasılığı (0-100) — dondurulmuş anlık görüntü.</summary>
    public int? ProbabilityPercent { get; set; }

    /// <summary>Seçim anındaki gerçek market oranı; sağlayıcıda karşılığı yoksa null.</summary>
    public decimal? OddAtSelection { get; set; }

    /// <summary>Olasılığı üreten motor sürümleri (ör. "INDEPENDENT_POISSON_V2/TEAM_STRENGTH_V2").</summary>
    public string? ModelVersions { get; set; }

    /// <summary>Karar paketinin parmak izi — hangi girdi kümesinden üretildiğinin kanıtı.</summary>
    public string? ModelFingerprint { get; set; }

    /// <summary>
    /// Maçın başlama anı (UTC) — seçim anında dondurulur. Kickoff sonradan
    /// güncellense bile "seçim maç başlamadan mı yapıldı?" sorusu yanıtlanabilir kalır.
    /// </summary>
    public DateTime? MatchKickoffUtc { get; set; }

    /// <summary>
    /// Seçimin yaşam durumu: "Active" (maç başlamadı) | "Pending" (maç oynanıyor/bekliyor)
    /// | "Settled" (sonuç hesaplandı) | "Unsettleable" (bu market için sonuç hesaplanamıyor).
    /// <see cref="Status"/>'tan farklıdır: o doğru/yanlış, bu ise nerede olduğudur.
    /// </summary>
    public string? SelectionStatus { get; set; }

    /// <summary>Sonucun hesaplandığı an (UTC). Hesaplanamıyorsa null — uydurma settlement YOK.</summary>
    public DateTime? SettledAtUtc { get; set; }

    /// <summary>Sonucun dayanağı ("MS 1-2 · KG Var"). Hesaplanamadıysa null.</summary>
    public string? SettlementNote { get; set; }
}
