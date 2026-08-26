# FORMAX — FINAL PREDICTION CONTRACT V1

**Tarih:** 2026-08-21
**Durum:** Build ✅ · 16/16 test ✅ · 18/18 denetim PASS · regresyon 4/4 segmentte MATCH · leakage 0

# `FINAL PREDICTION CONTRACT READY`

**Kapsam:** Preview/araştırma modu. Production DB'ye **yazılmadı** (yalnız migration planı),
mevcut prediction ucuna **bağlanmadı**, frontend değiştirilmedi, Gemma'ya verilmedi. Model
**dokunulmadı**.

---

## 0. SON RAPOR

| | |
|---|---|
| **ModelVersion** | `INDEPENDENT_POISSON_V2` |
| **TeamStrengthVersion** | `TEAM_STRENGTH_V2` |
| **GateVersion** | `GATE_V1` |
| **CalibrationVersion** | `NONE` |
| Accepted count | **33.203** |
| Rejected count | **698** |
| Probability min / max | **0,00691613 / 0,97453436** |
| Probability sum error | **2,2e-16** |
| Regression result | **MATCH — dört segmentin dördü** |
| Leakage result | **0 ihlal** |

Model parmak izi (validated config dosyasının SHA-256'sı) her koşuda başlangıçta alınıyor ve
raporlanıyor; config'e dokunulursa parmak izi değişir ve regresyon düşer.

---

## 1. KİLİTLİ ZİNCİR

```
Validated Team Strength V2  →  Independent Poisson V2  →  RAW probability
                                                              │
                                        NO_CALIBRATION ───────┤   (calibration_v1: NOT_PROVEN)
                                                              │
                                    CURRENT_PREDICTION_GATE ──┤   (gate policy validation: KEEP_CURRENT_GATE)
                                                              │
                                                       PredictionContract
                                                              │
                                              append-only PredictionStore
                                                              │
                                                        Settlement
```

Bu görevde model araştırması yapılmadı. Yeni olan tek şey **sözleşme katmanı**.

---

## 2. CANONICAL DTO

Backend tek bir tip döndürür: **`PredictionContract`**.

```
PredictionId            FMXP + 20 hex, deterministik
MatchId
MatchDate
PredictionTimestamp     UTC
EvidenceCutoff          kullanılabilecek son kanıt tarihi

ModelVersion            INDEPENDENT_POISSON_V2
TeamStrengthVersion     TEAM_STRENGTH_V2
GateVersion             GATE_V1
CalibrationVersion      NONE

HomeProbability         double?  — REJECTED ise NULL
DrawProbability         double?
AwayProbability         double?

PredictionEligible      bool
ConfidenceClass         NONE | LOW | MEDIUMLOW | MEDIUM | HIGH
GateStatus              ACCEPTED | REJECTED
GateReason              "OK" | "NO_TEAM_HISTORY|COMPETITION_NOT_COVERED" | ...

ContentHash             yayım anındaki parmak izi (denetim)
```

* Olasılık toplamı: **1,0 ± 1e-12** (ölçülen: 2,2e-16)
* Olasılık aralığı: **[0, 1]** (ölçülen: [0,00692 · 0,97453])
* Tip `record`, tüm üyeler `init`-only → **dilin kendisi** değiştirilmesine izin vermiyor.

---

## 3. OLASILIĞIN ANLAMI — kesin tanım

Bu tanım FORMAX'ın geri kalanı tarafından **birebir** alıntılanmalıdır:

> **`HomeProbability`** — Modelin, maçın **normal sonuç hedefinde** ev sahibi tarafın kazanması
> için verdiği tahmin edilen olasılık. "Normal sonuç", müsabakanın sonucu kaydettiği skor
> çizgisidir: veri setinin uzatmayı kaydettiği yerde uzatma dâhil, **penaltı atışları hariç**.
>
> **`DrawProbability`** — Aynı tanımla, maçın berabere bitmesi.
>
> **`AwayProbability`** — Aynı tanımla, deplasman tarafının kazanması.

Üçü birlikte bir dağılımdır: toplamları 1'dir ve başka bir sonuç yoktur.

**Bu sayılar bir tavsiye değildir.** Bahis oranı, beklenen getiri veya "değerli bahis" değildir.
Modelin, elindeki maç öncesi kanıtla üç sonuca verdiği olasılıktır.

---

## 4. YÜZDE GÖSTERİMİ — backend'in işi değil

Backend **ondalık** üretir:

```
HomeProbability = 0.6452
```

Frontend isterse `%65` gösterir. Backend:

* olasılığı **string'e çevirmez**
* **yuvarlamaz**
* yüzde işareti **eklemez**

Yuvarlama yalnız presentation katmanında yapılır. Sözleşme tipinde alanlar `double?`; bir yüzde
string'i o alana atanamaz. Ölçülen kanıt: 99.609 yayımlanan olasılığın **0'ı** tam iki ondalıklı
bir değere oturuyor — yani hiçbiri gösterim için önceden yuvarlanmamış.

---

## 5. CONFIDENCE ≠ PROBABILITY

`ConfidenceClass` **kanıtın** ne kadar ettiğini söyler; olasılığın ne olduğunu değil.

Sözleşmenin kendi örneği, testte doğrulanıyor:

```
HomeProbability = 0.78
ConfidenceClass = LOW        (zayıf tarafın yalnız 2 maçlık geçmişi var)
```

Yapısal güvence: confidence sınıflandırıcısının girdi tipinde **olasılık, λ, favori veya sonuç
taşıyan tek bir alan yok**. Biri "model %80 emin, o hâlde HIGH" demek isterse imzayı değiştirmek
zorunda kalır.

Ampirik kanıt — sınıfların olasılık aralıkları tamamen örtüşüyor:

```
LOW      : [0,35 – 0,89]
MEDIUMLOW: [0,33 – 0,90]
MEDIUM   : [0,34 – 0,96]
HIGH     : [0,33 – 0,97]
```

Altı çiftin **sıfırı** ayrık. Confidence olasılığın başka bir adı olsaydı bantlar ayrık olurdu.

Ayrıca: **yayımlanmış hiçbir tahmin `NONE` taşımıyor** (0/33.203). `NONE` yalnız gerçek kanıt
yokluğu için ayrılmış ve o durumda tahmin zaten reddediliyor.

---

## 6. GATE

`prediction_gate_v1` mevcut hâliyle kullanıldı; politika `KEEP_CURRENT_GATE`.

```
GateStatus = ACCEPTED  →  üç olasılık dolu, GateReason = "OK"
GateStatus = REJECTED  →  üç olasılık NULL, GateReason = tetiklenen kodlar
```

Reddedilen maçlarda **sahte olasılık gönderilmiyor**: 698 reddin 698'inde üç alan da NULL.
`GateStatus` ile `PredictionEligible` arasında tutarsız tek satır yok.

Red nedenleri: `NO_TEAM_HISTORY` 552, `COMPETITION_NOT_COVERED` 258 (112 maç ikisini birden).

---

## 7. MODEL IMMUTABILITY (§6)

Tahmin üretimi sırasında:

* Validated Team Strength config **okunur, değiştirilmez** — dosyanın SHA-256'sı başlangıçta
  alınır ve raporlanır.
* Runtime'da **optimizasyon yok**: bu fazda hiçbir parametre araması çalıştırılmadı.
* **Backtest çalıştırılmaz** — tarihsel replay yalnız bu araştırma harness'ında, sözleşme akışını
  beslemek için; üretim yolunda bir maçın tahmini kendi durumundan üretilir.
* **LLM yok, Gemma yok.** `ContractService` sınıfının bir LLM'e ulaşabileceği hiçbir bağımlılığı
  yok.

---

## 8. IMMUTABILITY VE KİMLİK (§10)

`PredictionId = SHA256(MatchId | 4 version | EvidenceCutoff | PredictionTimestamp)`

Deterministik olması iki şeyi birden verir:

* Aynı girdilerle yeniden yayım **aynı id**'yi üretir → idempotent, satır çoğaltmaz.
* **Yeni kanıt → yeni cutoff → yeni id** → düzeltme değil, **yeni tahmin**. Eskisi yerinde kalır.

Store append-only. Canlı probe her koşuda çalışıyor:

```
republish identical   -> AlreadyPublished     (no-op, satır çoğalmadı)
rewrite probabilities -> RejectedImmutable    (reddedildi)
saklanan değer 0.57694652 · denenen 0.62694652
```

Yayımdan sonra değişmiş satır: **0** (33.901 satırın `ContentHash`'i yayım anındakiyle aynı).

> **Yol boyunca bir tasarım hatası bulundu ve düzeltildi:** "hangi tahmin güncel?" sorusu
> `PredictionTimestamp`'e göre cevaplanıyordu. Ama replay'de aynı maçın iki tahmini **aynı**
> timestamp'i taşıyor (ikisi de maç günü 00:00Z), dolayısıyla sıralama hash karşılaştırmasına
> düşüyordu — yani keyfi. Append-only bir log zaten hangi satırın sonra geldiğini biliyor; store'a
> açık bir `Sequence` eklendi ve `Current` artık onu kullanıyor. Test bu davranışı kilitliyor.

---

## 9. SETTLEMENT (§11)

Sonuç **ayrı bir nesnede** tutulur. Tahminde settlement'ın yazabileceği bir alan **yok**.

```
settled 33.901 · already settled 0 · no result yet 0 · rejected as early 0
settlement tarafından değişen tahmin : 0
tampered rows                        : 0
```

Kurallar, testlerle kilitli:

* Bir tahmin **iki kez settle edilemez** — ikincisi `AlreadySettled`, ilk sonuç yerinde kalır.
* Tahminden **önceki** tarihli sonuç eklenemez — `RejectedAsEarly`.
* **Reddedilmiş tahmin loglanır ama asla skorlanmaz** — olasılığı yok, log loss'u `null`.

---

## 10. METRİKLER (§12)

Production log'undan hesaplanabilir, ve hesaplandı — **optimizasyon yapılmadan**:

```
LogLoss 0,998662   Brier 0,59616   RPS 0,20608   Accuracy 0,5133
(33.203 kabul edilmiş + settle edilmiş tahmin)
```

Store'dan yeniden hesaplanan log loss, sözleşmeden hesaplananla **birebir** aynı:
`0,99866184 = 0,99866184`.

Log'daki dört version alanı sayesinde bu metrik ileride **sürüm bazında** gruplanabilir; model
değiştiğinde eski ve yeni aynı log üzerinden karşılaştırılır.

---

## 11. REGRESYON (§16)

`model_validation_v2/model_comparison.csv`'den **okunan** referansa karşı (elle yazılmış sabit
değil):

| Segment | N | LogLoss | Referans | Brier | RPS | Accuracy | Durum |
|---|---|---|---|---|---|---|---|
| TRAIN | 18.472 | 1,006533 | 1,006533 | 0,60142 | 0,20849 | 0,5055 | **MATCH** |
| VALIDATION | 7.636 | 0,987473 | 0,987473 | 0,58865 | 0,20280 | 0,5236 | **MATCH** |
| TEST | 7.793 | 0,993544 | 0,993544 | 0,59285 | 0,20531 | 0,5212 | **MATCH** |
| FULL | 33.901 | 0,999254 | 0,999254 | 0,59657 | 0,20647 | 0,5132 | **MATCH** |

Tolerans 5e-7. Ayrıca sözleşmenin yayımladığı olasılıkların modelinkinden sapması: **tam 0**.

---

## 12. FRONTEND SINIRI (§13)

Frontend **hiçbir olasılık hesaplamaz**. Backend'den yalnız şunları alır:

```
HomeProbability, DrawProbability, AwayProbability   (ondalık)
PredictionEligible, ConfidenceClass, GateStatus
```

`PredictionEligible = false` ise frontend'in gösterecek yüzdesi **yoktur** — alanlar NULL gelir.
Bu durumda "tahmin yok" gösterilir; sıfır, ortalama veya yer tutucu bir yüzde **uydurulmaz**.

Frontend'in tek işi presentation: ondalığı yüzdeye çevirmek, yuvarlamak, biçimlendirmek.

---

## 13. GEMMA SINIRI (§14)

Final olasılıklar Gemma'ya gönderilirse **salt okunur girdi**dir. Gemma:

* yüzde **değiştiremez**
* yeni yüzde **üretemez**
* olasılık **hesaplayamaz**

Yapısal karşılığı: `PredictionContract` bir `record` ve tüm üyeleri `init`-only. Bir anlatı katmanı
onu okuyabilir, yanına metin koyabilir; içindeki sayıya dokunamaz. Sözleşme üretim yolunda hiçbir
LLM bağımlılığı taşımıyor.

---

## 14. DENETİM — 18/18 PASS

| Kontrol | Beklenen | Ölçülen |
|---|---|---|
| Regresyon (4 segment) | MATCH | **MATCH** |
| Olasılık toplam hatası | ≤ 1e-12 | **2,2e-16** |
| [0,1] dışında olasılık | 0 | **0** |
| Yayımlanan olasılık aralığı | [0,1] içinde | **[0,006916 · 0,974534]** |
| Reddedilip olasılık taşıyan | 0 | **0** |
| GateStatus ↔ PredictionEligible tutarsızlığı | 0 | **0** |
| Dört version ile damgalanmamış satır | 0 | **0** |
| Maç tarihinde/sonrasında kanıt | 0 | **0** |
| Kendi kanıtından önce damgalanmış tahmin | 0 | **0** |
| PredictionId tekilliği | 33.901 | **33.901** |
| Saklanan satır | 33.901 | **33.901** |
| Yayımdan sonra değişmiş tahmin | 0 | **0** |
| Ayrık olasılık bandında confidence sınıfı | 0 | **0 çift** |
| `NONE` confidence ile yayımlanmış tahmin | 0 | **0** |
| Store'dan yeniden hesaplanan log loss | 0,99866184 | **0,99866184** |
| Canlı probe: yayımlanmış tahmini yeniden yazma | REJECTED | **RejectedImmutable** |
| Canlı probe: aynı tahmini yeniden yayımlama | idempotent | **AlreadyPublished** |
| İki ondalığa oturan olasılık (yuvarlama izi) | < %1 | **0 / 99.609 (%0,000)** |

Ve 16/16 test: kabul/red, simplex, ondalık çıktı, versiyonlama, kanıt kesimi (aynı gün reddi dâhil),
confidence/probability ayrımı, id determinizmi, yeni-kanıt-yeni-id, yeniden yazma reddi, idempotent
yeniden yayım, settlement'ın tahmini değiştirmemesi, çift settlement ve erken sonuç reddi,
reddedilmiş tahminin skorlanmaması, gerçek veri regresyonu.

---

## 15. PRODUCTION DB (§17)

**Yazılmadı.** [`MIGRATION_PLAN.md`](MIGRATION_PLAN.md) hazırlandı: SQL Server, iki tablo
(`Predictions` INSERT-only + `PredictionSettlements` 1:1), simplex/kanıt/confidence `CHECK`
kısıtları, `DENY UPDATE` ile §10'un veritabanı düzeyinde uygulanması, indeksler, doğrulama
sorguları ve altı adımlı uygulama sırası — 5. adım "gölge modda en az bir maç haftası ölç",
6. adım "okuma ucunu bağla" ayrı karar olarak bırakıldı.

EF migration dosyası **üretilmedi**, `FormaxDbContext` **değiştirilmedi**, mevcut prediction ucu
**etkilenmedi**.

---

## 16. NE YAPILMADI

1. Production DB yazımı, EF migration, cut-over.
2. Frontend uç tasarımı veya değişikliği.
3. Gemma prompt değişikliği.
4. Model, calibration veya gate politikası araştırması — hepsi kilitli alındı.
5. **`PredictionTimestamp` replay semantiğinde** maç gününün 00:00Z'sidir; canlı çalışmada duvar
   saati olur. Bu, çıktı akışını deterministik tutmak için; §10'un kimlik kuralı iki durumda da
   aynı çalışır.
6. **Kimlik kapısı bu veride hiç tetiklenmedi**, çünkü veri okuyucu üst akışta zaten filtreliyor.
   Sentetik testle doğrulandı, gerçek veriyle değil.

---

## 17. ÇALIŞTIRMA

```bash
cd FORMAX_PROBABILITY_ENGINE/prediction_contract_v1/FormaxPredictionContract
dotnet build -c Release
dotnet run -c Release -- test    # 16 test
dotnet run -c Release -- all     # test + tam koşu (~3 sn)
```

Seçenekler: `--dataset`, `--split`, `--ts-config`, `--dc-config`, `--gate-config`, `--baseline`, `--out`.

`Formax.slnx`'e eklenmedi, NuGet bağımlılığı yok, önceki fazların kaynak kodu değiştirilmedi.

---

## 18. ÇIKTILAR

| Dosya | İçerik |
|---|---|
| `prediction_contract.csv` | 33.901 canonical DTO: id, versiyonlar, olasılıklar, gate durumu ve nedeni, confidence, content hash |
| `prediction_log.csv` | Append-only log: sıra numarası, tahmin, settlement, yayım-anı ve güncel content hash, bütünlük bayrağı |
| `regression.csv` | 4 segment × ölçülen vs `model_validation_v2` referansı |
| `contract_audit.csv` | 18 kontrol: beklenen vs ölçülen |
| `contract_versions.csv` | Dört version alanı + model parmak izi + split sürümü, her birinin kaynağıyla |
| `MIGRATION_PLAN.md` | Production DB planı — **uygulanmadı** |
