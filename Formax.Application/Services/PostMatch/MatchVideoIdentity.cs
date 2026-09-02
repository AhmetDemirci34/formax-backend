using System;
using System.Collections.Generic;

namespace Formax.Application.Services.PostMatch
{
    /// <summary>
    /// KEŞFEDİLMİŞ VİDEO ADAYI — henüz hiçbir şey doğrulanmamış ham kayıt.
    ///
    /// Bir aday DB'ye ASLA doğrudan yazılmaz; <see cref="MatchVideoIdentityValidator"/>
    /// kapısından geçmek zorundadır.
    /// </summary>
    public sealed record OfficialVideoCandidate(
        /// <summary>"YouTube" | "Web".</summary>
        string Platform,
        /// <summary>
        /// YouTube kanal kimliği (UC…) ya da Web kaynakları için
        /// <see cref="OfficialVideoSources"/> anahtarı. KİMLİK budur — kanal ADI değil.
        /// </summary>
        string SourceIdentifier,
        string ExternalVideoId,
        string Title,
        string? Description,
        DateTime PublishedUtc,
        string SourcePageUrl,
        string? ThumbnailUrl,
        int? DurationSeconds,
        /// <summary>
        /// Kaynağın bildirdiği ülke listesi (ISO alpha-2). Kaynak söylemediyse boş —
        /// "her yerde açık" VARSAYILMAZ, yalnız bilinen kısıt taşınır.
        /// </summary>
        IReadOnlyList<string>? AvailableCountries = null,
        /// <summary>
        /// Olay klibi meta verisi — YALNIZ kaynakta açıkça varsa doldurulur.
        /// Maç özetinden dakika/oyuncu çıkarımı YAPILMAZ.
        /// </summary>
        int? EventMinute = null,
        int? EventExtraMinute = null,
        string? EventPlayer = null,
        string? EventTeam = null);

    /// <summary>
    /// MAÇIN KİMLİĞİ — bir videonun bu maça ait OLDUĞUNUN kanıt kümesi.
    ///
    /// <see cref="OtherLegDatesUtc"/> olmadan çift maçlı tur ayrılamaz: iki ayak da aynı
    /// iki takımı, aynı turnuvayı ve çoğu zaman aynı başlık kalıbını taşır.
    /// </summary>
    public sealed record VideoFixtureIdentity(
        int MatchId,
        string ExternalFixtureId,
        DateTime MatchDateUtc,
        int HomeTeamId,
        int AwayTeamId,
        string HomeTeamName,
        string AwayTeamName,
        IReadOnlyList<DateTime> OtherLegDatesUtc);

    /// <summary>Doğrulama sonucu. <paramref name="Reason"/> ret hâlinde KÖK NEDENDİR.</summary>
    public sealed record MatchVideoVerdict(
        bool Accepted,
        string? VideoType,
        OfficialVideoSource? Source,
        string Reason,
        /// <summary>
        /// Ret hâlinde MAKİNE OKUNUR gerekçe
        /// (<see cref="Formax.Domain.Constants.MatchVideoRejectionReasons"/>).
        /// Geriye dönük tarama, kapanan kayıtları bu kodla gruplayabilsin diye vardır;
        /// null ise gerekçe sınıflandırılmamış demektir (kabul edilenlerde her zaman null).
        /// </summary>
        string? RejectionCode = null);
}
