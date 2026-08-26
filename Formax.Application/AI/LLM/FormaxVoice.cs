using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Formax.Application.AI.Decision;

namespace Formax.Application.AI.LLM
{
    /// <summary>Sesin konuşacağı ekran — aynı karakter, farklı derinlik/uzunluk.</summary>
    public enum FormaxVoiceScreen
    {
        Discover,     // Keşfet — vitrin, kısa ve davetkâr
        MatchDetail,  // Maç Detayı — tam katmanlı anlatım
        Live,         // Canlı — momentum odaklı, kısa
        Radar,        // Radar — neden öne çıktığı
        Notification, // Bildirim — tek cümle
        Global,       // Global — özet
        DiscoverCard  // Keşfet kartı "FORMAX AI Yorumu" — 2 cümlelik fragman (analiz DEĞİL)
    }

    /// <summary>
    /// FORMAX'ın SESİ (v1.0) — Voice Composer.
    ///
    /// FORMAX'ın editoryal karakterini KOD düzeyinde sabitler ve YALNIZ <see cref="AiDecisionPackage"/>
    /// okur (GDP/Provider/UnifiedContext/RawJSON OKUMAZ; başka veri kaynağı YOK). Karar vermez, olasılık
    /// hesaplamaz, yeni analiz üretmez — motorun ürettiği kararı insan diline çevirmek için LLM'e
    /// verilecek system + user prompt'ları kurar. Deterministik string kompozisyonu (LLM'in kendisi
    /// stochastic'tir; bu katman değil). Yasaklı teknik terimler (xG/Poisson/model...) paketten
    /// kullanıcıya ASLA sızmaz — burada insan diline çevrilir.
    ///
    /// Bu sınıf FORMAX Voice Guide'ın (docs/FORMAX_LLM_VOICE_GUIDE_v1.md) yürütülebilir karşılığıdır.
    /// </summary>
    public sealed class FormaxVoiceComposer
    {
        // ── Kullanıcıya asla gösterilmeyecek teknik terimler (motor bilir; ses çevirir). ──
        public static readonly IReadOnlyList<string> ForbiddenTerms = new[]
        {
            "xG", "expected goal", "poisson", "bayesian", "monte carlo", "ensemble",
            "confidence calibration", "probability distribution", "model", "determinism",
            "signal field", "net edge", "dna", "vektör", "regresyon", "matris"
        };

        // ════════════════════════════ SYSTEM PROMPT (KARAKTER) ════════════════════════════

        /// <summary>
        /// FORMAX karakterinin DEĞİŞMEZ sistem tanımı. Her ekranda, her cevapta aynıdır (LLM Memory =
        /// kişilik sabitliği). Yıllarca değişmeyecek çekirdek. Türkçe (kitle Türkçe).
        /// </summary>
        public string ComposeSystemPrompt() =>
@"Sen FORMAX'sın.

KİMLİK
Sen bir futbol karar-yardımcısının sesisin. Kararı SEN vermezsin; kararı FORMAX'ın motoru verir.
Senin işin o kararı, sakin ve net bir dille insanlara ANLATMAKTIR. Dünyanın en iyi futbol
anlatıcısı gibi konuşursun: çok zeki ama gösterişsiz, çok sakin, çok anlaşılır, çok doğal.

NASIL KONUŞURSUN
- Mühendis gibi değil: teknik jargon yok.
- Bahis yorumcusu gibi değil: kupon, oran, tüyo yok.
- Fanatik gibi değil: taraf tutmazsın.
- Manipülatif, abartılı, kibirli ya da soğuk değilsin.
- Güven verirsin ama asla ukala değilsin. Kesin konuşmazsın.

DİL FELSEFESİ (her cevabın iskeleti)
1) Önce ana fikir — kullanıcı 5 saniyede özü anlamalı.
2) Sonra nedenler — en önemliden başlayarak.
3) Sonra dikkat edilmesi gerekenler.
4) Sonra bilinmeyenler / belirsizlik.
Katmanlı yaz; tek paragrafa sıkıştırma.

YASAK KELİMELER (kullanıcıya ASLA gösterme)
xG, expected goal, Poisson, Bayesian, Monte Carlo, ensemble, model, olasılık dağılımı,
confidence calibration, algoritma, veri seti, sinyal ağırlığı gibi teknik terimler.
Bunları insan diline çevirirsin. Örnek: 'Kazanır' deme; 'ev sahibi bir adım önde görünüyor' de.

DÜRÜSTLÜK (pazarlık edilemez)
- Elindeki bilgi zayıfsa bunu AÇIKÇA söyle. Az bilgiyle çok konuşma.
- Asla emin değilmiş gibi davranma. Kesinlik iddia etme.
- Sana verilmeyen hiçbir şeyi uydurma: skor, istatistik, isim, olay icat etme.
- Yalnızca sana verilen karar özetini anlatırsın; kendi analizini eklemezsin.

ANLATIM ZEKÂSI (bir maç editörü gibi HİKÂYE anlat)
- Kararı tekrar etme; kararın ARKASINDAKİ futbol akışını anlat. Kullanıcı 'bu sistem maçı gerçekten anlamış' demeli.
- Her maçın bir hikâyesi var: nasıl başlayabilir, tempo ne zaman yükselebilir, kim oyunu kontrol etmeye çalışır, nerede kırılabilir, kritik anlar nerede.
- Maçı FAZlarıyla anlat: ilk bölüm → orta bölüm → son bölüm. Ritmi tarif et (sabırlı başlangıç, tempo yükselişi, son bölüm baskısı).
- SEBEP → ETKİ kur: 'şu yön → şu sonuç → şu ihtimal artar' gibi zincirlerle düşün. Tek tek gerçek değil, gerçeklerin İLİŞKİSİNİ anlat.
- TEKRAR YOK: her paragraf YENİ bilgi taşır. Aynı şeyi farklı cümleyle söyleme.
- Belirsizliği gizleme: en olası akışı söyle, ama alternatifi de anlat ('ancak erken bir gol bütün dengeyi değiştirebilir').
- Sana verilen hikâye unsurları DIŞINDA hiçbir şey uydurma (skor/dakika/olay icat etme). Bir bölüm için veri yoksa kısa geç.

DURUŞ
Kullanıcı senin çok akıllı olduğunu HİSSETSİN; ama bunu ona kanıtlamaya çalışma.
Zekâ, sadelikte görünür. Az kelimeyle çok şey anlat. Apple/Linear/Anthropic sadeliği: zarif, minimal, sıcak-ama-ölçülü.

Her yerde (Keşfet, Maç Detayı, Canlı, Radar, Bildirim, Global) aynı FORMAX konuşur. Karakter değişmez.";

        // ════════════════════════════ USER BRIEF (PAKETTEN) ════════════════════════════

        /// <summary>
        /// AiDecisionPackage'i insan-diline-çevrilecek YAPILANDIRILMIŞ brief'e dönüştürür. Sayısal/teknik
        /// alanlar nötr dile map'lenir; LLM bunları system prompt kurallarına göre yazıya döker. Paket
        /// dışında hiçbir kaynak kullanılmaz. Ekran türüne göre istenen derinlik değişir (karakter sabit).
        /// </summary>
        public string ComposeUserBrief(AiDecisionPackage pkg, FormaxVoiceScreen screen = FormaxVoiceScreen.MatchDetail)
        {
            if (pkg == null) return "Bu maç için yeterli karar verisi yok. Kısa ve dürüst ol: analiz için elde yeterli bilgi bulunmadığını söyle.";

            var sb = new StringBuilder();
            sb.AppendLine($"MAÇ: {pkg.HomeName} — {pkg.AwayName}");
            sb.AppendLine();

            // ── Yön/sonuç (nötr dile; 'kazanır' YASAK) ──
            var primary = pkg.PrimaryScenario;
            var lean = DirectionPhrase(pkg);
            sb.AppendLine("ANA FİKİR (nötr dille anlat, kesinlik yok):");
            sb.AppendLine($"- Genel eğilim: {lean}");
            if (primary != null && !string.IsNullOrWhiteSpace(primary.Title))
                sb.AppendLine($"- Öne çıkan senaryo: {Humanize(primary.Title)} (güven: {ConfidenceWord(primary.Confidence)}).");
            sb.AppendLine($"- Maçın karakteri: {PersonalityPhrase(pkg.Personality)}.");
            sb.AppendLine($"- Genel güven düzeyi: {ConfidenceWord(pkg.Confidence?.Level)} — buna göre ne kadar iddialı konuşacağını ayarla.");

            // ── Editoryal analiz (motorun v4 Editorial Intelligence çıktısı; gerçek veriden — anlat, EKLEME) ──
            AppendEditorial(sb, pkg.Editorial);

            // ── Maçın hikâyesi / akışı (faz faz; DNA faz-eğilimlerinden — uydurma yok) ──
            sb.AppendLine();
            sb.AppendLine("MAÇIN AKIŞI (faz faz anlat; kesinlik yok, 'olabilir' dili):");
            foreach (var beat in BuildMatchFlow(pkg))
                sb.AppendLine($"- {beat}");

            // ── Sebep → etki zincirleri (Interactions + kadro + çelişki) ──
            var chains = BuildCauseEffect(pkg);
            if (chains.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("SEBEP → ETKİ (ilişkileri kur, tek tek sinyal sayma):");
                foreach (var c in chains) sb.AppendLine($"- {c}");
            }

            // ── Nedenler (en önemliden) ──
            sb.AppendLine();
            sb.AppendLine("NEDENLER (en önemliden başlayarak, ayrı ayrı):");
            foreach (var r in TopReasons(pkg))
                sb.AppendLine($"- {r}");

            // ── Dikkat / zayıflatan ──
            sb.AppendLine();
            sb.AppendLine("DİKKAT EDİLMESİ GEREKENLER:");
            foreach (var w in Weakenings(pkg))
                sb.AppendLine($"- {w}");

            // ── Belirsizlik / bilinmeyen ──
            sb.AppendLine();
            sb.AppendLine("BELİRSİZLİK (dürüstçe söyle):");
            if (pkg.UnknownFactors != null && pkg.UnknownFactors.Count > 0)
                foreach (var u in pkg.UnknownFactors.Take(3)) sb.AppendLine($"- {u}");
            else
                sb.AppendLine("- Belirgin bir veri boşluğu yok, ama yine de kesinlik iddia etme.");
            if (LowData(pkg))
                sb.AppendLine("- ÖNEMLİ: Elde sınırlı bilgi var. Kısa konuş ve bunu açıkça belirt.");

            // ── Karşı görüş (neyi kaçırıyor olabiliriz) ──
            sb.AppendLine();
            sb.AppendLine("NEYİ KAÇIRIYOR OLABİLİRİZ (karşı görüş):");
            var surprise = pkg.SurpriseScenario;
            if (surprise != null && !string.IsNullOrWhiteSpace(surprise.Title))
                sb.AppendLine($"- {Humanize(surprise.Title)}: {StripForbidden(surprise.Reason)}");
            else
                sb.AppendLine("- Belirgin bir sürpriz senaryosu öne çıkmıyor.");

            // ── Kritik faktör (tek cümle) ──
            sb.AppendLine();
            sb.AppendLine("MAÇIN KADERİNİ DEĞİŞTİREBİLECEK EN ÖNEMLİ OLAY (tek cümle):");
            var pivotalRaw = pkg.Explainability?.PivotalFactor;
            var pivotal = string.IsNullOrWhiteSpace(pivotalRaw) ? "Belirgin tek çevirici olay yok." : Humanize(StripForbidden(pivotalRaw));
            sb.AppendLine($"- {pivotal}");

            // ── Ekran talimatı (derinlik/uzunluk) ──
            sb.AppendLine();
            sb.AppendLine(ScreenInstruction(screen));

            return sb.ToString();
        }

        // ════════════════════════════ KEŞFET KARTI "AI YORUMU" (FRAGMAN) ════════════════════════════

        /// <summary>Keşfet kartı yorumunda ASLA geçmeyecek ifadeler (canlı/sonuç dili — başka bileşenlerin işi).</summary>
        public static readonly IReadOnlyList<string> DiscoverBannedTerms = new[]
        {
            "tempo yükseli", "ikinci yarı", "son dakika", "momentum", "oyun şu an", "şu ana kadar",
            "devam eden", "kazanacak", "kazanır", "gol olacak", "2.5", "kg var", "üst gel", "alt gel",
            "beraberlik", "penaltı", "kırmızı kart", "olası sonuç", "olası skor"
        };

        /// <summary>
        /// Keşfet kartı için "AI Yorumu" — analiz DEĞİL, ANALİZİN FRAGMANI. Tek amaç: kullanıcının
        /// "Tüm Analizi Gör"e basmasını sağlamak. En fazla 2 cümle; maçın SONUCUNU değil HİKÂYESİNİ
        /// merakla ima eder. Maç başlamadı → canlı/sonuç dili YOK. Yalnız AiDecisionPackage'tan; uydurma
        /// yok. Maç tipine göre farklı arketip + 2 varyant (matchId parity) → tekrar etmez. Deterministik.
        /// </summary>
        public string ComposeDiscoverCardComment(AiDecisionPackage pkg)
        {
            if (pkg == null) return "Bu maç için henüz yeterli okuma yok; yine de tabloyu birlikte görmeye ve nasıl şekilleneceğini keşfetmeye değer.";

            var dna = pkg.Dna ?? new MatchDna();
            var edge = System.Math.Abs(pkg.Meta?.NetHomeEdge ?? 0);
            var ctx = pkg.Context ?? new ContextIntelligence();

            // Deterministik ama iyi dağılan varyant seçici: matchId bit-karıştırılır (yuvarlak id'ler
            // kümelenmesin) + DNA karakteri harmanlanır → benzer maçlar bile aynı cümleyi tekrar etmez.
            var seed = MixHash(pkg.MatchId);
            (string, string) Pick(params (string s1, string s2)[] variants) => variants[seed % variants.Length];

            (string s1, string s2) t;

            // Öncelik: en güçlü HİKÂYE önce. (Hepsi sonuç değil, merak imalı; her arketipte ≥3 varyant.)
            if (ctx.Derby != null && ctx.Derby.HasData)
                t = Pick(
                    ($"{pkg.HomeName} – {pkg.AwayName} rekabetinde kağıt üzerindeki dengeler çoğu zaman anlamını yitirir.", "Bu tür maçlarda karakter, sıralamadan çok daha yüksek sesle konuşabilir."),
                    ("Bu bir rekabet maçı ve alışılmış hesaplar böyle gecelerde kolayca yön değiştirebilir.", "İki takımın geçmişi, sahaya taşınacak asıl hikâyeyi yeniden yazabilir."),
                    ("Kadim bir rekabetin gölgesinde oynanacak bu maçın favorisi kâğıtta belli olmayabilir.", "Duygunun oyuna karıştığı yerde, tahminler çoğu zaman geride kalır."));
            else if (ctx.Competition != null && ctx.Competition.HasData &&
                     (ctx.Competition.IsElimination || ctx.Competition.CompetitionType == "Cup" || ctx.Competition.CompetitionType == "Knockout"))
                t = Pick(
                    ("Kaybedenin yolun sonuna geldiği, geri dönüşü olmayan bir eşleşme bizi bekliyor.", "Bu baskı, iki takımı da tanıdık oyunlarının sınırlarının dışına itebilir."),
                    ("Tek karşılaşmada her şeyin belirlendiği bir düğüm noktasındayız.", "Böyle gecelerde cesaret, çoğu zaman isimler kadar belirleyici olabilir."),
                    ("Eleme baskısı, en tanıdık takımları bile beklenmedik yollara sürükleyebilir.", "İşte bu yüzden maçın nasıl kurulacağı, kimin geçeceği kadar merak uyandırıyor."));
            else if ((pkg.Surprise != null && pkg.Surprise.HasAlert) ||
                     (pkg.Contradiction != null && pkg.Contradiction.HasContradiction))
                t = Pick(
                    ("Favori belli gibi görünüyor, ama elimizdeki işaretlerin hepsi aynı yöne bakmıyor.", "Beklenti ile sahadaki tablo arasındaki bu sessiz gerilim maçı ilginç kılıyor."),
                    ("Kağıt üzerindeki üstünlük, bu kez rahat bir tabloya işaret etmiyor.", "Ters yöne çeken küçük işaretler, bu maçın hikâyesini tahmin edilmez kılıyor."),
                    ("Görünürdeki dengeyle satır aralarındaki sinyaller bu maçta birbirini tutmuyor.", "Bu uyumsuzluk, sonucundan önce maçın kendisini merak edilir kılıyor."));
            else if (pkg.Importance != null && pkg.Importance.Score >= 60)
                t = Pick(
                    ("Bu karşılaşma iki taraf için de sıradan bir maçtan çok daha fazlasını taşıyor.", "Sahaya yansıyacak bu ağırlık, sonucun kendisi kadar merak uyandırıyor."),
                    ("Her iki takım için de ayrı bir anlam taşıyan, önemi büyük bir eşleşme.", "O yüzden mücadelenin nasıl kurulacağı şimdiden dikkatle bekleniyor."),
                    ("İki takımın da çok şey beklediği bu maçın havası daha ilk düdükten farklı olacak.", "Böyle maçlarda gerilim, oyunun rengini baştan belirleyebilir."));
            else if (edge >= 0.30)
                t = Pick(
                    ("Bir taraf kâğıt üzerinde öne çıkıyor, ama futbol her zaman kâğıda uymuyor.", "Asıl merak, bu üstünlüğün sahaya nasıl ve ne kadar taşınacağında saklı."),
                    ("Dengeler belirgin biçimde bir tarafı işaret ediyor, yine de hiçbir şey garanti değil.", "Bu üstünlüğün gerçeğe dönüşüp dönüşmeyeceği, maçın asıl hikâyesi olacak."),
                    ("Öne çıkan taraf belli gibi, ancak futbolun sürprizlere alan bıraktığını biliyoruz.", "Favorinin işini ne kadar kolay bitireceği şimdiden merak konusu."));
            else if (dna.Openness.Score < 42 && dna.Tempo.Score < 42)
                t = Pick(
                    ("Sabırlı, temkinli ve satranç gibi kurulan bir mücadele bizi bekliyor olabilir.", "Böyle maçlarda çoğu zaman en küçük detay, en büyük farkı yaratır."),
                    ("Bu eşleşme ölçülü, kontrollü bir oyuna işaret ediyor; aceleye yer yok gibi.", "Kapıyı aralayacak tek bir an, bütün dengeyi sessizce değiştirebilir."),
                    ("İki takım da riski sevmiyor gibi; temkinli bir satranç partisi kurulabilir.", "İşte tam da bu yüzden küçük bir hamle her şeyi belirleyebilir."),
                    ("Gardını düşürmeyen iki takım; ağır tempolu, dikkatli bir maç öngörülüyor.", "Dengeyi bozacak ilk cesur hamle, hikâyenin de başlangıcı olabilir."));
            else if (edge < 0.15 || dna.Balance.Score >= 66)
                t = Pick(
                    ("Kağıt üzerindeki bu denge kolay kolay bozulacak gibi durmuyor.", "Ama küçücük bir kırılma anı bile bütün senaryoyu tersine çevirebilir."),
                    ("Favoriyi belirlemek ilk bakışta hiç de kolay görünmüyor.", "Maçın nasıl şekilleneceği, en az sonucu kadar merak uyandırıyor."),
                    ("İki taraf da birbirine fazlasıyla yakın; net bir üstünlükten söz etmek güç.", "Bu yakınlık, maçı sonucundan bağımsız olarak izlemeye değer kılıyor."),
                    ("Terazinin iki kefesi de neredeyse eşit; bu maçta detaylar öne çıkacak.", "En ufak bir üstünlük anı, bütün dengeyi kendi lehine çevirebilir."));
            else
                t = Pick(
                    ("İki takım sahaya birbirinden farklı güçlü yönlerle çıkıyor.", "Bu taktik karşıtlık, eşleşmeyi baştan sona izlemeye değer kılıyor."),
                    ("İlk bakışta sessiz görünen bu maçın altında ilginç bir hikâye saklı.", "Nasıl şekilleneceği, çoğu zaman sonucundan daha çok ilgi çekiyor."),
                    ("Farklı oyun kimliklerinin karşılaştığı, dokusu zengin bir eşleşme.", "Hangi kimliğin baskın çıkacağı maçın asıl merak konusu."));

            var comment = $"{t.s1} {t.s2}";
            return SanitizeDiscover(comment);
        }

        /// <summary>Keşfet kartı için LLM sistem tanımı (gerçek model bağlanınca): fragman kuralları.</summary>
        public string ComposeDiscoverCardSystemPrompt() =>
            ComposeSystemPrompt() + @"

KEŞFET KARTI ""AI YORUMU"" — ÖZEL KURALLAR
- Bu bir ANALİZ DEĞİL, analizin FRAGMANIDIR. Tek amacı kullanıcının 'Tüm Analizi Gör'e basmasıdır.
- Maçın SONUCUNU söyleme; maçın HİKÂYESİNİ merakla ima et. Kullanıcı 'bu maçta ilginç bir şey var' hissetmeli.
- En fazla 2 cümle, 120-160 karakter. İlk cümle: maçın en büyük hikâyesi. İkinci cümle: bunu destekleyen en önemli futbol nedeni.
- Maç BAŞLAMADI: canlı dil YOK (tempo yükseliyor / ikinci yarı / son dakika / momentum / şu an...).
- Sonuç/market dili YOK (kazanır / gol olacak / 2.5 üst / KG var...). Bunlar başka bileşenlerin işi.
- 'AI Olası Sonuçlar' bölümünü TEKRAR ETME. Merak uyandır, cevabı verme.";

        /// <summary>Deterministik bit-karıştırma (integer avalanche) → yuvarlak/ardışık id'ler bile varyantlara eşit dağılır.</summary>
        private static int MixHash(int x)
        {
            uint h = unchecked((uint)x);
            h = ((h >> 16) ^ h) * 0x45d9f3b;
            h = ((h >> 16) ^ h) * 0x45d9f3b;
            h = (h >> 16) ^ h;
            return (int)(h & 0x7fffffff);
        }

        /// <summary>Keşfet kartı yorumu güvenlik ağı: yasak (canlı/sonuç) ifade sızarsa nötr fragmana düşer.</summary>
        private static string SanitizeDiscover(string comment)
        {
            if (string.IsNullOrWhiteSpace(comment)) return comment;
            foreach (var b in DiscoverBannedTerms)
                if (comment.IndexOf(b, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Dengeli görünen bu eşleşmede detaylar belirleyici olabilir; oyun profilleri maçı merakla beklenir kılıyor.";
            return comment;
        }

        /// <summary>Editorial Intelligence'ı brief'e dökер (yalnız dolu bölümler; motorun ürettiği, ekleme yok).</summary>
        private static void AppendEditorial(System.Text.StringBuilder sb, Formax.Application.AI.Decision.EditorialIntelligence e)
        {
            if (e == null || !e.HasData) return;
            sb.AppendLine();
            sb.AppendLine("EDİTORYAL ANALİZ (motorun GERÇEK veriden ürettiği maddeler — bunları bir TV analisti/editör gibi");
            sb.AppendLine("AKICI bir anlatıya ör; maddeleri sırayla okuma, aralarında sebep-sonuç kur; YENİ bilgi EKLEME):");
            void Block(string title, System.Collections.Generic.IReadOnlyList<string> pts)
            {
                if (pts == null || pts.Count == 0) return;
                foreach (var p in pts.Take(3)) if (!string.IsNullOrWhiteSpace(p)) sb.AppendLine($"  [{title}] {StripForbidden(p)}");
            }
            Block("Bağlam", e.MatchContext);
            if (e.HomeTeam != null && e.HomeTeam.HasData) Block("Ev", e.HomeTeam.Points);
            if (e.AwayTeam != null && e.AwayTeam.HasData) Block("Deplasman", e.AwayTeam.Points);
            Block("Kadro", e.Squad);
            Block("Fikstür", e.Fixture);
            Block("Teknik Direktör", e.Coach);
            Block("Haber", e.News);
            Block("Transfer", e.Transfers);
            Block("Psikoloji", e.Psychology);
            Block("Taktik", e.Tactical);
            Block("Kritik Oyuncu", e.KeyPlayers);
            Block("Gizli", e.Hidden);
            var v = e.Verdict;
            if (v != null && v.HasData)
            {
                sb.AppendLine("  [FORMAX Görüşü] En kritik konu: " + v.CriticalTopic);
                sb.AppendLine("  [FORMAX Görüşü] Neden izlemeli: " + v.WhyWatch);
                sb.AppendLine("  [FORMAX Görüşü] Avantaj: " + v.BiggestAdvantage);
                sb.AppendLine("  [FORMAX Görüşü] Risk: " + v.BiggestRisk);
                sb.AppendLine("  [FORMAX Görüşü] Kaderi değiştirebilecek: " + v.WhatCouldChange);
            }
        }

        // ════════════════════════════ ÇEVİRİ YARDIMCILARI ════════════════════════════

        /// <summary>Ekran türüne göre çıktı derinliği talimatı (karakter değişmez; format değişir).</summary>
        private static string ScreenInstruction(FormaxVoiceScreen screen) => screen switch
        {
            FormaxVoiceScreen.Notification =>
                "FORMAT: Tek cümle. Sadece ana fikir + varsa tek kritik uyarı. Sakin ve net.",
            FormaxVoiceScreen.Discover =>
                "FORMAT: 2-3 cümlelik davetkâr vitrin özeti. Ana fikir + neden ilginç. Detaya girme.",
            FormaxVoiceScreen.Live =>
                "FORMAT: Kısa ve anlık. Şu anki tabloyu ve momentumu anlat; kesinlik yok, 'şu an' vurgusu.",
            FormaxVoiceScreen.Radar =>
                "FORMAT: Bu maçın neden dikkat çektiğini 2-3 cümlede anlat. Öne çıkaran nedene odaklan.",
            FormaxVoiceScreen.Global =>
                "FORMAT: Kısa özet paragrafı + bir cümle belirsizlik notu.",
            _ =>
                "FORMAT (katmanlı): 1) Kısa Özet 2) Neden (3 madde) 3) Dikkat Edilmesi Gereken 4) Belirsizlik 5) İstersen Detay. Başlıkları sade tut; robotik olma."
        };

        /// <summary>Net eğilimi 'kazanır' demeden nötr dile çevirir.</summary>
        private static string DirectionPhrase(AiDecisionPackage pkg)
        {
            var edge = pkg.Meta?.NetHomeEdge ?? 0;
            var home = pkg.HomeName;
            var away = pkg.AwayName;
            var mag = Math.Abs(edge);
            if (mag < 0.08) return "iki taraf da birbirine çok yakın; dengeli bir karşılaşma görünüyor";
            var side = edge > 0 ? home : away;
            if (mag < 0.20) return $"{side} tarafı hafif önde görünüyor, ama fark küçük";
            if (mag < 0.40) return $"{side} tarafı bir adım önde görünüyor";
            return $"{side} tarafı belirgin biçimde önde görünüyor (yine de kesinlik yok)";
        }

        /// <summary>Güven skoru/etiketini insan diline çevirir (sayı/teknik terim yok).</summary>
        private static string ConfidenceWord(string level) => (level ?? "").ToUpperInvariant() switch
        {
            "YÜKSEK" => "güçlü bir okuma",
            "ORTA" => "temkinli bir okuma",
            "DÜŞÜK" => "zayıf bir okuma — dikkatli ol",
            _ => "belirsiz"
        };

        /// <summary>Maç karakterini (personality) sade bir cümleye çevirir.</summary>
        private static string PersonalityPhrase(MatchPersonality p)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Primary)) return "belirgin bir karakter oturmamış";
            var style = string.IsNullOrWhiteSpace(p.PlayStyle) ? "" : $", {p.PlayStyle.ToLowerInvariant()}";
            return $"{p.Primary.ToLowerInvariant()}{style} bir maç";
        }

        /// <summary>En önemli nedenleri (güçlendiren + reasoning) derler; teknik terim temizlenir.</summary>
        private static IEnumerable<string> TopReasons(AiDecisionPackage pkg)
        {
            var reasons = new List<string>();
            var ex = pkg.Explainability;
            if (ex?.StrengtheningFactors != null) reasons.AddRange(ex.StrengtheningFactors);
            if (reasons.Count < 3 && pkg.CriticalFactors != null) reasons.AddRange(pkg.CriticalFactors);
            if (reasons.Count < 3 && ex?.Reasoning != null) reasons.AddRange(ex.Reasoning);
            return reasons.Select(r => Humanize(StripForbidden(r))).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Take(3);
        }

        /// <summary>Zayıflatan etkenler + cautions.</summary>
        private static IEnumerable<string> Weakenings(AiDecisionPackage pkg)
        {
            var list = new List<string>();
            var ex = pkg.Explainability;
            if (ex?.WeakeningFactors != null) list.AddRange(ex.WeakeningFactors);
            if (list.Count == 0 && ex?.Cautions != null) list.AddRange(ex.Cautions);
            var clean = list.Select(w => Humanize(StripForbidden(w))).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Take(3).ToList();
            if (clean.Count == 0) clean.Add("Belirgin bir zayıflatıcı etken öne çıkmıyor.");
            return clean;
        }

        /// <summary>Skoru nitel banda çevirir (sayı sızdırmadan).</summary>
        private static string Band(int score, string hi, string mid, string lo)
            => score >= 60 ? hi : score >= 40 ? mid : lo;

        /// <summary>
        /// Maçın faz-faz hikâyesini DNA faz-eğilimlerinden kurar (EarlyGoal/Tempo/Openness/Balance/
        /// LateGoal/Pressure/ChaosRisk). Canlı ise mevcut tablodan başlar. Hiçbir olay/skor uydurulmaz.
        /// </summary>
        private static IReadOnlyList<string> BuildMatchFlow(AiDecisionPackage pkg)
        {
            var dna = pkg.Dna ?? new MatchDna();
            var beats = new List<string>();

            // Canlı: hikâye şu andan devam eder.
            if (pkg.LiveMomentum != null && pkg.LiveMomentum.HasData)
            {
                var lm = pkg.LiveMomentum;
                var dir = lm.Momentum > 10 ? pkg.HomeName : lm.Momentum < -10 ? pkg.AwayName : "iki taraf da";
                beats.Add($"Şu an: {lm.HomeScore}-{lm.AwayScore}, gidişat {(lm.Momentum == 0 ? "dengeli" : dir + " yönünde")} — kalan bölüm buradan şekillenecek.");
            }

            // İlk bölüm — erken gol eğilimi + tempo.
            var openStart = dna.EarlyGoalTendency.Score;
            beats.Add(Band(openStart,
                "İlk bölüm hızlı açılabilir; erken bir gole yatkın bir başlangıç görünüyor.",
                "İlk bölüm ölçülü başlayabilir; iki taraf da dengeyi arayabilir.",
                "İlk bölüm temkinli ve sabırlı geçebilir; erken gol beklentisi düşük."));

            // Orta bölüm — tempo + açıklık + kim kontrol eder.
            var control = Math.Abs(pkg.Meta?.NetHomeEdge ?? 0) >= 0.15
                ? ((pkg.Meta?.NetHomeEdge ?? 0) > 0 ? pkg.HomeName : pkg.AwayName) + " oyunu eline almaya çalışabilir"
                : "kontrol el değiştirebilir, net bir hâkim taraf oturmayabilir";
            beats.Add(Band(dna.Tempo.Score,
                $"Orta bölümde tempo yükselebilir; {control}.",
                $"Orta bölümde tempo dalgalanabilir; {control}.",
                $"Orta bölüm kontrollü kalabilir; {control}."));

            // Son bölüm — geç gol + baskı + kaos.
            beats.Add(Band(Math.Max(dna.LateGoalTendency.Score, dna.Pressure.Score),
                "Son bölümde baskı ve gol ihtimali artabilir; maç burada kırılabilir.",
                "Son bölümde tempo yeniden yükselebilir; skorda oynama görülebilir.",
                "Son bölüm kontrollü kapanabilir; büyük bir kırılma beklentisi düşük."));

            // Kırılma noktası — kaos yüksekse uyar.
            if (dna.ChaosRisk.Score >= 60)
                beats.Add("Uyarı: bu maç kolayca kontrolden çıkabilir; tek bir an dengeyi tümüyle değiştirebilir.");

            return beats;
        }

        /// <summary>
        /// Sebep→etki zincirleri: Interactions (Drivers→Effect, motor zaten üretti) + kadro eksiği →
        /// geçiş + yapı-bağlam çelişkisi. Yalnız gerçek paket verisinden; boşsa boş liste.
        /// </summary>
        private static IReadOnlyList<string> BuildCauseEffect(AiDecisionPackage pkg)
        {
            var chains = new List<string>();

            if (pkg.Interactions != null)
                foreach (var ie in pkg.Interactions.OrderByDescending(i => i.Magnitude).Take(2))
                {
                    var drivers = ie.Drivers != null && ie.Drivers.Count > 0 ? string.Join(" + ", ie.Drivers) : ie.Name;
                    chains.Add($"{drivers} → {StripForbidden(ie.Effect)}");
                }

            if (pkg.Contradiction != null && pkg.Contradiction.HasContradiction && chains.Count < 3)
                chains.Add("yapının gösterdiği favori ile son gelişmeler çelişiyor → bu okumayı temkinli tutmak gerekir");

            return chains.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().Take(3).ToList();
        }

        private static bool LowData(AiDecisionPackage pkg)
        {
            var conf = pkg.Confidence?.Score ?? 0;
            var q = pkg.DecisionQualityScore;
            return conf < 45 || q < 45;
        }

        /// <summary>Market/sinyal adlarını + iç etiketleri doğal ifadeye çevirir (LLM cilalar).</summary>
        private static string Humanize(string market)
        {
            if (string.IsNullOrWhiteSpace(market)) return "";
            var s = market
                // Market adları
                .Replace("Karşılıklı Gol Var", "iki takımın da gol bulması")
                .Replace("Karşılıklı Gol Yok", "en az bir takımın gol atamaması")
                .Replace("2.5 Üst", "gollü bir maç (2.5 üstü)")
                .Replace("2.5 Alt", "az gollü bir maç (2.5 altı)")
                .Replace("Ev Sahibi Kazanır", "ev sahibinin öne çıkması")
                .Replace("Deplasman Kazanır", "deplasmanın öne çıkması")
                // İç sinyal adları (İngilizce identifier → insan dili)
                .Replace("StandingsStrength", "lig sıralamasındaki üstünlük")
                .Replace("StandingsImpact", "lig sıralamasının etkisi")
                .Replace("FormStrength", "form çizgisi")
                .Replace("FormMomentum", "form ivmesi")
                .Replace("AttackStrength", "hücum gücü")
                .Replace("DefenceStrength", "savunma sağlamlığı")
                .Replace("DefenseStrength", "savunma sağlamlığı")
                .Replace("NewsConfidence", "haber akışı")
                .Replace("CompetitionImpact", "maçın önemi")
                .Replace("CompetitionStrength", "maçın önemi")
                .Replace("SocialSentiment", "resmi/sosyal gündem")
                .Replace("Availability", "kadro durumu")
                .Replace("H2HAdvantage", "geçmiş karşılaşma üstünlüğü")
                .Replace("TeamComparison", "takım karşılaştırması");
            return s;
        }

        /// <summary>Brief'e teknik terim sızarsa temizler (güvenlik ağı; kullanıcıya asla teknik sızmasın).</summary>
        private static string StripForbidden(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            var outp = s;
            foreach (var t in ForbiddenTerms)
            {
                var idx = outp.IndexOf(t, StringComparison.OrdinalIgnoreCase);
                while (idx >= 0)
                {
                    outp = outp.Remove(idx, t.Length);
                    idx = outp.IndexOf(t, StringComparison.OrdinalIgnoreCase);
                }
            }
            return outp.Replace("  ", " ").Trim();
        }
    }
}
