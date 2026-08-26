# FORMAX — PROBABILITY CALIBRATION V1

**Tarih:** 2026-08-21
**Durum:** Build ✅ · 16/16 test ✅ · 9 denetim kontrolü PASS · 8 calibration varyantı ölçüldü
**Kapsam:** Yalnız araştırma. Production DB, prediction endpoint, frontend, Gemma, ensemble, xG,
market odds, lineup, injuries — **hiçbiri yapılmadı**. Model **dokunulmadan** dondurularak kullanıldı.

---

## 0. SONUÇ — ÖNDEN

# `CALIBRATION_IMPROVEMENT_NOT_PROVEN`

| | VALIDATION log loss | TEST log loss |
|---|---|---|
| RAW (calibration yok) | 0,987473 | **0,993544** |
| Seçilen yöntem (`ISOTONIC_OVR__EXPANDING`) | 0,986493 | **0,997249** |
| Fark | −0,000980 | **+0,003705 (daha kötü)** |

Validation'daki kazanç kendi gürültüsünün içinde kaldı (%95 GA **[−0,00236, +0,00034]** — sıfırı
içeriyor) ve test'te **ters döndü**.

**Neden:** kalibre edilecek bir şey neredeyse kalmamış. Ham modelin test üzerindeki calibration
error'ı zaten **0,0085**. Bu fazın düzeltmek için kurulduğu az-güvenlilik sorununu, bir önceki
fazdaki **parametre doğrulaması** zaten çözmüş.

**Asıl cevap** (§8'in sorusu — "FORMAX %65 dediğinde gerçekte ne oluyor?"):

> **%60–70 bandı, TEST, 968 tahmin: FORMAX ortalama %64,52 dedi, gerçekte %65,08 oldu. Sapma 0,6 puan.**
> Bu bandın düzeltilmeye ihtiyacı yok.

---

## 1. SABİT MODEL — DEĞİŞTİRİLMEDİ

`model_validation_v2/validated_teamstrength_config.json` **olduğu gibi okundu**:

```
halfLifeDays=100000 | learningRate=0.08 | shrinkageK=0 | ratioSmoothing=1.5
clamp=[0.02,50] | useCompetitionTypePool=false
```

Model: **Independent Poisson V2**. Yeni Team Strength taraması, yeni Dixon-Coles ρ araması, yeni
bivariate taraması, yeni feature — **hiçbiri yapılmadı**.

**Kanıt (test paketinde):** dondurulmuş modelin ürettiği ham olasılıklar, `model_validation_v2`'nin
yayımladığı sayıları birebir veriyor:

```
VALIDATION log loss  0,987473  ✅
TEST log loss        0,993544  ✅   (33.901 tahmin)
```

Ayrıca bir yapısal kanıt: bir calibrator'ın gördüğü **tek şey** `(ham olasılık, sonuç)` çiftidir.
Takım, rating, λ, lig, tarih — hiçbirine erişimi yok. `ICalibrator` arayüzü buna izin vermiyor, yani
"calibration" adı altında gizlice bir model iyileştirmesi yapılamaz. Test: aynı ham olasılık her
zaman aynı kalibre olasılığı üretiyor (33.901 maç üzerinde tarandı, tutarsızlık 0).

---

## 2. SPLIT — DEĞİŞTİRİLMEDİ

`model_validation_v2/split.config.json` aynen kullanıldı:

| Segment | Maç | Aralık |
|---|---|---|
| TRAIN | 18.472 | 2017-06-27 … 2022-05-29 |
| VALIDATION | 7.636 | 2022-06-21 … 2024-06-02 |
| TEST | 7.793 | 2024-07-09 … 2026-05-30 |

---

## 3. KARŞILAŞTIRILAN YÖNTEMLER

Dört yöntem, iki fit rejimi. Üçü **iç içe geçmiş** (nested), yani aralarındaki karşılaştırma
anlamlı — birbiriyle ilgisiz formüllerin güzellik yarışması değil:

| Yöntem | Serbest parametre | Ne yapabilir |
|---|---|---|
| `NO_CALIBRATION` | 0 | referans |
| `TEMPERATURE_SCALING` | **1** | `p'_k ∝ p_k^(1/T)` — yalnız güveni artırır/azaltır |
| `VECTOR_SCALING` | **6** | `softmax(a_k·z_k + b_k)` — sınıf başına ölçek + kaydırma |
| `MULTINOMIAL_LOGISTIC` | **12** | `softmax(W·z + b)`, W tam 3×3 — sınıflar arası kütle taşıyabilir |
| `ISOTONIC_OVR` | parametresiz | monoton, biçim varsayımı yok |

`T ⊂ vector ⊂ multinomial`: sıcaklık, `W=(1/T)I, b=0` özel hâli; vektör, `W` köşegen hâli.
Test bunu doğruluyor: fit edilen küme üzerinde `matris ≤ vektör ≤ sıcaklık ≤ ham`.

Her fit **birim dönüşümden** (identity, yani tam olarak ham model) başlıyor. Yani optimizasyon
"calibration yok" noktasından başlıyor ve oradan uzaklaşan her şeyi veri talep etmiş oluyor.

### Isotonic nasıl uygulandı (§3-C açıkça istiyor)

1. Üç bağımsız ikili problem: "ev kazandı mı?", "beraberlik mi?", "deplasman kazandı mı?".
2. Her biri için ham olasılıktan gözlenen frekansa **monoton** bir eşleme, PAVA (Pool Adjacent
   Violators) ile. PAVA **parametresizdir** — bin sayısı yok, smoothing sabiti yok, test setine
   göre ayarlanabilecek hiçbir düğme yok.
3. Üç eşleme bağımsız uygulanınca toplam 1 **değildir**.
4. Üç değer tabanlanır ve toplamlarına bölünür — standart simplex-koruyan düzeltme.

> 4. adım gerçek bir tavizdir ve öyle raporlanır: yeniden normalizasyon, isotonic regresyonun bir
> sınıfa az önce atadığı frekanstan onu tekrar uzaklaştırabilir. §7'de bunun ölçülen zararı var.

### Optimizasyon

Ölçekleme yöntemleri genelleştirilmiş doğrusal modeldir → NLL **konveks** ve Hessian kapalı formda.
6–12 parametre için **sönümlü Newton** kullanıldı: ~10 geçişte gradyan çift duyarlıklı sıfıra iner.

> İlk uygulama Adam ile yapılmıştı ve 6000 iterasyonda `|grad| = 2,3e-5`'te takılıyordu. Bu
> **yakınsamış bir fit değildir** ve toleransı gevşetip PASS yazmak yerine çözücü değiştirildi.
> Test artık yakınsamayı doğruluyor.

---

## 4. FIT REJİMLERİ — §4 ile §5 arasındaki gerilim

Görev iki farklı şey istiyor:

* **§4:** "Calibration parametreleri yalnız TRAIN üzerinde öğrenilecek."
* **§5:** "rolling / expanding calibration uygula... en basit güvenli yaklaşım: expanding temporal calibration."

İkisi de uygulandı ve ikisi de ayrı ayrı raporlandı:

| Rejim | Nasıl | §'ye karşılık |
|---|---|---|
| `STATIC_TRAIN` | Bir kez TRAIN üzerinde fit edilir, **dondurulur** | §4, harfi harfine |
| `EXPANDING` | Her ayın 1'inde, **kesinlikle daha önce** oynanmış tüm maçlarla yeniden fit edilir | §5 |

Isınma: 500 maçlık geçmiş birikene kadar expanding rejim **hiç kalibre etmez**, ham olasılığı
olduğu gibi geçirir ve izinde "aktif değil" yazar. 107 refit noktasının 104'ü aktif oldu.

**Dürüstlük notu — bu ikisi aynı şey değildir.** `EXPANDING` bir test maçını tahmin ederken daha
önce oynanmış test maçlarından öğrenir. Bu **sızıntı değil** (gelecek kullanılmıyor, canlı bir
sistem tam olarak böyle çalışır) ama "donmuş model, dokunulmamış veri" değerlendirmesi de değildir;
canlı çalışmanın simülasyonudur. `STATIC_TRAIN` ise saf donmuş değerlendirmedir. İkisi de aşağıda
ayrı satırlarda.

---

## 5. SEÇİM — YALNIZ VALIDATION

Kural önceden ilan edildi: **en düşük VALIDATION log loss**; eşitlik Brier → RPS → calibration error
ile bozulur.

| Yöntem | LogLoss | Brier | RPS | CalErr | Accuracy |
|---|---|---|---|---|---|
| **ISOTONIC_OVR · EXPANDING** | **0,986493** | 0,58818 | 0,20275 | 0,00499 | 0,5224 |
| MULTINOMIAL_LOGISTIC · EXPANDING | 0,986599 | **0,58807** | **0,20267** | **0,00347** | 0,5237 |
| MULTINOMIAL_LOGISTIC · STATIC | 0,986658 | 0,58810 | 0,20269 | 0,00396 | 0,5236 |
| ISOTONIC_OVR · STATIC | 0,986676 | 0,58832 | 0,20281 | 0,00549 | 0,5229 |
| VECTOR_SCALING · EXPANDING | 0,986892 | 0,58825 | 0,20272 | 0,00501 | 0,5223 |
| VECTOR_SCALING · STATIC | 0,986901 | 0,58827 | 0,20274 | 0,00522 | 0,5215 |
| TEMPERATURE · STATIC | 0,987273 | 0,58846 | 0,20274 | 0,00515 | 0,5236 |
| TEMPERATURE · EXPANDING | 0,987299 | 0,58847 | 0,20275 | 0,00513 | 0,5236 |
| **NO_CALIBRATION** | 0,987473 | 0,58865 | 0,20280 | 0,00774 | 0,5236 |

Seçilen: **`ISOTONIC_OVR__EXPANDING`**.

Ama tabloya dikkatli bakmak gerekiyor: **en iyi ile ham model arasındaki fark 0,00098**, ve dokuz
satırın tamamı 0,001 log loss aralığına sıkışmış. Yöntemler arası fark daha da küçük — 4. hanede.

Eşleştirilmiş bootstrap bunu doğruluyor:

```
VALIDATION, seçilen vs ham: Δ = -0,000980   %95 GA [-0,00236, +0,00034]   NOT SIGNIFICANT
```

> **Seçim, veri tarafından desteklenmeyen bir farkın üzerine yapıldı.** İlan edilen kural en düşük
> log loss'u seçmeyi emrettiği için kural uygulandı; ama kuralın kendisi bir zayıflık taşıyordu ve
> bu, sonucu görmeden fark edilebilecek bir şeydi. §11'de öneri olarak duruyor.

---

## 6. TEST — KİLİTLENMİŞ SEÇİMİN TEK SEFERLİK DEĞERLENDİRMESİ

| | LogLoss | Brier | RPS | CalErr | Accuracy |
|---|---|---|---|---|---|
| RAW (calibration yok) | **0,993544** | 0,59285 | 0,20531 | 0,00846 | 0,5212 |
| ISOTONIC_OVR · EXPANDING | 0,997249 | **0,59269** | **0,20529** | **0,00628** | 0,5196 |

```
TEST, seçilen vs ham: Δ log loss = +0,003705   %95 GA [-0,00018, +0,00888]
                      Δ Brier    = -0,000157
                      Δ RPS      = -0,000012
                      calibration error 0,00846 -> 0,00628
```

**Calibration error düştü, log loss yükseldi.** Görevin §12/§19'da uyardığı tuzak tam olarak budur:
bir dönüşüm güvenilirlik eğrisini düzeltirken, eğrinin vekili olduğu şeyde daha kötü olabilir.

### Diğer yöntemler TEST'te (seçim kilitlendikten SONRA hesaplandı, hiçbir şeye karar vermedi)

| Yöntem | LogLoss | Ham modele göre |
|---|---|---|
| VECTOR_SCALING · EXPANDING | 0,992620 | −0,000924 |
| VECTOR_SCALING · STATIC | 0,992870 | −0,000674 |
| MULTINOMIAL_LOGISTIC · EXPANDING | 0,992895 | −0,000649 |
| MULTINOMIAL_LOGISTIC · STATIC | 0,993298 | −0,000246 |
| TEMPERATURE · EXPANDING | 0,993534 | −0,000010 |
| TEMPERATURE · STATIC | 0,993541 | −0,000003 |
| **NO_CALIBRATION** | **0,993544** | — |
| ISOTONIC_OVR · EXPANDING (seçilen) | 0,997249 | +0,003705 |
| ISOTONIC_OVR · STATIC | 0,998900 | +0,005356 |

Bu tablo kararı değiştirmez ama **başarısızlığın türünü** gösterir: sorun "yanlış yöntemi seçtik"
değil. Parametrik yöntemler test'te ham modeli ancak **0,0009'a kadar** geçiyor — ilan edilen 0,0010
operasyonel eşiğin altında. Sıcaklık ölçekleme fiilen birim dönüşüm (T = 0,95). Yani hiçbir yöntem
anlamlı bir kazanç üretmiyor; esnek olan (isotonic) ise açıkça zarar veriyor.

> **En iyi test sonucunu veren yöntem `VECTOR_SCALING__EXPANDING`'dir. SEÇİLMEDİ.** Seçmek, test
> setine bakarak yöntem seçmek olurdu — görevin §4 ve §12'de yasakladığı şey. Burada yalnız
> raporlanıyor.

---

## 7. GÜVENİLİRLİK BANTLARI — ASIL SORU

### Ham model, TEST (7.793 maç, üç olasılık da dâhil)

| Bant | N | Söylenen | Gerçekleşen | Fark |
|---|---|---|---|---|
| 0–10% | 650 | 0,0703 | 0,0723 | −0,0020 |
| 10–20% | 2.817 | 0,1586 | 0,1619 | −0,0032 |
| 20–30% | 8.753 | 0,2567 | 0,2492 | +0,0076 |
| 30–40% | 4.952 | 0,3410 | 0,3358 | +0,0052 |
| 40–50% | 2.780 | 0,4465 | 0,4647 | −0,0183 |
| **50–60%** | **1.783** | **0,5456** | **0,5637** | **−0,0180** |
| **60–70%** | **968** | **0,6452** | **0,6508** | **−0,0057** |
| **70–80%** | **465** | **0,7440** | **0,7376** | **+0,0064** |
| **80–90%** | **183** | **0,8417** | **0,8306** | **+0,0110** |
| **90–100%** | **28** | **0,9256** | **0,8571** | **+0,0684** |

Model %40–60 aralığında hâlâ hafifçe **az güvenli** (1,8 puan), %70–90 aralığında hafifçe **fazla
güvenli** (0,6–1,1 puan). %90+ bandında N = 28 — hiçbir sonuç çıkarılamaz.

### Bunun önemi: bir önceki faz bu işi zaten yapmış

V1 backtest raporunda, aynı bant için sapma **−8,0 puandı** (%64,2 deniyor, %72,2 oluyordu) ve
"modelin ana sorunu" olarak işaretlenmişti. Şimdi **−0,6 puan**.

⚠️ Bu birebir bir karşılaştırma değil: V1 tablosu Dixon-Coles V1 ile tüm 33.901 maç üzerinde,
buradaki ise Independent Poisson V2 ile yalnız 7.793 test maçı üzerinde ölçüldü. Ama yön ve
büyüklük farkı, sapmanın nereye gittiğini açıkça gösteriyor: **düzenlileştirmeyi kapatan parametre
doğrulaması, az-güvenliliği de kapatmış.** Calibration fazı, kendisine bırakılan işin çoğunu
zaten yapılmış buldu.

### Seçilen yöntem aynı bantlarda ne yaptı (TEST)

| Bant | N | Söylenen | Gerçekleşen | Fark |
|---|---|---|---|---|
| 40–50% | 3.046 | 0,4433 | 0,4485 | −0,0051 |
| 50–60% | 1.761 | 0,5489 | 0,5417 | +0,0072 |
| 60–70% | 1.105 | 0,6528 | 0,6452 | +0,0075 |
| 70–80% | 550 | 0,7342 | 0,7273 | +0,0069 |
| 80–90% | 233 | 0,8255 | 0,8112 | +0,0143 |
| 90–100% | 29 | 0,9699 | 0,8621 | +0,1078 |

Orta bantlardaki az-güvenliliği düzeltti (−1,8 → +0,7 puan), ama üst bantlarda fazla-güvenliliği
**büyüttü** — özellikle %90+ bandında 0,9256 → 0,9699 diyerek, gerçekleşme %86'da kalırken.
Log loss'un neden kötüleştiğini gösteren yer burası: log loss, yüksek güvenle yapılan hataları
ağır cezalandırır.

### Neden validation'da işe yaradı da test'te yaramadı

| Bant | VALIDATION sapması | TEST sapması |
|---|---|---|
| 40–50% | −0,0088 | −0,0183 |
| 50–60% | −0,0068 | −0,0180 |
| **60–70%** | **−0,0394** | **−0,0057** |
| 70–80% | −0,0193 | +0,0064 |
| 80–90% | +0,0356 | +0,0110 |

**Artık sapma iki örneklem-dışı pencere arasında bile sabit değil.** Validation'da %60–70 bandı
3,9 puan az güvenliydi; test'te 0,6 puan. Isotonic, validation'daki o 3,9 puanı düzeltmeyi öğrendi
ve test'e taşıdı — ama test'te düzeltilecek 3,9 puan yoktu. Bu, aşırı uydurmanın ders kitabı hâli
ve `CALIBRATION_IMPROVEMENT_NOT_PROVEN`'ın somut nedeni.

---

## 8. MÜSABAKA BAZINDA — TEST

| Tür | N | RAW log loss | Kalibre | Fark | Bootstrap kararı |
|---|---|---|---|---|---|
| DOMESTIC_LEAGUE | 5.867 | **0,998048** | 1,004235 | **+0,0062** | **RAW BETTER** (anlamlı) |
| UEFA_MAIN | 1.062 | 0,964033 | **0,961611** | −0,0024 | anlamlı değil |
| UEFA_QUALIFIER | 682 | 1,000970 | **0,996109** | −0,0049 | **CALIBRATION BETTER** (anlamlı) |
| UEFA_QUALIFICATION_PLAYOFF | 172 | 0,982216 | **0,972673** | −0,0095 | **CALIBRATION BETTER** (anlamlı) |
| DOMESTIC_PLAYOFF | 10 | 1,173437 | 1,183559 | +0,0101 | N=10, karar yok |

**Etki gerçek ama yönü müsabakaya göre ters.** Calibration UEFA dilimlerinde istatistiksel olarak
anlamlı biçimde yardım ediyor, yerel ligde istatistiksel olarak anlamlı biçimde zarar veriyor. Yerel
lig verinin %75'i olduğu için toplam sonuç negatif.

Bu tam olarak §19'un "competition bazında aşırı dengesiz" dediği durum ve `NOT_PROVEN` kararının
ikinci bağımsız gerekçesi.

Calibration error de aynı hikâyeyi anlatıyor: `DOMESTIC_LEAGUE`'de ham modelin ECE'si zaten **0,0063**
— yani orada düzeltilecek bir şey yok ve isotonic onu **0,0124'e yükseltiyor**. `UEFA_MAIN`'de ise
ham ECE **0,0451**, calibration onu **0,0342**'ye indiriyor.

> Ayrı ayrı, müsabaka bazında calibration bir sonraki fazın makul bir sorusu. **Bu görevde
> yapılmadı** — burada tek bir global dönüşüm test edildi.

---

## 9. COLD START — TEST

| Sınıf | N | RAW log loss | Kalibre | RAW ECE | Kalibre ECE |
|---|---|---|---|---|---|
| Rich | 7.225 | **0,991859** | 0,996008 | 0,0084 | 0,0065 |
| Established | 255 | **1,010336** | 1,011175 | 0,0226 | 0,0182 |
| Limited | 130 | **1,010886** | 1,015426 | 0,0382 | 0,0440 |
| Developing | 110 | 1,043291 | **1,034101** | 0,0502 | 0,0398 |
| NoHistory | 73 | 0,995847 | **0,983517** | 0,0539 | 0,0640 |

Aynı örüntü: calibration, ham modelin zaten iyi kalibre olduğu büyük dilimde (`Rich`, veri %93'ü)
zarar veriyor; kanıtın zayıf ve kalibrasyonun bozuk olduğu küçük dilimlerde yardım ediyor.

⚠️ `Developing` (110) ve `NoHistory` (73) örneklemleri karar için çok küçük. İşaret olarak
okunmalı, kanıt olarak değil.

---

## 10. DENETİM — 9/9 PASS

| Kontrol | Beklenen | Ölçülen |
|---|---|---|
| Olasılık toplamı hatası (9 varyant × 33.901 maç) | < 1e-12 | **2,2e-16** |
| [0,1] dışında olasılık | 0 | **0** |
| Eğitim geçmişi, uygulandığı maçlara uzanan refit | 0 | **0** |
| STATIC_TRAIN calibrator'ın gördüğü en geç maç | < 2022-06-01 | **2022-05-29** |
| TEST içindeki EXPANDING refit'leri (tasarım gereği, sızıntı değil) | zaman ihlali 0 | **92 refit, 0 ihlal** |
| Geçmişi uygulandığı döneme taşan refit | 0 | **0** |
| Aynı ham olasılığa farklı kalibre olasılık verilmesi | 0 | **0** |
| Dondurulmuş modelin V2 test log loss'unu yeniden üretmesi | 0,993544 | **0,993544** |
| Ham tahmini olmayan maç | 0 | **0** |

Ayrıca test paketinde (16/16), sentetik veriyle:

* Sonraki maçların sonuçları değiştirildiğinde **önceki** kalibre olasılıklar bit bit aynı kalıyor —
  ve boşluk kontrolü: aynı değişiklik sonraki olasılıkları gerçekten oynatıyor.
* Yeterli geçmiş yokken expanding rejim hiç kalibre etmiyor, ham olasılığı olduğu gibi geçiriyor.
* Az güvenli veriye `T < 1`, fazla güvenli veriye `T > 1` çıkıyor.
* Zaten kalibre veride `T ≈ 1` — yani yöntem, gerek yokken bir şey uydurmuyor.
* Uç girdilerde (`p = 1e-9`, `p = 0,999`) her yöntem hâlâ geçerli bir dağılım döndürüyor.

---

## 11. NE ÖĞRENİLDİ, NE ÖNERİLİYOR

**Kanıtlanan:**

1. Bu model, bu veride, **zaten kalibre**. Test ECE 0,0085; %60–70 bandında sapma 0,6 puan.
2. Test edilen hiçbir global calibration yöntemi anlamlı iyileşme sağlamıyor. En iyisi bile
   (`VECTOR_SCALING__EXPANDING`, −0,0009) ilan edilen operasyonel eşiğin altında.
3. Esnek, parametresiz yöntem (isotonic) en kötü genelleyen. Serbestlik derecesi arttıkça
   validation'a uyum artıyor, test'e taşınma azalıyor.
4. Artık sapma **kararsız**: iki örneklem-dışı pencere arasında bile aynı değil. Kalibre edilecek
   sabit bir sapma yoksa, calibration öğrenecek bir şey bulamaz.

**Kanıtlanmayan / yapılmayan:**

1. **Müsabaka bazında calibration.** UEFA dilimlerinde etki anlamlı ve olumlu; yerel ligde anlamlı
   ve olumsuz. Ayrı dönüşümler denenmedi — bu görevde tek global dönüşüm test edildi.
2. **Cold-start bazında calibration.** Aynı sinyal, ama örneklemler (73–255) karar için çok küçük.
3. **%90+ bandı.** TEST'te 28 tahmin. Bu bant hakkında hiçbir şey söylenemez.
4. `DOMESTIC_PLAYOFF`: N = 10, yalnız eksiksizlik için tabloda.

**Bir sonraki faz için öneri (bu görevde uygulanmadı):**

Seçim kuralı "en düşük validation log loss" idi ve anlamlı olmayan bir farkın üzerine yöntem
seçmeye zorladı. Daha sağlam kural: **calibration ancak validation kazancının güven aralığı sıfırı
dışlıyorsa seçilir, aksi hâlde ham model korunur.** Bu kuralla bu fazın çıktısı doğrudan
`NO_CALIBRATION` olurdu — ki test sonucu da tam olarak bunu söylüyor.

---

## 12. PRODUCTION KARARI

**Yok.** Üretime bağlanmadı, prediction endpoint değiştirilmedi, frontend'e yüzde gönderilmedi,
Gemma'ya verilmedi. `calibration_v1/` yalnız araştırma çıktısıdır.

Zincirin bu fazdaki durumu:

```
Historical Data → Validated Team Strength → Independent Poisson → Raw Probability
                                                                        │
                                                    Calibration ────────┘  (fayda kanıtlanmadı)
                                                                        │
                                            Brier / LogLoss / RPS / Reliability ✅ ölçüldü
```

Ham olasılık, kalibre olasılıktan **daha iyi**. Bu fazın üretime taşınacak çıktısı bir dönüşüm
değil, bir **ölçüm**: modelin hangi bantta ne kadar dürüst olduğu artık sayıyla biliniyor.

---

## 13. ÇALIŞTIRMA

```bash
cd FORMAX_PROBABILITY_ENGINE/calibration_v1/FormaxCalibration
dotnet build -c Release
dotnet run -c Release -- test    # 16 test (matematik, simplex, zaman sızıntısı)
dotnet run -c Release -- all     # test + tam calibration koşusu (~14 sn)
```

Seçenekler: `--dataset`, `--split`, `--ts-config`, `--dc-config`, `--out`.

`Formax.slnx`'e eklenmedi, NuGet bağımlılığı yok. `team_strength`, `dixon_coles` ve
`model_validation_v2` projelerinin kaynak kodu **değiştirilmedi**.

---

## 14. ÇIKTILAR

| Dosya | İçerik |
|---|---|
| `raw_predictions.csv` | 33.901 ham tahmin: λ'lar, H/D/A, gerçek sonuç, segment, müsabaka, cold-start |
| `calibrated_predictions.csv` | Aynı maçlar, **sekiz varyantın hepsinin** kalibre olasılıkları + seçilen yöntem |
| `calibration_model.json` | Her yöntemin fit edilmiş parametreleri, isotonic blokları, 107 refit noktasının izi |
| `calibration_comparison.csv` | 9 varyant × 3 segment: LogLoss, Brier, RPS, Accuracy, ECE, MCE |
| `calibration_by_competition.csv` | Müsabaka türü bazında, ham vs seçilen |
| `calibration_by_cold_start.csv` | Cold-start sınıfı bazında, ham vs seçilen |
| `calibration_by_probability_band.csv` | Her yöntem × segment için 10 bant: N, söylenen, gerçekleşen, fark |
| `reliability_curve.csv` | Ham vs kalibre güvenilirlik eğrisi, iki görünüm: tüm sınıflar ve yalnız en yüksek olasılık |
| `calibration_bootstrap.csv` | Eşleştirilmiş bootstrap: ΔLogLoss, ΔBrier, ΔRPS + %95 GA, genel ve müsabaka bazında |
| `calibration_audit.csv` | 9 kontrol: simplex, zaman sözleşmesi, dondurulmuş model |

`calibration_model.json` içindeki `refitTrace`, expanding rejimin her ayki parametresini ve o ay
kaç maçlık geçmişle fit edildiğini tek tek gösteriyor — sıcaklığın veya `W` matrisinin zaman içinde
nasıl kaydığı oradan izlenebilir.
