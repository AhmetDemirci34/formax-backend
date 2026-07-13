# FORMAX GDP — Provider Strategy

> **Girdi:** [PROVIDER_CATALOG.md](PROVIDER_CATALOG.md)
> **Durum:** Tavsiye dokümanı. Hiçbir provider seçilmedi/eklenmedi, hiçbir kod yazılmadı.
> **Bağlam:** Çok-provider mimarisi [[project_data_engine_v1]] · Conflict kriterleri FAZ 5 (`ConflictCandidate`).

## 0. Değiştirilemez Kurallar

Bu strateji aşağıdaki **kesin ve değiştirilemez** kurallara göre kurulmuştur:

1. **FORMAX hiçbir ücretli provider'a bağımlı olmayacak.**
2. **Freemium provider'lar sistemin omurgası olamaz.**
3. **Ücretli veya freemium provider'lar yalnızca opsiyonel eklenti olabilir.**
4. **Ana mimari tamamen ücretsiz ve açık kaynaklarla çalışabilmelidir.**

**Sonuç:** Doküman iki bölümdür — **§1 Core Strategy (zorunlu, yalnızca ücretsiz+açık)** ve **§2 Optional Strategy (opsiyonel, freemium/ücretli)**. Core tek başına çalışır. Core içinde **hiçbir** freemium/ücretli kaynak yoktur. Optional'ın tamamı devre dışı bırakıldığında sistem Core ile — §1.8'deki tanımlı sınırlarla — çalışmaya devam eder.

---

# 1. FORMAX Core Strategy (ZORUNLU)

**Yalnızca ücretsiz + açık + anahtarsız/açık-lisans kaynaklar.** Sistemin omurgası budur.

## 1.1 Core Provider Seti

| Provider | Erişim | Anahtar | Kapsadığı türler (Core rolü) |
|---|---|---|---|
| **OpenLigaDB** | Açık API | Yok | Fixture, Standings, Team, kısmi Live (DE + seçili ligler) |
| **Football-Data.co.uk** | Açık CSV | Yok | H2H, tarihsel Fixture/Standings, sonuçlar |
| **StatsBomb Open Data** | Açık JSON | Yok | Statistics, Lineup, Player (tarihsel, seçili turnuvalar) |
| **Wikidata** | Açık SPARQL/REST | Yok | Team, Player, Coach, Venue, Referee metadata; kimlik |
| **Wikipedia REST** | Açık API | Yok | Metadata/içerik zenginleştirme |
| **OpenStreetMap / Nominatim** | Açık API | Yok | Venue koordinatı (→ Weather zinciri) |
| **Open-Meteo** | Açık API | Yok | Weather (birincil) |
| **MET Norway (Yr)** | Açık API | Yok | Weather (ikincil) |
| **Meteostat** | Açık | Kısmi | Weather (tarihsel, fallback) |
| **RSS Feeds** (BBC/Sky/Guardian/lig) | RSS | Yok | News (birincil) |
| **GDELT** | Açık API | Yok | News (global, ikincil) |

> Tümü ücretsiz + açık + resmi/açık-veri. Resmi-olmayan (ESPN/SofaScore/FotMob) ve scraping (FBref/Transfermarkt) kaynakları — ücretsiz olsalar da — **Core'a alınmaz** (ToS/stabilite riski, "açık" değil). Bunlar §3 Risk Ek'inde ayrıca ele alınır.

## 1.2 Refresh Interval Volatilite Katmanları

| Katman | Doğa | Türler | Aralık |
|---|---|---|---|
| T0 | Canlı | Live, in-play Statistics | 30–60 sn |
| T1 | Maç-öncesi dinamik | Lineup | T-60dk'da 5–15 dk |
| T2 | Gün-içi | News / Injuries / Suspensions / Transfers | News 5–15 dk · diğerleri 6–12 s |
| T3 | Maç-sonrası | Standings, final Statistics, H2H | 1–6 s |
| T4 | Yarı-statik | Fixture | 12–24 s; matchday 1 s |
| T5 | Statik | Team, Player, Coach, Venue, Referee | 1–7 gün |
| Wx | Hava | Weather | 1–3 s; KO'ya <3s kala 30–60 dk |

> **Öncelik uygulaması:** Tür-bazlı öncelik, Conflict Engine'de `ConflictCandidate.ProviderPriority` ile atanır (manifest tekil priority değişmez).

## 1.3 Core — Güçlü Kapsanan Türler (tam çalışır)

Bu türlerde Core **global ve yeterli**:

### News
- **Primary:** RSS Feeds (90) · **Secondary:** GDELT (70) · **Fallback:** — 
- **Refresh:** T2 5–15 dk · **Fallback sırası:** RSS → GDELT
- **Conflict:** SourceCount + LastUpdated (çok-kaynak teyit; dedup [[project_data_engine_v2_news]]).

### Weather
- **Primary:** Open-Meteo (90) · **Secondary:** MET Norway (70) · **Fallback:** Meteostat (50)
- **Refresh:** Wx 1–3 s; KO'ya yakın 30–60 dk · **Fallback sırası:** Open-Meteo → MET Norway → Meteostat
- **Bağımlılık:** Venue koordinatı (OSM/Wikidata) · **Conflict:** LastUpdated (en taze tahmin) → ProviderPriority.

### H2H
- **Primary:** Football-Data.co.uk (90) · **Secondary:** OpenLigaDB (40, DE) · **Fallback:** —
- **Refresh:** T3/on-demand · **Fallback sırası:** FD.co.uk → OpenLigaDB
- **Conflict:** Tarihsel derinlik + SourceCount.

### Venue
- **Primary:** Wikidata (80, metadata/kapasite) · **Secondary:** OpenStreetMap/Nominatim (70, koordinat) · **Fallback:** OpenLigaDB (DE)
- **Refresh:** T5 statik · **Fallback sırası:** Wikidata → OSM → OpenLigaDB
- **Conflict:** ProviderPriority; koordinatta OSM otoritesi.

## 1.4 Core — Bölgesel/Tarihsel Kapsanan Türler (kısmi ama sağlam)

Bu türlerde Core **belirli bölge/tarih kapsamında** çalışır; global-güncel için §2 gerekir.

### Fixture
- **Primary:** OpenLigaDB (90, DE + seçili ligler) · **Secondary:** Football-Data.co.uk (40, tarihsel) · **Fallback:** —
- **Refresh:** T4 12–24 s; matchday 1 s · **Fallback sırası:** OpenLigaDB → FD.co.uk
- **Core kapsamı:** DE + seçili ligler güncel; global tarihsel. **Global-güncel fikstür → §2.**

### Standings
- **Primary:** OpenLigaDB (90, DE) · **Secondary:** Football-Data.co.uk (40, türetilmiş) · **Fallback:** —
- **Refresh:** T3 1–6 s · **Fallback sırası:** OpenLigaDB → FD.co.uk
- **Core kapsamı:** DE + türetilebilir. **Global-güncel → §2.**

### Team
- **Primary:** OpenLigaDB (85, DE) · **Secondary:** Wikidata (70, metadata) · **Fallback:** StatsBomb (tarihsel)
- **Refresh:** T5 · **Fallback sırası:** OpenLigaDB → Wikidata → StatsBomb
- **Core kapsamı:** DE güncel + global metadata. **Global-güncel kadro detayı → §2.**

### Statistics
- **Primary:** StatsBomb Open (80, tarihsel derin) · **Secondary:** OpenLigaDB (50, gol/olay) · **Fallback:** —
- **Refresh:** final T3 1–6 s · **Fallback sırası:** StatsBomb → OpenLigaDB
- **Core kapsamı:** Tarihsel derin + DE gol/olay. **Canlı/global-güncel istatistik → §2.**

### Lineup
- **Primary:** StatsBomb Open (80, tarihsel) · **Secondary:** — · **Fallback:** —
- **Refresh:** tarihsel statik · **Core kapsamı:** yalnızca tarihsel/seçili turnuva.
- **Canlı/güncel kadro → §2 (ya da §3 risk).**

### Player
- **Primary:** Wikidata (70, metadata) · **Secondary:** StatsBomb (60, tarihsel) · **Fallback:** Wikipedia (50)
- **Refresh:** T5 · **Fallback sırası:** Wikidata → StatsBomb → Wikipedia
- **Core kapsamı:** Kimlik/metadata + tarihsel. **Güncel sezon kadrosu → §2.**

### Coach / Referee (yalnızca metadata)
- **Primary:** Wikidata (60) · **Secondary:** Wikipedia (40) · **Fallback:** —
- **Refresh:** T5 · **Core kapsamı:** yalnızca kimlik/metadata. **Maç ataması (referee) / güncel görev (coach) → §2.**

## 1.5 Core — Kapsam DIŞI Türler (dürüst boşluk)

Aşağıdaki türler için **ücretsiz+açık+resmi/yapısal** kaynak yoktur. Core bunları **kapsayamaz**:

| Tür | Core durumu | Karşılanması |
|---|---|---|
| **Live** (global) | ❌ Yalnızca OpenLigaDB DE polling (zayıf) | §2 (freemium) veya §3 (risk) |
| **Injuries** | ❌ Yok | §2 (freemium) veya §3 (risk) |
| **Suspensions** | ❌ Yok | §2 (freemium) veya §3 (risk) |
| **Transfers** | ❌ Yapısal yok (Wikidata kısmi tarih) | §2 (freemium) veya §3 (risk) |

> Bu boşluklar **kabul edilmiş gerçektir**: kesin kurallar gereği Core ücretli/freemium içeremez. Bu türler ancak §2 opsiyonel eklenti *etkinleştirilirse* ya da §3 risk kabul edilirse gelir. Etkinleştirilmezse FORMAX bu türler olmadan çalışır.

## 1.6 Live — Core'un sınırlı çözümü
- **Primary:** OpenLigaDB (60, yalnız DE polling) · **Secondary/Fallback:** —
- **Refresh:** T0 30–60 sn (DE) · **Core kapsamı:** yalnızca DE + seçili ligler.
- **Global canlı Core'da yoktur** → §2 (freemium) veya §3 (risk).

## 1.7 Core Conflict Önceliği (free-first)

FAZ 5 kriter zinciri, Core içinde:
1. **ManualOverride** — operatör değeri her zaman kazanır.
2. **ProviderPriority** — bu bölümdeki değerler (bölgesel free-Primary önce).
3. **SourceCount** — News/H2H/Transfers'ta çok-kaynak teyidi.
4. **LastUpdated** — Live/Lineup/Weather/in-play Stat'ta tazelik belirleyici.
5. **ProviderConfidence** — son ayraç.

## 1.8 Core Tek Başına Kapsam Özeti

| Kategori | Core (yalnız) durumu |
|---|---|
| ✅ Tam & global | News, Weather, H2H, Venue (metadata/koordinat) |
| 🟡 Bölgesel/tarihsel | Fixture, Standings, Team, Statistics, Lineup, Player, Coach/Referee (metadata) |
| ❌ Boşluk | Live (global), Injuries, Suspensions, Transfers, Referee ataması |

**Core tek başına çalışır** — News + Weather + tarihsel/metadata + DE canlı-öncesi veriyle FORMAX işleyebilir. Global-güncel maç akışı ve dinamik türler için §2 opsiyoneldir.

---

# 2. Optional Strategy (OPSİYONEL)

**Yalnızca freemium/ücretli eklentiler. Hiçbiri omurga değildir; hiçbiri zorunlu değildir.** Tamamı devre dışıyken sistem §1 Core ile çalışır. Etkinleştirildiklerinde §1.5 boşluklarını kapatır ve bölgesel türleri global-güncele genişletir.

## 2.1 Optional — Freemium Eklentiler (B1)

| Provider | Sınıf | Genişlettiği türler | Not |
|---|---|---|---|
| **API-Football** | Freemium | Live, Fixture(global), Standings(global), Lineup, Statistics, Injuries, Suspensions, Transfers, Player, Coach, Venue, Referee | Tek eklentiyle en geniş boşluk kapatma |
| **football-data.org** | Freemium | Fixture(global-üst lig), Standings, H2H, Team | API-Football'a bağımsız alternatif |
| **SportMonks** | Freemium | Yukarıdakiler + fixture Weather + sidelined(Inj/Susp) | API-Football'a alternatif (ikisi birden değil) |

> **Kural gereği:** Bu sağlayıcılar **omurga olamaz** — yalnızca Core üzerine *eklenti* olarak açılır. Free-first: Core bir veriyi kapsıyorsa Core kazanır; freemium yalnızca Core'un **boş** olduğu yerde ya da operatör açıkça öncelik verdiğinde devreye girer.

## 2.2 Optional — Ücretli Eklentiler (B2)

| Provider | Sınıf | Genişlettiği türler | Not |
|---|---|---|---|
| **Sportradar** | Ücretli | Tümü (premium canlı doğruluk) | Kurumsal; yalnızca premium gerekirse |
| **Opta / Stats Perform** | Ücretli | Tümü + gelişmiş metrik (xG vb.) | En derin; en pahalı |
| **Genius Sports** | Ücretli | Live/Stat/Stand/Lineup (resmi/bahis) | Resmi veri hakları |

> Ücretli katman **asla zorunlu değildir**. Yalnızca premium canlı doğruluk/gelişmiş metrik ihtiyacı kesinleşirse opsiyonel açılır.

## 2.3 Optional — Boşluk Kapatma Haritası (etkinleştirilirse)

| Tür (Core durumu) | Optional Primary-when-enabled | Priority (Core'un altında) | Refresh |
|---|---|---|---|
| Live (❌ global) | API-Football (freemium) | 70 | T0 30–60 sn |
| Fixture (🟡 DE) | API-Football / football-data.org | 70 / 65 | T4 12 s |
| Standings (🟡 DE) | API-Football / football-data.org | 68 / 65 | T3 1–6 s |
| Lineup (🟡 tarihsel) | API-Football | 72 | T1 5–15 dk |
| Statistics (🟡) | API-Football | 70 | T0/T3 |
| Injuries (❌) | API-Football / SportMonks | 70 | T2 6–12 s |
| Suspensions (❌) | API-Football / SportMonks | 65 | T2 6–12 s |
| Transfers (❌) | API-Football | 68 | T2 1–24 s |
| Referee ataması (❌) | API-Football (kısmi) | 65 | T4 |
| Player (güncel) | API-Football | 70 | T5 |

> **Priority mantığı:** Optional değerleri (65–72) Core free-Primary değerlerinin (80–92) **altındadır** → aynı veri hem Core'da hem Optional'da varsa **Core (ücretsiz) kazanır**. Optional yalnızca Core'un boş olduğu yerde fiilen belirleyici olur. Bu, "freemium omurga olamaz" kuralının conflict katmanındaki uygulamasıdır.

## 2.4 Core + Optional Birleşik Conflict Önceliği

`ManualOverride > (Core ProviderPriority) > (Optional freemium) > (Optional ücretli) > Confidence`

- Ücretli veri daha doğru olsa da **varsayılan** olarak Core-first kalır (bağımsızlık kuralı). Operatör, belirli bir türde Optional'a açıkça öncelik verebilir — bu bir **opt-in yapılandırmadır**, varsayılan değildir.
- Optional tümüyle kapalıyken bu zincir doğal olarak yalnızca Core'a iner.

---

# 3. Risk Ek'i — Ücretsiz ama Resmi-Olmayan/Scraping (Core değil, Optional değil)

Bu kaynaklar **ücretsizdir ama açık/resmi değildir** (ToS/stabilite riski). Ne Core omurgasına ne de freemium/ücretli Optional'a girerler; yalnızca **kullanıcı riski açıkça kabul ederse** ayrı bir katman olarak düşünülebilir.

| Kaynak | Tür | Risk |
|---|---|---|
| ⚠️ ESPN (unofficial) | Live, News, Fixture, Stat, Standings | Resmi değil; habersiz kesilebilir |
| ⚠️ SofaScore / FotMob | Live, Lineup, Stat, H2H | Resmi değil; anti-scraping |
| ⚠️ FBref | Derin Statistics | Scraping ToS |
| ⚠️ Transfermarkt | Transfers, Injuries, Suspensions, Coach | Scraping ToS (bu boşluklar için en zengin içerik) |
| ⚠️ PhysioRoom | Injuries | Editoryal; yapısal API yok |

> Bu katman §1.5 boşluklarını (Live/Injuries/Susp/Transfers) **ücretsiz** ama **riskli** biçimde kapatabilir. Karar tamamen kullanıcıya aittir; varsayılan **kapalı**dır.

---

# 4. Kullanıcı Kararı Gereken Noktalar

1. **§1.5 boşlukları (Live global, Injuries, Suspensions, Transfers):** Core'da kalıp boş mu bırakılsın, §2 freemium ile mi, yoksa §3 riskle mi kapatılsın?
2. **Freemium eklenti seçimi:** API-Football **veya** SportMonks (ikisi birden değil) — hangisi?
3. **Ücretli katman:** Hiç değerlendirilecek mi, yoksa tamamen dışarıda mı?
4. **Risk katmanı (§3):** Açıkça reddedilecek mi, yoksa belirli türlerde kabul mü?
5. **Core-first öncelik:** Herhangi bir türde Optional'a opt-in öncelik verilecek mi?

# 5. Sonraki Adım

Bu doküman **tavsiye**dir. Onay sonrası yalnızca **Core** zorunlu olarak, **Optional** ise açıkça onaylanırsa manifest'e tanımlanır — bu Priority/Refresh/Fallback değerleriyle. **Hiçbir provider eklenmedi; hiçbir kod yazılmadı.**
