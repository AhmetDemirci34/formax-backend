using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// Football Intelligence v1.0 — YALNIZ UnifiedMatchAiContext'ten (GERÇEK GDP sinyalleri) 7 katmanlı
    /// futbol zekâsı üretir: Player + Squad + Coach + Transfer + Fixture (takım) + Competition (maç).
    /// HasData-gated (veri yoksa boş). Olasılık/gol modeline DOKUNMAZ → determinizm/hash korunur.
    /// Yorum/tahmin ÜRETMEZ; yalnız veriyi anlamlı Intelligence satırlarına dönüştürür.
    /// </summary>
    internal sealed class FootballIntelligenceEngine
    {
        // FORMAX haber sitesi değildir: kaynak dili taşıyan implikasyonlar düşürülür (güvenlik ağı).
        private static readonly string[] ForbiddenSource =
        {
            "kulüp açıkladı", "habere göre", "gazete", "basın toplantısı", "açıklama yaptı", "duyurdu",
            "bbc", "sky sports", "resmi sitesinde"
        };

        public FootballIntelligence Build(UnifiedMatchAiContext ctx)
        {
            var home = BuildTeam(ctx.HomeName, ctx.PlayerIntelligence?.Home, ctx.TeamProfile?.Home, ctx.Timeline?.Home);
            var away = BuildTeam(ctx.AwayName, ctx.PlayerIntelligence?.Away, ctx.TeamProfile?.Away, ctx.Timeline?.Away);
            var competition = BuildCompetition(ctx);
            var synthesis = BuildSynthesis(ctx);
            var newsImplications = BuildNewsImplications(ctx)
                .Where(l => !ForbiddenSource.Any(f => l.ToLowerInvariant().Contains(f)))
                .Distinct()
                .ToList();

            // vNext — Deep player NEDEN→SONUÇ anlatısı (PlayerIntelligence TEK okundu; burada yorumlanır).
            var playerNarrative = new List<string>();
            AppendPlayerNarrative(playerNarrative, ctx.PlayerIntelligence?.Home, ctx.HomeName);
            AppendPlayerNarrative(playerNarrative, ctx.PlayerIntelligence?.Away, ctx.AwayName);

            int depth = (home.HasData ? 1 : 0) + (away.HasData ? 1 : 0) + (competition.Count > 0 ? 1 : 0)
                      + (synthesis.Count > 0 ? 1 : 0) + (newsImplications.Count > 0 ? 1 : 0);

            return new FootballIntelligence
            {
                Home = home,
                Away = away,
                Competition = competition,
                Synthesis = synthesis,
                NewsImplications = newsImplications,
                PlayerNarrative = playerNarrative,
                CoverageDepth = depth,
                HasData = home.HasData || away.HasData || competition.Count > 0
                       || synthesis.Count > 0 || newsImplications.Count > 0
            };
        }

        // ── vNext PLAYER NARRATIVE (GÖREV 9): oyuncu = oyun planının parçası. Liste DEĞİL — takım başına
        // EN belirleyici 1 birincil (hücum ekseni/oyun kurucu) + en fazla 1 ikincil (kart riski). Klişe/filler yok.
        private static void AppendPlayerNarrative(List<string> list, TeamPlayerSignals? pi, string name)
        {
            if (pi is not { HasData: true }) return;

            // BİRİNCİL — takımın oyununu en çok belirleyen isim. Öncelik: hücum bağımlılığı > oyun kurucu.
            string primary = null;
            if (!string.IsNullOrWhiteSpace(pi.TopScorerName) && pi.TopScorerGoals > 0
                && pi.OneManDependency && pi.TopScorerGoalSharePct >= 35)
                primary = $"{name}'in hücum üretimi büyük ölçüde {pi.TopScorerName} üzerinden şekilleniyor; rakip bu ismi etkisiz bırakabilirse takımın gol yolları belirgin biçimde daralabilir.";
            else if (!string.IsNullOrWhiteSpace(pi.TopScorerName) && pi.Top2GoalSharePct >= 58)
                primary = $"{name}'in golleri dar bir kadroya, ağırlıkla {pi.TopScorerName} ve yakın çevresine bağlı; savunmalar bu ekseni kapatırsa hücum tıkanabilir.";
            else if (!string.IsNullOrWhiteSpace(pi.MidfieldBrainName) && pi.MidfieldBrainAssists >= 4)
                primary = $"{name}'in oyunu {pi.MidfieldBrainName} ekseninde kuruluyor; bu ismin baskı altına alınması, hücuma çıkan topun kalitesini doğrudan düşürebilir.";
            else if (!string.IsNullOrWhiteSpace(pi.TopScorerName) && pi.TopScorerGoals > 0)
                primary = $"{name} cephesinde en net gol tehdidi {pi.TopScorerName}; maçın kilidini onun bulacağı boşluklar açabilir.";
            if (primary != null) list.Add(primary);

            // İKİNCİL — yalnız gerçekten belirleyici bir DİSİPLİN riski (farklı boyut, tekrar değil).
            if (!string.IsNullOrWhiteSpace(pi.CardRiskName) && pi.CardRiskYellows >= 8)
                list.Add($"{name}, kart sınırındaki {pi.CardRiskName} nedeniyle sertleşen bir maçta erken bir kart riski taşıyor; bu, planı bozabilecek gizli bir kırılganlık.");
        }

        // ── v4 SENTEZ: katmanları BİRLEŞTİREN çok-faktörlü NEDEN→SONUÇ motoru. ───────────────────
        // Player + Timeline + Standings/Stats + Competition sinyallerini ÇAPRAZ değerlendirir. Her kural
        // yalnız gerçek verilerin KESİŞİMİNDE tetiklenir (tek sinyal yeterli değil). Editorial'in yapamadığı
        // oyuncu-düzeyi çıkarımlar buranın işidir (Editorial'de isimli oyuncu yok). Betimleme DEĞİL çıkarım.
        private static List<string> BuildSynthesis(UnifiedMatchAiContext ctx)
        {
            var s = new List<string>();

            foreach (var side in new[] { true, false })
            {
                var name = side ? ctx.HomeName : ctx.AwayName;
                var pi = side ? ctx.PlayerIntelligence?.Home : ctx.PlayerIntelligence?.Away;
                var tl = side ? ctx.Timeline?.Home : ctx.Timeline?.Away;
                var oppTl = side ? ctx.Timeline?.Away : ctx.Timeline?.Home;
                var st = side ? ctx.TeamStats?.Home : ctx.TeamStats?.Away;
                var stand = side ? ctx.Standings?.Home : ctx.Standings?.Away;
                var goodForm = FormIsGood(st?.Form) || FormIsGood(stand?.Form);

                bool rotationPressure = tl is { HasData: true } && (tl.RotationRisk || tl.UpcomingPriorityMatch || tl.CongestedSchedule);

                // ⓐ AMİRAL ÇAPRAZ: (iyi form) + (savunmada eksik) + (yaklaşan yoğun/öncelikli program) →
                //     savunma düzeni değişikliği ihtimali. Sprint'in amiral örneği.
                if (pi is { HasData: true } && pi.InjuredDefCount > 0 && rotationPressure)
                {
                    var formClause = goodForm ? "formu iyi gitse de " : "";
                    var rotClause = tl!.UpcomingPriorityMatch || (tl.RotationRisk && tl.NextIsDifferentCompetition)
                        ? "yaklaşan farklı kulvardaki maç"
                        : "yoğun fikstür";
                    s.Add($"{name} {formClause}savunmada {pi.InjuredDefCount} eksikle ve {rotClause} baskısıyla sahaya çıkıyor; arka bölge dizilişinde ve savunma düzeninde değişiklik ihtimali artıyor.");
                }

                // NOT: tek-adam hücum bağımlılığı → PlayerStory sahibi; orta-saha eksiği → SquadStory sahibi.
                //      Tek-sahiplik gereği (GÖREV 4) burada TEKRAR üretilmez. Synthesis yalnız ÇOK-faktörlü
                //      çapraz çıkarımları (form+eksik+fikstür / dinlenme asimetrisi / yarış-eleme) taşır.

                // ⓓ Dinlenme asimetrisi: bu takım kısa dinlenmiş, rakip iyi dinlenmiş → tempo dezavantajı.
                if (tl is { HasData: true, ShortRest: true } && oppTl is { HasData: true, LongRestAdvantage: true })
                    s.Add($"{name} bu maça yalnızca {tl.RestDaysBefore} gün dinlenerek gelirken rakip daha taze; ikinci yarıda tempo ve fiziksel dayanıklılık farkı belirleyici olabilir.");

                // ⓔ Yoğun fikstür + rotasyon riski (dinlenme asimetrisi yoksa) → rotasyon/yorgunluk.
                else if (tl is { HasData: true, CongestedSchedule: true, RotationRisk: true })
                    s.Add($"{name} yoğun bir fikstür döneminde ve yakında farklı kulvarda maçı var; bazı isimlerin dinlendirilmesi ve fiziksel yorgunluk bu maça yansıyabilir.");
            }

            // ⓕ Zirve yarışı × sezon sonu → hata maliyeti tavanda.
            if (ctx.StandingsContext is { HasData: true, TitleRace: true })
            {
                if (ctx.Season is { HasData: true, SeasonPhase: "End" })
                    s.Add("Sezon sonunda doğrudan şampiyonluk yarışını etkileyen bir maç; puan kaybının maliyeti tavanda, bu da hem baskıyı hem hata payının önemini artırıyor.");
                else
                    s.Add("Bu maç şampiyonluk yarışını doğrudan etkiliyor; puan kaybının maliyeti yüksek, iki taraf da temkin ile cesaret arasında denge kurmak zorunda.");
            }

            // ⓖ Eleme / rövanş-aggregate → tek sonucun turnuva kaderini belirlemesi, temkinli yaklaşım.
            if (ctx.Competition is { HasData: true, IsElimination: true })
                s.Add("Eleme maçı: sonuç doğrudan turnuvadaki geleceği belirliyor; bu, özellikle erken dakikalarda temkinli bir yaklaşımı beraberinde getirebilir.");
            else if (ctx.Tournament is { HasData: true, AggregateMatters: true })
                s.Add("Toplam skorun (aggregate) belirleyici olduğu bir eşleşme; takımlar riski ilk maçın sonucuna göre ayarlayacağından oyun temkinli başlayabilir.");

            return s;
        }

        /// <summary>News Intelligence — haberin HAM metni değil, futbola ETKİSİ (sebep→etki). Gerçek News/
        /// Player/Timeline/Social/Venue sinyallerinden. Kaynak dili üretmez (üst katmanda ayrıca süzülür).</summary>
        private static List<string> BuildNewsImplications(UnifiedMatchAiContext ctx)
        {
            var list = new List<string>();
            var news = ctx.News;
            var social = ctx.Social;

            // NOT (GÖREV 4 tek-sahiplik): sakatlık-bölge → SquadStory sahibi; rotasyon/yorgunluk → TimelineStory
            //   sahibi. News burada YALNIZ haber-kaynaklı gelişmelerin (koç/transfer/kulüp/ceza/breaking/turnuva
            //   gündemi) futbol ETKİSİNİ üretir — sakatlık/rotasyon TEKRAR edilmez.

            if (news is { HasData: true })
            {
                // 3) Teknik direktör gündemi → ilk 11 ve oyun planı tercihleri.
                if (news.CoachNews > 0 || (social is { HasData: true } && social.OfficialCoachStatement > 0))
                    list.Add("Teknik direktör cephesindeki hareketlilik, ilk 11 tercihlerini ve oyun planını değiştirebilir.");

                // 4) Transfer → oyun yapısı, rol dağılımı ve uyum.
                if (news.TransferNews > 0)
                    list.Add("Transfer hareketliliği takımın oyun yapısına, rol dağılımına ve saha içi uyuma yansıyabilir.");

                // 5) Kulüp/başkan gündemi → dış baskı ve psikoloji (kaynak dili YOK).
                if (news.ClubNews > 0 || (social is { HasData: true } && social.OfficialClubStatement > 0))
                    list.Add("Kulüp cephesindeki gündem, takım üzerinde beklenti ve baskı oluşturarak sahadaki psikolojiye yansıyabilir.");

                // 6) Müsabaka gündemi × eleme/aggregate → maçın bahsi ve tempoyu belirleme.
                if (news.CompetitionNews > 0 &&
                    (ctx.Competition is { HasData: true, IsElimination: true } || ctx.Tournament is { HasData: true, AggregateMatters: true }))
                    list.Add("Turnuva bağlamındaki gündem, maçın bahsini yükselterek oyunun temposunu ve risk yönetimini şekillendirebilir.");

                // 7) Ceza kaynaklı eksik → kadro uygunluğu.
                if (news.SuspensionNews > 0)
                    list.Add("Ceza kaynaklı eksikler kadro uygunluğunu daraltarak rotasyonu ve dizilişi etkileyebilir.");

                // 8) Son dakika gelişmesi → hazırlık ve konsantrasyon belirsizliği.
                if (news.HasBreakingNews || (social is { HasData: true } && social.BreakingOfficialNews))
                    list.Add("Son saatlerdeki gelişmeler maç öncesi hazırlığı ve konsantrasyonu etkileyebilir; kadroda son dakika sürprizi ihtimali var.");
            }

            return list;
        }

        /// <summary>Form dizisinin (WWDLW / GGBMG) İYİ olup olmadığı — çapraz sentez için.</summary>
        private static bool FormIsGood(string? form)
        {
            if (string.IsNullOrWhiteSpace(form)) return false;
            var chars = form.ToUpperInvariant().Where(ch => "WDLGBM".IndexOf(ch) >= 0).ToArray();
            if (chars.Length < 3) return false;
            int w = chars.Count(ch => ch == 'W' || ch == 'G');
            int l = chars.Count(ch => ch == 'L' || ch == 'M');
            return w >= l + 2 && w >= 3;
        }

        // "a", "a ve b", "a, b ve c" — Türkçe doğal liste.
        private static string JoinTr(IReadOnlyList<string> xs)
        {
            if (xs.Count == 0) return "";
            if (xs.Count == 1) return xs[0];
            return string.Join(", ", xs.Take(xs.Count - 1)) + " ve " + xs[^1];
        }

        private static string LastToken(string name)
        {
            var parts = (name ?? "").Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? name ?? "" : parts[^1];
        }

        private static TeamFootballIntelligence BuildTeam(
            string name, TeamPlayerSignals? pi, TeamProfileTeamSignals? profile, TeamTimelineSignals? tl)
        {
            var player = new List<string>();
            var squad = new List<string>();
            var coach = new List<string>();
            var transfer = new List<string>();
            var fixture = new List<string>();

            // ── 1. Player Intelligence (GERÇEK isimli oyuncu verisi) ──
            if (pi is { HasData: true })
            {
                if (!string.IsNullOrWhiteSpace(pi.TopScorerName) && pi.TopScorerGoals > 0)
                {
                    var s = $"En skorer: {pi.TopScorerName} — {pi.TopScorerGoals} gol";
                    if (pi.TopScorerAssists > 0) s += $", {pi.TopScorerAssists} asist";
                    if (pi.TopScorerRating > 0) s += $", rating {pi.TopScorerRating.ToString("0.0", CultureInfo.InvariantCulture)}";
                    player.Add(s);
                }
                if (!string.IsNullOrWhiteSpace(pi.TopAssistName) && pi.TopAssistCount > 0
                    && !string.Equals(pi.TopAssistName, pi.TopScorerName))
                    player.Add($"Asist lideri: {pi.TopAssistName} — {pi.TopAssistCount} asist");
                if (!string.IsNullOrWhiteSpace(pi.KeyPlayerName) && pi.KeyPlayerRating > 0
                    && !string.Equals(pi.KeyPlayerName, pi.TopScorerName))
                    player.Add($"En yüksek ratingli: {pi.KeyPlayerName} (rating {pi.KeyPlayerRating.ToString("0.0", CultureInfo.InvariantCulture)})");
                if (!string.IsNullOrWhiteSpace(pi.MinutesLeaderName) && pi.MinutesLeaderMinutes > 0)
                    player.Add($"En çok süre: {pi.MinutesLeaderName} ({pi.MinutesLeaderMinutes} dk)");
                if (!string.IsNullOrWhiteSpace(pi.ShotsLeaderName) && pi.ShotsLeaderCount > 0)
                    player.Add($"Şut lideri: {pi.ShotsLeaderName} ({pi.ShotsLeaderCount} şut)");
                if (!string.IsNullOrWhiteSpace(pi.KeyPassLeaderName) && pi.KeyPassLeaderCount > 0
                    && !string.Equals(pi.KeyPassLeaderName, pi.TopAssistName))
                    player.Add($"Kilit pas: {pi.KeyPassLeaderName} ({pi.KeyPassLeaderCount})");
                if (pi.OneManDependency && !string.IsNullOrWhiteSpace(pi.TopScorerName))
                    player.Add($"Hücum bağımlılığı: gollerin %{pi.TopScorerGoalSharePct}'i {pi.TopScorerName}'den, en skor eden ikilinin payı %{pi.Top2GoalSharePct}");
                if (!string.IsNullOrWhiteSpace(pi.CardRiskName) && pi.CardRiskYellows > 0)
                    player.Add($"Kart riski: {pi.CardRiskName} ({pi.CardRiskYellows} sarı)");
                if (pi.InjuredCount > 0 && pi.InjuredNames.Count > 0)
                    player.Add($"Sakat/eksik ({pi.InjuredCount}): {string.Join(", ", pi.InjuredNames)}");

                // ── 2. Squad Intelligence ──
                if (pi.SquadPlayerCount > 0)
                    squad.Add($"Kadro: {pi.SquadPlayerCount} oyuncu (K{pi.GkCount}/D{pi.DefCount}/O{pi.MidCount}/F{pi.AttCount})");
                if (!string.IsNullOrWhiteSpace(pi.DefenseLeaderName) && pi.DefenseLeaderRating > 0)
                    squad.Add($"Savunma lideri: {pi.DefenseLeaderName} (rating {pi.DefenseLeaderRating.ToString("0.0", CultureInfo.InvariantCulture)})");
                if (!string.IsNullOrWhiteSpace(pi.MidfieldBrainName))
                {
                    var mb = $"Orta saha beyni: {pi.MidfieldBrainName}";
                    if (pi.MidfieldBrainAssists > 0) mb += $" ({pi.MidfieldBrainAssists} asist)";
                    squad.Add(mb);
                }
                var weak = new List<string>();
                if (pi.InjuredDefCount > 0) weak.Add($"savunmada {pi.InjuredDefCount}");
                if (pi.InjuredMidCount > 0) weak.Add($"orta sahada {pi.InjuredMidCount}");
                if (pi.InjuredAttCount > 0) weak.Add($"hücumda {pi.InjuredAttCount}");
                if (weak.Count > 0) squad.Add($"Eksik bölge: {string.Join(", ", weak)}");
            }

            // ── 3. Coach Intelligence ──
            if (profile is { HasCoach: true } && !string.IsNullOrWhiteSpace(profile.CoachName))
            {
                var c = $"Teknik direktör: {profile.CoachName}";
                if (profile.CoachAge > 0) c += $" ({profile.CoachAge})";
                coach.Add(c);
            }

            // ── 4. Transfer Intelligence ──
            if (profile is { HasTransfers: true } && (profile.RecentTransfersIn > 0 || profile.RecentTransfersOut > 0))
                transfer.Add($"Son 12 ay: {profile.RecentTransfersIn} gelen, {profile.RecentTransfersOut} giden");

            // ── 5. Fixture Intelligence (Timeline) ──
            if (tl is { HasData: true })
            {
                if (tl.HasPrevMatch)
                {
                    var f = $"Son maçtan {tl.RestDaysBefore} gün dinlenme";
                    if (tl.ShortRest) f += " (kısa)";
                    else if (tl.LongRestAdvantage) f += " (avantaj)";
                    fixture.Add(f);
                }
                if (tl.CongestedSchedule) fixture.Add("Yoğun fikstür");
                if (tl.HasNextMatch && tl.RotationRisk)
                {
                    var f = $"Sonraki maç {tl.DaysToNextMatch} gün sonra";
                    if (tl.NextIsDifferentCompetition) f += " (farklı kulvar → rotasyon ihtimali)";
                    fixture.Add(f);
                }
            }

            var hasData = player.Count > 0 || squad.Count > 0 || coach.Count > 0
                       || transfer.Count > 0 || fixture.Count > 0;

            return new TeamFootballIntelligence
            {
                TeamName = name,
                HasData = hasData,
                Player = player,
                Squad = squad,
                Coach = coach,
                Transfer = transfer,
                Fixture = fixture
            };
        }

        // ── 6. Competition Intelligence (maç düzeyi) ──
        private static List<string> BuildCompetition(UnifiedMatchAiContext ctx)
        {
            var lines = new List<string>();
            var comp = ctx.Competition;
            var tour = ctx.Tournament;
            var stand = ctx.StandingsContext;
            var season = ctx.Season;

            if (comp is { HasData: true })
            {
                var kind = comp.CompetitionType switch
                {
                    "League" => "Lig maçı",
                    "Cup" => "Kupa maçı",
                    "Knockout" => "Kupa eleme maçı",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(kind))
                {
                    if (comp.IsElimination) kind += " — tek maç eleme";
                    lines.Add(kind);
                }
                if (!string.IsNullOrWhiteSpace(comp.Stage) && comp.Stage != "RegularSeason" && comp.Stage != "Unknown")
                    lines.Add($"Aşama: {comp.Stage}");
            }

            if (tour is { HasData: true })
            {
                if (tour.IsSecondLeg && tour.AggregateMatters) lines.Add("Rövanş — toplam skor belirleyici");
                else if (tour.IsFirstLeg) lines.Add("İki maçlı eşleşmenin ilk maçı");
                if (tour.PenaltiesPossible) lines.Add("Beraberlikte penaltı ihtimali");
            }

            if (stand is { HasData: true, TitleRace: true } && !string.IsNullOrWhiteSpace(stand.LeaderName))
                lines.Add($"Şampiyonluk yarışı (lider {stand.LeaderName}, {stand.LeaderPoints} puan)");

            if (season is { HasData: true } && !string.IsNullOrWhiteSpace(season.SeasonPhase))
            {
                var phase = season.SeasonPhase switch
                {
                    "Start" => "sezon başı",
                    "Middle" => "sezon ortası",
                    "End" => "sezon sonu (kritik viraj)",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(phase)) lines.Add($"Dönem: {phase}");
            }

            return lines;
        }
    }
}
