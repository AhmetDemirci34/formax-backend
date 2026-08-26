using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// FORMAX AI Brain vNext — TEK ANLATI BEYNİ (MatchReadingEngine).
    ///
    /// Editorial + Story + Football'ın ayrı çalıştığı üç yorum katmanını birleştirir: her ham blok TEK
    /// kez okunur, tek <see cref="MatchReading"/> üretilir. Oyuncu/haber/sentez zaten hesaplanmış
    /// <see cref="FootballIntelligence"/>'tan ÖDÜNÇ alınır (tekrar okuma YOK). Takım/kadro/taktik/psikoloji/
    /// verdict burada okunur. Canlı okuma YALNIZ in-play (LiveState) gerçek verisinden.
    ///
    /// YALNIZ context + hesaplanmış bileşenleri okur; olasılık/gol modeline DOKUNMAZ (hash sabit).
    /// HasData-gated: veri yoksa BOŞ (uydurma YOK). Maç öncesi taktik YALNIZ DNA + güç-index + gerçek
    /// gol/form'dan (xG/possession/ilk-11 maç öncesi context'te YOK → iddia edilmez).
    /// </summary>
    internal sealed class MatchReadingEngine
    {
        public MatchReading Read(
            UnifiedMatchAiContext ctx, FootballIntelligence football, MatchDna dna, ContextIntelligence ci,
            MatchImportance importance, IReadOnlyList<InteractionEffect> interactions,
            ContradictionReport contradiction, SurpriseAlert surprise, SignalField field, PoissonGoalModel model)
        {
            football ??= new FootballIntelligence();

            // Her ham blok TEK kez okunur; oyuncu/haber/sentez zaten tek-okuyan Football'dan gelir.
            var home        = BuildTeam(ctx, field, isHome: true);
            var away        = BuildTeam(ctx, field, isHome: false);
            var coach       = BuildCoach(ctx);
            var transfers   = BuildTransfers(ctx);
            var verdict     = BuildVerdict(ctx, dna, ci, importance, contradiction, surprise, field, home, away);
            var live        = BuildLive(ctx);
            var synthesis   = football.Synthesis?.ToList() ?? new List<string>();

            // ── 11 ADLI STORY BLOĞU (derin, neden→sonuç) ──
            var competitionStory = BuildCompetitionStory(ctx, ci, importance);          // GÖREV 7 (derin)
            var teamStory        = BuildTeamStory(ctx, home, away);                       // + H2H geçmişi
            var playerStory      = football.PlayerNarrative is { Count: > 0 } pn          // GÖREV 4 (causal)
                                     ? pn.ToList() : BuildKeyPlayers(ctx, football);
            var squadStory       = BuildSquadStory(ctx);                                  // GÖREV 3 (derin)
            var tacticalStory    = BuildTactical(ctx, dna);
            var timelineStory    = BuildTimelineStory(ctx, importance);                   // GÖREV 6 (derin)
            var newsStory        = football.NewsImplications?.ToList() ?? new List<string>();
            var psychologyStory  = BuildPsychology(ctx, ci, importance);
            var hiddenStory      = BuildHiddenStory(synthesis, interactions, contradiction, surprise);

            // ── Hikâye eksenleri (tek-sahiplik: MatchStory = manşet; synthesis YALNIZ Hidden'da) ──
            var main = verdict.HasData ? verdict.CriticalTopic : null;  // "bu maçın hikâyesi nedir?" tek manşet
            var sub = KeyPlayerThread(ctx);                            // yalnız eski projeksiyon için
            var turning   = verdict.HasData ? verdict.WhatCouldChange : "";
            var advantage = verdict.HasData ? verdict.BiggestAdvantage : "";
            var risk      = verdict.HasData ? verdict.BiggestRisk : "";
            var formaxView = verdict.HasData ? verdict.WhyWatch : "";
            var surprisePotential = surprise is { HasAlert: true } && !string.IsNullOrWhiteSpace(surprise.Summary)
                ? surprise.Summary : "";

            // MatchStory = TEK manşet (CriticalTopic). Synthesis Hidden'ın, oyuncu PlayerStory'nin,
            // değerlendirme FormaxOpinion'ın — burada tekrar edilmez.
            var matchStoryLines = new List<string>();
            if (!string.IsNullOrWhiteSpace(main)) matchStoryLines.Add(main);

            var formaxOpinion = BuildFormaxOpinion(verdict, formaxView, advantage, risk, turning, surprisePotential);

            var depth = new[]
            {
                competitionStory.Count > 0, home.HasData, away.HasData, squadStory.Count > 0,
                timelineStory.Count > 0, coach.Count > 0, playerStory.Count > 0, newsStory.Count > 0,
                transfers.Count > 0, tacticalStory.Count > 0, psychologyStory.Count > 0,
                hiddenStory.Count > 0, live.Count > 0, verdict.HasData
            }.Count(b => b);

            return new MatchReading
            {
                // ── Eski alanlar (Editorial/Story projeksiyonu; deepened kaynaklardan) ──
                Context = competitionStory,
                HomeTeam = home,
                AwayTeam = away,
                Squad = squadStory,
                Fixture = timelineStory,
                Coach = coach,
                KeyPlayers = playerStory,
                News = newsStory,
                Transfers = transfers,
                Tactical = tacticalStory,
                Psychology = psychologyStory,
                Hidden = hiddenStory,
                Synthesis = synthesis,
                MainStory = main ?? "",
                SubStory = sub ?? "",
                TurningPoint = turning ?? "",
                BiggestAdvantage = advantage ?? "",
                BiggestRisk = risk ?? "",
                SurprisePotential = surprisePotential,
                FormaxView = formaxView ?? "",
                Verdict = verdict,
                Live = live,
                // ── 11 adlı Story bloğu (LLM okur) ──
                MatchStoryLines = matchStoryLines,
                TeamStory = teamStory,
                PlayerStory = playerStory,
                SquadStory = squadStory,
                TacticalStory = tacticalStory,
                TimelineStory = timelineStory,
                CompetitionStory = competitionStory,
                NewsStory = newsStory,
                PsychologyStory = psychologyStory,
                HiddenStory = hiddenStory,
                FormaxOpinion = formaxOpinion,
                CoverageDepth = depth,
                HasData = depth >= 3
            };
        }

        // ── Projeksiyonlar: Editorial + Story tek okumadan türer (ayrı hesaplama YOK) ──

        public static EditorialIntelligence ToEditorial(MatchReading r) => new()
        {
            MatchContext = r.Context,
            HomeTeam = r.HomeTeam,
            AwayTeam = r.AwayTeam,
            Squad = r.Squad,
            Fixture = r.Fixture,
            Coach = r.Coach,
            News = r.News,
            Transfers = r.Transfers,
            Psychology = r.Psychology,
            Tactical = r.Tactical,
            KeyPlayers = r.KeyPlayers,
            Hidden = r.Hidden,
            Verdict = r.Verdict,
            CoverageDepth = r.CoverageDepth,
            HasData = r.HasData
        };

        public static MatchStory ToStory(MatchReading r) => new()
        {
            HasData = new[] { r.MainStory, r.SubStory, r.TurningPoint, r.BiggestAdvantage, r.BiggestRisk, r.SurprisePotential, r.FormaxView }
                          .Any(s => !string.IsNullOrWhiteSpace(s)) || r.News.Count > 0,
            MainStory = r.MainStory,
            SubStory = r.SubStory,
            TurningPoint = r.TurningPoint,
            BiggestAdvantage = r.BiggestAdvantage,
            BiggestRisk = r.BiggestRisk,
            SurprisePotential = r.SurprisePotential,
            FormaxView = r.FormaxView,
            NewsImplications = r.News
        };

        // ═══════════════════════════ CANLI OKUMA (in-play, gerçek) ═══════════════════════════
        // YALNIZ LiveState.HasData. Tahmini canlı veri ASLA — her satır gerçek alandan.
        private static List<string> BuildLive(UnifiedMatchAiContext ctx)
        {
            var list = new List<string>();
            var s = ctx.LiveState;
            if (s == null || !s.HasData || !s.IsLive) return list;

            var min = s.Minute > 0 ? $"{s.Minute}'" : (string.IsNullOrWhiteSpace(s.Phase) ? "canlı" : s.Phase);
            list.Add($"Maç canlı ({min}): {ctx.HomeName} {s.HomeScore}-{s.AwayScore} {ctx.AwayName}.");

            if (s.RedHome > 0 || s.RedAway > 0)
                list.Add($"Sayısal denge bozuldu: kırmızı kart ({ctx.HomeName} {s.RedHome} / {ctx.AwayName} {s.RedAway}) oyunun gidişatını doğrudan etkiliyor.");

            if (!s.HasRichStats) return list; // zengin istatistik yoksa skor/dakika yeter (dürüst)

            if (s.PossessionHome > 0 || s.PossessionAway > 0)
            {
                var dom = s.PossessionHome > s.PossessionAway ? ctx.HomeName : ctx.AwayName;
                var pct = Math.Max(s.PossessionHome, s.PossessionAway);
                if (pct >= 58) list.Add($"{dom} topa sahip olmada baskın (%{pct}); oyunu kontrol etme eğiliminde.");
                else list.Add($"Topa sahip olma dengeli (%{s.PossessionHome}-%{s.PossessionAway}); kontrol el değiştiriyor.");
            }

            if (s.ShotsHome > 0 || s.ShotsAway > 0)
                list.Add($"İsabetli/toplam şut — {ctx.HomeName} {s.ShotsOnTargetHome}/{s.ShotsHome}, {ctx.AwayName} {s.ShotsOnTargetAway}/{s.ShotsAway}: üretilen pozisyonlar oyunun yönünü gösteriyor.");

            if (s.XgHome > 0 || s.XgAway > 0)
            {
                var xgLead = s.XgHome > s.XgAway ? ctx.HomeName : ctx.AwayName;
                var diff = Math.Abs(s.XgHome - s.XgAway);
                if (diff >= 0.5)
                    list.Add($"Beklenen gol (xG) {xgLead} lehine ({s.XgHome:0.0}-{s.XgAway:0.0}); pozisyon kalitesi skora göre {(SkorUyum(s, xgLead) ? "tutarlı" : "skorun ötesinde")}.");
                else
                    list.Add($"Beklenen gol (xG) başa baş ({s.XgHome:0.0}-{s.XgAway:0.0}); pozisyon kalitesi iki tarafta da benzer.");
            }

            if (s.DangerousAttacksHome > 0 || s.DangerousAttacksAway > 0)
            {
                var momLead = s.DangerousAttacksHome > s.DangerousAttacksAway ? ctx.HomeName : ctx.AwayName;
                if (Math.Abs(s.DangerousAttacksHome - s.DangerousAttacksAway) >= 5)
                    list.Add($"Momentum {momLead} tarafında: tehlikeli atak üretimi ağırlıkla onlarda.");
            }
            return list;
        }

        private static bool SkorUyum(LiveStateSignals s, string xgLead)
            => (xgLead == null) || (s.HomeScore >= s.AwayScore) == (s.XgHome >= s.XgAway);

        // ═══════════════════════════ COMPETITION STORY (GÖREV 7 — derin) ═══════════════════════════
        // Yalnız "kupa maçı" demez; NEDEN önemli / kaybedilirse-kazanılırsa / lig-Avrupa-küme etkisi /
        // rövanş baskısı / derbi psikolojisi. Yalnız gerçek Competition/Tournament/StandingsContext/Season.
        private static IReadOnlyList<string> BuildCompetitionStory(UnifiedMatchAiContext ctx, ContextIntelligence ci, MatchImportance importance)
        {
            var list = new List<string>();
            var c = ctx.Competition;
            var tour = ctx.Tournament;
            var sc = ctx.StandingsContext;

            // Derbi psikolojisi.
            if (ci?.Derby != null && ci.Derby.HasData)
                list.Add("Bir derbi atmosferi var; bu tür maçlarda tablo ve form geri planda kalabilir, rekabetin getirdiği gerilim sonucu tek başına belirleyebilir.");

            if (c != null && c.HasData)
            {
                var type = c.CompetitionType switch { "Cup" => "kupa", "Knockout" => "eleme", _ => "lig" };
                var stage = string.IsNullOrWhiteSpace(c.Stage) || c.Stage == "RegularSeason" ? "" : $" ({TrStage(c.Stage)})";

                if (c.IsElimination)
                    list.Add($"Bu bir {type} eleme maçı{stage}: kaybeden turnuvaya veda ediyor. Tek sonucun geri dönüşü olmadığı için iki taraf da hata yapmamaya odaklanacak, bu da oyunu temkinli kılabilir.");
                else if (type == "kupa")
                    list.Add($"Kupa maçı{stage}: lig rutininden farklı bir motivasyon; kazanan bir üst tura ve kupada iddiaya yaklaşırken, kaybeden sezonun bir hedefini erken kaybedebilir.");
                else if (c.Importance == "Critical")
                    list.Add($"Lig maçı{stage} ama bağlam kritik; iki taraf da bu karşılaşmaya sezon hedefleri açısından yüklü anlamlar yüklüyor.");
                else if (!string.IsNullOrEmpty(stage))
                    list.Add($"Lig maçı{stage}: bu aşama, alınacak sonucun sezon içindeki ağırlığını artırıyor.");
                // NOT (GÖREV 6): stake'i olmayan sıradan bir lig maçında "bir lig maçı" gibi BOŞ bilgi
                //   üretilmez; maçın önemini lig-yarışı/sezon hikâyesi (aşağıda) taşır, o da yoksa blok boş kalır.
            }

            // Rövanş / çift maç baskısı.
            if (tour != null && tour.HasData)
            {
                if (tour.IsSecondLeg) list.Add("Rövanş ayağı: ilk maçın sonucu sahadaki risk iştahını doğrudan belirleyecek; önde gelen taraf temkinli, geride kalan taraf daha açık oynamak zorunda kalabilir.");
                else if (tour.IsFirstLeg) list.Add("Çift maçlı eşleşmenin ilk ayağı; takımlar ikinci maçı da düşüneceğinden bu karşılaşmada dengeli/temkinli bir yaklaşım öne çıkabilir.");
                else if (tour.AggregateMatters) list.Add("Toplam skorun belirleyici olduğu bir eşleşme; alınan/yenilen her gol iki maçlık denklemi doğrudan etkiliyor.");
            }

            // Lig yarışının anlamı — zirve / Avrupa / küme (StandingsContext + pozisyonlar).
            var stake = BuildLeagueStakeStory(ctx);
            if (stake != null) list.Add(stake);

            // Sezon virajı.
            var ss = SeasonStory(ctx);
            if (ss != null) list.Add(ss);

            return list.Distinct().ToList();
        }

        // Lig yarışının maça ETKİSİ (zirve/Avrupa/küme). Gerçek StandingsContext + Standings pozisyonlarından.
        private static string BuildLeagueStakeStory(UnifiedMatchAiContext ctx)
        {
            var sc = ctx.StandingsContext;
            var seasonEnd = ctx.Season is { HasData: true, SeasonPhase: "End" };
            if (sc != null && sc.HasData)
            {
                if (sc.TitleRace)
                    return seasonEnd
                        ? $"Zirve yarışında son viraj: {sc.LeaderName} önde ve bu haftalarda kaybedilen her puan şampiyonluğu doğrudan riske atıyor; kazanmak yarışı sürdürmek, kaybetmek geri dönüşü zor bir açık vermek demek."
                        : $"Şampiyonluk yarışı kızışıyor; {sc.LeaderName} ile aradaki farkta bu maçın üç puanı zirve mücadelesinin yönünü değiştirebilir.";
                if (sc.RelegationDataAvailable)
                {
                    var hpp = ctx.Standings?.Home; var app = ctx.Standings?.Away;
                    var low = new[] { hpp, app }.Where(x => x is { HasData: true }).Select(x => x!.Position).DefaultIfEmpty(0).Max();
                    if (low >= 16)
                    {
                        var struggler = (hpp?.Position ?? 0) >= (app?.Position ?? 0) ? ctx.HomeName : ctx.AwayName;
                        return $"Küme hattının nefes kesen bölgesinde bir maç; {struggler} için kazanmak kritik bir soluk, kaybetmek düşme baskısını doğrudan üzerine çekmek demek.";
                    }
                }
                if (sc.HomeGapToLeader >= 0 && (sc.HomeGapToLeader <= 6 || sc.AwayGapToLeader <= 6))
                    return $"Üst sıralar birbirine kenetlenmiş; {ctx.HomeName} ile {ctx.AwayName} arasındaki bu maçın puanları doğrudan Avrupa kotasına dokunuyor, kaybeden rakiplerine kapı aralayabilir.";
            }
            // İki takım da üst sıralarda → gerçek pozisyonlarla KİŞİSELLEŞTİR (klişe değil, bilgi taşısın).
            var h = ctx.Standings?.Home; var a = ctx.Standings?.Away;
            if (h is { HasData: true } && a is { HasData: true } && Math.Min(h.Position, a.Position) <= 5)
                return $"{ctx.HomeName} ({h.Position}.) ile {ctx.AwayName} ({a.Position}.) üst sıralarda karşılaşıyor; alınacak sonuç Avrupa/zirve kotasının el değiştirmesinde belirleyici olabilir.";
            if (h is { HasData: true } && a is { HasData: true })
            {
                var gap = Math.Abs(h.Position - a.Position);
                if (gap >= 8)
                {
                    var upper = h.Position < a.Position ? ctx.HomeName : ctx.AwayName;
                    var lower = h.Position < a.Position ? ctx.AwayName : ctx.HomeName;
                    return $"Tablo iki takımı farklı hedeflere ayırıyor: {upper} yukarıyı kovalarken {lower} kendi mücadelesini veriyor; bu, sahadaki motivasyon dengesini de belirleyebilir.";
                }
            }
            return null;
        }

        // ═══════════════════════════ TEAM (GÖREV 3 — neden-sonuç, istatistik listesi DEĞİL) ═══════════════════════════
        // "Takımlar bugün nasıl bir durumda?" → form/konumun MAÇA ETKİSİ (özgüven/baskı/eğilim). Ham sayı yok;
        // takım başına EN belirleyici 2 çıkarım (durum + eğilim).
        private static TeamEditorial BuildTeam(UnifiedMatchAiContext ctx, SignalField field, bool isHome)
        {
            var name = isHome ? ctx.HomeName : ctx.AwayName;
            var st = isHome ? ctx.TeamStats?.Home : ctx.TeamStats?.Away;
            var stand = isHome ? ctx.Standings?.Home : ctx.Standings?.Away;
            var points = new List<string>();

            var hasStats = st != null && st.HasData;
            var hasStand = stand != null && stand.HasData;
            if (!hasStats && !hasStand)
                return new TeamEditorial { TeamName = name, HasData = false, Points = points };

            var form = hasStand ? stand.Form : st?.Form;
            var pos = hasStand ? stand.Position : 0;

            // ── 1) DURUM: form (varsa) → özgüven/baskı; form yoksa uç konum → baskı. Tek cümle, sonuç odaklı. ──
            string situation =
                  FormIsGood(form) ? $"{name} son haftalarda yakaladığı istikrarı bu maça özgüvenle taşıyor; yüksek moral, riskli oynama cesaretini artırabilir."
                : FormIsPoor(form) ? $"{name} son dönemdeki düşüşün ardından baskı altında; bu maç bir çıkış arayışına dönüşebilir, bu da tempoyu ve hata payını etkiler."
                : !string.IsNullOrWhiteSpace(form) ? $"{name} dalgalı bir form çizgisiyle geliyor; tutarlılık arayışı, maç içindeki güven ve kararlarını doğrudan etkileyebilir."
                : (pos >= 1 && pos <= 3) ? $"{name} zirve mücadelesinin içinde; bu konum her puana ayrı bir ağırlık ve baskı yüklüyor."
                : pos >= 17 ? $"{name} küme hattının baskısını hissediyor; bu gerilim, oyun tercihlerini ve risk iştahını doğrudan etkileyebilir."
                : null;
            if (situation != null) points.Add(situation);

            // ── 2) EĞİLİM: hücum VEYA savunma karakterinin sonucu (en belirgin olan, tek cümle) ──
            if (hasStats)
            {
                var forAvg = isHome ? st.GoalsForAvgHome : st.GoalsForAvgAway;
                var againstAvg = isHome ? st.GoalsAgainstAvgHome : st.GoalsAgainstAvgAway;
                var where = isHome ? "iç sahada" : "deplasmanda";
                bool goodDef = againstAvg > 0 && againstAvg <= 0.9;
                bool leakyDef = againstAvg >= 1.7;
                bool sharpAtt = forAvg >= 1.7;
                bool blunt = forAvg > 0 && forAvg <= 0.9;

                if (sharpAtt && !leakyDef)
                    points.Add($"{name} {where} sürekli pozisyon üreten bir hücum eğiliminde; bu, rakip savunmasına maç boyu soru sordurabilir.");
                else if (blunt)
                    points.Add($"{name} {where} gol bulmakta zorlanıyor; üretim önde bireysel bir kıvılcıma bağlı kalabilir, bu da düşük skorlu bir maç riskini artırır.");
                else if (goodDef)
                    points.Add($"{name} {where} arkada derli toplu; bu disiplin, kontrollü ve düşük skorlu bir maça zemin hazırlayabilir.");
                else if (leakyDef)
                    points.Add($"{name} {where} savunmada alan tanıyor; bu kırılganlık, rakip hücumuna fırsat vererek gol yeme riskini yükseltiyor.");
            }

            var clean = points.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
            return new TeamEditorial { TeamName = name, HasData = clean.Count > 0, Points = clean };
        }

        // ═══════════════════════════ SQUAD STORY (GÖREV 3 — derin) ═══════════════════════════
        // "3 eksik var" DEĞİL; eksiğin oyuna ETKİSİ (bölge/derinlik/rotasyon). Bölge bilgisi PlayerIntelligence'tan.
        private static IReadOnlyList<string> BuildSquadStory(UnifiedMatchAiContext ctx)
        {
            var list = new List<string>();
            var av = ctx.Availability;

            foreach (var side in new[] { true, false })
            {
                var name = side ? ctx.HomeName : ctx.AwayName;
                var pi = side ? ctx.PlayerIntelligence?.Home : ctx.PlayerIntelligence?.Away;
                var abs = side ? av?.HomeKeyAbsences ?? 0 : av?.AwayKeyAbsences ?? 0;

                if (pi is { HasData: true } && pi.InjuredCount > 0 && (pi.InjuredDefCount + pi.InjuredMidCount + pi.InjuredAttCount) > 0)
                {
                    if (pi.InjuredDefCount >= 2)
                        list.Add($"{name} savunmada {pi.InjuredDefCount} önemli eksikle sahaya çıkıyor; bu, özellikle duran toplarda ve ikili mücadelelerde arka bölge dengesini zorlayabilir.");
                    else if (pi.InjuredDefCount == 1)
                        list.Add($"{name} savunmada bir eksikle oynayacak; yedek çözümün uyumu maç içinde sınanacak.");
                    if (pi.InjuredMidCount > 0)
                        list.Add($"{name} orta sahadaki eksik(ler) nedeniyle oyun kuruluşunda ve top kazanımında derinliğini zorlamak durumunda kalabilir.");
                    if (pi.InjuredAttCount > 0)
                        list.Add($"{name} hücumda eksik veriyor; bu, gol yollarında seçenek çeşitliliğini daraltabilir.");
                }
                else if (abs > 0)
                {
                    list.Add(abs >= 3
                        ? $"{name} {abs} önemli eksikle ciddi bir kadro sınavında; bu derinlikte kayıp, rotasyonu ve dengeyi doğrudan etkileyebilir."
                        : $"{name} tarafında {abs} önemli eksik var; bu, tercih ve rotasyonu etkileyebilir.");
                }
            }

            if (av is { HasData: true })
            {
                if (av.LineupConfirmed) list.Add("Kadrolar netleşmiş; sürpriz eksik ihtimali düşük, bu da planlamayı öngörülebilir kılıyor.");
                else if (av.HomeKeyAbsences + av.AwayKeyAbsences > 0) list.Add("Kesin kadrolar henüz doğrulanmadı; son dakika gelişmeleri dengeyi değiştirebilir.");
            }
            return list.Distinct().ToList();
        }

        // ═══════════════════════════ TIMELINE STORY (GÖREV 6 — derin) ═══════════════════════════
        // "3 gün sonra maç var" DEĞİL; rotasyon ihtimali / fiziksel yorgunluk / öncelik değişimi /
        // risk alma ihtimali / maçın önem sırası. Yalnız gerçek Timeline (+ önem bağlamı).
        private static IReadOnlyList<string> BuildTimelineStory(UnifiedMatchAiContext ctx, MatchImportance importance)
        {
            var list = new List<string>();
            var tl = ctx.Timeline;
            if (tl is { HasData: true })
            {
                AppendTimelineStory(list, ctx.HomeName, tl.Home, importance);
                AppendTimelineStory(list, ctx.AwayName, tl.Away, importance);
            }
            var t = ctx.Tournament;
            if (t is { HasData: true } && t.ExtraTimePossible)
                list.Add("Beraberlik halinde uzatma/penaltı ihtimali, oyuncuların fiziksel yönetimini ve son bölüm risk tercihlerini etkileyebilir.");
            return list.Distinct().ToList();
        }

        private static void AppendTimelineStory(List<string> list, string name, TeamTimelineSignals t, MatchImportance importance)
        {
            if (t is not { HasData: true }) return;

            // Fiziksel yorgunluk (kısa dinlenme) vs tazelik avantajı.
            if (t.HasPrevMatch && t.ShortRest)
                list.Add($"{name} bu maça yalnızca {t.RestDaysBefore} gün dinlenerek geliyor; biriken yorgunluk özellikle ikinci yarıda tempoyu ve ikili mücadele gücünü düşürebilir.");
            else if (t.HasPrevMatch && t.LongRestAdvantage && !t.CongestedSchedule)
                list.Add($"{name} {t.RestDaysBefore} günlük iyi bir dinlenmenin ardından çıkıyor; taze bacaklar yüksek tempoyu maç boyu sürdürme şansı veriyor.");

            // Öncelik değişimi + rotasyon ihtimali (yaklaşan farklı-kulvar maçı).
            if (t.HasNextMatch && t.UpcomingPriorityMatch)
                list.Add($"{name}, {t.DaysToNextMatch} gün sonra farklı bir kulvarda kritik bir maça çıkacak; teknik ekip önceliği oraya kaydırıp bu maçta bazı isimleri dinlendirmeyi seçebilir, bu da kadro gücünü değiştirebilir.");
            else if (t.HasNextMatch && t.RotationRisk)
                list.Add($"{name}'in {t.DaysToNextMatch} gün sonra bir maçı daha var; yoğun takvim, kilit oyuncuları koruma ve rotasyon ihtimalini gündeme getiriyor.");

            // Yoğun fikstür → risk yönetimi.
            if (t.CongestedSchedule && !t.RotationRisk && (t.MatchesLast14 + t.MatchesNext14) >= 2)
                list.Add($"{name} sıkışık bir fikstür döneminden geçiyor; fiziksel yük yönetimi, risk alma seviyesini ve son bölüm dayanıklılığını maçın parçası kılıyor.");
        }

        // ═══════════════════════════ 5. TEKNİK DİREKTÖR ═══════════════════════════
        private static IReadOnlyList<string> BuildCoach(UnifiedMatchAiContext ctx)
        {
            var list = new List<string>();
            var tp = ctx.TeamProfile;
            if (tp != null && tp.HasData)
            {
                if (tp.Home != null && tp.Home.HasCoach && !string.IsNullOrWhiteSpace(tp.Home.CoachName))
                    list.Add($"{ctx.HomeName}'in teknik patronu {tp.Home.CoachName}.");
                if (tp.Away != null && tp.Away.HasCoach && !string.IsNullOrWhiteSpace(tp.Away.CoachName))
                    list.Add($"{ctx.AwayName} kenarında {tp.Away.CoachName} var.");
            }
            var hp = ctx.Standings?.Home;
            if (hp != null && hp.HasData && FormIsPoor(hp.Form) && (hp.Position >= 12 || (ctx.StandingsContext?.RelegationDataAvailable == true && hp.Position >= 10)))
                list.Add($"{ctx.HomeName} kenarındaki teknik ekip baskı altında; son dönemdeki puan kayıpları bu maçı bir çıkış arayışına çeviriyor.");
            var ap = ctx.Standings?.Away;
            if (ap != null && ap.HasData && FormIsPoor(ap.Form) && (ap.Position >= 12 || (ctx.StandingsContext?.RelegationDataAvailable == true && ap.Position >= 10)))
                list.Add($"{ctx.AwayName} teknik ekibi de rahat değil; kötü giden form, tercihlerini doğrudan etkileyebilir.");
            var news = ctx.News;
            if (news != null && news.HasData && news.CoachNews > 0)
                list.Add("Teknik direktör cephesinde konuşulan gelişmeler var; açıklamalar maç öncesi havayı etkileyebilir.");
            if (ctx.Social != null && ctx.Social.HasData && ctx.Social.OfficialCoachStatement > 0)
                list.Add("Resmi bir teknik direktör açıklaması gündemde.");
            return list.Distinct().ToList();
        }

        // ═══════════════════════════ 6. KRİTİK OYUNCULAR (Football'dan) ═══════════════════════════
        private static IReadOnlyList<string> BuildKeyPlayers(UnifiedMatchAiContext ctx, FootballIntelligence football)
        {
            var list = new List<string>();
            void FromTeam(TeamFootballIntelligence t)
            {
                if (t is { HasData: true } && t.Player.Count > 0)
                    list.Add($"{t.TeamName} tarafında öne çıkan isim(ler): {string.Join("; ", t.Player.Take(2))}.");
            }
            FromTeam(football.Home);
            FromTeam(football.Away);
            var av = ctx.Availability;
            if (av != null && av.HasData && (av.HomeKeyAbsences + av.AwayKeyAbsences) > 0)
                list.Add("Kritik eksikler kadro dengelerini etkiliyor; belirleyici oyuncuların yokluğu maçın seyrini değiştirebilir.");
            return list.Distinct().ToList();
        }

        // ═══════════════════════════ 8. TRANSFER ═══════════════════════════
        private static IReadOnlyList<string> BuildTransfers(UnifiedMatchAiContext ctx)
        {
            var list = new List<string>();
            var tp = ctx.TeamProfile;
            if (tp != null && tp.HasData)
            {
                if (tp.Home != null && tp.Home.HasTransfers && (tp.Home.RecentTransfersIn + tp.Home.RecentTransfersOut) > 0)
                    list.Add($"{ctx.HomeName} kadrosunda son dönemde hareket var ({tp.Home.RecentTransfersIn} gelen, {tp.Home.RecentTransfersOut} giden); uyum süreci performansa yansıyabilir.");
                if (tp.Away != null && tp.Away.HasTransfers && (tp.Away.RecentTransfersIn + tp.Away.RecentTransfersOut) > 0)
                    list.Add($"{ctx.AwayName} tarafında da kadro hareketliliği söz konusu ({tp.Away.RecentTransfersIn} gelen, {tp.Away.RecentTransfersOut} giden).");
            }
            if (ctx.News != null && ctx.News.HasData && ctx.News.TransferNews > 0 && list.Count == 0)
                list.Add("Transfer gündemi maç öncesinde konuşulan başlıklardan biri.");
            return list.Distinct().ToList();
        }

        // ═══════════════════════════ 9. TAKTİK (yalnız DNA + güç-index + gerçek gol/form) ═══════════════════════════
        private static IReadOnlyList<string> BuildTactical(UnifiedMatchAiContext ctx, MatchDna dna)
        {
            var list = new List<string>();
            var s = ctx.Strength;
            var hasIdx = s != null && (s.HomeAttackIndex > 0 || s.AwayAttackIndex > 0 || s.HomeDefenceIndex > 0 || s.AwayDefenceIndex > 0);
            if (hasIdx)
            {
                if (s.HomeAttackIndex > 0 && s.AwayDefenceIndex > 0 && s.HomeAttackIndex > s.AwayDefenceIndex * 1.15)
                    list.Add($"{ctx.HomeName}'in hücum üretimi, {ctx.AwayName} savunmasına baskı kurabilecek düzeyde.");
                if (s.AwayAttackIndex > 0 && s.HomeDefenceIndex > 0 && s.AwayAttackIndex > s.HomeDefenceIndex * 1.15)
                    list.Add($"{ctx.AwayName}'in hücumu, {ctx.HomeName} arka bölgesi için tehdit oluşturabilir.");
            }
            if (dna != null)
            {
                if (dna.Openness.Score >= 62 && dna.Openness.Confidence >= 55)
                    list.Add("Zeminde açık, gol arayan bir oyun eğilimi öne çıkıyor.");
                else if (dna.Openness.Score <= 38 && dna.Openness.Confidence >= 55)
                    list.Add("Kontrollü, temkinli ve pozisyon vermemeye dayalı bir mücadele bekleniyor.");
            }
            return list.Distinct().ToList();
        }

        // ═══════════════════════════ 10. PSİKOLOJİ ═══════════════════════════
        private static IReadOnlyList<string> BuildPsychology(UnifiedMatchAiContext ctx, ContextIntelligence ci, MatchImportance importance)
        {
            var list = new List<string>();
            var pressureHigh = ci?.Pressure != null && ci.Pressure.HasData && ci.Pressure.OverallPressure >= 55;
            if (pressureHigh) list.Add("İki taraf üzerinde de hissedilir bir baskı var; bu, oyun planlarını temkinli kılabilir.");
            if (importance != null && importance.Score >= 60 && !pressureHigh)
                list.Add("Maçın ağırlığı, sahadaki psikolojiyi ilk dakikadan itibaren etkileyebilir.");
            if (ctx.Social != null && ctx.Social.HasData && ctx.Social.BreakingOfficialNews)
                list.Add("Resmi kanattan gelen son gelişme, kabin havasını etkilemiş olabilir.");
            if (ctx.News != null && ctx.News.HasData && ctx.News.HasBreakingNews && list.Count == 0)
                list.Add("Gündemdeki hareketlilik, takımların konsantrasyonu açısından izlenmeye değer.");
            return list.Distinct().ToList();
        }

        // ═══════════════════════════ HIDDEN STORY (çapraz sentez + gizli gerilim) ═══════════════════════════
        private static IReadOnlyList<string> BuildHiddenStory(IReadOnlyList<string> synthesis,
            IReadOnlyList<InteractionEffect> interactions, ContradictionReport contradiction, SurpriseAlert surprise)
        {
            var list = new List<string>();
            if (synthesis != null) list.AddRange(synthesis);   // çok-faktörlü neden→sonuç (Football)
            if (interactions != null)
                foreach (var ie in interactions.OrderByDescending(i => i.Magnitude).Take(2))
                {
                    var drivers = ie.Drivers != null && ie.Drivers.Count > 0 ? string.Join(" + ", ie.Drivers) : ie.Name;
                    list.Add($"{drivers} bir arada değerlendirildiğinde: {ie.Effect}");
                }
            if (contradiction != null && contradiction.HasContradiction)
                list.Add("Yapısal üstünlük ile son gelişmeler aynı yöne bakmıyor; bu gizli gerilim, tabloyu göründüğünden karmaşık kılıyor.");
            if (surprise != null && surprise.HasAlert)
                list.Add("Sinyaller bir arada, favori için beklenmedik bir sürpriz riskine işaret ediyor.");
            return list.Distinct().ToList();
        }

        // ═══════════════════════════ TEAM STORY (form/karakter + H2H geçmişi) ═══════════════════════════
        private static IReadOnlyList<string> BuildTeamStory(UnifiedMatchAiContext ctx, TeamEditorial home, TeamEditorial away)
        {
            var list = new List<string>();
            if (home is { HasData: true }) list.AddRange(home.Points);
            if (away is { HasData: true }) list.AddRange(away.Points);
            var h2h = BuildH2HStory(ctx);
            if (h2h != null) list.Add(h2h);
            return list.Distinct().ToList();
        }

        // H2H geçmişi (kullanılmayan gerçek veri devreye) — dominasyon/denge yorumu, ham skor listesi değil.
        private static string BuildH2HStory(UnifiedMatchAiContext ctx)
        {
            var h = ctx.H2H;
            if (h == null || h.TotalMatches < 3) return null; // az örnek → yorum yok (dürüstlük)
            if (h.HomeWins >= h.AwayWins * 2 && h.HomeWins >= 3)
                return $"Geçmiş karşılaşmalar {ctx.HomeName} lehine belirgin ({h.TotalMatches} maçta {h.HomeWins} galibiyet); bu psikolojik üstünlük sahaya güven olarak yansıyabilir.";
            if (h.AwayWins >= h.HomeWins * 2 && h.AwayWins >= 3)
                return $"Bu eşleşmenin geçmişi {ctx.AwayName}'in lehine işliyor ({h.TotalMatches} maçta {h.AwayWins} galibiyet); rakip saha psikolojisi bu kez de rol oynayabilir.";
            if (h.Draws * 2 >= h.TotalMatches)
                return $"İki takımın geçmişi çekişmeli ve sık sık beraberlikle bitmiş ({h.TotalMatches} maçta {h.Draws} beraberlik); bu, dengeli ve düğümlenen bir mücadele beklentisini güçlendiriyor.";
            return $"Geçmiş {h.TotalMatches} karşılaşma dengeli dağılmış ({ctx.HomeName} {h.HomeWins} - {h.AwayWins} {ctx.AwayName}); tarihsel bir üstünlük yok, bu maçın kendi dinamiği belirleyici olacak.";
        }

        // ═══════════════════════════ FORMAX OPINION (verdict → cümleler) ═══════════════════════════
        private static IReadOnlyList<string> BuildFormaxOpinion(EditorialVerdict v, string formaxView,
            string advantage, string risk, string turning, string surprisePotential)
        {
            // NOT: düğüm noktası (CriticalTopic) → MatchStory manşetinin sahibi; burada TEKRAR edilmez.
            // FORMAX Opinion = bütün verinin AI değerlendirmesi (neden izle + avantaj + risk + çevirici).
            var list = new List<string>();
            if (v is not { HasData: true }) return list;
            if (!string.IsNullOrWhiteSpace(formaxView)) list.Add(formaxView);
            if (!string.IsNullOrWhiteSpace(advantage)) list.Add(advantage);
            if (!string.IsNullOrWhiteSpace(risk)) list.Add(risk);
            if (!string.IsNullOrWhiteSpace(surprisePotential)) list.Add(surprisePotential);
            if (!string.IsNullOrWhiteSpace(turning)) list.Add($"Maçın kaderini çevirebilecek an: {LowerFirst(turning)}");
            return list;
        }

        // ═══════════════════════════ 13. FORMAX GÖRÜŞÜ (verdict) ═══════════════════════════
        private static EditorialVerdict BuildVerdict(UnifiedMatchAiContext ctx, MatchDna dna, ContextIntelligence ci,
            MatchImportance importance, ContradictionReport contradiction, SurpriseAlert surprise, SignalField field,
            TeamEditorial home, TeamEditorial away)
        {
            // DÜRÜSTLÜK KAPISI: FORMAX görüşü YALNIZ gerçek analitik içerik varsa üretilir. Yalnız tarih/
            // sezon-fazı gibi zayıf bağlamda (hazırlık maçı, veri-yoksun) jenerik görüş üretmez → "veri
            // yoksa yorum yok". Hash'e dokunmaz (anlatı).
            var substance = (home != null && home.HasData) || (away != null && away.HasData)
                || (importance != null && importance.Score >= 55)
                || (ctx.Competition != null && ctx.Competition.HasData && ctx.Competition.IsElimination)
                || (ctx.StandingsContext != null && ctx.StandingsContext.HasData)
                || (ctx.News != null && ctx.News.HasData)
                || (ci?.Derby != null && ci.Derby.HasData);
            if (!substance) return new EditorialVerdict { HasData = false };

            var edge = field.NetHomeEdge;
            var favor = Math.Abs(edge) < 0.12 ? null : (edge > 0 ? ctx.HomeName : ctx.AwayName);

            string critical =
                (ci?.Derby != null && ci.Derby.HasData) ? "Rekabetin kâğıt üstündeki dengeleri gölgelemesi." :
                (ctx.Competition != null && ctx.Competition.HasData && ctx.Competition.IsElimination) ? "Eleme baskısının iki takımı da sınırlarına itmesi." :
                (contradiction != null && contradiction.HasContradiction) ? "Yapısal üstünlük ile son gelişmeler arasındaki çelişki." :
                (importance != null && importance.Score >= 60) ? "Maçın iki taraf için de taşıdığı yüksek önem." :
                (favor == null) ? "Net bir favorinin olmayışı ve dengenin inceliği." :
                $"{favor} üstünlüğünün sahaya ne kadar yansıyacağı.";

            string why = favor == null
                ? "Sonucu baştan kestirmek güç; maçın nasıl şekilleneceği en az sonucu kadar merak uyandırıyor."
                : $"{favor} bir adım önde görünse de, bu üstünlüğün sınanacağı bir karşılaşma.";

            string advantage;
            if (favor == null)
            {
                // Dengeli maç — klişe yerine maçın GERÇEK karakterine (DNA) bağla.
                advantage =
                    (dna != null && dna.ChaosRisk.Score >= 60) ? "Net bir favori yok; avantaj, kolayca kaosa açılabilen bu maçta anı doğru yakalayan tarafta olacak."
                    : (dna != null && dna.Openness.Score >= 62) ? "Denge çok ince; açık geçebilecek bu maçta avantaj, üretilen pozisyonu golle bitirebilen tarafa geçecek."
                    : (dna != null && dna.Balance.Score >= 62) ? "İki taraf da birbirini dengeliyor; avantaj, bu kapalı maçta tek bir detayı (duran top, bireysel hata) lehine çevirene ait olacak."
                    : "Belirgin bir taraf öne çıkmıyor; avantaj, maç içindeki küçük detaylarda gizli.";
            }
            else
            {
                var reason = favor == ctx.HomeName && home.HasData ? home.Points.FirstOrDefault()
                           : favor == ctx.AwayName && away.HasData ? away.Points.FirstOrDefault() : null;
                advantage = reason != null ? $"Avantaj {favor} tarafında; {LowerFirst(TrimName(reason, favor))}" : $"Avantaj hafifçe {favor} tarafında görünüyor.";
            }

            string risk =
                (contradiction != null && contradiction.HasContradiction) ? "En büyük risk, güçlü görünen tarafın aleyhine biriken sinyaller." :
                (surprise != null && surprise.HasAlert) ? "En büyük risk, favori için beklenmedik bir sürpriz ihtimali." :
                (dna != null && dna.ChaosRisk.Score >= 60) ? "En büyük risk, maçın kolayca kontrolden çıkabilecek yapısı." :
                (ctx.Availability != null && ctx.Availability.HasData && (ctx.Availability.HomeKeyAbsences + ctx.Availability.AwayKeyAbsences) > 0) ? "En büyük risk, kritik eksiklerin dengeyi bozması." :
                "En büyük risk, dengenin çok ince olması ve tek bir anın belirleyici olabilmesi.";

            string change =
                (ctx.Availability != null && ctx.Availability.HasData && !ctx.Availability.LineupConfirmed && (ctx.Availability.HomeKeyAbsences + ctx.Availability.AwayKeyAbsences) > 0) ? "Kesin kadroların açıklanması dengeyi değiştirebilir." :
                (ctx.News != null && ctx.News.HasData && ctx.News.HasBreakingNews) ? "Son gelişmelerin netleşmesi maçın havasını değiştirebilir." :
                (dna != null && dna.EarlyGoalTendency.Score >= 60) ? "Erken gelebilecek bir gol, bütün dengeyi baştan kurabilir." :
                "İlk yarıda düşecek tek bir gol, senaryonun tümünü çevirebilir.";

            return new EditorialVerdict
            {
                HasData = true,
                CriticalTopic = critical,
                WhyWatch = why,
                BiggestAdvantage = advantage,
                BiggestRisk = risk,
                WhatCouldChange = change
            };
        }

        // ═══════════════════════════ Yardımcılar ═══════════════════════════
        private static string? KeyPlayerThread(UnifiedMatchAiContext ctx)
        {
            foreach (var (pi, name) in new[] { (ctx.PlayerIntelligence?.Home, ctx.HomeName), (ctx.PlayerIntelligence?.Away, ctx.AwayName) })
            {
                if (pi is { HasData: true } && !string.IsNullOrWhiteSpace(pi.TopScorerName) && pi.TopScorerGoals > 0)
                    return $"{name} cephesinde {pi.TopScorerName} sezonun öne çıkan ismi ve maçın gidişatında belirleyici olabilir.";
            }
            return null;
        }

        private static string? FirstNonEmpty(params string?[] xs)
            => xs.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        private static string FormReading(string form)
        {
            if (string.IsNullOrWhiteSpace(form)) return null;
            var f = form.ToUpperInvariant();
            var chars = f.Where(ch => "WDLGBM".IndexOf(ch) >= 0).ToArray();
            if (chars.Length == 0) return null;
            int w = chars.Count(ch => ch == 'W' || ch == 'G');
            int l = chars.Count(ch => ch == 'L' || ch == 'M');
            int total = chars.Length;

            int lead = 1; for (int i = 1; i < chars.Length && chars[i] == chars[0]; i++) lead++;
            int trail = 1; for (int i = chars.Length - 2; i >= 0 && chars[i] == chars[^1]; i--) trail++;
            char last; int streak;
            if (trail >= lead) { last = chars[^1]; streak = trail; } else { last = chars[0]; streak = lead; }
            bool lastWin = last == 'W' || last == 'G';
            bool lastLoss = last == 'L' || last == 'M';

            if (lastWin && streak >= 3) return $"üst üste {streak} galibiyetle net bir çıkış yakalamış durumda";
            if (lastLoss && streak >= 3) return $"üst üste {streak} mağlubiyetle belirgin bir düşüş yaşıyor";
            if (lastWin && streak == 2 && w > l) return "arka arkaya kazandığı maçlarla moral bulmuş görünüyor";
            if (lastLoss && streak == 2) return "son iki maçını kaybederek tempo kaybetmiş durumda";
            if (w >= total - 1 && w >= 3) return $"son maçlarında istikrarlı ({w} galibiyet)";
            if (l >= total - 1 && l >= 3) return $"son maçlarında ağır bir dönemde ({l} mağlubiyet)";
            if (w > l) return $"son maçlarında iyi bir çizgide ({w} galibiyet, {l} mağlubiyet)";
            if (l > w) return $"son maçlarında zorlanıyor ({l} mağlubiyet, {w} galibiyet)";
            return "son maçlarında dalgalı, bir tutarlılık arayan bir görüntü veriyor";
        }

        private static bool FormIsPoor(string form)
        {
            if (string.IsNullOrWhiteSpace(form)) return false;
            var f = form.ToUpperInvariant();
            var chars = f.Where(ch => "WDLGBM".IndexOf(ch) >= 0).ToArray();
            if (chars.Length < 3) return false;
            int w = chars.Count(ch => ch == 'W' || ch == 'G');
            int l = chars.Count(ch => ch == 'L' || ch == 'M');
            return l > w && l >= 3;
        }

        private static bool FormIsGood(string form)
        {
            if (string.IsNullOrWhiteSpace(form)) return false;
            var chars = form.ToUpperInvariant().Where(ch => "WDLGBM".IndexOf(ch) >= 0).ToArray();
            if (chars.Length < 3) return false;
            int w = chars.Count(ch => ch == 'W' || ch == 'G');
            int l = chars.Count(ch => ch == 'L' || ch == 'M');
            return w >= l + 2 && w >= 3;
        }

        private static string LeagueStory(UnifiedMatchAiContext ctx)
        {
            var sc = ctx.StandingsContext;
            var seasonEnd = ctx.Season != null && ctx.Season.HasData && ctx.Season.SeasonPhase == "End";
            if (sc != null && sc.HasData)
            {
                if (sc.TitleRace)
                    return seasonEnd
                        ? $"Zirve yarışında son viraj: {sc.LeaderName} önde ve bu haftalarda puan kaybına tahammül kalmadı."
                        : $"Şampiyonluk yarışı kızışıyor; {sc.LeaderName} ile aradaki fark her puanı kritik kılıyor.";
                if (sc.HomeGapToLeader >= 0 && (sc.HomeGapToLeader <= 3 || sc.AwayGapToLeader <= 3))
                    return "Üst sıralar birbirine kenetlenmiş durumda; bu maçın puanları doğrudan zirve mücadelesine dokunuyor.";
            }
            var hp = ctx.Standings?.Home; var ap = ctx.Standings?.Away;
            if (hp != null && hp.HasData && ap != null && ap.HasData)
            {
                var top = Math.Min(hp.Position, ap.Position);
                if (top <= 4) return "İki takım da üst sıralar/Avrupa yarışının içinde; alınacak sonuç bu mücadelede yön belirleyebilir.";
                if (sc != null && sc.HasData && sc.RelegationDataAvailable && Math.Max(hp.Position, ap.Position) >= 15)
                    return "Alt sıralar için nefeslerin tutulduğu bir maç; küme hattındaki denge bu sonuca göre değişebilir.";
                return "Orta sıralar için görece rahat bir tabloda oynansa da, iki takım için de ivme açısından anlamlı bir maç.";
            }
            return null;
        }

        private static string SeasonStory(UnifiedMatchAiContext ctx)
        {
            var s = ctx.Season;
            if (s == null || !s.HasData) return null;
            return s.SeasonPhase switch
            {
                "End" => "Sezonun son haftalarındayız; bu dönemde her karşılaşma hedeflerin doğrudan sınandığı bir final havası taşıyor.",
                "Start" => "Sezonun açılış dönemi; takımlar henüz ritim ararken bu maç erken bir gövde gösterisi anlamı taşıyabilir.",
                _ => null
            };
        }

        private static string TrStage(string stage) => stage switch
        {
            "Final" => "final", "SemiFinal" => "yarı final", "QuarterFinal" => "çeyrek final",
            "RoundOf16" => "son 16", "Group" => "grup aşaması", "Playoff" => "play-off",
            "Qualification" => "eleme turu", "ThirdPlace" => "üçüncülük", _ => stage
        };

        private static string LowerFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
        private static string TrimName(string s, string name) => s.StartsWith(name + " ") ? s.Substring(name.Length + 1) : s;
    }
}
