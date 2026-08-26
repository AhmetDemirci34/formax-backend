using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Formax.Application.AI.Radar.Reasoning
{
    /// <summary>
    /// FORMAX Radar v3 — Reasoning Layer çıktısı. LLM'e artık ham context değil, BU
    /// gönderilir. ReasoningEngine ham veriden anlam çıkarır; pack yalnızca SİNDİRİLMİŞ
    /// akıl yürütme taşır: güçlü sinyaller, çelişkiler, anlatı odağı, kanıt paketi,
    /// reasoning güven skoru ve sıralı senaryolar.
    ///
    /// LLM bu pack'i OKUR ve ANLATIR; düşünmeyi ReasoningEngine yapmıştır.
    /// </summary>
    public sealed class IntelligencePack
    {
        /// <summary>
        /// Çıktının yazılacağı dil. Ölçüldü: dil verilmediğinde model bir yüzeyi İngilizce
        /// üretebiliyor. Değer veride yazılı olduğu için model dili tahmin etmez.
        /// </summary>
        public string Dil { get; set; } = "Türkçe";

        public int MatchId { get; set; }

        /// <summary>
        /// BU MAÇIN ev sahibi/deplasmanı. TOP-LEVEL'DA DEĞİL, <see cref="CurrentMatch"/>
        /// içinde gönderilir: aynı JSON'da H2H satırlarının da ev sahibi/deplasmanı var ve
        /// iki kavram aynı adla yan yana durunca model yönü karıştırıyordu. Artık "bu maçın"
        /// tarafları ayrı bir nesnede, geçmiş maçınkiler ayrı adlarla.
        /// </summary>
        [JsonIgnore] public string HomeTeam { get; set; } = "";
        [JsonIgnore] public string AwayTeam { get; set; } = "";

        /// <summary>Bu maçın kanonik tarafları — model buradan okur, çıkarım yapmaz.</summary>
        [JsonPropertyName("BuMac")]
        public CurrentMatchBlock CurrentMatch { get; set; } = new();

        public sealed class CurrentMatchBlock
        {
            [JsonPropertyName("EvSahibi")] public string Home { get; set; } = "";
            [JsonPropertyName("Deplasman")] public string Away { get; set; } = "";
        }

        public string League { get; set; } = "";

        /// <summary>
        /// Tur/aşama — SAĞLAYICININ HAM METNİ ARTIK GÖNDERİLMEZ. Ölçüldü: "Regular Season - 1"
        /// metni modele bırakılınca hafta yanlış okunuyordu. Yerine backend'in çözdüğü
        /// anlamsal etiket (<see cref="StageLabel"/>) ve <see cref="Week"/> gider.
        /// </summary>
        [JsonIgnore]
        public string Round { get; set; } = "BİLİNMİYOR";

        /// <summary>
        /// Lig haftası — backend'in çözdüğü GERÇEK numara. Yoksa alan hiç gönderilmez ve
        /// model hafta numarası kullanamaz.
        /// </summary>
        [JsonPropertyName("Hafta")]
        public int? Week { get; set; }

        /// <summary>Sezon yılı — backend değerinden birebir. Bilinmiyorsa alan hiç gönderilmez.</summary>
        [JsonPropertyName("Sezon")]
        public int? Season { get; set; }
        public string Status { get; set; } = "";
        public string KickoffUtc { get; set; } = "";

        /// <summary>Backend'in maç önem etiketi (Düşük / Orta / Yüksek). Başka bağlam türetilemez.</summary>
        public string Importance { get; set; } = "";

        /// <summary>
        /// Turun futbolca karşılığı — GERÇEK sağlayıcı round metninden (ör. "Play-off turu").
        /// Veri yoksa alan hiç gönderilmez; LLM aşama uyduramaz.
        /// </summary>
        [JsonPropertyName("Asama")]
        public string? StageLabel { get; set; }

        /// <summary>
        /// SPORTİF ÖNEM — kullanıcı ilgisinden bağımsız. Ölçüldü (14.08): tek ölçüt
        /// WatchersCount olduğu için UEFA play-off'u dâhil her maç "Düşük" görünüyordu.
        /// Belirlenemiyorsa alan hiç gönderilmez.
        /// </summary>
        [JsonPropertyName("SportifOnem")]
        public string? SportingImportance { get; set; }

        /// <summary>Puan durumu — gerçek tablo satırı. Tablo yoksa alan hiç gönderilmez.</summary>
        [JsonPropertyName("PuanDurumu")]
        public StandingsBlock? Standings { get; set; }

        /// <summary>
        /// Takımların gerçek sezon istatistikleri. Veri yoksa alan hiç gönderilmez —
        /// model istatistik uyduramaz.
        /// </summary>
        [JsonPropertyName("TakimIstatistikleri")]
        public List<TeamStatsRow>? TeamStats { get; set; }

        /// <summary>Geçmiş karşılaşmalar — gerçek H2H. Kayıt yoksa alan hiç gönderilmez.</summary>
        [JsonPropertyName("GecmisKarsilasmalar")]
        public H2HBlock? H2H { get; set; }

        /// <summary>
        /// Backend'in GÜÇ SKORU'su (0–100) ve eşlik eden oynanma/sapma etiketi. Değerler
        /// deterministik motordan BİREBİR gelir; LLM yeniden hesaplamaz, yalnız okur.
        /// </summary>
        public PowerBlock Power { get; set; } = new();

        /// <summary>Form — backend'in ürettiği son maç dizisi ve form skorları (birebir).</summary>
        public FormBlock Form { get; set; } = new();

        /// <summary>
        /// Kadro eksikleri — MatchPlayerStatuses kaynaklı sonuç (birebir). VERİ YOKSA null
        /// bırakılır ve prompt'ta HİÇ GÖRÜNMEZ: ortada alan olmayınca model kadro/eksik/ilk 11
        /// konusuna girecek bir zemin bulamaz (ölçüldü: HasData=false iken bile yorum üretiyordu).
        /// </summary>
        public AvailabilityBlock? Availability { get; set; }

        /// <summary>
        /// YALNIZCA okuma kapılarından geçmiş (kaynak kalitesi eşiği + maç ilgisi + tazelik)
        /// Evidence Store kanıtı. Kapılardan geçmemiş ya da eski NABIZ kaynaklı içerik buraya
        /// GİRMEZ — "haber bulundu ≠ gerçek kabul edildi" kuralı burada da geçerlidir.
        /// Kanıt yoksa null kalır ve prompt'ta hiç görünmez.
        /// </summary>
        /// <remarks>
        /// AD "DogrulanmisHaberler" DEĞİL. Ölçüldü (14.08): elde yalnız BAŞLIK varken
        /// "doğrulanmış haber" adı modele haberin bir İDDİA taşıdığını düşündürdü ve
        /// "Basın, maçın gol dolu geçeceğini vurguluyor" gibi kaynakta OLMAYAN cümleler
        /// üretti. Alan adı ne olduğunu (başlık) söyleyince bu zemin ortadan kalkıyor.
        /// </remarks>
        [JsonPropertyName("HaberBasliklari")]
        public FactualEvidenceBlock? FactualEvidence { get; set; }

        /// <summary>
        /// Reasoning Layer'ın güven skoru (0–100). JSON adı bilerek sade: model alan adını
        /// metne kopyaladığında bile ""ReasoningConfidence"" gibi teknik bir kelime çıkmasın.
        /// </summary>
        [JsonPropertyName("GuvenSeviyesi")]
        public int ReasoningConfidence { get; set; }

        /// <summary>
        /// Maçın öne çıkan okumaları. ALAN ADI ""Signals"" DEĞİL: ölçüldü, model alan adını
        /// metne taşıyıp kullanıcıya ""savunma sinyalleri"" yazıyordu.
        /// </summary>
        [JsonPropertyName("OneCikanlar")]
        public List<ReasonedSignal> Signals { get; set; } = new();

        /// <summary>Tabloda birbiriyle çelişen yönler — LLM bunları dengeli yansıtmalı.</summary>
        [JsonPropertyName("DengelenecekNoktalar")]
        public List<string> Contradictions { get; set; } = new();

        /// <summary>Anlatının odaklanacağı konular (sıralı).</summary>
        [JsonPropertyName("AnlatiOdagi")]
        public List<string> NarrativeFocus { get; set; } = new();

        /// <summary>Sindirilmiş nitel etiketler (ham veri değil).</summary>
        [JsonPropertyName("GenelTablo")]
        public List<EvidenceItem> Evidence { get; set; } = new();

        /// <summary>Deterministik sıralı senaryolar (LLM yüzdeye dokunmaz).</summary>
        public List<ScenarioInsight> Scenarios { get; set; } = new();

        public string? WorldHeadline { get; set; }

        public sealed class PowerBlock
        {
            /// <summary>
            /// HAM DEĞERLER VE İÇ METİN ARTIK PROMPT'A GİTMEZ. Ölçüldü (14.08): payload'da
            /// giden Note metni "Çoğunluk ev sahibine yoğunlaşmış, güç yönü (Away) ile
            /// ayrışıyor (sapma: 47)" ve "vitrin bunu sadece gösterir" gibi İÇ dil taşıyordu;
            /// modelin ürettiği "ev sahibi avantajı / saha avantajı" cümlelerinin kaynağı
            /// buydu — yani veri, yasakladığımız kırılımı modele kendisi öneriyordu.
            /// Anlatı için gereken NİTEL bilgi zaten Zone etiketinde ve GenelTablo'da var.
            /// </summary>
            [JsonIgnore] public string Kapsam { get; set; } = "";

            [JsonIgnore] public int MacGucSkoru { get; set; }
            [JsonIgnore] public int MacOynanmaSkoru { get; set; }
            [JsonIgnore] public string? Note { get; set; }

            /// <summary>Maçın denge etiketi (nitel) — anlatıda tona çevrilir.</summary>
            [JsonPropertyName("DengeEtiketi")]
            public string Zone { get; set; } = "";
        }

        public sealed class FormBlock
        {
            /// <summary>
            /// KURAL METNİ VERİDEN ÇIKARILDI: talimat prompt'un işidir, veri alanının değil;
            /// veride durunca model bu cümleyi çıktıya kopyalayabiliyordu.
            /// </summary>
            [JsonIgnore] public string Kapsam { get; set; } = "";

            /// <summary>
            /// Son 5 maç dökümü — TAKIM ADIYLA birlikte, hazır cümle olarak. Ölçüldü (14.08):
            /// alan adları "EvSahibiSon5"/"DeplasmanSon5" olarak gidiyordu ve model bunları
            /// hem metne kopyaladı ("…iki mağlubiyet aldığı (EvSahibiSon5)…") hem de formu
            /// ev/deplasman diye bölmeye çalıştı. Ad yerine cümlenin içinde takım adı geçince
            /// sızacak bir iç ad KALMIYOR. İçerik AYNI; yeni hesap yoktur.
            /// </summary>
            [JsonPropertyName("Son5")]
            public List<string> Lines { get; set; } = new();

            // HAM DİZİ ("B B G G G") BİLEREK TAŞINMAZ: ölçüldü, model diziyi kendi sayıyor ve
            // yanılıyor ("beş galibiyet", 6 maçlık toplam). Tek doğruluk kaynağı aşağıdaki
            // sayılardır; yeni hesap değil, aynı dizinin sayımıdır.
            /// <summary>
            /// HAZIR CÜMLE — model sayı yorumlamasın diye. Ölçüldü: sayaç verilse bile bir
            /// yüzeyde ""sadece bir galibiyet"" yazıp 0 galibiyeti bire çıkardı. Anlatıda bu
            /// ifadenin AYNISI kullanılır.
            /// </summary>
            [JsonIgnore] public string HomeSummary { get; set; } = "";
            [JsonIgnore] public string AwaySummary { get; set; } = "";

            [JsonIgnore] public int HomeWins { get; set; }
            [JsonIgnore] public int HomeDraws { get; set; }
            [JsonIgnore] public int HomeLosses { get; set; }
            [JsonIgnore] public int AwayWins { get; set; }
            [JsonIgnore] public int AwayDraws { get; set; }
            [JsonIgnore] public int AwayLosses { get; set; }

            // 0–100 form göstergesi kullanıcı anlatısında hiç kullanılmadığı için prompt'a
            // TAŞINMAZ; taşındığında ""puan"" diye sunuluyordu.
            [JsonIgnore] public int HomeFormScore { get; set; }
            [JsonIgnore] public int AwayFormScore { get; set; }

            /// <summary>
            /// FORM DÖKÜMÜNÜN DAYANAĞI — kaç maç, ne kadar eski, hangi turnuvalardan.
            ///
            /// Neden pakete girer: model daha önce yalnız G/B/M sayılarını görüyordu ve o
            /// sayıların 2024'ten mi bu haftadan mı geldiğini bilemiyordu; "galibiyet hasreti
            /// sürüyor" cümlesi 607 gün önceki beş maçtan kuruldu (ölçüldü: Lask Linz).
            /// Sayı doğruydu, zaman yanlıştı. Artık dayanak da veriyle birlikte gider.
            /// </summary>
            [JsonPropertyName("Son5Dayanak")]
            public FormBasisSide? Basis { get; set; }
        }

        /// <summary>İki tarafın form dayanağı; taraf ayrımı takım adıyla yapılır.</summary>
        public sealed class FormBasisSide
        {
            [JsonPropertyName("EvSahibi")] public FormBasis? Home { get; set; }
            [JsonPropertyName("Deplasman")] public FormBasis? Away { get; set; }
        }

        public sealed class FormBasis
        {
            /// <summary>Dökümün dayandığı GERÇEK maç sayısı. "Son beş" ifadesi ancak 5 ise kullanılabilir.</summary>
            [JsonPropertyName("MacSayisi")] public int SampleCount { get; set; }

            /// <summary>Örneklemdeki en yeni maçın üstünden geçen gün.</summary>
            [JsonPropertyName("EnYeniMacKacGunOnce")] public int? AgeDays { get; set; }

            /// <summary>Dökümün geldiği turnuvalar (kapsam). Takımın tüm maçları DEĞİL.</summary>
            [JsonPropertyName("Turnuvalar")] public List<string>? Competitions { get; set; }

            /// <summary>
            /// GÜNCEL FORM SAYILIR MI? false → bu takım için zamansal kesinlik içeren hiçbir
            /// ifade kurulamaz (hasret, uzun süredir, son dönemde, seri, yükselişte…).
            /// Karar backend'in; model bu bayrağı yorumlamaz, uygular.
            /// </summary>
            [JsonPropertyName("GuncelFormSayilir")] public bool AllowsTrendClaim { get; set; }
        }

        public sealed class AvailabilityBlock
        {
            /// <summary>false → kadro verisi hiç yok; LLM bu konuda konuşmamalı.</summary>
            public bool HasData { get; set; }
            public bool LineupsAnnounced { get; set; }
            public int HomeOut { get; set; }
            public int AwayOut { get; set; }

            /// <summary>
            /// GERÇEK STATÜLER AYRI TUTULUR. "Sakat" ile "cezalı" ve "şüpheli" farklı
            /// şeylerdir; hiçbiri "kadro dışı" diye anlatılamaz. 0 ise o durumda oyuncu
            /// yoktur — model bu alanlarda yazmayan bir durum üretemez.
            /// </summary>
            /// <summary>
            /// TARAF BAZLI KADRO DURUMU. Ölçüldü — iki yanlış bir arada oluyordu:
            /// (a) düz sayılar verilince model pakette olmayan kategori uyduruyordu
            ///     ("bir grup isim maça çıkamayacak"), sakat ile şüpheliyi topluyordu;
            /// (b) hazır "Takım: 2 sakat, 2 şüpheli" cümlesi verilince onu OLDUĞU GİBİ
            ///     yapıştırıyordu — doğru ama robotik veri dökümü.
            /// Çözüm: sayı backend'in, CÜMLE modelin. Her taraf kendi nesnesinde; model
            /// "kaç kişi eksik" hesabı yapmaz, sayıları doğal cümleye çevirir.
            /// </summary>
            [JsonPropertyName("EvSahibi")] public Side Home { get; set; } = new();
            [JsonPropertyName("Deplasman")] public Side Away { get; set; } = new();

            public sealed class Side
            {
                [JsonPropertyName("Sakat")] public int Injured { get; set; }
                [JsonPropertyName("Cezali")] public int Suspended { get; set; }
                [JsonPropertyName("Supheli")] public int Doubtful { get; set; }
            }

            [JsonIgnore] public int HomeInjured { get; set; }
            [JsonIgnore] public int HomeSuspended { get; set; }
            [JsonIgnore] public int HomeDoubtful { get; set; }
            [JsonIgnore] public int AwayInjured { get; set; }
            [JsonIgnore] public int AwaySuspended { get; set; }
            [JsonIgnore] public int AwayDoubtful { get; set; }
        }

        /// <summary>
        /// Takımın gerçek sezon istatistikleri — backend hesabı, birebir. LLM bu sayıları
        /// yeniden hesaplamaz; yalnız futbol diline çevirir.
        /// </summary>
        public sealed class TeamStatsRow
        {
            [JsonPropertyName("Takim")] public string TeamName { get; set; } = "";
            [JsonPropertyName("MacBasiAttigiGol")] public double AvgGoalsFor { get; set; }
            [JsonPropertyName("MacBasiYedigiGol")] public double AvgGoalsAgainst { get; set; }
            [JsonPropertyName("GolAtmaYuzdesi")] public int GoalScoringRate { get; set; }
            [JsonPropertyName("GolYemeyipBitirmeYuzdesi")] public int CleanSheetRate { get; set; }
        }

        /// <summary>
        /// Geçmiş bir karşılaşmanın GERÇEK sonucu (backend kaydı).
        ///
        /// ALAN ADLARI BİLEREK "OMacin…" ÖNEKLİ: o maçın ev sahibi BUGÜNKÜ maçın ev sahibi
        /// olmayabilir. Ölçüldü — paket içinde "EvSahibi" iki farklı anlamda geçtiği için
        /// model geçmiş sonucun yönünü karıştırdı ("Getafe evinde üstünlük kurmuş" dedi,
        /// oysa Getafe deplasmanda kazanmıştı). Goller de ayrı alanlarda verilir; "2-0"
        /// gibi tek metin yönü gizliyordu.
        /// </summary>
        public sealed class H2HResultRow
        {
            [JsonPropertyName("Tarih")] public string Date { get; set; } = "";

            /// <summary>
            /// SONUCUN HAZIR CÜMLESİ — yönü ve skoru birlikte taşır.
            ///
            /// Ölçüldü: ayrı alanlar (ev sahibi / deplasman / iki ayrı gol sayısı) yönü
            /// koruyordu ama iki yan etkisi vardı. (a) Model dört alanı birleştirirken
            /// kazananı karıştırabiliyordu; (b) skor pakette hiçbir yerde "1-2" METNİ
            /// olarak geçmediği için, model doğru skoru yazdığında bile skor denetimi
            /// cümleyi "pakette olmayan skor" sayıp atıyordu → H2H sonucu anlatıya HİÇ
            /// giremiyordu. Form dökümünde işe yarayan çözüm buraya da uygulanır: cümleyi
            /// backend kurar, model yalnız aynı gerçeği kendi diliyle aktarır.
            /// </summary>
            [JsonPropertyName("Sonuc")] public string Result { get; set; } = "";

            [JsonIgnore] public string H2HHomeTeam { get; set; } = "";
            [JsonIgnore] public string H2HAwayTeam { get; set; } = "";
            [JsonIgnore] public int HomeGoals { get; set; }
            [JsonIgnore] public int AwayGoals { get; set; }
        }

        public sealed class FactualEvidenceBlock
        {
            /// <summary>
            /// Aşağıda GERÇEKTEN verilen başlık sayısı. Ölçüldü (14.08): buraya 24 saatlik
            /// ham haber hacmi yazılıyordu; Celta–Osasuna paketinde "Count: 3" derken listede
            /// tek başlık vardı → model görmediği iki habere de anlam üretti.
            /// </summary>
            [JsonPropertyName("BaslikSayisi")]
            public int Count { get; set; }

            // TopType/Signals (kanıt TİPLERİ) da bilerek taşınmaz — Notes'taki Category ile
            // aynı hatalı taksonomiden gelirler ve modeli yanlış yönlendirirler.

            /// <summary>
            /// Kullanılabilir kanıt kayıtları: kategori + kaynak kalitesi + güven + zaman +
            /// TEMİZLENMİŞ kısa başlık. LLM bunları GERÇEK bilgi olarak kullanabilir; kanıtın
            /// güvenilirliğine yeniden karar vermez. Süzgeci geçemeyen kayıt hiç taşınmaz.
            /// </summary>
            public List<EvidenceNote> Notes { get; set; } = new();

            // HAM BAŞLIK BİLEREK TAŞINMAZ. Ölçüldü (13.08 maçları): kapılardan geçen
            // başlıklar "Ajax - Shelbourne 3-1 Samenvatting" ve "…Predictions, Picks &
            // Odds" gibi SKOR ve BAHİS metni içeriyor. Bunlar pack'e girerse LLM başka
            // bir maçın skorunu bu maçın verisi sanar (llama3.2 testindeki uydurma skorun
            // kaynağı tam olarak budur) ve kumar dili prompt'a sızar. Kanıt, tip düzeyinde
            // (SignalExtractor'ın zaten ürettiği sinyal) taşınır; ham metin taşınmaz.
        }

        public sealed class EvidenceNote
        {
            // KATEGORİ BİLEREK TAŞINMAZ. Ölçüldü: SignalExtractor "has no doubts"u Injury,
            // "final training session"ı Weather etiketliyor; model etiketi olgu sanıp
            // "sakatlık ve hava koşullarıyla ilgili haberler" yazdı. Etiket yerine yalnız
            // temizlenmiş metin verilir; anlamı metnin kendisi taşır.
            //
            // KAYNAK KALİTESİ/GÜVEN SAYILARI DA GİTMEZ: bunlar okuma kapısının İÇ ölçüleridir,
            // modelin kararına girmez (kapıyı geçen kayıt zaten kullanılabilirdir) ve metne
            // sızabilecek iki ham sayı daha demektir.
            [JsonIgnore] public int SourceQuality { get; set; }
            [JsonIgnore] public int Confidence { get; set; }

            [JsonPropertyName("Tarih")]
            public string PublishedUtc { get; set; } = "";

            /// <summary>Haberin BAŞLIĞI.</summary>
            [JsonPropertyName("Baslik")]
            public string Text { get; set; } = "";

            /// <summary>
            /// Bu gelişmeyi kaç FARKLI yayıncı doğruladı. Aynı olay 5 sitede çıktıysa 5 ayrı
            /// haber değil, KaynakSayisi=5 olan TEK olaydır (backend tekilleştirdi).
            /// </summary>
            [JsonPropertyName("KaynakSayisi")]
            public int SourceCount { get; set; } = 1;

            /// <summary>Gelişme hangi takımla ilgili (tek takım gelişmesiyse dolu, yoksa gönderilmez).</summary>
            [JsonPropertyName("Takim")]
            public string? Team { get; set; }

            /// <summary>O takımın bu maçtaki rakibi.</summary>
            [JsonPropertyName("Rakip")]
            public string? Opponent { get; set; }

            /// <summary>
            /// Olayın öznesi olan oyuncu. Backend bu oyuncuyu "Takim" alanındaki takıma
            /// bağlamıştır; başka bir takıma bağlanamaz. Boşsa gönderilmez.
            /// </summary>
            [JsonPropertyName("Oyuncu")]
            public string? Player { get; set; }

            /// <summary>Zaman konumu: MaçÖncesi / MaçGünü. Eski içerik pack'e hiç gelmez.</summary>
            [JsonPropertyName("Zaman")]
            public string? Timing { get; set; }

            /// <summary>Olayın öznesi teknik direktörse adı. Boşsa gönderilmez.</summary>
            [JsonPropertyName("TeknikDirektor")]
            public string? Coach { get; set; }

            /// <summary>
            /// FUTBOL OLAY TÜRÜ — backend kararı (Transfer / Sakatlık / Kadro / İlk 11 /
            /// Teknik Direktör Açıklaması / …). Olayın ne olduğu buradadır; metinden
            /// yeniden çıkarılmaz.
            /// </summary>
            [JsonPropertyName("Tur")]
            public string? EventType { get; set; }

            /// <summary>Olayın maç açısından önemi: Yüksek / Orta / Düşük.</summary>
            [JsonPropertyName("Onem")]
            public string? Importance { get; set; }

            /// <summary>
            /// Haberin GERÇEK kısa içeriği — provider snippet'i, birebir taşınır. Yoksa alan
            /// hiç gönderilmez (null): o zaman elde yalnız başlık vardır ve başlıktan içerik
            /// üretilemez. Başlığın yankısı olan sahte "özet"ler bu alana GİRMEZ.
            /// </summary>
            [JsonPropertyName("Ozet")]
            public string? Summary { get; set; }
        }

        public sealed class ReasonedSignal
        {
            // İngilizce teknik ad ("High Attack Tempo") ve 0–100 skorlar prompt'a TAŞINMAZ:
            // ölçüldü, model bunları kullanıcıya olduğu gibi gösteriyordu. Yalnız Türkçe
            // okuma cümlesi gider.
            [JsonIgnore] public string Name { get; set; } = "";
            [JsonIgnore] public int Strength { get; set; }
            [JsonIgnore] public int Confidence { get; set; }

            [JsonPropertyName("Okuma")]
            public string Evidence { get; set; } = "";
        }

        public sealed class EvidenceItem
        {
            public string Label { get; set; } = "";
            public string Value { get; set; } = "";
        }

        /// <summary>Bir takımın gerçek puan durumu satırı (backend'den birebir).</summary>
        public sealed class StandingRow
        {
            public string Takim { get; set; } = "";
            public int Sira { get; set; }
            public int Oynanan { get; set; }
            public int Galibiyet { get; set; }
            public int Beraberlik { get; set; }
            public int Maglubiyet { get; set; }
            public int AttigiGol { get; set; }
            public int YedigiGol { get; set; }
            public int Puan { get; set; }
        }

        public sealed class StandingsBlock
        {
            public StandingRow? EvSahibi { get; set; }
            public StandingRow? Deplasman { get; set; }
        }

        /// <summary>Geçmiş karşılaşma dökümü — backend'in saydığı değerler.</summary>
        public sealed class H2HBlock
        {
            public int Toplam { get; set; }

            /// <summary>
            /// Toplamlar TAKIM ADIYLA verilir. "EvSahibiGalibiyeti" adı belirsizdi: hem
            /// bugünkü ev sahibinin galibiyeti hem de geçmiş maçlarda ev sahibi olanın
            /// galibiyeti diye okunabiliyordu.
            /// </summary>
            [JsonPropertyName("GalibiyetSahibi")]
            public string HomeTeamName { get; set; } = "";
            [JsonPropertyName("GalibiyetSayisi")]
            public int EvSahibiGalibiyeti { get; set; }

            [JsonPropertyName("RakipAdi")]
            public string AwayTeamName { get; set; } = "";
            [JsonPropertyName("RakipGalibiyetSayisi")]
            public int DeplasmanGalibiyeti { get; set; }

            public int Beraberlik { get; set; }

            /// <summary>
            /// HAZIR CÜMLE — toplamlar backend'de cümleye çevrilir, model saymaz.
            /// Ölçüldü: "Son5" hazır cümle olduğu için form sayılarında HİÇ hata çıkmadı;
            /// H2H'de hazır cümle olmadığı için model 2 beraberliği "son üç maç berabere"
            /// diye yazdı. Aynı çözüm buraya da uygulanır: anlatıda bu cümledeki sayılar
            /// AYNEN kullanılır.
            /// </summary>
            [JsonPropertyName("Ozet")]
            public string Summary { get; set; } = "";

            /// <summary>
            /// Son karşılaşmaların GERÇEK sonuçları. Buradaki skorlar backend kaydıdır ve
            /// anlatıda kullanılabilir; bu listede olmayan hiçbir skor kullanılamaz.
            /// Kayıt yoksa alan gönderilmez.
            /// </summary>
            [JsonPropertyName("SonKarsilasmalar")]
            public List<H2HResultRow>? RecentResults { get; set; }
        }

        public sealed class ScenarioInsight
        {
            public string Market { get; set; } = "";
            public int Probability { get; set; }
            public string Confidence { get; set; } = "";
            public string Reason { get; set; } = "";
        }

        private static readonly JsonSerializerOptions PromptJson = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public string ToPromptJson() => JsonSerializer.Serialize(this, PromptJson);
    }
}
