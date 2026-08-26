# FORMAX — PROBABILITY OUTPUT + PREDICTION GATE V1

**Tarih:** 2026-08-21
**Durum:** Build ✅ · 27/27 test ✅ · 15/15 denetim PASS · regresyon 4/4 segmentte MATCH
**Kapsam:** Yalnız araştırma. Production DB'ye **yazılmadı**, prediction endpoint değiştirilmedi,
frontend'e yüzde gönderilmedi, Gemma'ya verilmedi. Model **dokunulmadı**.

---

## 0. SONUÇ — ÖNDEN

Zincir kuruldu ve ölçüldü:

```
RAW probability  →  Prediction Gate  →  Prediction DTO  →  Prediction Log  →  Settlement
```

| | |
|---|---|
| Toplam maç | 33.901 |
| **Yayımlanan** | **33.203 (%97,94)** |
| Reddedilen | 698 (%2,06) |
| Regresyon (4 segment) | **MATCH** — çıktı katmanı modeli değiştirmedi |
| Yayımlanan olasılıkların modelden farkı | **0** (maks delta 0,0e+0) |
| Olasılık toplamı hatası | **2,2e-16** |
| Kırpılan yüksek olasılık | **0** — en yüksek yayımlanan 0,9745 = modelin ürettiği en yüksek |

---

## 1. MODEL DEĞİŞTİRİLMEDİ — KANIT

`model_validation_v2/validated_teamstrength_config.json` olduğu gibi okundu, Independent Poisson V2
dondurulmuş hâlde çalıştı. Yeni Team Strength tuning, Dixon-Coles, bivariate, calibration, feature —
hiçbiri yapılmadı.

**Regresyon**, `model_validation_v2/model_comparison.csv`'den **okunan** referans değerlere karşı
(elle yazılmış sabit değil, dosyadan karşılaştırma):

| Segment | N | LogLoss | Referans | Brier | RPS | Accuracy | Durum |
|---|---|---|---|---|---|---|---|
| TRAIN | 18.472 | 1,006533 | 1,006533 | 0,60142 | 0,20849 | 0,5055 | **MATCH** |
| VALIDATION | 7.636 | 0,987473 | 0,987473 | 0,58865 | 0,20280 | 0,5236 | **MATCH** |
| TEST | 7.793 | 0,993544 | 0,993544 | 0,59285 | 0,20531 | 0,5212 | **MATCH** |
| FULL | 33.901 | 0,999254 | 0,999254 | 0,59657 | 0,20647 | 0,5132 | **MATCH** |

Tolerans 5e-7. Dört segmentin dördü de birebir tutuyor.

Ayrıca yapısal kanıt: DTO, yayımladığı olasılığı **hiç dokunmadan** kopyalıyor. 33.203 yayımlanmış
tahminin hepsinde `PublishedProbability − ModelProbability = 0` (kayan nokta anlamında tam sıfır,
yuvarlama bile yok).

---

## 2. PREDICTION DTO

```
MatchId
HomeProbability      ← PredictionEligible=false ise NULL
DrawProbability      ← NULL
AwayProbability      ← NULL
ModelVersion             INDEPENDENT_POISSON_V2
TeamStrengthVersion      TEAM_STRENGTH_V2_VALIDATED
PredictionTimestamp
EvidenceCutoff
ConfidenceClass          NONE | LOW | MEDIUMLOW | MEDIUM | HIGH
PredictionEligible
GateReason               "OK" | "NO_TEAM_HISTORY|COMPETITION_NOT_COVERED" | ...
```

**Reddedilen bir maçta olasılıklar NULL'dur.** Sıfır değil, prior değil, fallback değil.
Denetim: `refused matches that still carry a published probability = 0`.

Olasılık toplamı: yayımlanan her satırda **1,0 ± 2,2e-16** (tolerans 1e-12).

Ayrıca DTO'da denetim amaçlı `ModelHomeProbability/...` alanları var — bunlar **yayımlanan
sözleşmenin parçası değil**; çıktı katmanının modeli değiştirmediğini kanıtlamak için duruyorlar.
API yalnız `HomeProbability/DrawProbability/AwayProbability` verir.

**Backend açıklama üretmiyor.** DTO'da anlatı alanı yok. `GateReason` bir operasyonel durum kodu,
olasılığın yorumu değil.

---

## 3. PREDICTION GATE

Kapı **tek bir soruya** cevap veriyor: bu olasılık yayımlanabilir mi? Ve bunu olasılığa
**dokunmadan** yapıyor.

| Kod | Ne demek |
|---|---|
| `IDENTITY_UNCONFIRMED` | İki taraftan biri doğrulanmış kimlik değil |
| `SNAPSHOT_MISSING` | Modelin gerektirdiği maç-öncesi durum yok |
| `NO_TEAM_HISTORY` | Bir tarafın hiç önceki maçı yok → snapshot'ı **prior'ın kendisi** |
| `INSUFFICIENT_HISTORY` | Geçmiş var ama eşiğin altında |
| `COMPETITION_NOT_COVERED` | Müsabakanın gol baseline'ı hâlâ config tohumu, öğrenilmiş veri değil |
| `MODEL_STATE_AT_GUARD_RAIL` | λ kendi clamp'inin üstünde oturuyor → doymuş, ölçülmüş değil |
| `MODEL_STATE_INVALID` | λ veya olasılık sonlu/kullanılabilir değil |
| `PROBABILITY_NOT_NORMALISED` | Üç olasılık bir dağılım oluşturmuyor |
| `EVIDENCE_NOT_STRICTLY_PRE_MATCH` | Maç tarihinde veya sonrasında tarihli kanıt |

Başarısız olan **tüm** kontroller raporlanıyor, ilkinde durulmuyor: operatör bütün nedenleri bir
kerede görmeli, sürüm sürüm keşfetmemeli.

### Gerçek veride ne oldu

| Neden | Maç |
|---|---|
| `NO_TEAM_HISTORY` | 552 |
| `COMPETITION_NOT_COVERED` | 258 |
| **Toplam reddedilen** | **698** (112 maç ikisini birden tetikledi) |

Kalan kodlar hiç tetiklenmedi ve **bu bir bulgu, bir eksiklik değil**:

* `IDENTITY_UNCONFIRMED` = 0 — çünkü veri okuyucu bu satırları zaten motora sokmuyor
  (bu veri setinde 0 satır bu nedenle düştü). Kontrol yine de kapıda duruyor, çünkü canlı akışta
  okuyucu kadar sıkı olmayan bir yol açılabilir. Sentetik testle doğrulandı.
* `MODEL_STATE_AT_GUARD_RAIL` = 0 — ölçüldü: λ aralığı [0,248 · 5,684], clamp [0,05 · 6,0].
  Hiçbir λ raya değmiyor. Kontrol boşuna değil, sadece bu veride tetiklenmiyor.
* `PROBABILITY_NOT_NORMALISED`, `MODEL_STATE_INVALID`, `EVIDENCE_NOT_STRICTLY_PRE_MATCH` = 0 —
  dondurulmuş model bunları zaten üretmiyor; kapı bir güvenlik ağı olarak duruyor.

### Müsabaka bazında (TEST)

| Tür | Maç | Yayımlanan | Oran |
|---|---|---|---|
| DOMESTIC_LEAGUE | 5.867 | 5.854 | %99,78 |
| UEFA_QUALIFICATION_PLAYOFF | 172 | 172 | %100,00 |
| UEFA_MAIN | 1.062 | 1.055 | %99,34 |
| UEFA_QUALIFIER | 682 | 629 | %92,23 |
| **DOMESTIC_PLAYOFF** | **10** | **0** | **%0,00** |

`DOMESTIC_PLAYOFF` tamamen reddedildi ve bu **kapının doğru çalıştığının en net örneği**: tüm veri
setinde bu türden yalnız 25 maç var, yani 50'lik kapsam eşiğinin altında. FORMAX bu müsabakanın
gol seviyesini hiç öğrenmedi; λ'ları config tohumundan ölçekleniyor. Kapı, ölçülmemiş bir ölçeğe
dayanan sayıyı yayımlamak yerine susuyor.

### Cold-start bazında (TEST)

| Sınıf | Maç | Yayımlanan | Oran |
|---|---|---|---|
| Rich | 7.225 | 7.215 | %99,86 |
| Established | 255 | 255 | %100,00 |
| Limited | 130 | 130 | %100,00 |
| Developing | 110 | 110 | %100,00 |
| **NoHistory** | **73** | **0** | **%0,00** |

---

## 4. KAPI POLİTİKASI — KARAR, KANIT DEĞİL

Varsayılan: **yalnız sıfır geçmişli taraf reddedilir** (`minPriorMatchesPerTeam = 1`).

Gerekçe bir metrik iddiası değil, bir sözleşme iddiası: sıfır önceki maçı olan bir takımın
snapshot'ı **prior'ın kendisidir**. Yayımlanacak sayı rakibi ve müsabakayı tarif eder, bu takımı
değil.

**Dürüst olmak gerekirse bu politika bir maliyet taşıyor.** Önceki fazın VALIDATION ölçümünde
Poisson, `NoHistory` diliminde bile hâlâ en iyi seçenekti (1,042329; rating-only 1,048232;
naif 1,077684). Yani bu 552 maçı reddetmek **doğruluk kaybettiriyor**. Bilerek yapılan muhafazakâr
bir tercih ve öyle etiketlenmeli — ölçümün dayattığı bir sonuç değil.

Daha sıkı bir çıtanın maliyeti ölçüldü (**VALIDATION üzerinde**, test'e bakılmadan):

| `minPriorMatchesPerTeam` | Yayımlanan | Kapsam | Yayımlanan LogLoss |
|---|---|---|---|
| **1 (varsayılan)** | 7.571 | **%99,15** | 0,986917 |
| 2 | 7.514 | %98,40 | 0,986940 |
| 3 | 7.454 | %97,62 | 0,987101 |
| 5 | 7.330 | %95,99 | 0,986625 |
| 10 | 7.044 | %92,25 | 0,985818 |

10'a çıkarmak kapsamın %7'sini yakıyor ve log loss'u yalnız 0,0011 iyileştiriyor. Tablo karar
vermek için değil, **kararın fiyatını görünür kılmak** için burada. Varsayılan bu sayılara bakarak
seçilmedi; `gate.config.json`'da ilan edilmiş durumda.

---

## 5. CONFIDENCE ≠ PROBABILITY

Confidence yalnız **kanıtın** ne kadar ettiğini söyler. Bu bir söz değil, **yapısal** bir kısıt:
`ConfidenceClassifier.Classify` bir `ConfidenceInput` alır ve o tipte olasılık, λ, favori ya da
sonuç taşıyan **tek bir alan yoktur**. İleride biri "model %80 emin, o hâlde HIGH" demek isterse
imzayı değiştirmek zorunda kalır, bu da gözden kaçmaz.

Sınıf **zayıf taraf** tarafından belirlenir. 400 maçlık bir kulüple bir çıraklık maçı iyi
kanıtlanmış değildir; yarısı kanıtlanmıştır ve eksik olan yarı, sayının güvenilirliğine karar
veren yarıdır.

Yayımlanan tahminlerin dağılımı:

| Sınıf | Adet | Oran |
|---|---|---|
| HIGH | 29.420 | %88,61 |
| MEDIUM | 1.683 | %5,07 |
| MEDIUMLOW | 763 | %2,30 |
| LOW | 1.337 | %4,03 |

**Ampirik kanıt:** eğer confidence olasılığın başka bir adı olsaydı, sınıflar ayrık olasılık
bantlarında otururdu. Ölçülen aralıklar:

```
LOW      : [0,35 – 0,89]
MEDIUMLOW: [0,33 – 0,90]
MEDIUM   : [0,34 – 0,96]
HIGH     : [0,33 – 0,97]
```

Altı çiftin **sıfırı** ayrık. Model %93 deyip LOW confidence verebiliyor (test bunu doğruluyor) ve
%34 deyip HIGH verebiliyor.

---

## 6. AŞIRI OLASILIK KORUMASI YOK — KASITLI

Kırpma, tavan, "% 90 çok yüksek, 85'e indir" — **hiçbiri yok**.

| | |
|---|---|
| ≥%90 yayımlanan tahmin | **85** |
| ≥%80 yayımlanan tahmin | **720** |
| En yüksek yayımlanan olasılık | **0,974534** |
| Modelin ürettiği en yüksek olasılık | **0,974534** |

İkisi **birebir aynı**. Denetim bunu her koşuda kontrol ediyor: eğer bir yerde kırpma olsaydı
yayımlanan maksimum modelinkinden küçük çıkardı.

Kapı yalnız veri kalitesi, örneklem büyüklüğü ve model durumu ile çalışıyor. `λ = 5,99` (yüksek
ama doymamış) geçiyor; `λ = 6,00` (rayda oturan) reddediliyor — çünkü ikincisi bir ölçüm değil,
bir sınır.

---

## 7. YAYIMLANAN vs TÜMÜ

| Segment | Yayımlanan | Reddedilen | Yayımlanan LogLoss | Tümü |
|---|---|---|---|---|
| TRAIN | 17.922 | 550 | 1,005935 | 1,006533 |
| VALIDATION | 7.571 | 65 | 0,986917 | 0,987473 |
| **TEST** | **7.710** | **83** | **0,993289** | **0,993544** |
| FULL | 33.203 | 698 | 0,998662 | 0,999254 |

Kapı, yayımlanan kümenin kalitesini her segmentte hafifçe artırıyor (TEST'te −0,000255). Küçük bir
fark ve **büyütülmemeli**: kapının işi metriği iyileştirmek değil, dayanaksız sayıyı yayımlamamak.
Ölçünün iyileşmesi bunun yan etkisi.

---

## 8. LOGGING VE SETTLEMENT

Her tahmin, sonuç gelmeden **önce** loglanıyor:

```
MatchId, PredictionTimestamp, EvidenceCutoff, ModelVersion, TeamStrengthVersion,
HomeProbability, DrawProbability, AwayProbability, ConfidenceClass, GateStatus
```

`prediction_log.csv` sonuç sütunları **boş** hâlde yazıldı — yani tahmin anındaki gerçek durum.
`prediction_log_settled.csv` ise sonuçlar geldikten sonraki hâli.

Model sürümü ve kanıt kesim tarihi **satırla birlikte seyahat ediyor**: altı ay sonra hesaplanan
bir skor bile hangi modelin ürettiğini ve o an neyi görebildiğini bilir.

Doğrulanan davranışlar:

* Bir satır **iki kez settle edilemez** (ikinci deneme `AlreadySettled` döner).
* Tahminden **önceki** tarihli bir sonuç eklenemez (`RejectedAsEarly`).
* Reddedilmiş bir tahmin loglanır ama **asla skor üretmez** (olasılığı yok, log loss'u null).
* 33.901 satırın 33.901'i settle edildi; yayımlanan 33.203'ün hepsi skorlanabilir.
* Log'dan yeniden hesaplanan log loss, metrik katmanının verdiği sayının **birebir aynısı**
  (0,99866184 = 0,99866184).

---

## 9. YOL BOYUNCA BULUNAN İKİ HATA

İkisi de testlerle değil, **çıktıya bakarak** bulundu. İkisi de düzeltildi ve ikisi için de kontrol
eklendi.

**1. `INSUFFICIENT_HISTORY`, 533 maçta eşik artefaktı olarak tetikleniyordu.**
`minEffectiveMatchesPerTeam = 1.0` iken, zayıf tarafında **tam olarak 1** maç olan 533 maç
reddediliyordu. Sebep politika değildi: doğrulanmış config'in `halfLifeDays = 100000` değeriyle
decay 9 yılda ~%2 — bu da `EffectiveMatches`'i 0,978'e indirip tamsayı sınırın altına düşürüyordu.
Bu maçları reddetmek savunulabilir bir tercih olabilir, ama **yuvarlama artefaktı olarak değil,
politika olarak** ifade edilmeli. Eşik 0,5'e çekildi; 1 maçlık takımları reddetmek isteyen
`minPriorMatchesPerTeam`'i 2 yapar ve maliyetini §4 tablosunda görür.

**2. 509 yayımlanmış tahmin `ConfidenceClass = NONE` taşıyordu.**
Sözleşme çelişkisi: kapıdan geçmiş, olasılığı yayımlanmış, ama "arkasında hiç kanıt yok" diyen bir
satır. Aynı decay artefaktının confidence tarafındaki yansımasıydı. `NONE` artık **yalnız gerçekten
kanıt yokluğu** için ayrılmış: oynamış bir taraf, kanıtı ne kadar eskimiş olursa olsun en kötü
`LOW`'dur. Denetime kalıcı kontrol eklendi (`published predictions carrying ConfidenceClass NONE = 0`)
ve iki test yazıldı.

---

## 10. DENETİM — 15/15 PASS

| Kontrol | Beklenen | Ölçülen |
|---|---|---|
| Yayımlanan metrikler V2 referansını yeniden üretiyor (4 segment) | MATCH | **MATCH** |
| Modelden farklı yayımlanan olasılık | 0 | **0** (maks delta 0,0e+0) |
| Yayımlanan olasılık toplam hatası | ≤ 1e-12 | **2,2e-16** |
| [0,1] dışında yayımlanan olasılık | 0 | **0** |
| Reddedilip yine de olasılık taşıyan maç | 0 | **0** |
| Gate reason'ı boş reddedilmiş maç | 0 | **0** |
| Uygun olduğu hâlde red nedeni taşıyan maç | 0 | **0** |
| En yüksek yayımlanan = modelin en yükseği (kırpma yok) | 0,974534 | **0,974534** |
| Maç tarihinde/sonrasında kanıtla kurulmuş yayımlanan tahmin | 0 | **0** |
| Kendi kanıtından önce zaman damgalı tahmin | 0 | **0** |
| Ayrık olasılık bandında oturan confidence sınıfı çifti | 0 | **0 / 6** |
| `NONE` confidence ile yayımlanmış tahmin | 0 | **0** |
| Tahmin başına log satırı | 33.901 | **33.901** |
| Settle edilebilen yayımlanmış tahmin | 33.203 | **33.203** |
| Settle edilmiş log'un yeniden ürettiği log loss | 0,99866184 | **0,99866184** |

Ayrıca 27/27 test: kapı kabul/red (her neden ayrı ayrı), çoklu neden, sızıntı reddi, aşırı olasılığın
kırpılmadan geçmesi, confidence'ın olasılıktan bağımsızlığı, determinizm, log round-trip, settlement
kuralları ve müsabaka kapsam sayacının aynı-gün maçlarını saymaması.

---

## 11. NE YAPILMADI

1. **Production DB'ye yazılmadı.** §9 gereği. Çıktılar CSV.
2. **Prediction endpoint değiştirilmedi**, frontend'e yüzde gönderilmedi, Gemma'ya verilmedi.
3. **Calibration eklenmedi.** Bir önceki faz `CALIBRATION_IMPROVEMENT_NOT_PROVEN` dedi; burada
   `NO_CALIBRATION` kullanıldı.
4. **Kapı eşikleri politika, kanıt değil.** Hiçbiri test segmentine bakılarak seçilmedi ve zaten
   seçilemez: kapı bir olasılığın yayımlanıp yayımlanmayacağına karar verir, olasılığı değiştirmez.
5. **`PredictionTimestamp` replay semantiğinde** maç gününün 00:00Z'sidir — model durumu o günün
   başından olduğu için anlamlıdır ve çıktıyı deterministik tutar. Canlı çalışmada duvar saati olur.
6. **Kimlik kapısı bu veride hiç tetiklenmedi**, çünkü okuyucu üst akışta zaten filtreliyor.
   Sentetik testle doğrulandı, gerçek veriyle değil.

---

## 12. ÇALIŞTIRMA

```bash
cd FORMAX_PROBABILITY_ENGINE/prediction_gate_v1/FormaxPredictionGate
dotnet build -c Release
dotnet run -c Release -- test    # 27 test
dotnet run -c Release -- all     # test + tam koşu (~2 sn)
```

Seçenekler: `--dataset`, `--split`, `--ts-config`, `--dc-config`, `--baseline`, `--gate-config`, `--out`.

`Formax.slnx`'e eklenmedi, NuGet bağımlılığı yok. `team_strength`, `dixon_coles`,
`model_validation_v2` ve `calibration_v1` projelerinin kaynak kodu **değiştirilmedi**.

---

## 13. ÇIKTILAR

| Dosya | İçerik |
|---|---|
| `predictions_dto.csv` | 33.901 DTO: yayımlanan olasılıklar, gate durumu ve nedeni, confidence, denetim alanları |
| `prediction_log.csv` | Tahmin anındaki log — sonuç sütunları **boş** |
| `prediction_log_settled.csv` | Aynı log, sonuçlar eklendikten sonra + satır bazında log loss |
| `regression_baseline.csv` | 4 segment × ölçülen vs `model_validation_v2` referansı, MATCH/DIFFERS |
| `gate_summary.csv` | Segment bazında yayımlanan/reddedilen + yayımlanan ve tüm küme metrikleri |
| `gate_reasons.csv` | Her gate kodunun kaç maçta tetiklendiği |
| `gate_by_competition.csv` | Müsabaka türü bazında kapsam ve log loss, segment bazında |
| `gate_by_cold_start.csv` | Cold-start sınıfı bazında kapsam ve log loss, segment bazında |
| `gate_policy_sensitivity.csv` | Daha sıkı geçmiş çıtasının kapsam ve kalite maliyeti (VALIDATION) |
| `gate_audit.csv` | 15 kontrol: beklenen vs ölçülen |
| `gate.config.json` | Kapı politikası, her eşiğin gerekçesiyle |

`predictions_dto.csv` hem yayımlanan sözleşmeyi hem denetim alanlarını taşıyor; API'nin vereceği
alanlar yalnız `HomeProbability`, `DrawProbability`, `AwayProbability`, `ModelVersion`,
`TeamStrengthVersion`, `PredictionTimestamp`, `EvidenceCutoff`, `ConfidenceClass`,
`PredictionEligible`, `GateReason`.
