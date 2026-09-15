using System;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Formax.Application.DTOs.Highlights;
using Formax.Application.Interfaces;
using Formax.Application.Services.Fixtures;
using Formax.Application.Services.Matches;
using Formax.Application.Services.News.Intelligence;

namespace Formax.Application.UseCases
{
    /// <summary>
    /// ÖNEMLİ ANLAR — <c>GET /api/matches/{id}/highlights</c>.
    ///
    /// AI MAÇ ANALİZİNE DOKUNMAZ (ayrı uç, ayrı içerik, ayrı zincir).
    ///
    /// İKİ GERÇEK KAYNAK:
    ///  • Anlar: FORMAX'ın maç olayı deposu (gol / penaltı / kart / VAR). Gerçekten olan
    ///    olaylardır; "maçın en önemli pozisyonu" gibi öznel etiket ÜRETİLMEZ.
    ///  • Videolar: resmi hesapların canonical sosyal paylaşımları (bugün YouTube RSS).
    ///
    /// TELİF: video indirilmez, yeniden barındırılmaz, proxy'lenmez. Yalnız platformun
    /// kendi izin verdiği embed adresi üretilir. Embed edilemeyen kaynakta EmbedUrl null
    /// kalır; UI "FORMAX içinde oynatılamıyor" der ve kaynağı yalnız ikincil seçenek sunar.
    ///
    /// KATI EŞLEŞTİRME: bir video ancak başlığı/özeti HER İKİ takımı da anıyorsa bu maça
    /// bağlanır. "Fenerbahçe yeni transfer yaptı" bu maçın önemli anı değildir.
    /// </summary>
    public sealed class GetMatchHighlightsUseCase
    {
        private readonly IMatchReadRepository _matches;
        private readonly ITeamRepository _teams;
        private readonly FormaxMatchIdFactory _matchIdFactory;
        private readonly ISocialPostRepository _social;
        private readonly IMatchLiveEventIngestionRepository _events;
        private readonly IMatchLiveStatsRepository _liveStats;
        private readonly IMemoryCache _cache;

        public GetMatchHighlightsUseCase(
            IMatchReadRepository matches,
            ITeamRepository teams,
            FormaxMatchIdFactory matchIdFactory,
            ISocialPostRepository social,
            IMatchLiveEventIngestionRepository events,
            IMatchLiveStatsRepository liveStats,
            IMemoryCache cache)
        {
            _matches = matches;
            _teams = teams;
            _matchIdFactory = matchIdFactory;
            _social = social;
            _events = events;
            _liveStats = liveStats;
            _cache = cache;
        }

        /// <summary>Maç yoksa null (404).</summary>
        public MatchHighlightsDto? Execute(int matchId)
        {
            // Video araştırması PAHALIDIR; aynı maç için tekrar tekrar yapılmaz.
            // Canlı Takip ucu bu önbellekten etkilenmez (ayrı uç, ayrı hat).
            if (_cache.TryGetValue(CacheKey(matchId), out MatchHighlightsDto? cached) && cached != null)
                return cached;

            var match = _matches.Query().FirstOrDefault(m => m.Id == matchId);
            if (match == null) return null;

            var homeName = _teams.GetById(match.HomeTeamId)?.Name ?? string.Empty;
            var awayName = _teams.GetById(match.AwayTeamId)?.Name ?? string.Empty;

            var state = ResolveState(match);

            var dto = new MatchHighlightsDto
            {
                MatchId  = match.Id,
                State    = state,
                HomeTeam = homeName,
                AwayTeam = awayName,
                Status   = "NotStartedYet"
            };

            // MAÇ BAŞLAMADI → içerik aranmaz, uydurulmaz.
            if (state == MatchLiveStateResolver.NotStarted) return dto;

            // ÖNCE gerçek olaylar: video araması bunlarla DARALTILIR (dakika çapraz kontrolü).
            var events  = Clean(_events.GetByMatchId(match.Id));
            var minutes = events.Select(e => e.Minute).Distinct().ToList();

            // VİDEO ÖZELLİĞİ KALDIRILDI (15.09.2026 ürün kararı): önemli anlar yalnız resmî olaylardan; video listesi boş.
            dto.Videos  = new List<MatchHighlightVideoDto>();
            dto.Moments = BuildMoments(events, dto.Videos);

            dto.Status = (dto.Moments.Count > 0 || dto.Videos.Count > 0) ? "Ready" : "NoContent";

            // ÖNBELLEK: aynı maç için video araştırması tekrar tekrar yapılmaz. Maç sürerken
            // kısa TTL (yeni olay/video gelebilir), bittiyse uzun TTL.
            _cache.Set(CacheKey(matchId), dto,
                state == MatchLiveStateResolver.Finished ? FinishedTtl : LiveTtl);

            return dto;
        }

        private static string CacheKey(int matchId) => $"highlights:{matchId}";

        /// <summary>Maç sürerken yeni olay/video gelebilir → kısa ömür.</summary>
        private static readonly TimeSpan LiveTtl = TimeSpan.FromMinutes(2);

        /// <summary>Bitmiş maçın önemli anları değişmez → uzun ömür.</summary>
        private static readonly TimeSpan FinishedTtl = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Önemli Anlar için durum. Bu ekran canlılık İDDİA ETMEZ (canlı etiketi Canlı
        /// Takip'in işidir); yalnız "maç başladı mı" ve "bittiği doğrulandı mı" ayrımı
        /// yapılır. Doğrulanamayan durum <c>Unknown</c>'dır ve içerik yine gösterilir.
        /// </summary>
        private string ResolveState(Domain.Entities.Match match)
        {
            var status = (match.Status ?? string.Empty).Trim();
            if (status.Equals("Postponed", StringComparison.OrdinalIgnoreCase)) return MatchLiveStateResolver.NotStarted;
            if (DateTime.UtcNow < match.MatchDate) return MatchLiveStateResolver.NotStarted;

            if (status.Equals("Finished", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                return MatchLiveStateResolver.Finished;

            var phase = _liveStats.GetByMatchId(match.Id)?.Phase?.Trim().ToUpperInvariant();
            if (phase is "FT" or "AET" or "PEN" or "MATCH FINISHED" or "PENALTIES")
                return MatchLiveStateResolver.Finished;

            return MatchLiveStateResolver.Unknown;
        }

        // ── ANLAR ────────────────────────────────────────────────────────────────

        private List<MatchHighlightMomentDto> BuildMoments(
            List<Domain.Entities.MatchLiveEvent> events, List<MatchHighlightVideoDto> videos)
        {
            var list = new List<MatchHighlightMomentDto>();

            foreach (var e in events)
            {
                var label = ResolveLabel(e.EventType, e.Detail);
                if (label == null) continue;   // önemli an değil (ör. oyuncu değişikliği)

                var team   = string.IsNullOrWhiteSpace(e.Team) ? null : e.Team.Trim();
                var player = string.IsNullOrWhiteSpace(e.Player) ? null : e.Player.Trim();

                var shootout = e.Minute > RegulationPlusExtraTime;

                list.Add(new MatchHighlightMomentDto
                {
                    Minute      = e.Minute,
                    // Sağlayıcı penaltı atışlarını 121, 122… olarak yazar; "121. dakika"
                    // demek YANLIŞ olur — atış serisi ayrı etiketlenir.
                    MinuteLabel = shootout ? "PEN" : e.Minute.ToString(CultureInfo.InvariantCulture),
                    Type        = e.EventType,
                    Label       = shootout ? ShootoutLabel(label) : label,
                    Description = BuildDescription(team, player, e.Detail),
                    Team        = team,
                    Player      = player,
                    // Video ancak başlığında AYNI dakika AÇIKÇA yazıyorsa bağlanır.
                    VideoId     = videos.FirstOrDefault(v => v.Minute == e.Minute)?.Id
                });
            }

            // En yeni an en üstte (maçın sonundan başına).
            return list.OrderByDescending(m => m.Minute).ToList();
        }

        /// <summary>Normal süre + uzatma sınırı; bunun üzeri penaltı atışlarıdır.</summary>
        private const int RegulationPlusExtraTime = 120;

        /// <summary>Penaltı atışı serisindeki olayın etiketi.</summary>
        private static string ShootoutLabel(string label) => label switch
        {
            "Penaltı Golü"  => "Penaltı Atışı — Gol",
            "Kaçan Penaltı" => "Penaltı Atışı — Kaçtı",
            _               => $"Penaltı Atışı — {label}"
        };

        /// <summary>
        /// OLAY TEMİZLİĞİ — depodaki gerçek bozukluğu düzeltir, veri UYDURMAZ.
        ///
        /// 1) ÇİFT KAYIT: canlı yoklama aynı olayı bir tur sonra 1 dakika kaymış olarak
        ///    tekrar yazabiliyor (depo tekilleştirme anahtarı dakikayı içerdiği için
        ///    yakalayamıyor). Ölçüldü (maç 5120, gerçek skor 2-5): 34'/35' Petkovic ve
        ///    44'/45' Sego aynı goller; liste 10 gol gösteriyordu. Aynı tür + aynı takım +
        ///    uyumlu oyuncu ve ≤2 dakika fark → TEK olay sayılır (oyuncu adı olan kayıt
        ///    tercih edilir).
        ///
        /// 2) VAR İPTALİ: VAR "Goal Disallowed" varsa aynı dakikadaki gol SAYILMAZ —
        ///    kullanıcıya iptal edilmiş bir golü "gol" diye göstermek yanlış bilgidir.
        ///    İptal satırının kendisi korunur.
        /// </summary>
        private static List<Domain.Entities.MatchLiveEvent> Clean(
            List<Domain.Entities.MatchLiveEvent> events)
        {
            var ordered = events.OrderBy(e => e.Minute).ToList();

            var disallowed = ordered
                .Where(e => string.Equals(e.EventType, "Var", StringComparison.OrdinalIgnoreCase)
                            && (e.Detail ?? string.Empty).Contains("disallowed", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var kept = new List<Domain.Entities.MatchLiveEvent>();

            foreach (var e in ordered)
            {
                // VAR ile iptal edilen gol listeye alınmaz.
                if (string.Equals(e.EventType, "Goal", StringComparison.OrdinalIgnoreCase)
                    && disallowed.Any(v => SameTeam(v.Team, e.Team) && Math.Abs(v.Minute - e.Minute) <= 1))
                    continue;

                var twin = kept.FirstOrDefault(k =>
                    string.Equals(k.EventType, e.EventType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(k.Detail ?? "", e.Detail ?? "", StringComparison.OrdinalIgnoreCase)
                    && SameTeam(k.Team, e.Team)
                    && CompatiblePlayer(k.Player, e.Player)
                    && Math.Abs(k.Minute - e.Minute) <= 2);

                if (twin == null)
                {
                    kept.Add(e);
                    continue;
                }

                // Aynı olayın iki kaydı: oyuncu adı taşıyanı tut.
                if (string.IsNullOrWhiteSpace(twin.Player) && !string.IsNullOrWhiteSpace(e.Player))
                {
                    kept[kept.IndexOf(twin)] = e;
                }
            }

            return kept;
        }

        private static bool SameTeam(string? a, string? b)
            => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>Oyuncular aynı ya da biri boş (sağlayıcı bazı kayıtlarda oyuncu vermiyor).</summary>
        private static bool CompatiblePlayer(string? a, string? b)
        {
            var x = (a ?? "").Trim();
            var y = (b ?? "").Trim();
            if (x.Length == 0 || y.Length == 0) return true;
            return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Kısa açıklama — YALNIZ sağlayıcının kendi alanlarından. Skor durumu, üstünlük
        /// veya "öne geçti" gibi ÇIKARIM yapılmaz.
        /// </summary>
        private static string? BuildDescription(string? team, string? player, string? detail)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(player)) parts.Add(player!);
            if (!string.IsNullOrWhiteSpace(team))   parts.Add(team!);

            var d = TranslateDetail(detail);
            if (d != null) parts.Add(d);

            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        /// <summary>Sağlayıcı ayrıntısının Türkçesi; tanınmıyorsa null (ham metin sızmaz).</summary>
        private static string? TranslateDetail(string? detail)
        {
            var d = (detail ?? string.Empty).Trim().ToLowerInvariant();
            if (d.Length == 0) return null;

            if (d.Contains("missed penalty"))   return "penaltı kaçtı";
            if (d.Contains("penalty"))          return "penaltıdan";
            if (d.Contains("own goal"))         return "kendi kalesine";
            if (d.Contains("normal goal"))      return "akan oyunda";
            if (d.Contains("offside"))          return "ofsayt";
            if (d.Contains("handball"))         return "elle oynama";
            if (d.Contains("foul"))             return "faul";
            if (d.Contains("video review"))     return "video inceleme";
            if (d.Contains("red card"))         return null;   // etikette zaten var
            if (d.Contains("yellow card"))      return null;
            return null;
        }

        /// <summary>
        /// Sağlayıcı olay türünün Türkçe karşılığı. Tanınmayan/önemsiz tür → null
        /// (uydurma etiket üretilmez, oyuncu değişikliği önemli an sayılmaz).
        /// </summary>
        private static string? ResolveLabel(string? eventType, string? detail)
        {
            var t = (eventType ?? string.Empty).Trim().ToLowerInvariant();
            var d = (detail ?? string.Empty).Trim().ToLowerInvariant();

            if (t == "goal")
            {
                if (d.Contains("missed penalty")) return "Kaçan Penaltı";
                if (d.Contains("penalty"))        return "Penaltı Golü";
                if (d.Contains("own goal"))       return "Kendi Kalesine Gol";
                return "Gol";
            }

            if (t == "card")
            {
                if (d.Contains("red"))    return "Kırmızı Kart";
                if (d.Contains("yellow")) return "Sarı Kart";
                return null;
            }

            if (t == "var")
            {
                if (d.Contains("disallowed")) return "VAR — Gol İptali";
                return "VAR İncelemesi";
            }

            if (t == "penalty") return "Penaltı";

            return null;
        }

        // ── VİDEOLAR ─────────────────────────────────────────────────────────────

        /// <summary>
        /// GLOBAL VIDEO DISCOVERY → VIDEO MATCHING → SOURCE VALIDATION → EMBED VALIDATION.
        ///
        /// Aday havuzu: maça bağlı canonical sosyal paylaşımlar (resmi hesaplar). Her aday
        /// <see cref="VideoMatchValidator"/> kapısından geçer; geçemeyen GÖSTERİLMEZ.
        /// Gerçek olay dakikaları çapraz doğrulama için verilir (rule: olay → video daraltma).
        /// </summary>
        private List<MatchHighlightVideoDto> BuildVideos(
            Domain.Entities.Match match, string homeName, string awayName,
            IReadOnlyCollection<int> eventMinutes)
        {
            var list = new List<MatchHighlightVideoDto>();
            if (homeName.Length == 0 || awayName.Length == 0) return list;

            string formaxMatchId;
            try { formaxMatchId = _matchIdFactory.Create(match.MatchDate, homeName, awayName); }
            catch { return list; }

            var ctx = new VideoMatchValidator.MatchContext
            {
                HomeTeam     = homeName,
                AwayTeam     = awayName,
                KickoffUtc   = match.MatchDate,
                EventMinutes = eventMinutes
            };

            foreach (var p in _social.GetByMatch(formaxMatchId, 40))
            {
                // Kaynağın kulübü: doğrulanmış resmi hesabın takımı. Kulüp kendi kanalında
                // kendi adını yazmadığı için bu kimlik "anıldı" sayılır (karşı takım yine
                // başlıkta AÇIKÇA geçmelidir).
                var sourceTeam = p.RelatedTeamId == match.HomeTeamId ? homeName
                               : p.RelatedTeamId == match.AwayTeamId ? awayName
                               : null;

                var verdict = VideoMatchValidator.Validate(
                    p.Headline, p.Summary, p.PublishedUtc, ctx, sourceTeam);
                if (!verdict.Accepted) continue;

                var embed = VideoEmbedResolver.Resolve(p.Platform, p.Url);

                list.Add(new MatchHighlightVideoDto
                {
                    Id           = p.ContentHash,
                    Title        = p.Headline,
                    Minute       = MatchMinuteExtractor.Extract(p.Headline, p.Summary).Value,
                    Platform     = p.Platform,
                    Source       = string.IsNullOrWhiteSpace(p.AccountName) ? p.AccountHandle : p.AccountName,
                    Url          = p.Url ?? string.Empty,
                    EmbedUrl     = embed.EmbedUrl,
                    ThumbnailUrl = embed.ThumbnailUrl,
                    Embeddable   = embed.Embeddable,
                    PublishedUtc = p.PublishedUtc
                });
            }

            return list.OrderByDescending(v => v.PublishedUtc).ToList();
        }

    }
}
