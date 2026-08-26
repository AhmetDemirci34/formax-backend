# FORMAX — MODEL BASELINE V2: TEAM STRENGTH DOĞRULAMA + POISSON AİLESİ KARŞILAŞTIRMASI

**Tarih:** 2026-08-21
**Durum:** Build ✅ · 19/19 test ✅ · 6.645 aday değerlendirildi · leakage denetimi 10/10 PASS
**Kapsam:** Yalnız araştırma. Üretim DB'si, prediction endpoint, Gemma, frontend, AI Maç Analizi,
Canlı Takip, video, calibration ve ensemble **yapılmadı**.

---

## 0. SONUÇ — ÖNDEN

| Karşılaştırma | TEST log loss | Fark | Karar |
|---|---|---|---|
| Poisson vs Team Strength | 0,993544 vs 1,005900 | −0,012356 | **`POISSON_IMPROVEMENT_PROVEN`** |
| Dixon-Coles vs Poisson | 0,992893 vs 0,993544 | −0,000651 | **`DIXON_COLES_IMPROVEMENT_NOT_PROVEN`** |
| Bivariate Poisson vs Dixon-Coles | 0,993544 vs 0,992893 | +0,000651 | **`BIVARIATE_POISSON_IMPROVEMENT_NOT_PROVEN`** |

Ve doğrulamanın asıl getirisi, model seçiminde değil **parametrelerde**:

| | Eski (UNVALIDATED V1) | Yeni (VALIDATED V2) | Kazanç |
|---|---|---|---|
| TEST log loss, bağımsız Poisson | 1,011418 | **0,993544** | **−0,017874** |
| TEST log loss, Dixon-Coles | 1,013502 | **0,992893** | −0,020609 |
| TEST log loss, Team Strength | 1,023018 | **1,005900** | −0,017118 |

> Team Strength parametrelerini doğrulamak, dört modelin **hepsini** birden, Dixon-Coles ile
> Poisson arasındaki farkın **27 katı** kadar iyileştirdi. Bu fazda kazanılan şey daha iyi bir
> olasılık formülü değil, **daha iyi ayarlanmış bir rating'dir.**

---

## 1. AYNI SPLIT, AYNI FRAMEWORK — KANITI

Yeni ve avantajlı bir test kurgusu üretilmedi. V1 backtest'in gün-gün yürüyen (walk-forward)
mantığı, aynı veri seti, aynı sıralama, aynı λ üretimi ve aynı metrikler **birebir** korundu.

**Kanıt (test paketinde, gerçek veriyle):** V2 harness'i V1 config'leri ve V1'in ürettiği snapshot
CSV'siyle çalıştırıldığında, V1'in yayımladığı dört sayıyı **tam olarak** yeniden üretiyor:

```
SIMPLE_BASELINE      1,06727   ✅
TEAM_STRENGTH        1,02587   ✅
INDEPENDENT_POISSON  1,01318   ✅
DIXON_COLES          1,01515   ✅   (33.901 tahmin)
```

Bunun üzerine eklenen tek şey: **segment etiketi**, **bivariate Poisson modeli** ve **test çiti**.

### Split

V1'de train/validation/test ayrımı **yoktu** — tüm tarih tek parça skorlanıyordu. Parametre
seçebilmek için ayrım zorunlu. Ayrım, ilk aday değerlendirilmeden **önce** sabitlendi:

| Segment | Maç | Aralık | Rolü |
|---|---|---|---|
| TRAIN | 18.472 | 2017-06-27 … 2022-05-29 | Rating kurulur, skorlanmaz |
| VALIDATION | 7.636 | 2022-06-21 … 2024-06-02 | **Tüm parametreler burada seçilir** |
| TEST | 7.793 | 2024-07-09 … 2026-05-30 | **Bir kez, en sonda skorlanır** |

Sınırlar (`2022-06-01`, `2024-06-15`) takvimin **gerçek boşluklarına** düşüyor: sınır gününde
0 maç var, en yakın maçlar 23 ve 37 gün uzakta. Yani hiçbir maç günü ikiye bölünmedi. Sezon
etiketiyle değil **tarihle** bölündü, çünkü 2019/20 ile 2020/21 etiketleri COVID nedeniyle
tarihte çakışıyor.

---

## 2. NASIL SEÇİLDİ — PROTOKOL

Protokol ilk adaydan önce yazıldı ve sonradan değiştirilmedi:

| Aşama | Ne | Nerede |
|---|---|---|
| S1 | `halfLifeDays × learningRate × shrinkageK` tam grid (2.184 aday) | VALIDATION |
| S2 | Kalan parametreler, koordinat inişi | VALIDATION |
| S3 | S1'i S2 değerlerinde tekrarla; hiçbir şey oynamayana kadar | VALIDATION |
| S4 | Dixon-Coles ρ | VALIDATION (+ TRAIN üzerinde ayrıca MLE) |
| S5 | Bivariate bağımlılık parametresi | VALIDATION (+ TRAIN MLE) |

**Seçim ölçütü:** bağımsız Poisson'un VALIDATION log loss'u. Neden o model: λ'ları doğrudan
tüketen ve kendi başına serbest parametresi olmayan tek model o. Seçilen tek Team Strength
config'i sonra **değiştirilmeden** dört modele birden verildi.

Arama **3. turda yakınsadı** (bir tam tur hiçbir değeri oynatmadı).

---

## 3. DOĞRULANMIŞ PARAMETRELER

| Parametre | V1 (doğrulanmamış) | V2 (doğrulanmış) | Not |
|---|---|---|---|
| `halfLifeDays` | 365 | **100000** | = zaman decay'i **yok** |
| `learningRate` | 0,08 | **0,08** | değişmedi |
| `shrinkageK` (prior ağırlığı) | 6 | **0** | = kısmi havuzlama **yok** |
| `ratioSmoothing` | 0,60 | **1,50** | |
| `useCompetitionTypePool` | true | **false** | = havuz priorı **yok** |
| `minIndex` / `maxIndex` | 0,25 / 4,0 | **0,02 / 50,0** | = kırpma fiilen **yok** |
| `minBaselineSamples` | 50 | **50** | değişmedi |
| `venueShrinkageK` | 4 | **4 (doğrulanamaz)** | aşağıda |
| Dixon-Coles `ρ` | −0,05 | **+0,06** | işaret değişti |
| Bivariate `c` | — | **0,00** | = bağımsız Poisson |

### Bu ne anlama geliyor

Doğrulama, V1 rating motorunun **her düzenlileştirme mekanizmasını kapattı**: zaman decay'i,
havuza çekme, kırpma, hepsi. Kalan şey saf, çevrimiçi, çarpımsal bir rating.

Bu, V1 README'sinin kendi bulgusuyla birebir tutarlı: V1 modeli **sistematik olarak az güvenli**
idi (%60 dediğinde gerçekte %72 oluyordu). Az güvenliliğin kaynağı tam olarak bu mekanizmalardı —
hepsi rating'i nötr 1,0'a doğru çekiyor, λ'ları baseline'a yaklaştırıyor ve modeli fazla
beraberlik üretmeye itiyordu. Kapatılınca güven aralığı açıldı ve log loss düzeldi.

> ⚠️ `halfLifeDays=100000`, `shrinkageK=0` ve `clamp=[0,02, 50]` aday aralığının **ucunda**
> seçildi. Bunlar yapay grid sınırı değil, **limit durumları**: sıfırın altında shrinkage,
> "decay yok"un ötesinde daha az decay yoktur. Aramanın yeri kalmadığı için değil, mekanizma
> fiilen kapatıldığı için uçta duruyorlar. `validation_parameters.csv` bunu satır satır
> "LIMITING CASE" olarak etiketliyor.

### Doğrulanamayan parametreler — uydurulmadı, ölçüldü

* **`venueShrinkageK`**: dört modelin **hiçbiri** ev/deplasman indekslerini okumuyor. k = 1 / 4 / 16
  için validation log loss farkı ölçüldü: **0,00000000**. Yani bu parametre bu modellerden
  **tanımlanamaz**. V1 değerinde bırakıldı ve `NOT IDENTIFIABLE` olarak işaretlendi.
* **Cold-start eşikleri** (`limited/developing/established/rich`): bunlar yalnız **etiket** üretir,
  hiçbir olasılığa girmez. Test: eşikler değiştirildiğinde etiketler değişiyor ama her olasılık
  **bit bit aynı** kalıyor.
* **Competition context** (gol baseline'ları, sonuç frekansları) ve sayısal koruma bantları
  (`maxGoals`, λ kırpma, olasılık tabanı) V1 değerlerinde **donduruldu**: bunlar dört modele de
  aynı giren **paylaşılan girdi**, model parametresi değil. Ayarlanmaları dördünü aynı yöne
  iterdi, aralarındaki karşılaştırmayı değiştirmezdi.

---

## 4. DÖRT MODEL — TEST SEGMENTİ (7.793 maç, ham olasılık, calibration YOK)

| Model | LogLoss | Brier | RPS | Accuracy |
|---|---|---|---|---|
| SIMPLE_BASELINE (referans) | 1,063466 | 0,64289 | 0,23005 | 0,4505 |
| TEAM_STRENGTH_BASELINE_V2 | 1,005900 | 0,60116 | 0,20918 | 0,5216 |
| INDEPENDENT_POISSON_V2 | 0,993544 | 0,59285 | 0,20531 | 0,5212 |
| **DIXON_COLES_V2** | **0,992893** | **0,59239** | **0,20523** | **0,5220** |
| BIVARIATE_POISSON_V2 | 0,993544 | 0,59285 | 0,20531 | 0,5212 |

Referans: tamamen belirsiz tahmin (1/3, 1/3, 1/3) log loss = 1,0986.

Dixon-Coles sayısal olarak en önde — **ama bu bir üstünlük kanıtı değil**; §5 nedenini gösteriyor.

---

## 5. ANLAMLILIK VE STABİLİTE

Eşleştirilmiş (paired) bootstrap, 2.000 yeniden örnekleme, sabit tohum, TEST üzerinde. Modeller
her örneklemde **aynı maçları** görüyor.

| Karşılaştırma | Δ LogLoss | %95 GA | Karar |
|---|---|---|---|
| Poisson − Team Strength | −0,012356 | [−0,01641, −0,00813] | **anlamlı** |
| Dixon-Coles − Poisson | −0,000651 | [−0,00136, **+0,00008**] | anlamlı **değil** |
| Bivariate − Dixon-Coles | +0,000651 | [−0,00008, +0,00136] | anlamlı **değil** |
| Poisson − SIMPLE | −0,069922 | [−0,07799, −0,06154] | **anlamlı** |

**Büyüklük ölçütü** (önceden ilan edildi): 0,0010 log loss altındaki fark operasyonel olarak
anlamsız sayılır. Dixon-Coles'un avantajı 0,000651 — gerçekleşen sonuca verilen olasılığı yalnız
**%0,065** oynatıyor. Poisson'un Team Strength'e üstünlüğü ise %1,24.

**Stabilite:** Dixon-Coles her iki test sezonunu da kazanıyor, ama en büyük dilimi —
`DOMESTIC_LEAGUE` (N=5.867) — **kaybediyor** (0,998453 vs 0,998048). Bir model, verinin %75'inde
geride kalıp toplamda önde çıkıyorsa, bu sağlam bir iyileşme değildir.

→ `DIXON_COLES_IMPROVEMENT_NOT_PROVEN`

---

## 6. MÜSABAKA TÜRÜNE GÖRE — TEST

| Tür | N | Team Strength | Poisson | Dixon-Coles | Bivariate |
|---|---|---|---|---|---|
| DOMESTIC_LEAGUE | 5.867 | 1,011899 | **0,998048** | 0,998453 | 0,998048 |
| UEFA_MAIN | 1.062 | 0,974931 | 0,964033 | **0,959978** | 0,964033 |
| UEFA_QUALIFIER | 682 | 1,005507 | 1,000970 | **0,997220** | 1,000970 |
| UEFA_QUALIFICATION_PLAYOFF | 172 | 0,985940 | 0,982216 | **0,978874** | 0,982216 |
| DOMESTIC_PLAYOFF | 10 | 1,145399 | 1,173437 | 1,172412 | 1,173437 |

Dixon-Coles bütün UEFA dilimlerinde önde, yerel ligde geride. Bu tutarlı bir örüntü ama örneklem
UEFA tarafında küçük (1.916 maç toplam); tek başına bir karar dayanağı değil.

`DOMESTIC_PLAYOFF` N=10 — hiçbir sonuç çıkarılamaz, tabloda yalnız eksiksizlik için var.

### V1 → V2, tür bazında (bağımsız Poisson)

| Tür | V1 | V2 | Kazanç |
|---|---|---|---|
| DOMESTIC_LEAGUE | 1,009830 | 0,998048 | −0,0118 |
| UEFA_MAIN | 0,997944 | 0,964033 | −0,0339 |
| **UEFA_QUALIFIER** | 1,042248 | 1,000970 | **−0,0413** |
| UEFA_QUALIFICATION_PLAYOFF | 1,016116 | 0,982216 | −0,0339 |

Doğrulama **her türü** iyileştirdi ve en çok **UEFA elemelerini** iyileştirdi — yani havuz priorını
kapatmak, en çok havuza dayandığı sanılan dilime yaradı. V1'in bu maçlarda "havuz priorı gerekli"
varsayımı, ölçüldüğünde tutmadı.

---

## 7. COLD START — TEST

Zayıf taraf sınıfına göre (log loss):

| Sınıf | N | SIMPLE | Team Strength | Poisson | Dixon-Coles |
|---|---|---|---|---|---|
| Rich | 7.225 | 1,064421 | 1,005445 | 0,991859 | **0,991358** |
| Established | 255 | 1,061457 | 1,022443 | **1,010336** | 1,011204 |
| Limited | 130 | 1,073536 | 1,018750 | 1,010886 | **1,009367** |
| Developing | 110 | **1,027214** | **1,000880** | 1,043291 | 1,034328 |
| NoHistory | 73 | 1,012640 | **0,977853** | 0,995847 | 0,989043 |

**Gol modelinin üstünlüğü Rich sınıfına ait.** Kanıt inceldiğinde tersine dönüyor: `Developing`
ve `NoHistory` dilimlerinde **Team Strength baseline gol modellerini yeniyor**, `Developing`'te
naif frekans baseline'ı bile Poisson'dan iyi. V1'de de aynı örüntü görülmüştü ve doğrulama
sonrasında **kaybolmadı**.

⚠️ Bu üç dilimin toplamı 313 maç. Örneklem küçük; yön göstergesi olarak okunmalı, kesin sonuç
olarak değil.

### UEFA_QUALIFIER, cold-start sınıfına göre (görev §10)

| Sınıf | N | SIMPLE | Team Strength | Poisson | Dixon-Coles |
|---|---|---|---|---|---|
| Rich | 354 | 1,052515 | 1,004476 | 0,993282 | **0,990590** |
| Established | 123 | 1,055578 | 1,028672 | 1,009368 | **1,008761** |
| Limited | 89 | 1,048694 | 1,028575 | 1,031970 | **1,029067** |
| Developing | 63 | 0,982183 | **0,955472** | 0,988104 | 0,975585 |
| NoHistory | 53 | 1,010641 | **0,979368** | 0,996062 | 0,986957 |

Aynı örüntü UEFA elemelerinde daha keskin: `Limited` ve altında gol modeli bir şey katmıyor,
`Developing`/`NoHistory`'de rating-only baseline açık ara önde.

---

## 8. DIXON-COLES ρ — İKİ KRİTER, İKİ FARKLI İŞARET

ρ iki bağımsız yolla belirlendi, ikisi de TEST'i **görmeden**:

| Kriter | Nerede | Seçilen ρ |
|---|---|---|
| 1X2 log loss minimumu | VALIDATION (7.636 maç) | **+0,06** ← kullanılan |
| Tam skor olabilirlik maksimumu (MLE) | TRAIN (18.472 maç) | **−0,06** ← raporlandı |

```
ρ        VALIDATION 1X2 LL     TRAIN skor log-olabilirlik
-0,15        0,993084                -2,982096
-0,10        0,990534                -2,980498
-0,06        0,988977                -2,980087   <- TRAIN MLE tepe noktası
-0,05        0,988656                -2,980101   <- V1'in kanonik sabiti
 0,00        0,987473                -2,980862   (= bağımsız Poisson)
+0,03        0,987110                -2,981871
+0,06        0,987015                -2,983299   <- VALIDATION minimumu
+0,09        0,987198                -2,985154
+0,20        0,990416                -2,995907
```

İki eğrinin de **iç bir optimumu** var — ikisi de aramanın ucuna dayanmadı. Ama **zıt işaretli**.

Bu, V1 README'sinin açık bıraktığı soruyu kapatıyor. V1 "ρ = −0,05 çünkü literatürde böyle"
demişti ve `UNVALIDATED` diye işaretlemişti. Şimdi ölçüldü:

* Dixon-Coles'un orijinal yöntemiyle, **tam skorlar üzerinde** fit edilirse ρ gerçekten negatif
  çıkıyor (−0,06) ve kanonik −0,05'i neredeyse birebir doğruluyor. Yani literatür yanlış değil.
* Ama bizim **skorladığımız hedef tam skor değil, 1X2**. O hedefte aynı veri ρ'nun **pozitif**
  olmasını istiyor.

Sebep beraberlik kütlesinde: negatif ρ, 0-0 ve 1-1'in olasılığını artırır → daha çok beraberlik.
Rating'ten gelen λ'lar zaten fazla beraberlik üretiyordu; negatif ρ sapmayı büyütüyor. Pozitif ρ
tam tersini yapıyor. **Bir modelin doğru parametresi, hangi soruyu sorduğuna bağlı** — ve ρ'yu
tam skor olabilirliğiyle fit edip 1X2 tahmininde kullanmak, bu veri setinde yanlış yönde bir
düzeltme demek.

Yine de: doğru işaretli ρ ile bile Dixon-Coles'un kazancı anlamlılık eşiğinin altında (§5).

---

## 9. BIVARIATE POISSON — NEDEN SIFIR

Model (Karlis-Ntzoufras):

```
X = W1 + W3,  Y = W2 + W3,  W3 ~ Poisson(λ3)
E[X] = λ_ev,  E[Y] = λ_dep,  Cov(X,Y) = λ3 ≥ 0
λ1 = λ_ev − λ3,  λ2 = λ_dep − λ3
```

Marjinal ortalamalar diğer modellerle **aynı λ**'lara sabitlendi; bivariate modelin eklediği tek
şey bağımlılık. λ3 = 0 olduğunda model **tam olarak** bağımsız Poisson'a çöker (test:
1e-12 toleransla birebir aynı).

İki parametrelendirme de tarandı — `λ3 = c·min(λ_ev,λ_dep)` ve sabit `λ3` — ikisi de VALIDATION
üzerinde **c = 0**'ı seçti. Eğri monoton:

```
c        VALIDATION 1X2 LL     TRAIN skor log-olabilirlik
0,00         0,987473               -2,980862   <- her iki kriterin de optimumu
0,02         0,987641               -2,981063
0,06         0,988118               -2,982503
0,10         0,988806               -2,985266
0,20         0,991694               -2,997957
0,40         1,005550               -3,052676
```

Sebep yapısal: bivariate Poisson **yalnız pozitif kovaryans** üretebilir (Cov = λ3 ≥ 0). Pozitif
kovaryans skorları birbirine yaklaştırır → daha çok beraberlik. Bu veri ise (§8'de görüldüğü gibi)
**daha az** beraberlik istiyor. Yani bivariate Poisson'un ekleyebileceği tek şey, tam olarak
verinin istemediği şey.

→ Model daha karmaşık diye üstün sayılmadı; ölçüldü, **hiçbir şey eklemedi** ve seçilen
parametresiyle bağımsız Poisson'un birebir aynısı oldu.

→ `BIVARIATE_POISSON_IMPROVEMENT_NOT_PROVEN`

---

## 10. SEZON BAZINDA STABİLİTE

| Sezon | Segment | Team Strength | Poisson | Dixon-Coles |
|---|---|---|---|---|
| 2017/18 | TRAIN | 1,030074 | 1,013448 | 1,012949 |
| 2018/19 | TRAIN | 1,020607 | **0,999794** | 1,000089 |
| 2019/20 | TRAIN | 1,020980 | 1,009650 | 1,008871 |
| 2020/21 | TRAIN | 1,014787 | 1,008106 | 1,007034 |
| 2021/22 | TRAIN | 1,014406 | 1,002129 | 1,001441 |
| 2022/23 | VALIDATION | 1,006841 | 0,993979 | 0,992954 |
| 2023/24 | VALIDATION | 1,000058 | **0,980909** | 0,981024 |
| 2024/25 | TEST | 1,000216 | 0,985644 | 0,984889 |
| 2025/26 | TEST | 1,011630 | 1,001507 | 1,000961 |

Poisson, Team Strength'i **dokuz sezonun dokuzunda** yeniyor. Dixon-Coles ise Poisson'u dokuz
sezonun **yedisinde** yeniyor, 2018/19 ve 2023/24'te kaybediyor — ve kazandığı sezonlarda da fark
0,0005–0,0010 bandında. İşaret bile sabit değil.

Kalibrasyon ölçülmedi (§17 gereği); yukarıdaki her sayı **ham** olasılıktan.

---

## 11. LEAKAGE DENETİMİ — 10/10 PASS

| Kontrol | Beklenen | Ölçülen |
|---|---|---|
| Gelecek maçın kanıt olarak kullanılması | 0 | **0** |
| Aynı gün oynanan maçın kanıt olması | 0 | **0** |
| Maçın kendi sonucunun kendi tahminine girmesi | 0 | **0,0** (2018-12-22'nin 55 maçı yeniden skorlandı) |
| ↳ aynı değişikliğin sonraki tahminleri oynatması (boşluk kontrolü) | > 0 | **27.733 / 28.159** |
| Parametre aramasının gördüğü TEST maçı | 0 | **0** |
| Aramanın gördüğü en geç maç tarihi | < 2024-06-15 | **2024-06-02** |
| Her aramadan saklanan TEST maçı | > 0 | **7.793** |
| Olasılık toplamı hatası (tüm model × maç) | < 1e-12 | **2,2e-16** |
| Çitli koşu vs tam koşu, λ sapması (26.108 maç) | 0 | **0,0** |
| Tahmin üretilmeyen maç | 0 | **0** |

Üçü ayrıca vurgulanmalı:

1. **Kendi sonucu kendi tahminine girmiyor** — veri setinin en yoğun günü (2018-12-22, 55 maç)
   yeniden skorlandı ve motor baştan çalıştırıldı. O 55 maçın kendi tahminleri **bit bit aynı**
   kaldı. Boşluk kontrolü de yapıldı: aynı değişiklik **sonraki** 28.159 maçın 27.733'ünü oynattı —
   yani perturbasyon gerçekten etkiliydi, test boşuna geçmedi.
2. **Test setinden parametre seçilmedi** — bu bir niyet beyanı değil, mimari bir kısıt. Arama
   yoluna giren tek maç listesi test çitinden geçiyor ve skorlanan **her** maç çite kaydediliyor.
   173.513.768 skorlama işleminin hepsi doğrulandı, ihlal 0, aramanın gördüğü en geç tarih
   2024-06-02.
3. **Çitin kendisi bir şeyi bozmadı** — TEST segmenti veri setinden tamamen çıkarıldığında,
   önceki 26.108 maçın λ'ları **tam olarak** aynı kalıyor (sapma 0,0). Nedensel replay'in
   gerektirdiği şey buydu ve ölçüldü.

---

## 12. ÖNCEKİ SONUÇLARLA KARŞILAŞTIRMA (görev §18)

**Eski sayılar** (V1, tüm tarih 33.901 maç, doğrulanmamış parametreler):

```
Independent Poisson  1,01318      Dixon-Coles  1,01515
Team Strength        1,02587      Simple       1,06727
```

Bu sayılar aynen yeniden üretildi (§1) — ama tüm tarih üzerinde ölçüldükleri için yeni TEST
sayılarıyla doğrudan karşılaştırılamaz. Adil karşılaştırma için **aynı TEST segmenti** üzerinde
her iki parametre seti de skorlandı:

| Model | V1 param (TEST) | V2 param (TEST) | Fark |
|---|---|---|---|
| SIMPLE_BASELINE | 1,063466 | 1,063466 | 0 (rating kullanmıyor — kontrol) |
| TEAM_STRENGTH | 1,023018 | 1,005900 | −0,017118 |
| INDEPENDENT_POISSON | 1,011418 | 0,993544 | −0,017874 |
| DIXON_COLES | 1,013502 | 0,992893 | −0,020609 |

Ve tüm tarih üzerinde, V1 ile aynı ayak izinde:

| Model | V1 param (FULL) | V2 param (FULL) |
|---|---|---|
| TEAM_STRENGTH | 1,025872 | 1,013096 |
| INDEPENDENT_POISSON | 1,013176 | 0,999254 |
| DIXON_COLES | 1,015150 | 0,998704 |

### Soru soru cevap

* **Team Strength tuning sonrası ne değişti?** Rating'in tüm düzenlileştirmesi kapandı ve bu
  dört modelin hepsini iyileştirdi. Bağımsız Poisson TEST'te 1,011418 → 0,993544. Bu kazanç,
  bu fazda ölçülen model-arası tüm farklardan büyük.
* **Poisson ne durumda?** Team Strength baseline'ını anlamlı, istikrarlı ve operasyonel olarak
  anlamlı biçimde yeniyor — dokuz sezonun dokuzunda, beş müsabaka türünün dördünde (beşincisi
  N=10). **Bu fazın taban çizgisi budur.**
* **Dixon-Coles ne durumda?** Doğru işaretli ρ ile artık Poisson'un **altına düşmüyor** (V1'de
  düşüyordu: 1,013502 vs 1,011418). Ama üstünlüğü 0,000651 — güven aralığı sıfırı içeriyor, ilan
  edilen 0,0010 eşiğinin altında ve en büyük dilimi kaybediyor. **Kanıtlanmadı.**
* **Bivariate Poisson ne durumda?** Bağımlılık parametresi sıfır seçildi; model bağımsız
  Poisson'un birebir aynısı. **Hiçbir şey eklemedi.**

**Doğrulama gerçekten genelledi mi?** Evet, ve bunun kanıtı aşırı-uydurmanın tersi yönde:
VALIDATION kazancı −0,015211 (1,002684 → 0,987473), TEST kazancı **−0,017874**. Test kazancı
validation kazancından **büyük**. 6.645 aday denenmiş olmasına rağmen seçim, üzerinde seçim
yapılan sete kayarak değil, dışına genelleyerek çalıştı.

---

## 13. NE KANITLANMADI

1. **Modelin "iyi" olduğu.** TEST log loss 0,9929, tamamen belirsiz tahminin (1,0986) yalnız
   **%9,6** altında. Bu bir taban çizgisidir, ürün kalitesi değildir.
2. **Dixon-Coles ve Bivariate Poisson'un faydası.** İkisi de `NOT_PROVEN`.
3. **Kalibrasyon.** Ölçülmedi, uygulanmadı — bir sonraki fazın işi (görev §17). V1'de belirgin
   bir az-güvenlilik vardı; V2 config'i düzenlileştirmeyi kapattığı için bunun **azalmış** olması
   beklenir, ama bu **ölçülmedi** ve varsayılmamalıdır.
4. **Cold-start dilimlerinde gol modelinin faydası.** `Developing` ve `NoHistory`'de rating-only
   baseline hâlâ önde (§7). Örneklem küçük ama işaret V1'den beri sabit.
5. **`venueShrinkageK`.** Bu model ailesinden tanımlanamaz; ölçüldü ve öyle işaretlendi.
6. **Tek global config'in her dilim için doğru olduğu.** Config, verinin %75'i olan
   `DOMESTIC_LEAGUE` tarafından domine edilen genel validation log loss'una göre seçildi. Dilim
   bazında ayrı config denenmedi.
7. **Sabit λ-üretimi dışındaki hiçbir şey.** Rating yalnız gollerden besleniyor; xG, kadro,
   sakatlık, seyahat, dinlenme, seviye farkı (`competition_offset`) bu fazda yok.

---

## 14. ÇALIŞTIRMA

```bash
cd FORMAX_PROBABILITY_ENGINE/model_validation_v2/FormaxModelValidation
dotnet build -c Release
dotnet run -c Release -- test     # 19 test (matematik + leakage + framework eşdeğerliği)
dotnet run -c Release -- probe    # tek aday değerlendirmesinin süresi
dotnet run -c Release -- all      # test + tam doğrulama (~3 dk, 8 çekirdek)
```

Seçenekler: `--dataset`, `--split`, `--ts-config`, `--dc-config`, `--snapshots`, `--out`.

Proje `Formax.slnx`'e **eklenmedi**, NuGet bağımlılığı yok, `team_strength` ve `dixon_coles`
projelerinin kaynak kodu **değiştirilmedi** — ikisi de referans olarak olduğu gibi kullanıldı.

---

## 15. ÇIKTILAR

| Dosya | İçerik |
|---|---|
| `validated_teamstrength_config.json` | Doğrulanmış config + her alanın kaynağı ve durumu |
| `validation_parameters.csv` | Parametre parametre: seçilen değer, eski değer, aşama, hangi segment, ölçüt, durum |
| `model_comparison.csv` | 2 parametre seti × 5 model × 4 segment (+ cold-start hariç kesit) |
| `metrics_by_competition.csv` | Müsabaka türü ve yerel lig bazında, segment bazında |
| `metrics_by_cold_start.csv` | Cold-start sınıfı, cold-start bayrağı, prior havuzu, **UEFA_QUALIFIER × cold-start**, **UEFA_QPO × cold-start** |
| `metrics_by_season.csv` | Sezon bazında, segment etiketiyle |
| `predictions_all_models.csv` | 33.901 maç × 5 modelin olasılıkları, geniş format (eşleştirilmiş karşılaştırma için) |
| `significance_bootstrap.csv` | Eşleştirilmiş bootstrap: model çiftleri, sezon ve müsabaka türü bazında |
| `dependence_sweep.csv` | ρ ve bivariate c taramalarının tamamı, iki kriterle birlikte |
| `tuning_trace.csv` | Denenen **6.645 adayın hepsi**, parametreleri ve validation metrikleriyle |
| `leakage_audit.csv` | 10 kontrol, beklenen vs ölçülen |
| `split.config.json` | Split tanımı ve seçim ölçütü |

`tuning_trace.csv` bilerek eksiksiz: hangi adayın denendiği ve hangisinin seçildiği dışarıdan
denetlenebilsin diye. Seçim kuralı tek satır — VALIDATION log loss minimumu — ve dosyada
doğrulanabilir.
