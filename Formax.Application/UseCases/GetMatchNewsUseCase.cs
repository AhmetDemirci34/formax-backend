using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Nabiz;
using Formax.Application.Interfaces;
using Formax.Application.Services.Fixtures;
using Formax.Application.Services.News.Feed;
using Formax.Application.Services.News.Translation;

namespace Formax.Application.UseCases
{
    /// <summary>
    /// SON DAKİKA — dile duyarlı haber ucu (<c>GET /api/matches/{id}/news?lang=xx</c>).
    ///
    /// NEDEN AYRI UÇ: çeviri bir LLM çağrısıdır (saniyeler sürer). Maç detayının tamamını
    /// bekletmemek için çeviri buraya alınmıştır; <c>/detail</c> haberleri ORİJİNAL diliyle
    /// ve eskisi kadar hızlı döndürmeye devam eder. Liste üretimi ikisinde de AYNI
    /// <see cref="MatchNewsFeedService"/>'tir — iki farklı haber listesi oluşamaz.
    ///
    /// Maç bilgisi doğrudan maç deposundan okunur; ağır maç-detayı zinciri (AI, olasılık,
    /// kadro, puan durumu) bu uç için ÇALIŞTIRILMAZ.
    /// </summary>
    public sealed class GetMatchNewsUseCase
    {
        private readonly IMatchReadRepository _matches;
        private readonly ITeamRepository _teams;
        private readonly FormaxMatchIdFactory _matchIdFactory;
        private readonly MatchNewsFeedService _feed;
        private readonly NewsTranslationService _translation;

        public GetMatchNewsUseCase(
            IMatchReadRepository matches,
            ITeamRepository teams,
            FormaxMatchIdFactory matchIdFactory,
            MatchNewsFeedService feed,
            NewsTranslationService translation)
        {
            _matches = matches;
            _teams = teams;
            _matchIdFactory = matchIdFactory;
            _feed = feed;
            _translation = translation;
        }

        /// <summary>Maç yoksa null döner (404). Haber yoksa boş liste döner (boş durum).</summary>
        public async Task<NabizSectionDto?> ExecuteAsync(
            int matchId, string? language, CancellationToken ct = default)
        {
            var match = _matches.Query().FirstOrDefault(m => m.Id == matchId);
            if (match == null) return null;

            var home = _teams.GetById(match.HomeTeamId);
            var away = _teams.GetById(match.AwayTeamId);
            var homeName = home?.Name ?? string.Empty;
            var awayName = away?.Name ?? string.Empty;
            if (homeName.Length == 0 || awayName.Length == 0)
                return new NabizSectionDto();

            var formaxMatchId = _matchIdFactory.Create(match.MatchDate, homeName, awayName);

            var items = await _feed.BuildAsync(
                formaxMatchId, homeName, awayName, match.MatchDate, ct);

            if (items.Count == 0) return new NabizSectionDto();

            // Çeviri: desteklenmeyen/boş dilde hiç denenmez, orijinal içerik döner.
            // Başarısızlıkta da orijinal korunur (bkz. NewsTranslationService).
            if (NewsTranslationService.IsSupported(language))
                await _translation.ApplyAsync(items, language!, formaxMatchId, ct);

            return new NabizSectionDto { Items = items };
        }
    }
}
