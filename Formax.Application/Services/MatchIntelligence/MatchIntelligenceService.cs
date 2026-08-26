using Formax.Application.DTOs.MatchIntelligence;
using Formax.Application.DTOs.Matches;
using Formax.Application.DTOs.Nabiz;
using Formax.Application.Interfaces;
using Formax.Application.Services.Fixtures;
using Formax.Application.Services.MatchIntelligence.Lineup;
using Formax.Application.Services.MatchIntelligence.Live;
using Formax.Application.Services.MatchIntelligence.News;
using Formax.Application.Services.MatchIntelligence.Market;
using Formax.Application.Services.MatchIntelligence.Outlook;
using Formax.Application.Services.MatchIntelligence.WatchLive;
using Formax.Application.UseCases;
using Formax.Domain.Entities;

namespace Formax.Application.Services.MatchIntelligence;

/// <summary>
/// MI v2 servis implementasyonu. Mevcut <see cref="GetMatchDetailAIContextUseCase"/>'i çağırır
/// (match verisi + Radar LLM narrative), Expected Lineup (Görev #013) ve Living Lineup (Görev #014)
/// motorlarıyla kadro tahmini/karşılaştırmasını üretir, sonra <see cref="MatchIntelligenceMapper"/>
/// ile Experience bazlı DTO'ya çevirir. Eski endpoint/DTO/UseCase değişmez; tamamen paraleldir.
/// </summary>
public sealed class MatchIntelligenceService : IMatchIntelligenceService
{
    private readonly GetMatchDetailAIContextUseCase _matchDetail;
    private readonly IExpectedLineupEngine _expectedLineup;
    private readonly ILivingLineupEngine _livingLineup;
    private readonly INewsIntelligenceEngine _newsIntelligence;
    private readonly IMatchOutlookEngine _matchOutlook;
    private readonly IMarketIntelligenceEngine _marketIntelligence;
    private readonly IWatchLiveEngine _watchLive;
    private readonly ILiveIntelligenceEngine _liveIntelligence;
    private readonly IMatchNewsRepository _matchNews;
    private readonly FormaxMatchIdFactory _formaxMatchIdFactory;

    public MatchIntelligenceService(
        GetMatchDetailAIContextUseCase matchDetail,
        IExpectedLineupEngine expectedLineup,
        ILivingLineupEngine livingLineup,
        INewsIntelligenceEngine newsIntelligence,
        IMatchOutlookEngine matchOutlook,
        IMarketIntelligenceEngine marketIntelligence,
        IWatchLiveEngine watchLive,
        ILiveIntelligenceEngine liveIntelligence,
        IMatchNewsRepository matchNews,
        FormaxMatchIdFactory formaxMatchIdFactory)
    {
        _matchDetail = matchDetail;
        _expectedLineup = expectedLineup;
        _livingLineup = livingLineup;
        _newsIntelligence = newsIntelligence;
        _matchOutlook = matchOutlook;
        _marketIntelligence = marketIntelligence;
        _watchLive = watchLive;
        _liveIntelligence = liveIntelligence;
        _matchNews = matchNews;
        _formaxMatchIdFactory = formaxMatchIdFactory;
    }

    public async Task<MatchIntelligenceDto?> BuildAsync(int matchId, CancellationToken ct = default)
    {
        var detail = await _matchDetail.ExecuteAsync(matchId, ct);
        if (detail == null) return null;

        var homeId = detail.HomeTeam.Id;
        var awayId = detail.AwayTeam.Id;
        var homeName = detail.HomeTeam.Name;
        var awayName = detail.AwayTeam.Name;

        // Expected Lineup (muhtemel 11) — 07 Experience.
        var homeExpected = _expectedLineup.Predict(matchId, homeId, homeName, "Home");
        var awayExpected = _expectedLineup.Predict(matchId, awayId, awayName, "Away");

        // Living Lineup (beklenen ↔ resmi karşılaştırma) — 08 Experience.
        var homeLiving = _livingLineup.Analyze(matchId, homeId, homeName, "Home");
        var awayLiving = _livingLineup.Analyze(matchId, awayId, awayName, "Away");

        // News Intelligence (sınıflandırma + önem + etki + hero + özet) — 10 Experience.
        // Kaynak: NewsDiscovery'nin zaten topladığı per-maç haberleri (MatchNewsArticles),
        // kanonik FORMAX_MATCH_ID ile çözülür. İkinci bir toplama YAPILMAZ. Bu maça ait
        // keşfedilmiş haber yoksa legacy NABIZ sosyal feed'ine dönülür (graceful fallback).
        var newsItems = await ResolveNewsItemsAsync(detail, homeName, awayName, ct);
        var news = _newsIntelligence.Analyze(newsItems, homeName, awayName);

        // Match Outlook (form/performans/gol/tempo/denge/momentum/güven) — 11 Experience.
        var outlook = _matchOutlook.Analyze(detail);

        // Market Intelligence (eğilim/açılış→güncel/volatilite/kararlılık/sapma) — 12 Experience.
        var market = _marketIntelligence.Analyze(matchId, detail.Sapma, homeName, awayName);

        // Watch Live (resmi yayıncı + durum + öneri) — 13 Experience.
        var watchLive = _watchLive.Analyze(detail.League, detail.Status, detail.MatchDate, homeName, awayName);

        // Live Intelligence (canlı ritim/momentum/olay/özet) — 14 Experience.
        var liveIntel = _liveIntelligence.Analyze(detail.Live, detail.Status, homeName, awayName);

        return MatchIntelligenceMapper.Map(
            detail, homeExpected, awayExpected, homeLiving, awayLiving, news, outlook, market, watchLive, liveIntel);
    }

    /// <summary>
    /// News Engine'in girdisini üretir. Önce NewsDiscovery'nin bu maça yazdığı
    /// <see cref="MatchNewsArticle"/> kayıtlarını (kanonik FORMAX_MATCH_ID ile) okur;
    /// hiç yoksa legacy NABIZ sosyal feed'ine düşer. İkinci toplama yapmaz.
    /// </summary>
    private async Task<List<NabizFeedItemDto>> ResolveNewsItemsAsync(
        MatchDetailDto detail, string homeName, string awayName, CancellationToken ct)
    {
        // Kimlik lig-bağımsız + Kind-güvenlidir (tek kimlik otoritesi); MatchDate zaten UTC.
        var formaxMatchId = _formaxMatchIdFactory.Create(detail.MatchDate, homeName, awayName);

        var articles = await _matchNews.GetArticlesAsync(formaxMatchId, 20, ct);
        if (articles.Count > 0)
            return articles.Select(MapDiscoveredArticle).ToList();

        return detail.NabizFeed?.Items ?? new List<NabizFeedItemDto>();
    }

    /// <summary>MatchNewsArticle → NabizFeedItemDto (News Engine'in beklediği şekil).</summary>
    private static NabizFeedItemDto MapDiscoveredArticle(MatchNewsArticle a) => new()
    {
        Type = string.Empty,
        Source = FirstSource(a.Sources),
        Author = string.Empty,
        AuthorVerified = false,
        Headline = a.Headline,
        Summary = a.Summary,
        ImageUrl = null,
        SourceUrl = a.Url,
        PublishedAt = a.PublishedUtc
    };

    /// <summary>Virgülle ayrık kaynak listesinden birincil (ilk) kaynağı alır.</summary>
    private static string FirstSource(string sources)
    {
        if (string.IsNullOrWhiteSpace(sources)) return string.Empty;
        var first = sources.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return first.Length > 0 ? first[0] : string.Empty;
    }
}
