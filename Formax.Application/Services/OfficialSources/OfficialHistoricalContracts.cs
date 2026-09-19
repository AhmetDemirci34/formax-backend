using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Services.OfficialSources
{
    /// <summary>Kaynağın kendi sezon kimliği ve etiketi.</summary>
    /// <param name="SeasonId">Kaynağın sezon anahtarı (PL: "2025"; Serie A: "serie-a::Football_Season::…").</param>
    /// <param name="Label">İnsan okunur etiket ("2025/2026").</param>
    /// <param name="StartYear">Sezonun başladığı takvim yılı — en yeniden eskiye sıralama için.</param>
    public sealed record OfficialSeason(string SeasonId, string Label, int StartYear);

    /// <summary>
    /// GEÇMİŞ KADRO KAYNAĞI — canlı toplama penceresinin (T−60 … kickoff+10) DIŞINDA,
    /// tamamlanmış sezonların maç listesini veren kaynaklar.
    ///
    /// Bu arayüz YALNIZ LİSTELEMEYİ genişletir: kadro okuma, doğrulama ve yazma yolu
    /// <see cref="IOfficialCompetitionSource.ReadLineupAsync"/> ve mevcut toplayıcıyla AYNIDIR.
    /// Bir kaynağın bu arayüzü uygulamaması "geçmiş desteklenmiyor" demektir; zorlanmaz.
    /// </summary>
    public interface IOfficialHistoricalLineupSource
    {
        string SourceKey { get; }

        /// <summary>
        /// Kaynağın geçmiş kadro için GERÇEKTEN desteklediği sezonlar, en yeniden eskiye.
        /// Ölçülmemiş sezon döndürülmez.
        /// </summary>
        Task<OfficialRead<IReadOnlyList<OfficialSeason>>> ReadSeasonsAsync(
            OfficialRoundContext round, CancellationToken ct = default);

        /// <summary>Tek sezonun BÜTÜN maçları (canlı penceredeki tarih süzgeci olmadan).</summary>
        Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadSeasonMatchesAsync(
            OfficialSeason season, OfficialRoundContext round, CancellationToken ct = default);
    }

    /// <summary>
    /// GERÇEK KATILIM (değişiklik + dakika) — yalnız kaynağın kendi yayımladığı veriden.
    /// Kaynak değişiklik yayımlamıyorsa bu arayüzü UYGULAMAZ ve dakika alanları null kalır;
    /// "ilk 11 oynadı → 90 dakika" gibi bir varsayım hiçbir yerde yapılmaz.
    /// </summary>
    public interface IOfficialParticipationSource
    {
        string SourceKey { get; }

        /// <summary>
        /// Maçın değişiklikleri. Değer null ve sonuç Ok ise kaynak geçerli cevap verdi ama
        /// değişiklik yayımlamadı (dakika ÜRETİLMEZ).
        /// </summary>
        Task<OfficialRead<OfficialParticipationDocument>> ReadParticipationAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default);
    }

    /// <summary>Tek değişiklik — kaynağın kendi oyuncu kimlikleriyle.</summary>
    /// <param name="Side">"Home" | "Away".</param>
    /// <param name="PlayerOnOfficialId">Oyuna giren oyuncunun kaynak kimliği.</param>
    /// <param name="PlayerOffOfficialId">Çıkan oyuncunun kaynak kimliği.</param>
    /// <param name="Minute">Kaynağın yayımladığı dakika.</param>
    public sealed record OfficialSubstitution(string Side, string? PlayerOnOfficialId, string? PlayerOffOfficialId, int Minute);

    /// <summary>Maçın katılım belgesi — yalnız gerçekten yayımlanmış değişiklikler.</summary>
    public sealed record OfficialParticipationDocument(
        string SourceKey,
        string OfficialMatchId,
        string SourceUrl,
        string ContentHash,
        IReadOnlyList<OfficialSubstitution> Substitutions);
}
