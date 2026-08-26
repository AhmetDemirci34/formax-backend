# FORMAX — DIXON-COLES BASELINE + WALK-FORWARD BACKTEST

**Tarih:** 2026-08-20
**Durum:** Çalışıyor. Build ✅ · 13/13 test ✅ · 33.901 tahmin · leakage ihlali **0** · normalizasyon ihlali **0**

**SONUÇ (önden söylenir):** Dixon-Coles düzeltmesi bu split'te **fayda sağlamadı**.
`DIXON-COLES IMPROVEMENT NOT PROVEN`. Ayrıntı ve nedeni §5'te.

Bu bir **araştırma/backtest** modelidir. Bivariate Poisson, ensemble, calibration, global context,
xG ve Gemma **yapılmadı**; üretim DB'sine yazılmadı, frontend'e bağlanmadı, mevcut prediction
endpoint'i değiştirilmedi.

---

## 1. MODEL

```
λ_ev   = baselineEv(competitionType)  × Attack_ev  × Defense_dep
λ_dep  = baselineDep(competitionType) × Attack_dep × Defense_ev

P(x,y) = τ(x,y,λ_ev,λ_dep,ρ) × Poisson(x;λ_ev) × Poisson(y;λ_dep)

τ = 1 − λμρ (0-0) · 1 + λρ (0-1) · 1 + μρ (1-0) · 1 − ρ (1-1) · 1 (diğer)
```

* `Attack`/`Defense` = Team Strength motorunun **maç öncesi** snapshot'ından (`PriorWeight`,
  `ColdStartClass` dâhil aynen taşınır).
* `baseline*` = o CompetitionType'ın **genişleyen pencereli** gol ortalaması. Saha avantajı ayrı
  bir elle-konmuş terim değildir; ev ve deplasman baseline'ı arasındaki fark olarak **veriden**
  öğrenilir.
* Skor ızgarası 0..`maxGoals` ile kesilir ve **yeniden normalize edilir** → `P(H)+P(D)+P(A) = 1`
  (testte 1e-12 toleransla doğrulandı; gerçek veride 33.901 satırın **0'ında** ihlal).

---

## 2. WALK-FORWARD

Zaman **gün gün** yeniden oynatılır:

```
her GÜN için:
    o günün tüm maçları için tahmin üret      ← yalnız daha ÖNCEKİ günlerin bilgisi
    SONRA o günün sonuçlarını context'e ekle
```

* Takım gücü zaten maç öncesi snapshot'tan gelir (`LastMatchDate < MatchDate`, doğrulandı).
* Competition context (baseline gol ortalamaları, sonuç frekansları) yalnız **kesinlikle önceki
  günlerden** birikir.
* Aynı gün oynanan iki maç birbirini besleyemez (test: *same day*).
* Test: sonraki bir maçın skoru değiştirildiğinde önceki tahmin **bit bit aynı** kalıyor.

---

## 3. KARŞILAŞTIRILAN DÖRT MODEL (aynı split, aynı sıra)

| Model | Ne kullanır |
|---|---|
| `SIMPLE_BASELINE_V1` | Yalnız o CompetitionType'ın genişleyen H/D/A frekansı. Takım bilgisi **yok** |
| `TEAM_STRENGTH_BASELINE_V1` | Güç oranı + ampirik beraberlik oranı. Gol modeli **yok** |
| `INDEPENDENT_POISSON_V1` | Aynı λ'lar, ρ = 0 |
| `DIXON_COLES_BASELINE_V1` | Aynı λ'lar + Dixon-Coles düşük-skor düzeltmesi (ρ = −0,05) |

Dördü de **tam olarak aynı** yürüyen pencerede, aynı girdilerle skorlandı.

---

## 4. SONUÇLAR (ham olasılık, calibration YOK)

### Genel — 33.901 tahmin

| Model | LogLoss | Brier | RPS | Accuracy |
|---|---|---|---|---|
| SIMPLE_BASELINE_V1 | 1,06727 | 0,64535 | 0,23026 | 0,4466 |
| TEAM_STRENGTH_BASELINE_V1 | 1,02587 | 0,61495 | 0,21506 | 0,5046 |
| **INDEPENDENT_POISSON_V1** | **1,01318** | **0,60635** | **0,21086** | 0,5029 |
| DIXON_COLES_BASELINE_V1 | 1,01515 | 0,60770 | 0,21109 | 0,5015 |

Referans: tamamen belirsiz tahmin (1/3, 1/3, 1/3) log loss = **1,0986**.

### CompetitionType (log loss)

| Model | DOM_LEAGUE (26.846) | UEFA_MAIN (3.587) | UEFA_QUAL (2.838) | UEFA_QPO (605) | DOM_PLAYOFF (25) |
|---|---|---|---|---|---|
| SIMPLE | 1,07245 | 1,04682 | 1,04866 | 1,04341 | 1,12795 |
| TEAM_STRENGTH | 1,02615 | 1,01285 | **1,03786** | 1,03011 | 1,13491 |
| INDEPENDENT_POISSON | **1,01120** | **1,00491** | 1,03927 | **1,02164** | 1,14925 |
| DIXON_COLES | 1,01276 | 1,00822 | 1,04338 | 1,02444 | 1,14993 |

### Cold start (zayıf taraf sınıfına göre, DC / Poisson / Naive log loss)

| Sınıf | N | DC | Poisson | Naive |
|---|---|---|---|---|
| Rich | 29.756 | 1,01115 | **1,00935** | 1,06845 |
| Established | 1.784 | 1,03587 | **1,03383** | 1,06344 |
| Developing | 834 | 1,03852 | **1,03388** | 1,04816 |
| Limited | 975 | 1,06512 | **1,06179** | 1,07272 |
| **NoHistory** | **552** | 1,04033 | 1,03537 | **1,03518** |

### Prior havuzu

| Grup | N | DC LogLoss |
|---|---|---|
| İki taraf da UEFA havuzuna çekilmiş | 6.903 | 1,02367 |
| Hiçbiri UEFA havuzuna çekilmemiş | 26.998 | 1,01297 |

### Güvenilirlik bantları — DC, ham olasılık

| Bant | N | Ortalama tahmin | Gerçekleşen | Fark |
|---|---|---|---|---|
| 50-60% | 6.096 | 0,5418 | **0,6074** | −0,066 |
| 60-70% | 2.283 | 0,6416 | **0,7219** | −0,080 |
| 70-80% | 719 | 0,7382 | **0,8095** | −0,071 |
| 80-90% | 115 | 0,8346 | **0,8783** | −0,044 |
| 90-100% | 4 | 0,9142 | 0,7500 | +0,164 (N=4, anlamsız) |

Model **sistematik olarak az güvenli** (under-confident): %60 dediğinde gerçekte %72 oluyor.

---

## 5. NEDEN DIXON-COLES FAYDA SAĞLAMADI

ρ duyarlılık taraması (yalnız **teşhis**, parametre seçimi DEĞİL):

| ρ | LogLoss | Brier | RPS | Accuracy |
|---|---|---|---|---|
| −0,20 | 1,02518 | 0,61449 | 0,21222 | 0,4886 |
| −0,15 | 1,02117 | 0,61177 | 0,21176 | 0,4944 |
| −0,10 | 1,01782 | 0,60950 | 0,21139 | 0,4986 |
| **−0,05 (kanonik DC, gönderilen config)** | 1,01515 | 0,60770 | 0,21109 | 0,5015 |
| 0,00 (= bağımsız Poisson) | 1,01318 | 0,60635 | 0,21086 | 0,5029 |
| +0,05 | 1,01193 | 0,60547 | 0,21071 | 0,5038 |
| +0,10 | 1,01146 | 0,60504 | 0,21064 | 0,5042 |

**Log loss ρ arttıkça monoton düzeliyor.** Klasik Dixon-Coles'un negatif ρ'su bu veri setinde
**yanlış yöne** itiyor. Sebep, güvenilirlik tablosunda görünüyor: λ'lar shrinkage'lı bir
rating'den geldiği için model zaten **fazla beraberlik** üretiyor (az güvenli). Negatif ρ,
0-0/1-1 kütlesini daha da artırarak sapmayı büyütüyor.

> **+0,10 değeri "doğru ρ" olarak SEÇİLMEDİ.** Bu tarama backtest'in *kendisi* üzerinde yapıldı;
> oradan bir değer seçmek test setine uydurmak olurdu. Gönderilen config kanonik **−0,05**'te
> bırakıldı ve `UNVALIDATED` işaretli. Gerçek ρ, ayrı bir doğrulama penceresinde **tahmin
> edilmelidir** (Dixon-Coles orijinalinde ρ maksimum olabilirlikle fit edilir; burada sabit alındı).

**Diğer başarısızlık noktaları:**

1. **UEFA_QUALIFIER**: gol modelleri naive'i zar zor geçiyor; `TEAM_STRENGTH_BASELINE` (1,03786)
   burada Poisson'dan (1,03927) ve DC'den (1,04338) **daha iyi**. Sebep: bu maçların
   %99,6'sında bir tarafın kilitli yerel liglerde geçmişi yok → λ'lar havuz priorına dayanıyor,
   gol dağılımı varsayımı bu gürültüyü büyütüyor.
2. **NoHistory (552 maç)**: naive frekans baseline (1,03518) gol modelinden **daha iyi**.
   Geçmiş yoksa gol modeli hiçbir şey eklemiyor.
3. **DOMESTIC_PLAYOFF (25 maç)**: tüm modeller naive'in altında; örneklem 25, hiçbir sonuç çıkmaz.
4. **Accuracy paradoksu**: DC accuracy'de (0,5015) `TEAM_STRENGTH_BASELINE`'ın (0,5046) altında.
   Accuracy argmax'a bakar; beraberliğe fazla kütle veren model daha az "doğru tahmin" üretir
   ama olasılık kalitesinde (Brier/RPS) yine de önde.

---

## 6. NE KANITLANDI, NE KANITLANMADI

**Kanıtlandı:**
* Takım gücü gerçekten bilgi taşıyor: naive → team strength arasında log loss **1,06727 → 1,02587**.
* Gol modeli ek bilgi katıyor: team strength → Poisson **1,02587 → 1,01318**.
* Motor çalışıyor: 0 leakage ihlali, 0 normalizasyon ihlali, olasılıklar tam 1.

**Kanıtlanmadı:**
* **Dixon-Coles düzeltmesinin faydası.** Sabit ρ = −0,05 ile DC, bağımsız Poisson'dan
  0,00197 log loss **daha kötü**. → `DIXON-COLES IMPROVEMENT NOT PROVEN`.
* Modelin "iyi" olduğu. Log loss 1,015 ile tamamen belirsiz tahminin (1,0986) yalnız **%7,6**
  altında. Bu bir **başlangıç taban çizgisidir**, ürün kalitesi değildir.

---

## 7. ÇALIŞTIRMA

```bash
cd FORMAX_PROBABILITY_ENGINE/dixon_coles/FormaxDixonColes
dotnet build
dotnet run -- test        # 13 test (matematik + metrik + leakage + gerçek veri)
dotnet run -- backtest    # 33.901 tahmin, 4 model, ~0,4 sn
```
Seçenekler: `--dataset`, `--snapshots`, `--config`, `--out`.

Proje `Formax.slnx`'e eklenmedi, NuGet bağımlılığı yok.

---

## 8. ÇIKTILAR

| Dosya | İçerik |
|---|---|
| `output/predictions.csv` | 33.901 DC tahmini: MatchId, PredictionDate, ModelVersion, TeamStrengthVersion, ConfigVersion, H/D/A olasılık, olasılık toplamı, λ'lar, gerçek sonuç, cold-start bilgisi, kanıt kesim tarihi |
| `output/backtest_summary.csv` | 4 model × (genel, cold-start hariç, sezon bazlı) |
| `output/metrics_by_competition.csv` | 4 model × CompetitionType ve yerel lig bazında |
| `output/metrics_by_cold_start.csv` | Cold-start sınıfı, cold-start bayrağı ve prior havuzu bazında |
| `output/metrics_by_probability_band.csv` | 10 bantta tahmin vs gerçekleşme (calibration YOK) |
| `dixoncoles.config.json` | Tüm parametreler, hepsi `UNVALIDATED` |

---

## 9. SINIRLAR

1. **Hiçbir parametre fit edilmedi.** ρ sabit alındı; Dixon-Coles orijinalinde ρ ve takım
   parametreleri birlikte maksimum olabilirlikle tahmin edilir. Burada takım gücü ayrı bir online
   rating'den, ρ ise config'ten geliyor.
2. **Calibration yok.** Ham olasılıklar ölçüldü; güvenilirlik tablosu belirgin bir under-confidence
   gösteriyor — bir sonraki fazın ana hedefi bu olmalı.
3. **Isınma dönemi ayıklanmadı.** İlk sezonlar da metriklere dâhil (sezon bazlı tablo
   `backtest_summary.csv` içinde: 2017/18 1,0228 → 2023/24 0,9990 → 2025/26 1,0217).
4. **Competition context tek parametre grubu.** Ayrı competition-specific katsayı denenmedi
   (görev gereği ilk modelde ortak baseline).
5. **Beraberlik yapısı zayıf.** Model beraberliği ortalama olarak fazla tahmin ediyor;
   RPS/Brier bunu cezalandırıyor.
