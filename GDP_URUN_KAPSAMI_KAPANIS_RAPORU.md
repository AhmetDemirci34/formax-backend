# GDP — Ürün İhtiyacına Göre Değerlendirme ve Kapanış Raporu

**Tarih:** 2026-07-12
**Kapsam:** GDP'yi capability listesine göre değil, FORMAX'ın **Radar Engine**, **Recommendation Engine** ve **Match Intelligence** katmanlarının veri ihtiyacına göre değerlendirmek; ücretsiz+güvenilir provider'la kapatılabilen boşlukları mevcut generic pipeline'a entegre etmek; kapatılamayanları bilinçli kapsam dışı olarak raporlamak.

---

## 1. Yöntem

Değerlendirme "GDP hangi capability'lere sahip?" sorusuyla değil, **"Üç motor karar anında hangi veriyi okumak istiyor ve GDP bunu verebiliyor mu?"** sorusuyla yapıldı. Referans, motorların kod düzeyindeki okuma sözleşmeleri (`PreMatchContext`, `SquadContext`, `LiveMatchContext`, `MatchEvidenceRecords`) alındı.

---

## 2. Bulgular

### 2.1 GDP bugün gerçekte ne üretiyor
Kanonik çıktı `NormalizedFixture`: takım, lig, sezon, tur, mekan, kickoff, durum, skor. Yan store'lar: hava (OpenMeteo/MetNorway), mekan-coğrafya (OSM/Wikidata/Wikipedia), haber (GDELT/RSS), event-istatistik (StatsBomb).

### 2.2 Kritik yapısal bulgu — GDP çıktısı tüketilmiyordu
- `GdpMatchLink` / persist edilen GDP metadata'sını Application/Engine/Controller katmanında **okuyan hiçbir tüketici yoktu** (ölü uç).
- Motorlar fixture'dan fakir olan Domain `Matches` tablosundan okuyordu (tur/mekan/sezon yok).
- `PreMatchReadProvider` anlamlı tüm alanları **bilinçli boş bırakıyordu** → Radar'ın maç çerçevesi hep default (League / Low / false) idi.

**Sonuç:** En yüksek değerli boşluklar provider eksikliği değildi; eksik olan **türetme katmanı + GDP→motor tüketim yolu** idi. ("Capability eklemek çözüm değil" teşhisi doğrulandı.)

---

## 3. Öncelikli Boşluk Haritası

| Öncelik | İhtiyaç (motor sözleşmesi) | Durum | Aksiyon |
|---|---|---|---|
| **P0** | CompetitionType, IsElimination, IsFinal, ImportanceLevel | GDP veriyi zaten çekiyor, hesaplamıyor + bağlamıyordu | ✅ **Bu raporla yapıldı** (türetme + bağlama, yeni provider yok) |
| **P1** | Form (son N maç), H2H | Ücretsiz kaynak var: `football-data.co.uk` (GDP'de zaten implemente) | Türetme + tablo işi — sıradaki faz |
| **P1** | Puan durumu (Standings) | Ücretsiz kaynak var: OpenLigaDb (DE) / football-data.org free tier | **Provider seçimi kullanıcı kararı** — bekliyor |
| **P2** | Kadro (LineupsAnnounced), Sakatlık/ceza (HasKeyAbsence) | Güvenilir ücretsiz yapısal kaynak **yok** | ⛔ Bilinçli kapsam dışı |
| **P2** | Canlı dakika/skor/olay (LiveMatchContext) | Güvenilir ücretsiz canlı besleme **yok**, ToS riskli | ⛔ Bilinçli kapsam dışı |
| **P2** | IsDerby | GDP kanonik verisinde takım-şehri/rakiplik yok | ⛔ Bilinçli kapsam dışı (bkz. §5) |
| — | Match Intelligence: haberden sinyal/kanıt | GDELT/RSS ile **zaten karşılanıyor** | Aksiyon gerekmez |

---

## 4. Yapılan İş (P0) — Türetme + Bağlama

Yeni provider **eklenmedi** (provider seçimi kullanıcının mimari kararıdır). Yalnızca GDP'nin zaten çektiği veriden çerçeve türetildi ve motora bağlandı.

**Değişen/eklenen dosyalar (`exciting-antonelli-83d4af` worktree):**
1. `Formax.Application/AI/Contexts/MatchFraming.cs` — türetilen çerçeve değeri (yeni).
2. `Formax.Application/AI/Contexts/MatchFramingDeriver.cs` — saf, çok dilli türetme fonksiyonu (yeni).
3. `Formax.Infrastructure/Persistence/GdpMatchMetadata.cs` — `GdpMatchLink.Round` alanı eklendi.
4. `Formax.Infrastructure/Data/FormaxDbContext.cs` — `Round` kolon konfigürasyonu (maxlen 64).
5. `Formax.Infrastructure/Persistence/GdpMatchPersister.cs` — `Round` insert/update + değişiklik takibi.
6. `Formax.Infrastructure/ReadProviders/PreMatchReadProvider.cs` — GDP verisinden `PreMatchContext` doldurma (asıl bağlantı).
7. `Formax.Infrastructure/Migrations/*_AddGdpMatchLinkRound.cs` — tek kolon migration.

**Türetme mantığı (standings olmadan, sahte kesinlik iddia edilmez):**
- `CompetitionType` ← lig adı kalıpları (Champions League / Cup-Pokal-Kupa-Copa / League)
- `IsFinal` / `IsElimination` ← tur etiketi kalıpları (çok dilli, "Achtelfinale"/"Cuartos" gibi final-yanlış-pozitifleri elenmiş)
- `ImportanceLevel` ← (final|eleme → High; CL|Cup → Medium; diğer → Low)
- GDP linki yoksa graceful: yalnızca lig adından türetir.

**Doğrulama (gerçek kanıt):**
- Tüm projeler derleniyor: `Formax.API` + `Formax.Engine` → **0 Hata**.
- Migration model diff'i doğru: `GdpMatchLinks`'e yalnızca tek `Round nvarchar(64)` kolonu.
- Türetme davranışı: 13 senaryoluk doğruluk tablosu → **13/13 PASS** (League, CL grup/eleme/final, DFB Pokal Achtelfinale/Finale, Türkiye Kupası Yarı Final, Copa del Rey Cuartos, FA Cup Semi-Final, Coppa Italia Quarter-Final, null/boş graceful).

> Not: Canlı SQL Server'a `database update` uygulanmadı (bu ortamda DB erişimi yok). Migration üretimi ve model doğrulaması yapıldı; deploy adımında `dotnet ef database update` çalıştırılmalıdır.

---

## 5. Bilinçli Kapsam Dışı (Sahte Çözüm Üretilmedi)

| Veri | Neden kapsam dışı |
|---|---|
| **Kadro / son 11** | Güvenilir, ücretsiz, yapısal kadro API'si yok. Kazımaya dayalı kaynaklar kırılgan ve ToS riskli. |
| **Sakatlık / ceza** | Aynı — güvenilir ücretsiz yapısal kaynak yok. Uydurma "eksik oyuncu" sinyali güven skorunu zehirler. |
| **Canlı dakika/skor/olay** | Ücretsiz canlı besleme güvenilmez ve çoğu ToS'a aykırı. |
| **IsDerby** | GDP kanonik verisinde takım-şehri/koordinatı yok (`NormalizedTeam` şehir taşımıyor; `NormalizedVenue` yalnız ev sahibi şehri). Rakiplik listesi veya takım-coğrafya verisi olmadan türetmek uydurma olur. |

Bu alanlar için altyapı (interface + tablo) şemada mevcut ama **besleyen ücretsiz kaynak yok**; ücretli provider bir kullanıcı kararıdır.

---

## 6. Sıradaki Fazlar (Karar Kullanıcının)

- **P1-a — Form & H2H:** `football-data.co.uk` tarihsel sonuçlarından türet (yeni bağımlılık yok, zaten implemente). En yüksek sıradaki değer.
- **P1-b — Standings:** provider seçimi bekliyor (football-data.org free tier vs. yalnız OpenLigaDb). `ImportanceLevel`'i rafine eder.
- **P0+ — SquadContext bağlama:** kadro verisi ücretsiz gelmediğinden şimdilik kapsam dışı; ücretli kaynak kararına bağlı.
