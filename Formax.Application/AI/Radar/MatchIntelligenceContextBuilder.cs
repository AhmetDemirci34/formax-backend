using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.AI.Context;
using Formax.Application.DTOs.Matches;
using Formax.Application.DTOs.Nabiz;
using Formax.Application.Services.Matches;
using Formax.Application.Services.Radar.Intelligence.Scenarios;
using EvidenceContext = Formax.Application.Services.News.Intelligence.MatchIntelligenceContext;

namespace Formax.Application.AI.Radar
{
    /// <summary>
    /// FORMAX Radar v2 — mevcut <see cref="MatchDetailDto"/>'yu (News + Stats + Form +
    /// H2H + Importance + Discovery sinyalleri + deterministik olasılıklar zaten
    /// içinde) tek bir sindirilmiş <see cref="MatchIntelligenceContext"/>'e indirger.
    ///
    /// Yeni veri çekmez, repo'ya dokunmaz → mevcut akışı bozmaz, maksimum yeniden
    /// kullanım. Ham metin/istatistik LLM'e gitmez; yalnız özet sinyaller geçer.
    /// </summary>
    public sealed class MatchIntelligenceContextBuilder
    {
        private const int RecentFormCount = 5;
        private const int MaxNewsThemes = 3;
        private const int FreshNewsHours = 24;
        private const int MaxEvidenceItems = 6;

        public MatchIntelligenceContext Build(
            MatchDetailDto detail,
            string? worldHeadline = null,
            IReadOnlyList<ScenarioCandidate>? rankedScenarios = null,
            EvidenceContext? evidence = null,
            AvailabilityAiSignals? availability = null)
        {
            return new MatchIntelligenceContext
            {
                MatchId = detail.MatchId,
                HomeTeam = detail.HomeTeam.Name,
                AwayTeam = detail.AwayTeam.Name,
                League = detail.League,
                Round = detail.Round,

                // HAFTA NUMARASI BACKEND'DE ÇÖZÜLÜR. Ham sağlayıcı metni ("Regular Season - 1")
                // modele bırakılınca yanlış okunuyordu (ölçüldü: "beşinci hafta" yazdı, oysa
                // 1. hafta — sayıyı puan tablosu sırasından almıştı). Sayı yoksa null kalır.
                WeekNumber = WeekNumberOf(detail.Round),
                // Sezon: puan durumu bölümünün kullandığı gerçek değer (yeniden hesaplanmaz).
                SeasonYear = detail.Standing?.SeasonYear > 0 ? detail.Standing.SeasonYear : (int?)null,
                Status = detail.Status,
                KickoffUtc = detail.MatchDate.ToString("u"),
                WorldHeadline = worldHeadline,

                Importance = new MatchIntelligenceContext.ImportanceBlock
                {
                    Level = string.IsNullOrWhiteSpace(detail.Sapma.SapmaBolgesi) ? "Denge" : detail.Sapma.SapmaBolgesi,
                    WatchersCount = detail.WatchersCount,
                    Note = Trim(detail.Sapma.SapmaMetni, 120),
                    // Güç/Oynanma skoru DTO'da zaten hesaplı; buraya BİREBİR taşınır (formüle dokunulmaz).
                    GucSkoru = detail.Sapma.GucSkoru,
                    OynanmaSkoru = detail.Sapma.OynanmaSkoru,
                    // Sportif önem KULLANICI SAYISINDAN AYRI: yalnız müsabaka + gerçek tur verisi.
                    StageLabel = StageLabel(detail.Round),
                    SportingLevel = SportingLevel(detail.League, detail.Round)
                },

                Standings = BuildStandings(detail),

                Availability = BuildAvailability(detail, availability),

                Form = new MatchIntelligenceContext.FormBlock
                {
                    HomeRecent = RecentForm(detail.HomeTeamLastMatches),
                    AwayRecent = RecentForm(detail.AwayTeamLastMatches),
                    HomeFormScore = detail.Comparison.Home.FormScore,
                    AwayFormScore = detail.Comparison.Away.FormScore,

                    // Kanıt kalitesi, G/B/M dizisini üreten AYNI listeden çıkarılır: dizi
                    // yalnız oynanmış ve kapsam içi maçlardan kurulur, dolayısıyla sayı,
                    // tarih ve turnuva bilgisi anlatılan maçlarla birebir aynıdır.
                    HomeEvidence = FormEvidencePolicy.FromLastMatches(detail.HomeTeamLastMatches),
                    AwayEvidence = FormEvidencePolicy.FromLastMatches(detail.AwayTeamLastMatches),

                    // MEVCUT SEZON ÖZETİ — backend hesabı, birebir taşınır.
                    HomeSeason = detail.HomeSeasonForm,
                    AwaySeason = detail.AwaySeasonForm
                },

                Stats = new MatchIntelligenceContext.StatsBlock
                {
                    HomeAvgGoalsFor = Math.Round(detail.Comparison.Home.AvgGoalsFor, 2),
                    AwayAvgGoalsFor = Math.Round(detail.Comparison.Away.AvgGoalsFor, 2),
                    HomeGoalScoringRate = detail.Comparison.Home.GoalScoringRate,
                    AwayGoalScoringRate = detail.Comparison.Away.GoalScoringRate,
                    HomeCleanSheetRate = detail.Comparison.Home.CleanSheetRate,
                    AwayCleanSheetRate = detail.Comparison.Away.CleanSheetRate,
                    HomeRank = detail.HomeTeam.Rank,
                    AwayRank = detail.AwayTeam.Rank
                },

                H2H = BuildH2H(detail),

                // Takımların gerçek sezon istatistikleri (backend hesabı; yeni hesap yok).
                TeamStats = BuildTeamStats(detail),

                News = BuildNews(detail.NabizFeed, evidence),

                Social = new MatchIntelligenceContext.SocialBlock
                {
                    CommunityInterest = detail.WatchersCount,
                    Level = InterestLevel(detail.WatchersCount)
                },

                Scenarios = BuildScenarios(detail, rankedScenarios)
            };
        }

        /// <summary>
        /// GEÇMİŞ KARŞILAŞMALAR — yalnız GERÇEKTEN OYNANMIŞ maçlar.
        ///
        /// Ölçüldü (Charlton–Derby, 15.08): H2H kaydında BU MAÇIN KENDİSİ de bir satır
        /// olarak duruyordu (aynı tarih, 0-0) ve toplamlara "3 maç, 2 beraberlik" diye
        /// giriyordu — oysa oynanmış maç 2, beraberlik 1'di. Yani hem anlatının dayandığı
        /// toplam hem de "0-0 berabere kaldılar" cümlesi yanlış bir kayıttan geliyordu.
        ///
        /// Burada yeni bir H2H hesabı YOKTUR: maç gününde ya da sonrasında görünen
        /// (= henüz oynanmamış) satır listeden çıkarılır ve toplamlar aynı kayıtların
        /// üzerinden yalnızca bu satır düşülerek düzeltilir.
        /// </summary>
        private static MatchIntelligenceContext.H2HBlock BuildH2H(MatchDetailDto detail)
        {
            var rows = detail.H2H?.Matches ?? new List<DTOs.Matches.H2HMatchDto>();
            var kickoffDay = detail.MatchDate.ToString("yyyy-MM-dd");

            var played = rows
                .Where(m => string.Compare(DayOf(m.MatchDate), kickoffDay, StringComparison.Ordinal) < 0)
                .OrderByDescending(m => m.MatchDate, StringComparer.Ordinal)
                .ToList();

            // Elenen satır sayısı kadar toplamı düzelt (sonucuna göre doğru sayaçtan düşülür).
            int total = detail.H2H?.TotalMatches ?? 0;
            int homeWins = detail.H2H?.HomeWins ?? 0;
            int awayWins = detail.H2H?.AwayWins ?? 0;
            int draws = detail.H2H?.Draws ?? 0;

            foreach (var m in rows.Except(played))
            {
                total--;
                if (m.HomeScore == m.AwayScore) draws--;
                else
                {
                    var winner = m.HomeScore > m.AwayScore ? m.HomeTeamName : m.AwayTeamName;
                    if (string.Equals(winner, detail.HomeTeam?.Name, StringComparison.OrdinalIgnoreCase)) homeWins--;
                    else awayWins--;
                }
            }

            return new MatchIntelligenceContext.H2HBlock
            {
                Total = Math.Max(0, total),
                HomeWins = Math.Max(0, homeWins),
                AwayWins = Math.Max(0, awayWins),
                Draws = Math.Max(0, draws),

                // Gerçek geçmiş sonuçlar — "ne oldu" sorusunun cevabı. Skor backend
                // kaydından birebir; en yeniden eskiye en çok 3 karşılaşma.
                RecentResults = played
                    .Take(3)
                    .Select(m => new MatchIntelligenceContext.H2HResult
                    {
                        Date = m.MatchDate,
                        H2HHomeTeam = m.HomeTeamName,
                        H2HAwayTeam = m.AwayTeamName,
                        HomeGoals = m.HomeScore,
                        AwayGoals = m.AwayScore,
                        Competition = m.Competition ?? ""
                    })
                    .ToList()
            };
        }

        /// <summary>H2H kaydının gün kısmı ("2026-01-20T18:00:00" → "2026-01-20").</summary>
        private static string DayOf(string? date)
        {
            var s = (date ?? "").Trim();
            return s.Length >= 10 ? s[..10] : s;
        }

        /// <summary>
        /// Takım sezon istatistikleri — DTO'daki gerçek değerler BİREBİR taşınır. Hepsi
        /// sıfırsa (veri yok) satır üretilmez: model boş istatistikten yorum çıkaramaz.
        /// </summary>
        private static List<MatchIntelligenceContext.TeamStatsRow> BuildTeamStats(MatchDetailDto detail)
        {
            var rows = new List<MatchIntelligenceContext.TeamStatsRow>();
            if (detail.Comparison == null) return rows;

            void Add(string name, DTOs.Matches.TeamComparisonDto? s)
            {
                if (s == null) return;
                if (s.AvgGoalsFor <= 0 && s.AvgGoalsAgainst <= 0 &&
                    s.GoalScoringRate <= 0 && s.CleanSheetRate <= 0) return;

                rows.Add(new MatchIntelligenceContext.TeamStatsRow
                {
                    TeamName = name,
                    AvgGoalsFor = s.AvgGoalsFor,
                    AvgGoalsAgainst = s.AvgGoalsAgainst,
                    GoalScoringRate = s.GoalScoringRate,
                    CleanSheetRate = s.CleanSheetRate
                });
            }

            Add(detail.HomeTeam?.Name ?? "", detail.Comparison.Home);
            Add(detail.AwayTeam?.Name ?? "", detail.Comparison.Away);
            return rows;
        }

        // Senaryolar: ranked aday varsa (kanıt etiketli) onu kullan; yoksa DTO'daki
        // probabilities'e düş (geri-uyum). Yüzdeler her iki yolda da deterministik.
        private static List<MatchIntelligenceContext.ScenarioBlock> BuildScenarios(
            MatchDetailDto detail, IReadOnlyList<ScenarioCandidate>? ranked)
        {
            if (ranked is { Count: > 0 })
                return ranked.Select(c => new MatchIntelligenceContext.ScenarioBlock
                {
                    Market = c.Market,
                    Probability = c.Probability,
                    Confidence = c.Confidence,
                    EvidenceTags = c.EvidenceTags
                }).ToList();

            return detail.Probabilities.Select(p => new MatchIntelligenceContext.ScenarioBlock
            {
                Market = p.Market,
                Probability = p.Probability,
                Confidence = p.Confidence
            }).ToList();
        }

        // Kadro eksikleri. DİKKAT: DTO'daki PlayerStatus.TeamId SAĞLAYICI (api-football) id'sidir;
        // detail.HomeTeam.Id ise canonical id'dir (ölçüldü: 2073/405 ↔ 3282/4883). Bu yüzden DTO
        // üzerinden ev/deplasman ayrımı YAPILMAZ — sayılar, kimlik çözümünü zaten yapan
        // MatchAiContextBuilder'ın ürettiği AvailabilityAiSignals'ten aynen alınır.
        // LineupsAnnounced yalnız DTO'da olduğu için oradan okunur.
        private static MatchIntelligenceContext.AvailabilityBlock BuildAvailability(
            MatchDetailDto detail, AvailabilityAiSignals? availability) => new()
        {
            HasData = availability?.HasData ?? false,
            LineupsAnnounced = detail.Lineup?.LineupsAnnounced ?? false,
            HomeOut = availability?.HomeKeyAbsences ?? 0,
            AwayOut = availability?.AwayKeyAbsences ?? 0,
            HomeInjured = availability?.HomeInjured ?? 0,
            HomeSuspended = availability?.HomeSuspended ?? 0,
            HomeDoubtful = availability?.HomeDoubtful ?? 0,
            AwayInjured = availability?.AwayInjured ?? 0,
            AwaySuspended = availability?.AwaySuspended ?? 0,
            AwayDoubtful = availability?.AwayDoubtful ?? 0
        };

        // ── Puan durumu: DTO'daki gerçek tablo satırı BİREBİR taşınır ──────────────
        // Yeni hesap yok; tablo yoksa null döner (uydurma satır üretilmez).
        private static MatchIntelligenceContext.StandingsBlock? BuildStandings(MatchDetailDto detail)
        {
            var s = detail.Standing;
            if (s == null) return null;
            if (s.HomeTeamPeek == null && s.AwayTeamPeek == null) return null;

            // OYNANMAMIŞ SEZONUN TABLOSU ANLAM TAŞIMAZ. Ölçüldü (Charlton–Derby, 1. hafta):
            // tabloda her takım 0 maç, 0 puanla duruyordu ama sıralar (11 ve 4) doluydu;
            // model bunu gerçek bir yarış sanıp "4. sıradaki Derby play-off yarışında",
            // "Charlton'ın düşüşteki konumu" diye yazdı. Oynanmamış satır pack'e girmez →
            // sıra hakkında konuşulacak bir zemin kalmaz. Tablo hesabı DEĞİŞMEDİ.
            static MatchIntelligenceContext.StandingRow? Map(
                Formax.Application.DTOs.Standings.TeamStandingDto? r) => r == null || r.Played <= 0 ? null : new()
            {
                TeamName = r.TeamName,
                Position = r.Position,
                Played = r.Played,
                Won = r.Won,
                Drawn = r.Drawn,
                Lost = r.Lost,
                GoalsFor = r.GoalsFor,
                GoalsAgainst = r.GoalsAgainst,
                Points = r.Points
            };

            return new MatchIntelligenceContext.StandingsBlock
            {
                TeamCount = s.TableSlice?.Count ?? 0,
                Home = Map(s.HomeTeamPeek),
                Away = Map(s.AwayTeamPeek)
            };
        }

        /// <summary>
        /// Round metnindeki GERÇEK hafta numarası ("Regular Season - 1" → 1, "Week 12" → 12).
        /// Yalnız lig aşamasında anlamlıdır; eleme/final turlarında ve sayı yoksa null döner.
        /// Yeni sınıflandırma değildir — mevcut metinden sayıyı okur.
        /// </summary>
        private static int? WeekNumberOf(string? round)
        {
            if (string.IsNullOrWhiteSpace(round)) return null;
            var r = round.ToLowerInvariant();
            if (!(r.Contains("regular season") || r.Contains("week") || r.Contains("hafta")))
                return null;

            var m = System.Text.RegularExpressions.Regex.Match(round, @"(\d{1,2})");
            return m.Success && int.TryParse(m.Groups[1].Value, out var w) && w is > 0 and <= 60
                ? w : null;
        }

        // ── Aşama etiketi: sağlayıcının GERÇEK round metninden ────────────────────
        // Metin yoksa null (LLM aşama uydurmasın). Yeni sınıflandırma motoru değildir;
        // yalnız gelen metnin futbolca karşılığını verir.
        private static string? StageLabel(string? round)
        {
            if (string.IsNullOrWhiteSpace(round)) return null;
            var r = round.ToLowerInvariant();

            if (r.Contains("play-off") || r.Contains("playoff") || r.Contains("play off")) return "Play-off turu";
            if (r.Contains("qualif") || r.Contains("eleme")) return "Eleme turu";
            if (r.Contains("final") && !r.Contains("semi") && !r.Contains("quarter")) return "Final";
            if (r.Contains("semi")) return "Yarı final";
            if (r.Contains("quarter")) return "Çeyrek final";
            if (r.Contains("round of") || r.Contains("1/8") || r.Contains("1/16")) return "Eleme turu";
            if (r.Contains("group")) return "Grup aşaması";
            if (r.Contains("regular season") || r.Contains("league stage") || r.Contains("week")) return "Lig maçı";

            return null;
        }

        // ── Sportif önem: KULLANICI SAYISINDAN BAĞIMSIZ ──────────────────────────
        // Yalnız iki gerçek girdi kullanılır: müsabaka adı ve gerçek tur metni.
        // İkisi de yoksa null döner → "bilinmiyor" olarak kalır, "Düşük" denmez.
        private static string? SportingLevel(string? league, string? round)
        {
            var stage = StageLabel(round);
            var l = (league ?? "").ToLowerInvariant();
            var isEuropean = l.Contains("uefa") || l.Contains("champions league")
                             || l.Contains("europa") || l.Contains("conference");

            if (stage is "Final" or "Yarı final") return "Çok yüksek";
            if (isEuropean && stage is "Play-off turu" or "Eleme turu" or "Çeyrek final") return "Yüksek";
            if (isEuropean && stage != null) return "Yüksek";
            if (isEuropean) return "Yüksek";
            if (stage == "Lig maçı") return "Normal";

            return null;
        }

        // Son N maçın sonucunu G/B/M dizisine indirger (en yeni → en eski varsayımıyla).
        private static string RecentForm(IReadOnlyList<LastMatchDto> matches)
        {
            if (matches == null || matches.Count == 0) return "";
            return string.Join(" ", matches
                .Take(RecentFormCount)
                .Select(m => m.Result switch
                {
                    "W" => "G",
                    "D" => "B",
                    "L" => "M",
                    _ => "-"
                }));
        }

        // ÖNCELİK: v2.1 Evidence Store (signal-typed, taze, kalite-ağırlıklı). Yoksa eski NABIZ.
        // Ham haber yine gönderilmez: hacim + baskın sinyal + en çok 3 başlık teması.
        private static MatchIntelligenceContext.NewsBlock BuildNews(NabizSectionDto feed, EvidenceContext? evidence)
        {
            if (evidence != null && evidence.TotalEvidence > 0)
                return BuildNewsFromEvidence(evidence);

            var items = feed?.Items ?? new List<NabizFeedItemDto>();
            var fresh = items
                .Where(i => (DateTime.UtcNow - i.PublishedAt).TotalHours <= FreshNewsHours)
                .ToList();

            var topType = fresh
                .GroupBy(i => i.Type)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            var themes = fresh
                .OrderByDescending(i => i.PublishedAt)
                .Select(i => Trim(i.Headline, 70))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .Take(MaxNewsThemes)
                .ToList();

            return new MatchIntelligenceContext.NewsBlock
            {
                Volume24h = fresh.Count,
                TopType = string.IsNullOrWhiteSpace(topType) ? null : topType,
                Themes = themes
            };
        }

        // v2.1 Evidence Store → NewsBlock. Reasoning artık ham haberi değil, sinyalleri görür.
        private static MatchIntelligenceContext.NewsBlock BuildNewsFromEvidence(EvidenceContext ev)
        {
            var signals = ev.Signals
                .Where(s => !s.Key.Equals("General", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.Value)
                .Select(s => s.Key)
                .ToList();

            return new MatchIntelligenceContext.NewsBlock
            {
                Volume24h = ev.TotalEvidence,
                TopType = signals.FirstOrDefault()
                          ?? ev.Signals.OrderByDescending(s => s.Value).Select(s => s.Key).FirstOrDefault(),
                Themes = ev.TopHeadlines.Select(h => Trim(h, 70))
                           .Where(s => !string.IsNullOrWhiteSpace(s)).Cast<string>()
                           .Take(MaxNewsThemes).ToList(),
                Signals = signals.Take(5).ToList(),
                FromEvidence = true,

                // Kanıtların tekil kayıtları — kapılardan geçmiş TopEvidence'tan birebir.
                // Güven sırasına göre en çok MaxEvidenceItems tanesi taşınır.
                // SIRA: önce TİPLİ kanıt (Injury/Lineup/Coach/Suspension…), sonra kalite/güven.
                // "General" kayıtlar ölçüldüğünde fikstür sayfası, TV yayın listesi veya ÖNCEKİ
                // maçın özeti çıkıyor → futbol olgusu taşımıyor. Tipli kanıt listenin altında
                // kalmasın diye sıralama tip önceliklidir.
                // SIRA EK ÖLÇÜT: gerçek içeriği (özeti) olan haber öne alınır — anlatının
                // dayanabileceği tek gerçek malzeme odur; başlıktan içerik üretilemez.
                // SIRA EK ÖLÇÜT (2): aynı olayı doğrulayan kaynak sayısı — çok kaynaklı
                // gelişme tek kaynaklı iddiadan önce gelir.
                Items = (ev.TopEvidence ?? new List<Services.News.Intelligence.MatchEvidence>())
                    .OrderByDescending(x => x.Summary.Length > 0)
                    .ThenByDescending(x => IsTypedSignal(x.Type))
                    .ThenByDescending(x => x.SourceCount)
                    .ThenByDescending(x => x.SourceQuality).ThenByDescending(x => x.Confidence)
                    .Take(MaxEvidenceItems)
                    .Select(x => new MatchIntelligenceContext.EvidenceItem
                    {
                        Category = x.Type,
                        SourceQuality = x.SourceQuality,
                        Confidence = x.Confidence,
                        PublishedUtc = x.PublishedUtc.ToString("u"),
                        Headline = x.Headline,
                        Summary = x.Summary,
                        SourceCount = Math.Max(1, x.SourceCount),
                        Relation = x.Relation,
                        Timing = x.Timing,
                        RelatedTeam = x.RelatedTeam,
                        OpponentTeam = x.OpponentTeam,
                        Player = x.Player,
                        Coach = x.Coach,
                        EventType = x.EventType,
                        Importance = x.Importance
                    })
                    .ToList()
            };
        }

        /// <summary>SignalExtractor bir futbol sinyali bulabildi mi? "General" = bulamadı.</summary>
        private static bool IsTypedSignal(string? type) =>
            !string.IsNullOrWhiteSpace(type) &&
            !type.Equals("General", StringComparison.OrdinalIgnoreCase);

        // Takip eden kullanıcı sayısını niteliksel banda indirir (dürüst proxy).
        private static string InterestLevel(int watchers) =>
            watchers >= 5000 ? "Yüksek" : watchers >= 1000 ? "Orta" : "Düşük";

        private static string? Trim(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length <= max ? s : s[..max].TrimEnd() + "…";
        }
    }
}
