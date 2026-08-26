using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Live;
using Formax.Application.Interfaces;
using Formax.Application.Services.Fixtures;
using Formax.Application.Services.Matches;
using Formax.Application.Services.News.Feed;
using Formax.Application.Services.News.Intelligence;

namespace Formax.Application.UseCases
{
    /// <summary>
    /// CANLI TAKİP — <c>GET /api/matches/{id}/livefeed</c>.
    ///
    /// SORU: "Maçta ne oluyor?" → ÇIKTI: kanonik MAÇ OLAYLARI.
    ///
    /// BU HAT VİDEO ARAMAZ. Video araması "Önemli Anları İzle" hattındadır ve bu ucun
    /// performansını etkilemez (ayrı uç, ayrı çağrı, ayrı önbellek).
    ///
    /// LLM/GEMMA YOKTUR: olaylar <see cref="MatchEventExtractor"/> ile deterministik
    /// çıkarılır. Tanınmayan içerik olay sayılmaz ve akışa girmez.
    ///
    /// KAYNAK: global kaynaklar — haber deposu (Son Dakika ile AYNI katı maç kapıları) ve
    /// resmi sosyal paylaşımlar. api-football canlı akışı kaynak DEĞİLDİR.
    ///
    /// CANLI ETİKETİ: yalnız TAZE bir global olay maçın oynandığını doğrularsa. Sabit zaman
    /// penceresi canlılık kanıtı sayılmaz (yalnız olumsuzlama için kullanılır).
    /// </summary>
    public sealed class GetMatchLiveFeedUseCase
    {
        private const int FeedLimit = 50;

        private readonly IMatchReadRepository _matches;
        private readonly ITeamRepository _teams;
        private readonly FormaxMatchIdFactory _matchIdFactory;
        private readonly MatchNewsFeedService _newsFeed;
        private readonly ISocialPostRepository _social;
        private readonly IMatchLiveStatsRepository _liveStats;
        private readonly IMatchLiveEventIngestionRepository _events;

        public GetMatchLiveFeedUseCase(
            IMatchReadRepository matches,
            ITeamRepository teams,
            FormaxMatchIdFactory matchIdFactory,
            MatchNewsFeedService newsFeed,
            ISocialPostRepository social,
            IMatchLiveStatsRepository liveStats,
            IMatchLiveEventIngestionRepository events)
        {
            _matches = matches;
            _teams = teams;
            _matchIdFactory = matchIdFactory;
            _newsFeed = newsFeed;
            _social = social;
            _liveStats = liveStats;
            _events = events;
        }

        public async Task<MatchLiveFeedDto?> ExecuteAsync(int matchId, CancellationToken ct = default)
        {
            var match = _matches.Query().FirstOrDefault(m => m.Id == matchId);
            if (match == null) return null;

            var homeName = _teams.GetById(match.HomeTeamId)?.Name ?? string.Empty;
            var awayName = _teams.GetById(match.AwayTeamId)?.Name ?? string.Empty;

            var kickoff = match.MatchDate;
            var now = DateTime.UtcNow;
            var status = (match.Status ?? string.Empty).Trim();

            var dto = new MatchLiveFeedDto
            {
                MatchId    = match.Id,
                KickoffUtc = kickoff,
                HomeTeam   = homeName,
                AwayTeam   = awayName
            };

            // MAÇ BAŞLAMADI → canlı skor, canlı dakika, canlı olay YOK.
            if (status.Equals("Postponed", StringComparison.OrdinalIgnoreCase) || now < kickoff)
            {
                dto.State = MatchLiveStateResolver.NotStarted;
                dto.StateMessage = MatchLiveStateResolver.Message(dto.State);
                return dto;
            }

            var events = new List<MatchLiveEventItemDto>();
            var scores = new List<(DateTime At, int Home, int Away)>();

            if (homeName.Length > 0 && awayName.Length > 0)
            {
                await CollectNewsEventsAsync(events, scores, match, homeName, awayName, kickoff, ct);
                CollectSocialEvents(events, scores, match, homeName, awayName, kickoff);
            }

            dto.Events = events
                .OrderByDescending(e => e.PublishedAt)   // EN YENİ EN ÜSTTE
                .Take(FeedLimit)
                .ToList();

            var stats = _liveStats.GetByMatchId(match.Id);

            dto.State = MatchLiveStateResolver.Resolve(kickoff, now, new MatchLiveStateResolver.Signals
            {
                RecordSaysFinished = status.Equals("Finished", StringComparison.OrdinalIgnoreCase)
                                     || status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase),
                RecordPhaseIsFinal = IsFinalPhase(stats?.Phase),
                // Global kaynak "maç sona erdi" olayı ürettiyse maç bitmiştir.
                GlobalSaysFullTime = dto.Events.Any(e => e.EventType == MatchEventExtractor.FullTime),
                // CANLI KANITI: taze bir maç-içi olay. Sabit süre penceresi değil.
                GlobalConfirmsInPlay = dto.Events.Any(e =>
                    IsInPlayEvent(e.EventType) && IsFresh(e.PublishedAt, now)),
                RecordSaysPostponed = false
            });

            dto.StateMessage  = MatchLiveStateResolver.Message(dto.State);
            dto.LiveConfirmed = dto.State == MatchLiveStateResolver.Live;

            ApplyScore(dto, stats, scores, now);

            // SKOR ↔ OLAY ÇELİŞKİSİ: kayıtlı skor maçın tamamını kapsamıyorsa dürüstçe
            // söylenir (UI'da gizlenmez). Ölçüldü: 65 maçta skor 90'da donmuş ama olaylar
            // uzatma/penaltılarla devam etmiş.
            if (dto.ScoreIsFinal)
            {
                var rec = MatchResultReconciler.Reconcile(
                    stats, _events.GetByMatchId(match.Id), homeName, awayName);

                if (rec.StoredScoreIsPartial)
                {
                    dto.ScoreIsFinal = false;   // "kesin sonuç" DİYE SUNULMAZ
                    dto.ScoreNote    = rec.Note;
                    dto.ShootoutHome = rec.ShootoutHome;
                    dto.ShootoutAway = rec.ShootoutAway;
                }
            }

            return dto;
        }

        // ── Skor ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// SKOR İKİ MEŞRU KAYNAK:
        ///  • Maç sürerken: GLOBAL kaynağın kendi yazdığı skor (SCORE_UPDATE olayı).
        ///    api-football canlı skoru KULLANILMAZ.
        ///  • Maç bittiğinde: kayıtlı kesin sonuç.
        /// İkisi de yoksa skor gösterilmez; dürüst gerekçe yazılır.
        /// </summary>
        private static void ApplyScore(
            MatchLiveFeedDto dto,
            Domain.Entities.MatchLiveStats? stats,
            List<(DateTime At, int Home, int Away)> scores,
            DateTime now)
        {
            if (dto.State == MatchLiveStateResolver.Finished)
            {
                if (stats == null)
                {
                    dto.ScoreUnavailableReason = "Bu maç için kayıtlı sonuç bulunmuyor.";
                    return;
                }

                dto.Score = new MatchLiveScoreDto
                {
                    HomeScore = stats.HomeScore,
                    AwayScore = stats.AwayScore,
                    Phase     = string.IsNullOrWhiteSpace(stats.Phase) ? null : stats.Phase,
                    Origin    = "record",
                    UpdatedAt = stats.UpdatedAt
                };
                dto.ScoreIsFinal = true;
                return;
            }

            var latest = scores.OrderByDescending(s => s.At).FirstOrDefault();
            if (latest.At != default && IsFresh(latest.At, now))
            {
                dto.Score = new MatchLiveScoreDto
                {
                    HomeScore = latest.Home,
                    AwayScore = latest.Away,
                    Phase     = null,
                    Origin    = "global",
                    UpdatedAt = latest.At
                };
                return;
            }

            dto.ScoreUnavailableReason = "Canlı skor bilgisi şu anda global kaynaklardan alınamıyor.";
        }

        // ── Global kaynaklardan olay toplama ─────────────────────────────────────

        private async Task CollectNewsEventsAsync(
            List<MatchLiveEventItemDto> events,
            List<(DateTime At, int Home, int Away)> scores,
            Domain.Entities.Match match,
            string homeName, string awayName, DateTime kickoff,
            CancellationToken ct)
        {
            string formaxMatchId;
            try { formaxMatchId = _matchIdFactory.Create(match.MatchDate, homeName, awayName); }
            catch { return; }

            // Son Dakika ile AYNI servis → aynı katı maç kapıları (her iki takım da geçmeli).
            var news = await _newsFeed.BuildAsync(formaxMatchId, homeName, awayName, kickoff, ct);

            foreach (var n in news)
            {
                if (n.PublishedAt < kickoff) continue;   // ilk düdükten öncesi olay değildir

                var ev = MatchEventExtractor.Extract(n.Headline, n.Summary, homeName, awayName);
                if (ev == null) continue;                // olay değil → akışa girmez

                if (ev.HomeScore.HasValue && ev.AwayScore.HasValue)
                    scores.Add((n.PublishedAt, ev.HomeScore.Value, ev.AwayScore.Value));

                events.Add(Map(ev, n.Id, n.Headline, n.Summary, n.Source, n.SourceUrl, n.PublishedAt, "news"));
            }
        }

        private void CollectSocialEvents(
            List<MatchLiveEventItemDto> events,
            List<(DateTime At, int Home, int Away)> scores,
            Domain.Entities.Match match,
            string homeName, string awayName, DateTime kickoff)
        {
            string formaxMatchId;
            try { formaxMatchId = _matchIdFactory.Create(match.MatchDate, homeName, awayName); }
            catch { return; }

            foreach (var p in _social.GetByMatch(formaxMatchId, 40))
            {
                if (p.PublishedUtc < kickoff) continue;

                var text = (p.Headline ?? string.Empty) + " " + (p.Summary ?? string.Empty);

                // KATI EŞLEŞTİRME — tek takım adı YETMEZ.
                var relation = MatchIntelligenceService.ResolveRelation(
                    text, homeName, awayName, strictBothTeams: true,
                    kickoffUtc: kickoff, publishedUtc: p.PublishedUtc,
                    homeIsNextMatch: true, awayIsNextMatch: true);
                if (relation == null) continue;

                var ev = MatchEventExtractor.Extract(p.Headline, p.Summary, homeName, awayName);
                if (ev == null) continue;

                if (ev.HomeScore.HasValue && ev.AwayScore.HasValue)
                    scores.Add((p.PublishedUtc, ev.HomeScore.Value, ev.AwayScore.Value));

                var source = string.IsNullOrWhiteSpace(p.AccountName) ? p.AccountHandle : p.AccountName;
                events.Add(Map(ev, p.ContentHash, p.Headline, p.Summary, source, p.Url ?? "", p.PublishedUtc, "social"));
            }
        }

        private static MatchLiveEventItemDto Map(
            MatchEventExtractor.ExtractedEvent ev,
            string id, string? headline, string? summary,
            string source, string sourceUrl, DateTime publishedAt, string origin)
            => new()
            {
                Id          = id,
                EventType   = ev.Type,
                Label       = EventLabel(ev.Type),
                Minute      = ev.Minute,
                MinuteLabel = ev.MinuteLabel,
                Team        = ev.Team,
                Player      = ev.Player,
                // Açıklama = KAYNAĞIN kendi metni. FORMAX cümle üretmez.
                Description = string.IsNullOrWhiteSpace(headline) ? (summary ?? string.Empty) : headline!,
                Source      = source,
                SourceUrl   = sourceUrl,
                PublishedAt = publishedAt,
                Origin      = origin
            };

        /// <summary>Kanonik türün Türkçe ekran etiketi (sabit sözlük — çeviri motoru YOK).</summary>
        public static string EventLabel(string type) => type switch
        {
            MatchEventExtractor.Kickoff        => "MAÇ BAŞLADI",
            MatchEventExtractor.Goal           => "GOL",
            MatchEventExtractor.PenaltyGoal    => "PENALTI GOLÜ",
            MatchEventExtractor.MissedPenalty  => "KAÇAN PENALTI",
            MatchEventExtractor.PenaltyAwarded => "PENALTI",
            MatchEventExtractor.YellowCard     => "SARI KART",
            MatchEventExtractor.SecondYellow   => "İKİNCİ SARI",
            MatchEventExtractor.RedCard        => "KIRMIZI KART",
            MatchEventExtractor.Substitution   => "OYUNCU DEĞİŞİKLİĞİ",
            MatchEventExtractor.Var            => "VAR",
            MatchEventExtractor.VarDisallowed  => "VAR — GOL İPTAL",
            MatchEventExtractor.HalfTime       => "DEVRE ARASI",
            MatchEventExtractor.SecondHalf     => "İKİNCİ YARI",
            MatchEventExtractor.ExtraTime      => "UZATMA",
            MatchEventExtractor.FullTime       => "MAÇ SONA ERDİ",
            MatchEventExtractor.ScoreUpdate    => "SKOR",
            _                                  => type
        };

        /// <summary>Maçın İÇİNDEN olan olaylar — canlılık kanıtı olabilirler.</summary>
        private static bool IsInPlayEvent(string type) => type is
            MatchEventExtractor.Kickoff or MatchEventExtractor.Goal or
            MatchEventExtractor.PenaltyGoal or MatchEventExtractor.MissedPenalty or
            MatchEventExtractor.PenaltyAwarded or MatchEventExtractor.YellowCard or
            MatchEventExtractor.SecondYellow or MatchEventExtractor.RedCard or
            MatchEventExtractor.Substitution or MatchEventExtractor.Var or
            MatchEventExtractor.VarDisallowed or MatchEventExtractor.HalfTime or
            MatchEventExtractor.SecondHalf or MatchEventExtractor.ExtraTime or
            MatchEventExtractor.ScoreUpdate;

        private static bool IsFresh(DateTime at, DateTime nowUtc)
            => (nowUtc - at).TotalMinutes <= MatchLiveStateResolver.LiveEvidenceFreshnessMinutes;

        private static bool IsFinalPhase(string? phase)
        {
            if (string.IsNullOrWhiteSpace(phase)) return false;
            var p = phase.Trim().ToUpperInvariant();
            return p is "FT" or "AET" or "PEN" or "MATCH FINISHED" or "PENALTIES";
        }
    }
}
