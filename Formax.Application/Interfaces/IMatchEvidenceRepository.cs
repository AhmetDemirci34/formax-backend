using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.News.Intelligence;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — Evidence Store kalıcılık katmanı. Sinyalleri
    /// FORMAX_MATCH_ID altında tutar (ContentHash ile tekil) ve Reasoning için taze
    /// MatchIntelligenceContext üretir (süresi geçen kanıt okuma sırasında düşer).
    /// </summary>
    public interface IMatchEvidenceRepository
    {
        Task<int> UpsertAsync(string formaxMatchId, IEnumerable<MatchEvidence> evidence, CancellationToken ct = default);

        Task<MatchIntelligenceContext> GetContextAsync(string formaxMatchId, CancellationToken ct = default);

        /// <summary>
        /// Güvenlik kapıları OKUMA anında da uygulanmış context.
        ///
        /// NEDEN GEREKLİ: kalite/relevance kapıları YAZMA tarafına eklendi; kapılar eklenmeden
        /// önce yazılmış kayıtlar depoda durmaya devam eder ve yalnız freshness'a takılır.
        /// Ölçüldü: eşik altı (SourceQuality &lt; 85) 650 kanıt hâlâ tazeydi ve 107 maçta AI'a
        /// ulaşıyordu. "Haber bulundu ≠ gerçek kabul edildi" kuralı yalnız yazma anında değil,
        /// AI'ın okuduğu HER anda geçerlidir → aynı kapılar burada tekrar uygulanır.
        ///
        /// Takım adları verilirse maç-ilgisi (her iki takım anılmalı) kapısı da uygulanır;
        /// verilmezse yalnız kalite + freshness uygulanır (uydurma eşleşme yapılmaz).
        /// </summary>
        Task<MatchIntelligenceContext> GetContextAsync(
            string formaxMatchId, string? homeTeam, string? awayTeam, CancellationToken ct = default);

        /// <summary>
        /// Kickoff BİLİNEREK okunan context. Haberin maça göre zaman konumu (maç öncesi /
        /// maç günü / maç sonrası / eski) yalnız kickoff ile belirlenebilir; kickoff
        /// verilmezse bu ayrım yapılamaz ve eski içerik "güncel gelişme" sanılabilir.
        /// Yeni sütun/tablo YOK: zaman konumu okuma anında hesaplanır.
        /// </summary>
        Task<MatchIntelligenceContext> GetContextAsync(
            string formaxMatchId, string? homeTeam, string? awayTeam, System.DateTime? kickoffUtc,
            CancellationToken ct = default);

        /// <summary>
        /// Bilinen kadro adları da verilerek okunan context. Haber olayının öznesi bir
        /// OYUNCU ise, adı yalnız bu listeyle eşleştiğinde taşınır — serbest ad çıkarımı
        /// yapılmaz. Liste verilmezse oyuncu alanı boş kalır (tahmin yok).
        /// </summary>
        Task<MatchIntelligenceContext> GetContextAsync(
            string formaxMatchId, string? homeTeam, string? awayTeam, System.DateTime? kickoffUtc,
            IEnumerable<string>? knownPlayerNames, CancellationToken ct = default);
    }
}
