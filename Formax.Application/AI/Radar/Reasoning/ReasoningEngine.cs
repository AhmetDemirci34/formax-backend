using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.AI.Radar.Reasoning
{
    /// <summary>
    /// FORMAX Radar v3 — Intelligence Reasoning Layer (FORMAX'ın beyni).
    ///
    /// MatchIntelligenceContext'ten anlam çıkarır; LLM'e ham veri yerine sindirilmiş
    /// bir <see cref="IntelligencePack"/> hazırlar. Tamamen deterministik:
    ///   1) Signal Extraction  2) Contradiction Detection
    ///   3) Narrative Focus     4) Evidence Pack   5) Reasoning Confidence
    /// Yeni veri kaynağı eklemez; mevcut context üzerinde akıl yürütür.
    /// </summary>
    public sealed class ReasoningEngine
    {
        public IntelligencePack Build(MatchIntelligenceContext ctx)
        {
            var attack = (ctx.Stats.HomeGoalScoringRate + ctx.Stats.AwayGoalScoringRate) / 2;
            var defense = (ctx.Stats.HomeCleanSheetRate + ctx.Stats.AwayCleanSheetRate) / 2; // yüksek = güçlü savunma
            var newsVol = ctx.News.Volume24h;
            var importanceHigh = ctx.Importance.Level.Contains("Yüksek", StringComparison.OrdinalIgnoreCase)
                                 || ctx.Importance.WatchersCount >= 5000;

            var signals = ExtractSignals(ctx, attack, defense, newsVol, importanceHigh);
            var contradictions = DetectContradictions(ctx, attack, defense, newsVol, importanceHigh);
            var focus = BuildFocus(signals);
            var evidence = BuildEvidence(ctx, attack, defense, newsVol, importanceHigh);
            var confidence = ScoreConfidence(ctx, signals, contradictions, attack);

            return new IntelligencePack
            {
                MatchId = ctx.MatchId,
                HomeTeam = ctx.HomeTeam,
                AwayTeam = ctx.AwayTeam,
                CurrentMatch = new IntelligencePack.CurrentMatchBlock
                {
                    Home = ctx.HomeTeam,
                    Away = ctx.AwayTeam
                },
                League = ctx.League,
                Round = string.IsNullOrWhiteSpace(ctx.Round) ? "BİLİNMİYOR" : ctx.Round,
                Week = ctx.WeekNumber,
                Status = ctx.Status,
                KickoffUtc = ctx.KickoffUtc,
                Importance = ImportanceLabel(ctx, importanceHigh),

                // GERÇEK MAÇ BAĞLAMI — hepsi backend değerinden birebir; veri yoksa null kalır.
                Season = ctx.SeasonYear,
                StageLabel = ctx.Importance.StageLabel,
                SportingImportance = ctx.Importance.SportingLevel,
                Standings = MapStandings(ctx.Standings),
                H2H = ctx.H2H.Total > 0
                    ? new IntelligencePack.H2HBlock
                    {
                        Toplam = ctx.H2H.Total,
                        HomeTeamName = ctx.HomeTeam,
                        EvSahibiGalibiyeti = ctx.H2H.HomeWins,
                        AwayTeamName = ctx.AwayTeam,
                        DeplasmanGalibiyeti = ctx.H2H.AwayWins,
                        Beraberlik = ctx.H2H.Draws,
                        Summary = H2HOzet(ctx),

                        // Gerçek geçmiş sonuçlar — "ne oldu" sorusunun tek meşru kaynağı.
                        RecentResults = ctx.H2H.RecentResults.Count == 0
                            ? null
                            : ctx.H2H.RecentResults
                                .Select(r => new IntelligencePack.H2HResultRow
                                {
                                    Date = r.Date,
                                    Result = H2HSonucCumlesi(r),
                                    H2HHomeTeam = r.H2HHomeTeam,
                                    H2HAwayTeam = r.H2HAwayTeam,
                                    HomeGoals = r.HomeGoals,
                                    AwayGoals = r.AwayGoals
                                }).ToList()
                    }
                    : null,

                // Takım sezon istatistikleri — backend hesabı, birebir. Satır yoksa gönderilmez.
                TeamStats = ctx.TeamStats.Count == 0
                    ? null
                    : ctx.TeamStats.Select(t => new IntelligencePack.TeamStatsRow
                    {
                        TeamName = t.TeamName,
                        AvgGoalsFor = Math.Round(t.AvgGoalsFor, 2),
                        AvgGoalsAgainst = Math.Round(t.AvgGoalsAgainst, 2),
                        GoalScoringRate = t.GoalScoringRate,
                        CleanSheetRate = t.CleanSheetRate
                    }).ToList(),

                // ── BİREBİR TAŞIMA (reasoning YOK) ──────────────────────────────
                // Aşağıdaki dört blok backend'in zaten hesapladığı gerçek değerlerdir.
                // Burada tek yaptığımız pack'e kopyalamaktır; hiçbir formül uygulanmaz.
                Power = new IntelligencePack.PowerBlock
                {
                    MacGucSkoru = ctx.Importance.GucSkoru,
                    MacOynanmaSkoru = ctx.Importance.OynanmaSkoru,
                    Zone = ctx.Importance.Level,
                    Note = ctx.Importance.Note
                },
                Form = new IntelligencePack.FormBlock
                {
                    // Aynı döküm, yalnız takım adıyla birlikte hazır cümle olarak taşınır.
                    Lines = Son5Satirlari(ctx),
                    HomeSummary = Son5Ozet(ctx.Form.HomeRecent),
                    AwaySummary = Son5Ozet(ctx.Form.AwayRecent),
                    HomeWins = Count(ctx.Form.HomeRecent, 'G'),
                    HomeDraws = Count(ctx.Form.HomeRecent, 'B'),
                    HomeLosses = Count(ctx.Form.HomeRecent, 'M'),
                    AwayWins = Count(ctx.Form.AwayRecent, 'G'),
                    AwayDraws = Count(ctx.Form.AwayRecent, 'B'),
                    AwayLosses = Count(ctx.Form.AwayRecent, 'M'),
                    HomeFormScore = ctx.Form.HomeFormScore,
                    AwayFormScore = ctx.Form.AwayFormScore,

                    // Dökümün dayanağı da birlikte gider: kaç maç, ne kadar eski, hangi
                    // turnuvalar ve zamansal kesinliğe izin var mı. Karar backend'in.
                    Basis = FormBasisOf(ctx)
                },
                // Kadro verisi yoksa blok HİÇ gönderilmez (null → prompt JSON'unda görünmez).
                Availability = ctx.Availability.HasData
                    ? new IntelligencePack.AvailabilityBlock
                    {
                        HasData = true,
                        LineupsAnnounced = ctx.Availability.LineupsAnnounced,
                        HomeOut = ctx.Availability.HomeOut,
                        AwayOut = ctx.Availability.AwayOut,

                        // Eksiğin nedeni: sakat mı, cezalı mı. Motor bu alanları okumaz.
                        Home = new IntelligencePack.AvailabilityBlock.Side
                        {
                            Injured = ctx.Availability.HomeInjured,
                            Suspended = ctx.Availability.HomeSuspended,
                            Doubtful = ctx.Availability.HomeDoubtful
                        },
                        Away = new IntelligencePack.AvailabilityBlock.Side
                        {
                            Injured = ctx.Availability.AwayInjured,
                            Suspended = ctx.Availability.AwaySuspended,
                            Doubtful = ctx.Availability.AwayDoubtful
                        },
                        HomeInjured = ctx.Availability.HomeInjured,
                        HomeSuspended = ctx.Availability.HomeSuspended,
                        HomeDoubtful = ctx.Availability.HomeDoubtful,
                        AwayInjured = ctx.Availability.AwayInjured,
                        AwaySuspended = ctx.Availability.AwaySuspended,
                        AwayDoubtful = ctx.Availability.AwayDoubtful
                    }
                    : null,
                // KAPI: yalnız Evidence Store'dan (okuma kapılarından) gelen kanıt factual
                // sayılır. FromEvidence=false ise kaynak eski NABIZ akışıdır → gerçek bilgi
                // olarak SUNULMAZ, blok null bırakılır.
                // SAYI = GERÇEKTEN VERİLEN BAŞLIK SAYISI. Süzgeci geçen hiç başlık kalmadıysa
                // blok hiç gönderilmez; "haber var ama gösterilmiyor" durumu oluşmaz.
                FactualEvidence = BuildEvidenceBlock(ctx),
                // PROMPT INJECTION SINIRI: WorldHeadline pack'teki TEK dış-kaynaklı serbest
                // metindir (diğer tüm alanlar bizim ürettiğimiz etiket/sayıdır). Ham hâliyle
                // prompt'a girerse içindeki "ignore previous instructions" benzeri bir cümle
                // LLM tarafından TALİMAT sanılabilir. Sanitize onu VERİ seviyesinde tutar.
                WorldHeadline = SanitizeExternalText(ctx.WorldHeadline),
                ReasoningConfidence = confidence,
                Signals = signals,
                Contradictions = contradictions,
                NarrativeFocus = focus,
                Evidence = evidence,
                Scenarios = ctx.Scenarios.Select(s => new IntelligencePack.ScenarioInsight
                {
                    Market = s.Market,
                    Probability = s.Probability,
                    Confidence = s.Confidence,
                    Reason = s.EvidenceTags.Count > 0 ? string.Join(", ", s.EvidenceTags.Take(2)) : ""
                }).ToList()
            };
        }

        /// <summary>"G G B M G" dizisindeki sonuç sayısı — yeni hesap değil, aynı dizinin sayımı.</summary>
        private static int Count(string? recent, char result) =>
            string.IsNullOrEmpty(recent) ? 0 : recent.Count(c => c == result);

        /// <summary>
        /// Son 5 maçın HAZIR Türkçe ifadesi. Model sayıyı yeniden yorumlamasın diye cümle
        /// backend'de kurulur; sıfır olan sonuç hiç yazılmaz ("0 galibiyet" denmez).
        /// </summary>
        private static string Son5Ozet(string? recent)
        {
            if (string.IsNullOrWhiteSpace(recent)) return "";
            var g = Count(recent, 'G'); var b = Count(recent, 'B'); var m = Count(recent, 'M');
            var parts = new List<string>();
            if (g > 0) parts.Add($"{g} galibiyet");
            if (b > 0) parts.Add($"{b} beraberlik");
            if (m > 0) parts.Add($"{m} mağlubiyet");
            if (parts.Count == 0) return "";
            var ozet = string.Join(", ", parts);

            // KAÇ MAÇ VARSA O YAZILIR. Ölçüldü (14.08): Osasuna'nın kayıtlı TEK maçı varken
            // cümle "son 5 maçta galibiyet yok: 1 mağlubiyet" diyordu — hem eksik maçı beşe
            // yuvarlıyor hem de iki nokta üst üste kurgusu "1 galibiyet" gibi okunuyordu
            // (model "tek galibiyet bulmuş" yazdı). Sayım aynı; yalnız ifade dürüstleşti.
            var n = g + b + m;
            var kapsam = n == 1 ? "son maçında" : $"son {n} maçında";
            return g == 0
                ? $"{kapsam} hiç galibiyet alamadı ({ozet})"
                : $"{kapsam} {ozet} aldı";
        }

        /// <summary>
        /// H2H TOPLAMLARINI HAZIR CÜMLEYE ÇEVİRİR — model saymasın diye. Yeni hesap yoktur;
        /// aynı sayılar takım adlarıyla cümleye dökülür. Sıfır olan sonuç yazılmaz
        /// ("0 galibiyet" denmez); kayıt yoksa boş döner ve konu hiç açılmaz.
        /// </summary>
        private static string H2HOzet(MatchIntelligenceContext ctx)
        {
            var h = ctx.H2H;
            if (h.Total <= 0) return "";

            var parts = new List<string>();
            if (h.HomeWins > 0) parts.Add($"{ctx.HomeTeam} {h.HomeWins} kez kazandı");
            if (h.AwayWins > 0) parts.Add($"{ctx.AwayTeam} {h.AwayWins} kez kazandı");
            if (h.Draws > 0) parts.Add($"{h.Draws} maç berabere bitti");
            if (parts.Count == 0) return "";

            var kapsam = $"Aralarındaki son {h.Total} maçta";
            var govde = string.Join(", ", parts);

            // Hiç kazanamayan taraf varsa bunu AÇIKÇA söyle — model "üstünlük" yorumunu
            // kendi uydurmasın.
            if (h.HomeWins == 0 && h.AwayWins > 0) govde += $"; {ctx.HomeTeam} kazanamadı";
            else if (h.AwayWins == 0 && h.HomeWins > 0) govde += $"; {ctx.AwayTeam} kazanamadı";

            return $"{kapsam} {govde}.";
        }

        /// <summary>
        /// GEÇMİŞ BİR MAÇIN SONUCU — hazır cümle. Yeni hesap yoktur; aynı kayıttaki
        /// takım adları, goller ve saha bilgisi tek bir Türkçe cümlede birleştirilir.
        /// Kazanan açıkça yazılır → model yönü çıkarmak zorunda kalmaz; skor cümlenin
        /// içinde METİN olarak geçtiği için skor denetimi de bu değeri tanır.
        /// </summary>
        private static string H2HSonucCumlesi(MatchIntelligenceContext.H2HResult r)
        {
            var ev = r.H2HHomeTeam;
            var dep = r.H2HAwayTeam;
            if (string.IsNullOrWhiteSpace(ev) || string.IsNullOrWhiteSpace(dep)) return "";

            if (r.HomeGoals == r.AwayGoals)
                return $"{ev} sahasında oynandı, {ev} ile {dep} {r.HomeGoals}-{r.AwayGoals} berabere kaldı";

            var kazanan = r.HomeGoals > r.AwayGoals ? ev : dep;
            var kaybeden = r.HomeGoals > r.AwayGoals ? dep : ev;
            var skor = r.HomeGoals > r.AwayGoals
                ? $"{r.HomeGoals}-{r.AwayGoals}"
                : $"{r.AwayGoals}-{r.HomeGoals}";
            var saha = r.HomeGoals > r.AwayGoals ? "kendi sahasında" : $"{ev} deplasmanında";

            return $"{kazanan}, {saha} {kaybeden} karşısında {skor} kazandı";
        }

        /// <summary>
        /// Son 5 dökümünü TAKIM ADIYLA hazır cümleye çevirir. Yeni hesap yoktur; aynı
        /// <see cref="Son5Ozet"/> çıktısının başına takım adı gelir. Böylece prompt'ta
        /// sızabilecek "EvSahibiSon5 / DeplasmanSon5" gibi bir iç alan adı kalmaz.
        /// </summary>
        /// <summary>
        /// Form dökümünün dayanağını pakete çevirir. Hiçbir tarafta gerçek maç yoksa blok
        /// HİÇ gönderilmez (null) — o zaman pakette form dökümü de yoktur ve konu açılmaz.
        /// </summary>
        private static IntelligencePack.FormBasisSide? FormBasisOf(MatchIntelligenceContext ctx)
        {
            static IntelligencePack.FormBasis? Map(Services.Matches.FormEvidence e)
                => !e.HasData
                    ? null
                    : new IntelligencePack.FormBasis
                    {
                        SampleCount = e.SampleCount,
                        AgeDays = e.AgeDays,
                        Competitions = e.Competitions.Count > 0 ? e.Competitions.ToList() : null,
                        AllowsTrendClaim = e.AllowsTrendClaim
                    };

            var h = Map(ctx.Form.HomeEvidence);
            var a = Map(ctx.Form.AwayEvidence);
            if (h == null && a == null) return null;

            return new IntelligencePack.FormBasisSide { Home = h, Away = a };
        }

        private static List<string> Son5Satirlari(MatchIntelligenceContext ctx)
        {
            var lines = new List<string>();
            var h = Son5Ozet(ctx.Form.HomeRecent);
            var a = Son5Ozet(ctx.Form.AwayRecent);
            if (h.Length > 0) lines.Add($"{ctx.HomeTeam} {h}.");
            if (a.Length > 0) lines.Add($"{ctx.AwayTeam} {a}.");
            return lines;
        }

        // 1) ── Signal Extraction ────────────────────────────────────────────────
        private static List<IntelligencePack.ReasonedSignal> ExtractSignals(
            MatchIntelligenceContext ctx, int attack, int defense, int newsVol, bool importanceHigh)
        {
            var s = new List<IntelligencePack.ReasonedSignal>();

            void Add(string name, int strength, int conf, string ev) =>
                s.Add(new IntelligencePack.ReasonedSignal
                {
                    Name = name,
                    Strength = Math.Clamp(strength, 0, 100),
                    Confidence = Math.Clamp(conf, 0, 100),
                    Evidence = ev
                });

            if (attack >= 60)
                Add("High Attack Tempo", attack,
                    Math.Min(ctx.Stats.HomeGoalScoringRate, ctx.Stats.AwayGoalScoringRate) >= 55 ? 85 : 70,
                    "İki tarafın hücum üretimi yüksek seyrediyor");

            if (defense <= 40)
                Add("Defensive Weakness", 100 - defense, 75, "Savunma istikrarı düşük");
            else if (defense >= 60)
                Add("Defensive Stability", defense, 75, "Savunmalar istikrarlı");

            if (ctx.Form.HomeFormScore >= 65)
                Add("Strong Home Form", ctx.Form.HomeFormScore, 80, "Ev sahibi form grafiği güçlü");
            if (ctx.Form.AwayFormScore >= 65)
                Add("Strong Away Form", ctx.Form.AwayFormScore, 80, "Deplasman form grafiği güçlü");

            if (newsVol >= 8)
                Add("High News Volume", Math.Min(100, 50 + newsVol * 3), 70, "Son 24 saatte haber hareketi yoğun");

            // v2.1'de kanıt TİPLERİ buradan sinyale çevriliyordu ("Injury" → "Sakatlık haberleri
            // gündemde"). Ölçüldü: taksonomi yanılıyor ("has no doubts" → Injury, "final training
            // session" → Weather) ve model bu etiketi olgu sanıp kullanıcıya sakatlık/hava haberi
            // olduğunu söylüyordu. Kanıt artık LLM'e YALNIZ temizlenmiş metin olarak gider
            // (FactualEvidence.Notes); tip türevli sinyal üretilmez.
            // Yan etki (bilerek): kanıt-türevli sinyal listeden çıktığı için ReasoningConfidence
            // bu maçlarda birkaç puan değişebilir. Skor/olasılık formülleri DEĞİŞMEDİ.

            if (importanceHigh)
                Add("High Match Importance", 80, 75, "Bu karşılaşma sezon içinde ağırlığı olan bir maç");

            if (string.Equals(ctx.Social.Level, "Yüksek", StringComparison.OrdinalIgnoreCase))
                Add("Community Attention", 75, 65, "FORMAX topluluk ilgisi ortalamanın üzerinde");

            if (Math.Abs(ctx.Form.HomeFormScore - ctx.Form.AwayFormScore) <= 6)
                Add("Balanced Contest", 60, 60, "Form göstergeleri birbirine yakın");

            return s.OrderByDescending(x => x.Strength).ToList();
        }

        /// <summary>Backend'in maç önem etiketi — Evidence Pack'teki "Maç Önemi" ile AYNI kural.</summary>
        private static string ImportanceLabel(MatchIntelligenceContext ctx, bool importanceHigh) =>
            importanceHigh ? "Yüksek" : ctx.Importance.WatchersCount >= 1000 ? "Orta" : "Düşük";

        // 2) ── Contradiction Detection ──────────────────────────────────────────
        private static List<string> DetectContradictions(
            MatchIntelligenceContext ctx, int attack, int defense, int newsVol, bool importanceHigh)
        {
            var c = new List<string>();

            // Bu metinler fallback yoluyla KULLANICIYA gidebiliyor → teknik kelime kullanılmaz.
            if (attack >= 60 && defense >= 60)
                c.Add("Hücum tarafı güçlü ama iki takım da savunmada istikrarlı; tempo beklentisi temkinli okunmalı.");

            if (ctx.Stats.HomeRank is int hr && ctx.Stats.AwayRank is int ar && hr > 0 && ar > 0)
            {
                var favoredHome = hr < ar;
                var favForm = favoredHome ? ctx.Form.HomeFormScore : ctx.Form.AwayFormScore;
                var othForm = favoredHome ? ctx.Form.AwayFormScore : ctx.Form.HomeFormScore;
                var favName = favoredHome ? ctx.HomeTeam : ctx.AwayTeam;
                // METİN KULLANICIYA GİDEBİLİR (fallback): sıra ve iç ölçüm dili kullanılmaz —
                // sıra iddiası puan durumu pakette yoksa zaten kurulamaz.
                if (othForm - favForm >= 8)
                    c.Add($"{favName} kâğıt üstünde önde görünse de son haftalardaki görüntüsü rakibinin gerisinde.");
            }

            if (newsVol >= 8 && !importanceHigh)
                c.Add("Maç çevresinde konuşulan çok şey var ama karşılaşmanın kendisi sakin bir maç görüntüsü veriyor.");

            var top = ctx.Scenarios.FirstOrDefault();
            if (attack >= 60 && top != null && top.Market.Contains("Alt", StringComparison.OrdinalIgnoreCase))
                c.Add("Hücum tarafı güçlü olsa da öne çıkan beklenti düşük skor yönünde; tablo iki yönü birden gösteriyor.");

            return c;
        }

        // 3) ── Narrative Focus ──────────────────────────────────────────────────
        private static List<string> BuildFocus(List<IntelligencePack.ReasonedSignal> signals)
        {
            string? Map(string name) => name switch
            {
                "High Attack Tempo" => "Tempo ve hücum",
                "Defensive Weakness" => "Savunma kırılganlığı",
                "Defensive Stability" => "Savunma istikrarı",
                "Strong Home Form" or "Strong Away Form" => "Form",
                "High News Volume" => "Haber gündemi",
                "High Match Importance" => "Maç önemi",
                "Community Attention" => "Taraftar ilgisi",
                "Balanced Contest" => "Denge",
                _ => null
            };

            var focus = new List<string>();
            foreach (var sig in signals)
            {
                var f = Map(sig.Name);
                if (f != null && !focus.Contains(f)) focus.Add(f);
                if (focus.Count == 3) break;
            }
            if (focus.Count == 0) focus.Add("Genel maç bağlamı");
            return focus;
        }

        // ── Factual Evidence notları ────────────────────────────────────────────
        // Kaynak güvenilirliği ZATEN okuma kapılarında karara bağlandı (SourceQuality eşiği +
        // maç ilgisi + tazelik). Burada YENİ bir doğrulama motoru YOKTUR; yalnız metin
        // güvenliği uygulanır: enjeksiyon zararsızlaştırma + bu ekranda kullanıcıya gitmemesi
        // gereken metin türlerinin (skor, oran/bahis, söylenti/forum) ELENMESİ.
        // Süzgeci geçemeyen kayıt taşınmaz — LLM onu hiç görmez.
        /// <summary>
        /// Puan durumu satırlarını pack'e çevirir. Yeni hesap yok; tablo yoksa null.
        /// </summary>
        private static IntelligencePack.StandingsBlock? MapStandings(
            MatchIntelligenceContext.StandingsBlock? s)
        {
            if (s == null) return null;

            static IntelligencePack.StandingRow? Map(MatchIntelligenceContext.StandingRow? r)
                => r == null ? null : new IntelligencePack.StandingRow
                {
                    Takim = r.TeamName,
                    Sira = r.Position,
                    Oynanan = r.Played,
                    Galibiyet = r.Won,
                    Beraberlik = r.Drawn,
                    Maglubiyet = r.Lost,
                    AttigiGol = r.GoalsFor,
                    YedigiGol = r.GoalsAgainst,
                    Puan = r.Points
                };

            var home = Map(s.Home);
            var away = Map(s.Away);
            if (home == null && away == null) return null;

            return new IntelligencePack.StandingsBlock { EvSahibi = home, Deplasman = away };
        }

        /// <summary>
        /// Haber başlığı bloğu. Kapı aynı: yalnız Evidence Store'dan gelen kayıtlar. Fark,
        /// sayının artık listeyle TUTARLI olması — eskiden 24 saatlik ham hacim yazılıyordu.
        /// </summary>
        private static IntelligencePack.FactualEvidenceBlock? BuildEvidenceBlock(
            MatchIntelligenceContext ctx)
        {
            if (!ctx.News.FromEvidence) return null;

            var notes = BuildEvidenceNotes(ctx.News.Items);
            if (notes.Count == 0) return null;

            return new IntelligencePack.FactualEvidenceBlock
            {
                Count = notes.Count,
                Notes = notes
            };
        }

        private static List<IntelligencePack.EvidenceNote> BuildEvidenceNotes(
            List<MatchIntelligenceContext.EvidenceItem> items)
        {
            var notes = new List<IntelligencePack.EvidenceNote>();
            foreach (var it in items)
            {
                // TİP KAPISI: "General" = SignalExtractor futbol sinyali bulamadı. Bu kayıtlar
                // ağırlıkla fikstür/istatistik sayfası, TV yayın listesi veya ÖNCEKİ maçın
                // özeti oluyor → anlatıya girerse model başka bir maçı bu maç sanır.
                //
                // AMA "General" tek başına ret gerekçesi DEĞİLDİR: gerçek bir futbol gelişmesi
                // de sözlüğe düşmediği için General etiketlenebiliyordu ve çöpe gidiyordu.
                // Ayrım: içerik futbol dili taşıyor VE yüksek kaliteli kaynaktan geliyorsa
                // (resmi/doğrulanmış yayıncı) kanıt sayılır; aksi hâlde elenir.
                // ÖZNESİ ÇÖZÜLEMEYEN OLAY PACK'E GİRMEZ. Ölçüldü: "Charlton v Derby:
                // Nathan Jones previews…" ve "Norwich City v West Brom: Fisher and
                // Kvistgaarden fit" başlıklarında iki takım da anılıyor, hiçbir çekim eki
                // ya da edat yok → backend özneyi (haklı olarak) BOŞ bırakıyor. Ama olay
                // yine de pakete giriyor ve model boşluğu kendi tahminiyle dolduruyordu
                // (Jones'u Derby'ye, fit oyuncuları West Brom'a yazdı). Backend olayın
                // kime ait olduğunu bilmiyorsa o olay ANLATILMAZ.
                if (string.IsNullOrWhiteSpace(it.RelatedTeam)) continue;

                // OLAY TÜRÜ KESİN DEĞİLSE OLAY ANLATILMAZ. "Diğer" = kural kümesinin hiçbiri
                // tutmadı; elde ne olduğu belli olmayan bir metin vardır. Bu kayıtlar
                // ölçüldüğünde fikstür/istatistik sayfası ya da başka bir maçın içeriği
                // çıkıyor; model bunlara anlam üretmek zorunda kalıyordu.
                if (string.IsNullOrWhiteSpace(it.EventType) ||
                    it.EventType.Equals(Services.News.Intelligence.MatchIntelligenceService.EventTypes.Other,
                                        StringComparison.OrdinalIgnoreCase)) continue;

                // KİŞİ ADI ANCAK TAM ÖZNE ZİNCİRİYLE TAŞINIR: gelişmenin sahibi (Takim) ve
                // o maçtaki karşı taraf (Rakip) birlikte bilinmiyorsa, kişiyi bir takıma
                // bağlayan zincir eksiktir → ad taşınmaz (olay takım düzeyinde kalır).
                var opponentKnown = !string.IsNullOrWhiteSpace(it.OpponentTeam);

                // KİŞİ OLAYINDA KİŞİ BİLİNMİYORSA OLAY ANLATILMAZ. Transfer, sakatlık ve
                // ceza tek bir FUTBOLCUNUN başına gelir; backend o kişiyi çözemediyse
                // (başlıkta birden çok ad geçtiği için) elde yalnız "birisi transfer oldu"
                // kalır. Ölçüldü (Sevilla–Rayo): "Xabi Alonso gets his man! … as Pep
                // Chavarria arrives from Rayo Vallecano" başlığında backend haklı olarak
                // kişiyi boş bıraktı, model boşluğu doldurup transferi YANLIŞ kişiye yazdı.
                var personEvent = it.EventType is
                    Services.News.Intelligence.MatchIntelligenceService.EventTypes.Transfer or
                    Services.News.Intelligence.MatchIntelligenceService.EventTypes.Injury or
                    Services.News.Intelligence.MatchIntelligenceService.EventTypes.Suspension;
                if (personEvent && string.IsNullOrWhiteSpace(it.Player)
                                && string.IsNullOrWhiteSpace(it.Coach)) continue;

                var typed = !string.IsNullOrWhiteSpace(it.Category)
                            && !it.Category.Equals("General", StringComparison.OrdinalIgnoreCase);
                if (!typed)
                {
                    var body = (it.Headline ?? "") + " " + (it.Summary ?? "");
                    var realFootballNews =
                        Services.News.Intelligence.MatchIntelligenceService.IsFootballContent(body)
                        && it.SourceQuality >= 85;
                    if (!realFootballNews) continue;
                }

                // HAVA DURUMU FORMAX ANLATISINDA YOKTUR: Weather etiketli kanıt LLM'e hiç gitmez.
                if (typed && it.Category.Equals("Weather", StringComparison.OrdinalIgnoreCase)) continue;

                var text = SanitizeExternalText(it.Headline);
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (IsUnusableEvidenceText(text)) continue;

                // GERÇEK HABER İÇERİĞİ — varsa birebir taşınır (özetlenmez, yorumlanmaz).
                // Aynı kullanılabilirlik kapısından geçer: içerik metni de fikstür/oran/TV
                // metniyse taşınmaz. Yoksa alan hiç gönderilmez → modelin elinde yalnız başlık
                // kalır ve prompt kuralı gereği haberden söz etmez.
                var ozet = SanitizeExternalText(it.Summary);
                if (!string.IsNullOrWhiteSpace(ozet) && IsUnusableEvidenceText(ozet)) ozet = "";

                notes.Add(new IntelligencePack.EvidenceNote
                {
                    SourceQuality = it.SourceQuality,
                    Confidence = it.Confidence,
                    PublishedUtc = it.PublishedUtc,
                    Text = text.Length <= 110 ? text : text[..110].TrimEnd() + "…",
                    Summary = string.IsNullOrWhiteSpace(ozet)
                        ? null
                        : (ozet.Length <= 320 ? ozet : ozet[..320].TrimEnd() + "…"),

                    // Backend'in ZATEN belirlediği gerçekler — model bunları yeniden çıkarmaya
                    // çalışmaz: olayı kaç kaynak doğruladı, hangi takımın gelişmesi, rakibi kim,
                    // maça göre ne zaman. Boş alan hiç gönderilmez.
                    SourceCount = Math.Max(1, it.SourceCount),
                    Team = it.RelatedTeam,
                    Opponent = opponentKnown ? it.OpponentTeam : null,
                    Player = opponentKnown && !string.IsNullOrWhiteSpace(it.Player) ? it.Player : null,
                    Coach = opponentKnown && !string.IsNullOrWhiteSpace(it.Coach) ? it.Coach : null,
                    EventType = string.IsNullOrWhiteSpace(it.EventType) ? null : it.EventType,
                    Importance = string.IsNullOrWhiteSpace(it.Importance) ? null : it.Importance,
                    Timing = string.IsNullOrWhiteSpace(it.Timing) ? null : it.Timing
                });
                if (notes.Count == 5) break;
            }
            return notes;
        }

        /// <summary>
        /// Kanıt başlığı LLM'e GERÇEK bilgi olarak verilebilir mi? Kural tek yerde tanımlıdır
        /// (Evidence kapıları); burada yalnız çağrılır — yazma, okuma ve pack tarafı AYNI
        /// süzgeci kullanır.
        /// </summary>
        internal static bool IsUnusableEvidenceText(string text) =>
            Services.News.Intelligence.MatchIntelligenceService.IsNonFactualContent(text);

        /// <summary>
        /// Dış kaynaklı serbest metni LLM prompt'una VERİ olarak sokar (talimat olarak değil).
        ///
        /// Uygulananlar: satır sonu/kontrol karakterleri tek boşluğa indirilir (çok satırlı
        /// sahte "system:" blokları kurulamaz), prompt/rol sınırı taklit eden işaretler ve
        /// yaygın talimat kalıpları ayıklanır, uzunluk sınırlandırılır. İçerik SİLİNMEZ —
        /// yalnız yapısal olarak zararsızlaştırılır; başlığın anlamı korunur.
        /// </summary>
        internal static string? SanitizeExternalText(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            // 1) Tüm kontrol karakterleri (\n, \r, \t…) → boşluk: çok satırlı enjeksiyon biter.
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (var ch in raw)
                sb.Append(char.IsControl(ch) ? ' ' : ch);
            var s = sb.ToString();

            // KÜLTÜR-BAĞIMSIZ eşleşme ZORUNLU: sunucu tr-TR kültüründe çalıştığında büyük "I"
            // küçük "ı"ya düşer; salt IgnoreCase ile "Ignore" ↔ "ignore" EŞLEŞMEZ ve enjeksiyon
            // kalıbı süzgeçten kaçar (ölçüldü). CultureInvariant bunu kapatır.
            const System.Text.RegularExpressions.RegexOptions Opts =
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.CultureInvariant;

            // 2) Rol/şema sınırı taklidi ve JSON/markdown kaçışları.
            s = System.Text.RegularExpressions.Regex.Replace(
                s, @"\b(system|assistant|user)\s*:", " ", Opts);
            s = s.Replace("```", " ").Replace("{", "(").Replace("}", ")");

            // 3) Yaygın talimat kalıpları (TR+EN) — cümle olarak değil, kalıp olarak ayıklanır.
            s = System.Text.RegularExpressions.Regex.Replace(
                s,
                @"\b(ignore|disregard|forget)\s+(all\s+|the\s+|any\s+)?(previous|prior|above)\s+(instructions?|prompts?|rules?)\b"
                + @"|\b(onceki|önceki|yukaridaki|yukarıdaki)\s+(tum\s+|tüm\s+)?(talimatlar[ıi]|kurallar[ıi])[a-zçğıöşü]*\s+(yok\s*say|unut|gormezden\s*gel|görmezden\s*gel)[a-zçğıöşü]*"
                + @"|\b(new|yeni)\s+(instructions?|talimat(lar)?)\s*:",
                " ", Opts);

            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s{2,}", " ").Trim();
            return s.Length <= 200 ? s : s[..200].TrimEnd() + "…";
        }

        // 4) ── Evidence Pack ────────────────────────────────────────────────────
        private static List<IntelligencePack.EvidenceItem> BuildEvidence(
            MatchIntelligenceContext ctx, int attack, int defense, int newsVol, bool importanceHigh)
        {
            var e = new List<IntelligencePack.EvidenceItem>();
            void Add(string l, string v) => e.Add(new IntelligencePack.EvidenceItem { Label = l, Value = v });

            // ETİKETLER İÇ ÖLÇÜM DİLİ TAŞIMAZ: model bu etiketleri metne kopyaladığında
            // kullanıcıya "haber hacmi", "hücum sinyali" gibi sistem kelimeleri çıkıyordu.
            // Ölçüt ve eşikler AYNI; yalnız etiketin adı futbol diline çevrildi.
            Add("Gündem", newsVol >= 8 ? "Yüksek" : newsVol >= 3 ? "Orta" : "Düşük");
            Add("Hücum Tarafı", attack >= 60 ? "Güçlü" : attack >= 45 ? "Orta" : "Zayıf");
            Add("Savunma Tarafı", defense >= 60 ? "Güçlü" : defense >= 45 ? "Orta" : "Zayıf");
            Add("Maç Önemi", importanceHigh ? "Yüksek" : ctx.Importance.WatchersCount >= 1000 ? "Orta" : "Düşük");
            if (!string.IsNullOrWhiteSpace(ctx.Social.Level))
                Add("Topluluk İlgisi", ctx.Social.Level);

            var top = ctx.Scenarios.FirstOrDefault();
            if (top != null)
            {
                // YÜZDE BU ETİKETE YAZILMAZ: prompt "yüzde metne girmez" derken veri
                // "KG Var (%88)" diye hazır bir görüntü cümlesi veriyordu. Yüzde zaten
                // Scenarios bloğunda ayrı alan olarak duruyor; UI onu oradan alır.
                Add("En Güçlü Senaryo", top.Market);
                if (top.EvidenceTags.Count > 0)
                    Add("Senaryo Nedeni", string.Join(", ", top.EvidenceTags.Take(2)));
            }

            return e;
        }

        // 5) ── Reasoning Confidence (0–100) ─────────────────────────────────────
        private static int ScoreConfidence(
            MatchIntelligenceContext ctx, List<IntelligencePack.ReasonedSignal> signals,
            List<string> contradictions, int attack)
        {
            var score = 40;
            if (ctx.Scenarios.Count > 0) score += 10;   // senaryo verisi var
            if (attack > 0) score += 10;                 // istatistik verisi var
            if (ctx.H2H.Total > 0) score += 8;           // geçmiş veri var
            if (ctx.News.Volume24h > 0) score += 7;      // haber verisi var

            if (signals.Count > 0) score += signals[0].Strength / 10; // en güçlü sinyal katkısı (0–10)
            score -= contradictions.Count * 8;           // çelişki güveni düşürür

            return Math.Clamp(score, 0, 100);
        }
    }
}
