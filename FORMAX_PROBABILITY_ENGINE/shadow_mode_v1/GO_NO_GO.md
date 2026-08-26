# FORMAX — FINAL SHADOW OBSERVATION / GO-NO-GO

**Tarih:** 2026-08-22 · **Saat:** 13:45 UTC
**Model:** DEĞİŞTİRİLMEDİ — `INDEPENDENT_POISSON_V2` / `TEAM_STRENGTH_V2` / `GATE_V1` / `NONE`
**Model parmak izi:** `94FC079D00190B341B2AEB6F0A54712E` (tüm koşularda aynı)

---

# `PRODUCTION_SHADOW_NOT_READY`

§12'nin 9 zorunlu maddesinden **7'si PASS**, **2'si FAIL**.

İki eksik de **zamana bağlıdır, kusura değil**: tahmin edilen ilk maç 15:00 UTC'de başlıyor,
şu an 13:45 UTC. Bu oturumda ne bir maç haftası geçirilebilir ne de bir maç bitebilir.

---

## GO / NO-GO TABLOSU (§12)

| # | Zorunlu kriter | Durum | Kanıt |
|---|---|---|---|
| 1 | en az 1 gerçek maç haftası | **FAIL** | Gölge penceresi 22 Ağu – 3 Eyl; bugün 22 Ağu 13:45 UTC |
| 2 | en az 1 gerçek settlement | **FAIL** | Settled = 0; hiçbir tahmin edilen maç henüz başlamadı |
| 3 | model regression MATCH | **PASS** | 4/4 segment, sapma ≤ 4,4e-9 |
| 4 | EvidenceCutoff violation = 0 | **PASS** | 80/80 satır temiz |
| 5 | immutable violation = 0 | **PASS** | UPDATE/DELETE canlı denendi, trigger reddetti |
| 6 | duplicate prediction = 0 | **PASS** | 80 satır / 80 benzersiz id |
| 7 | orphan settlement = 0 | **PASS** | 0 |
| 8 | unexplained production error = 0 | **PASS** | 0 açıklanamayan; bulunan 3 hata açıklandı ve düzeltildi |
| 9 | /detail shadow regression yok | **PASS** | A/B ölçüldü, gölge AÇIK her metrikte daha hızlı |

Competition type eksikliği §12 gereği READY engeli değil — `NOT_TESTED` olarak raporlandı (§9).

---

## 1. GERÇEK SHADOW HAFTASI — **FAIL** (§1)

Gözlem penceresi geçirilemedi. Bugüne kadar biriken gerçek veriler:

| Ölçüm | Değer |
|---|---|
| Prediction count | **80** (76 farklı maç) |
| Accepted / Rejected | **80 / 0** |
| Duplicate | **0** |
| Job errors | **0** |
| Job restarts | 4 (kontrollü: API 3 kez, hosttest 1 kez) |
| Latency (maç başına) | avg 0,13–1,37 ms · p95 0,28 ms |
| Job cycle | 1.936 – 5.158 ms |
| DB insert failures | **0** (1 çakışma reddi — §11, beklenen) |
| Missing predictions | **0** — kapsamdaki her maç tahmin aldı |

Tarih aralığı: **2026-08-22 … 2026-09-03**.

> 80 satır / 76 maç: dört maç ikişer tahmin taşıyor. Hata değil — o maçların takımları arada
> oynadı, `EvidenceCutoff` 2026-05-23'ten 2026-08-21'e taşındı, dolayısıyla **yeni** `PredictionId`
> ve **yeni satır** yazıldı; eski satır olduğu gibi duruyor. Sözleşmenin "düzeltme değil, yeni
> tahmin" kuralının canlı kanıtı.

---

## 2. GERÇEK SETTLEMENT — **FAIL** (§2)

**Settled = 0.** §2'nin kendi kuralı gereği bu tek başına `NOT_READY` demektir.

| | |
|---|---|
| Tahmin edilen maçlardan başlamış olan | **0** |
| Bunlardan bitmiş olan | **0** |
| İlk kickoff | **2026-08-22 15:00 UTC** (1 saat 15 dk sonra) |
| İlk sonucun beklenmesi | ~17:00 UTC |

Mekanizma **sentetik satırlarla** tam olarak doğrulandı (`eftest`): settlement INSERT kabul,
ikinci settlement PK ihlaliyle red, settlement sonrası tahmin olasılığı ve ConfidenceClass sabit.
Ama gerçek bir maç sonucuyla **çalıştırılmadı**.

Settlement job 3 saatte bir koşuyor; gölge açık bırakılırsa ilk gerçek settlement bu akşam
kendiliğinden gerçekleşecek. Doğrulanması gereken: `ActualResult` doğru mu, tahmin satırı değişti
mi, LogLoss/Brier/RPS hesaplanabiliyor mu.

---

## 3. HOSTED JOB — **PASS** (§3)

Tam dizi gerçek yaşam döngüsüyle çalıştırıldı: **startup → cycle → prediction → shutdown →
restart → cycle → shutdown**. `hosttest`, **11/11 PASS**:

```
[SHADOW] Job started. Horizon 8d, startup delay 00:00:02, loop 00:00:18.
[SHADOW] cycle done in 3774 ms — inserted 0, already published 65, conflicted 0, failed 0
[SHADOW] cycle done in 1936 ms — inserted 0, already published 65, conflicted 0, failed 0
[SHADOW] Job stopped gracefully.
[SETTLEMENT] Job stopped gracefully before its first cycle.
--- restart ---
[SHADOW] Job started. …
[SHADOW] cycle done in 1983 ms — inserted 0, already published 65, conflicted 0, failed 0
[SHADOW] Job stopped gracefully.
```

Ayrıca gerçek `Formax.API` host'unda da koştu (port 5063): 2 cycle, idempotent, hata yok.

**Graceful shutdown PASS** — ve bu, bu fazda düzeltilen bir hatanın sonucu (§7).

---

## 4. IMMUTABILITY — **PASS** (§4)

Gerçek SQL Server, gerçek gölge satırı üzerinde:

| İşlem | Sonuç | Mekanizma |
|---|---|---|
| INSERT (tek satır) | **PASS** | `Sequence` geri döndü |
| INSERT (toplu) | **PASS** | 3 satır, sequence'lar döndü |
| UPDATE | **REJECT** | `TR_Predictions_NoUpdate` — "Predictions is INSERT-ONLY…" |
| DELETE | **REJECT** | `TR_Predictions_NoDelete` — "Predictions is append-only…" |
| duplicate publish | **REJECT** | benzersiz `PredictionId` (§11'de canlı yarışla denendi) |
| double settlement | **REJECT** | `PK_PredictionSettlements` ihlali |

UPDATE denemesinden sonra saklanan değerler bit-identical: olasılık `0.67297114`, ContentHash
`EDD4CE3BB593FDF754489CD3FFED5BD9`.

---

## 5. MODEL REGRESSION — **PASS** (§5)

| Segment | N | LogLoss | Referans | Sapma | Durum |
|---|---|---|---|---|---|
| TRAIN | 18.472 | 1,006533 | 1,006533 | 1,6e-9 | **MATCH** |
| VALIDATION | 7.636 | 0,987473 | 0,987473 | 4,4e-9 | **MATCH** |
| TEST | 7.793 | 0,993544 | 0,993544 | 4,3e-9 | **MATCH** |
| FULL | 33.901 | 0,999254 | 0,999254 | 3,2e-9 | **MATCH** |

Sözleşmenin yayımladığı olasılıkların ham modelden sapması: **0,0e+0** (33.203 tahmin).
Referans değerler `model_validation_v2/model_comparison.csv`'den **okunuyor**, elle yazılmıyor.

Ayrıca `prediction_contract_v1` test paketi bu oturumda yeniden koştu: **16/16 PASS**.

> Gölge tahminleri gelecekteki maçlar için üretildiğinden satır satır karşılaştırılamaz. Bunun
> yerine: (a) aynı motor sabit girdiyle referansı birebir üretiyor, (b) canlı gölge koşusundaki
> model parmak izi regresyon koşusuyla aynı. Canlı koşu motora daha güncel kanıt verir (253 yeni
> üretim maçı) — bu model değişikliği değil, aynı modelin güncel veriyle çalışmasıdır.

---

## 6. EVIDENCE CUTOFF — **PASS** (§6)

**80/80 satırda** `EvidenceCutoff < MatchDate`. Tek ihlal yok.

Üç katmanda korunuyor:
1. Servis, her yaklaşan maç TARİHİ için ayrı replay yapar; listeye yalnız bitmiş maçlar ve o
   tarihin maçları girer.
2. `CK_Predictions_EvidencePreMatch` kısıtı GÜN karşılaştırması yapar — aynı gün kanıtı da red.
3. `integrity` komutu her satırı yeniden tarar.

---

## 7. IDENTITY BRIDGE — **PASS** (§7)

`ExternalTeamId → ProviderTeamId → CanonicalTeamId` zinciri **80/80 tahminde** başarılı;
her satır dolu bir `CanonicalMatchId` taşıyor.

`ApiFootballTeamId` kullanılmıyor (9.050 takımın yalnız 4'ünde dolu). Çözülemeyen maç zaten
kapsam dışı bırakılır — uydurma kimlik üretilmez.

---

## 8. /DETAIL — **PASS, regresyon yok** (§8)

Gerçek API host'u, aynı maç (`/api/matches/42844/detail`), her fazda **n=20**, ilk iki ısınma
çağrısı atıldı:

| | n | avg | p50 | p95 | max | min |
|---|---|---|---|---|---|---|
| **Gölge KAPALI** | 20 | 262,3 ms | 238 ms | 307 ms | 553 ms | 218 ms |
| **Gölge AÇIK** (2 cycle koştu) | 20 | **234,4 ms** | **229 ms** | **279 ms** | **281 ms** | 211 ms |
| Fark | | −27,9 ms (−%10,6) | −9 ms | −28 ms | −272 ms | |

**Gölge AÇIK her metrikte daha hızlı.** Bir arka plan işi istekleri hızlandıramaz; bu fark
ölçüm gürültüsüdür. Doğru okuma: **gölge modun /detail üzerinde ölçülebilir bir bozucu etkisi
yok.**

> §8'in dediği gibi "~0,1 sn" mutlak referansı zorunlu kabul edilmedi — ve zaten bu makinede
> gölge tamamen kapalıyken bile üretilemiyor (warm ~262 ms). Kriter "gölge kötüleştirmesin"di;
> ölçüm bunu karşılıyor.

---

## 9. COMPETITION COVERAGE (§9)

**Gölge penceresinde gerçekten bulunan:**

| CompetitionType | Tahmin | Durum |
|---|---|---|
| DOMESTIC_LEAGUE | 44 | **TESTED** |
| UEFA_QUALIFICATION_PLAYOFF | 36 | **TESTED** |

**Bulunmayan:**

| CompetitionType | Durum | Sebep |
|---|---|---|
| UEFA_MAIN | **NOT_TESTED_IN_SHADOW** | Pencerede 0 maç; grup aşaması Eylül'de |
| UEFA_QUALIFIER | **NOT_TESTED_IN_SHADOW** | Pencerede 0 maç; eleme turları bitti |
| DOMESTIC_PLAYOFF | **NOT_TESTED_IN_SHADOW** | Pencerede 0 maç; veri setinde toplam 25 maç |

Confidence dağılımı: HIGH 73 · MEDIUM 6 · LOW 1.

---

## 10. DATA INTEGRITY — **13/13 PASS** (§10)

| Kontrol | Beklenen | Ölçülen |
|---|---|---|
| PredictionId benzersiz | 80 | **80** |
| Sequence benzersiz | 80 | **80** |
| Sequence yayım sırasında kesin artan | evet | **evet** |
| **ContentHash satırdan YENİDEN hesaplandı** | 0 uyumsuzluk | **0 uyumsuzluk** |
| **PredictionId satırdan YENİDEN türetildi** | 0 uyumsuzluk | **0 uyumsuzluk** |
| orphan settlement | 0 | **0** |
| orphan prediction (olmayan maça işaret) | 0 | **0** |
| aynı maç + aynı kanıt kesimi iki kez | 0 | **0** |
| çok tahminli maçlar farklı kanıt kesimi taşıyor | 4 maç, hepsi farklı | **4 maç, hepsi farklı** |
| EvidenceCutoff ihlali | 0 | **0** |
| Olasılık toplam hatası | ≤ 1e-12 | **2,2e-16** |
| Kilitli versiyon damgası taşımayan satır | 0 | **0** |

> `ContentHash` ve `PredictionId`'nin saklanan alanlardan **yeniden hesaplanması** bu fazda
> eklendi. Trigger'lar UPDATE'i engelliyor olsa bile, yanlış yazılmış bir INSERT'i hiçbir trigger
> yakalamaz; satırın yayımlandığı gibi olduğunu ancak bağımsız yeniden hesaplama kanıtlar.

---

## 11. MULTI-INSTANCE — `MULTI_INSTANCE_STATUS = NOT_SUPPORTED_BUT_SAFE` (§11)

**Gerçek bir yarış üretilerek** ölçüldü. İki örneğin de "bu tahmin henüz yok" görmesi için ufuk
genişletildi, sonra iki servis örneği (ayrı DbContext, ayrı scope) **eşzamanlı** koşturuldu:

```
instance A: seen 76, predicted 76, inserted 1,  already published 75, refused 0, failed 0
instance B: seen 76, predicted 76, inserted 0,  already published 75, refused 1, failed 0
rows after: 80  (+1)
```

| Kontrol | Sonuç |
|---|---|
| Örneklerden biri çöktü mü | **hayır** |
| PredictionId hâlâ benzersiz | **evet (80/80)** |
| Aynı maç+kanıt iki kez yazıldı mı | **hayır** |
| Eklenen satır = tek örneğin işi mi | **evet (+1, iki katı değil)** |
| Çakışma veri kaybı olmadan yönetildi mi | **evet** |

**Durum:** dağıtık kilit **yok**. İki örnek aynı işi **tekrar yapar** (CPU israfı) ama benzersiz
`PredictionId` kısıtı sayesinde veriyi **çoğaltamaz**. §11 gereği bu tek başına READY engeli
değil, ayrı bir risk olarak kaydedilmiştir.

---

## 12. BU FAZDA BULUNAN VE DÜZELTİLEN HATALAR

**1. Job'lar graceful kapanmıyordu — host'u düşürebilirdi. (kritik)**
Döngü sonundaki `await Task.Delay(loopDelay, stoppingToken)` `try` bloğunun **dışındaydı**.
Kapanış sinyali vakaların neredeyse tamamında oraya düşer; `OperationCanceledException`
`ExecuteAsync`'ten sızıyor, kapanış log satırına hiç ulaşılmıyordu. .NET 8'in varsayılan
`BackgroundServiceExceptionBehavior.StopHost` davranışıyla bu, host'u düşürme yolu. Hem döngü
beklemesi hem başlangıç gecikmesi iptal-güvenli yapıldı.

**2. Çakışma sonrası log yanlış sayı raporluyordu.**
`Inserted` sayacı `SaveChanges`'ten **önce** artırılıyordu. Eşzamanlılık testinde kaybeden örnek
`inserted 10` diyordu, oysa **0 satır** yazmıştı. Sayaç artık reddedilme hâlinde sıfırlanıyor ve
yazılamayan satırlar ayrı bir alanda (`conflicted`) raporlanıyor. **Bu hatayı §11'in yarış testi
ortaya çıkardı** — tek örnekle asla görülemezdi.

**3. `verify` komutu gerçek gölge verisini kirletiyordu. (önceki turda bulundu)**
Settlement 1:1 kuralını gerçek bir tahmine sahte sonuç iliştirerek sınıyordu. Prob kaldırıldı;
aynı kural artık yalnız `eftest` içinde kendi sentetik satırlarıyla sınanıyor.

Açıklanamayan hata: **0**.

---

## 13. SON KARAR

# `PRODUCTION_SHADOW_NOT_READY`

### Gerçekten kalan engeller (yalnız bunlar)

1. **En az 1 gerçek maç haftası gözlemi.** Gölge açık bırakılmalı ve 7 gün izlenmeli.
   Bugün ölçülen her şey tek oturumluk; bir hafta kota davranışı, gece yeniden başlatmaları,
   uzun süreli bellek profili ve kilit çekişmesi gibi sadece zamanla görünen şeyleri açar.
2. **En az 1 gerçek settlement.** İlk maç bugün 15:00 UTC'de başlıyor, ilk sonuç ~17:00 UTC'de
   beklenir. Settlement job 3 saatte bir koşuyor. Doğrulanacaklar: `ActualResult` doğru, tahmin
   satırı değişmemiş, ikinci settlement reddediliyor, LogLoss/Brier/RPS hesaplanabiliyor.

Bu ikisi dışında **hiçbir teknik engel kalmadı.** Diğer tüm zorunlu kriterler gerçek veritabanı,
gerçek API host'u ve gerçek maçlar üzerinde ölçülerek geçti.

### READY'ye giden yol

Gölge modu açık bırakın (`Predictions:ShadowMode:Enabled = true`). Bir hafta sonra tek komut:

```bash
dotnet run -c Release -- verify      # kabul kriterleri
dotnet run -c Release -- integrity   # veri bütünlüğü
dotnet run -c Release -- report      # settled sayısı + LogLoss/Brier/RPS
```

`report` çıktısında **Settled > 0** ve metrik sütunları dolu geldiğinde, ve bir haftalık
`prediction count / duplicate / error / restart` sayıları temiz olduğunda kriterlerin tamamı
karşılanmış olur.

---

## 14. ÇIKTILAR

| Dosya | İçerik | Kontrol |
|---|---|---|
| `shadow_predictions.csv` | 80 gerçek tahmin, tüm sözleşme alanları | — |
| `shadow_summary.csv` | Genel + CompetitionType + ConfidenceClass | — |
| `shadow_acceptance.csv` | §12 kabul kriterleri, canlı DB | 15/15 |
| `data_integrity.csv` | §10 veri bütünlüğü, hash yeniden hesaplama | 13/13 |
| `multi_instance.csv` | §11 eşzamanlılık yarışı | 5/5 |
| `host_lifecycle.csv` | §3 yaşam döngüsü + restart | 11/11 |
| `ef_trigger_probe.csv` | §4 EF + trigger davranışı | 12/12 |
| `GO_NO_GO.md` | bu dosya | — |
