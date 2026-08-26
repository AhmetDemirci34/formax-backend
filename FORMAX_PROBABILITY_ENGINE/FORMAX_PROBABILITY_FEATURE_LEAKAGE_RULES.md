# FORMAX — Feature Leakage Kuralları ve Test Planı

**Tarih:** 2026-08-20
**Kapsam:** Yalnızca kural ve test tanımı. Model kurulmadı, katsayı belirlenmedi, tahmin üretilmedi.

---

## 1. TEMEL KURAL — PredictionTimestamp

Her maç için bir **PredictionTimestamp** vardır: o maçın `Date` alanı (saat biliniyorsa `KickoffUtc`).

> Bir feature, yalnızca **olay zamanı PredictionTimestamp'tan kesinlikle ÖNCE** olan verilerden
> hesaplanabiliyorsa `LEAKAGE_SAFE`'tir.

Bu tek cümle üç şeyi yasaklar:

1. **Aynı maçın kendi bilgisi** — ilk yarı skoru, maç statüsü (AET/PEN), uzatma golü, penaltı serisi.
   Bunlar maç *sırasında* oluşur; tahmin anında bilinmez.
2. **Sonraki maçlar** — sezon sonu puanı, sezon toplam golü, nihai lig sırası, "sezonun geri kalanı".
3. **Zamanı belirsiz üçüncü taraf türevleri** — kaynak dosyanın içinde hazır gelen Elo ve Form
   kolonları. Maçın kendisini içerip içermediği kanıtlanamıyorsa `LEAKAGE_RISK`'tir.

---

## 2. REDDEDİLEN FEATURE'LAR VE GEREKÇELERİ

| Feature | Neden reddedildi |
|---|---|
| `HalfTimeHomeGoals/AwayGoals` (girdi olarak) | Maç içi bilgi. Yalnız İY marketleri için **hedef** olabilir. |
| `ExtraTimeHomeGoals/AwayGoals` (girdi) | Maç içi bilgi. |
| `PenaltyShootoutHome/Away` (girdi) | Maç içi bilgi. |
| `MatchStatus` (girdi) | `AET`/`PEN` değeri maçın uzatmaya gittiğini söyler — sonucun bir parçası. |
| `HomeElo` / `AwayElo` (football-data kaynağı, %98,71) | Üçüncü tarafın hesabı; maç öncesi mi sonrası mı hesaplandığı **doğrulanamıyor**. Kendi Elo'muzu genişleyen pencereyle üretmek serbest. |
| `Form3Home/Form5Home/...` (football-data, %100) | Aynı gerekçe. |
| Sezon sonu sıralaması / sezon toplam istatistikleri | Tahmin tarihinden sonraki maçları kullanır. |
| `DataQualityFlag` | Futbol sinyali değil, veri hattı izi. Sezon ve kaynakla korelasyonlu olduğu için modele sahte sinyal taşır. |

**Kural:** `LEAKAGE_RISK` işaretli hiçbir feature data contract'ta CORE veya OPTIONAL olamaz.

---

## 3. GENİŞLEYEN PENCERE (EXPANDING WINDOW) ZORUNLULUĞU

Türetilmiş her takım feature'ı şu şekilde üretilmelidir:

```
maçları Date'e göre artan sırala
her maç için:
    feature'ları = f(bu takımın SADECE önceki maçları)
    feature satırını yaz
    ANCAK BUNDAN SONRA bu maçı geçmişe ekle
```

Bu sıra (`önce oku, sonra yaz`) tek satırlık ama kritik bir kuraldır; ters çevrilirse her satır
kendi sonucunu görür. Bu envanterdeki tüm coverage ölçümleri bu sırayla hesaplandı
(`scripts/p2_derived.ps1`).

**Aynı gün oynanan maçlar:** aynı tarihe düşen iki maç birbirini beslememelidir. Kural:
karşılaştırma `<` (kesin küçük) olmalı, `<=` değil. Saat bilgisi %79 satırda var ama saat dilimi
karışık olduğu için **tarih düzeyinde kesin eşitsizlik** kullanılmalıdır.

---

## 4. İKİ AYAKLI EŞLEŞME (UEFA) — ÖZEL DURUM

UEFA satırlarının **%85,5'i** iki ayaklı bir eşleşmenin parçası (4.001 eşleşmenin 2.984'ü çok ayaklı).

* İkinci ayak için **ilk ayağın skoru bilinir** → `tie_aggregate_*` LEAKAGE_SAFE'tir ve kullanılmalıdır.
* İkinci ayak maçları **bağımsız gözlem değildir**. Train/test bölmesi yaparken bir eşleşmenin iki
  ayağı farklı taraflara düşerse sızıntı olur.
  **Kural:** bölme birimi maç değil, **eşleşme (tie)** olmalıdır.
* Uzatma yalnız ikinci ayakta oynanır → `HomeGoals` (uzatma dâhil) ile `RegularTimeHomeGoals`
  arasındaki fark UEFA'da sistematiktir. Knockout maçlarda hedef olarak **90 dakika skoru**
  (`RegularTime*`) önerilir.

---

## 5. ZAMAN BÖLMESİ KURALI

Rastgele (random) train/test bölmesi **yasaktır** — 2025/26 verisiyle eğitip 2018/19'u tahmin etmek
sızıntıdır.

Zorunlu yöntem: **walk-forward / genişleyen pencere**. Örn. sezon bazlı:
eğitim = 2017/18…2021/22 → doğrulama = 2022/23 → sonra pencereyi bir sezon kaydır.
(Katsayı, pencere uzunluğu ve model seçimi bu görevin kapsamı dışındadır.)

---

## 6. OTOMATİK LEAKAGE TESTLERİ

Aşağıdaki testler feature üretim hattına CI olarak bağlanmalıdır. Her test, feature dosyası
üretildikten sonra **veri üzerinde** çalışır; model gerektirmez.

### T1 — Gelecek referansı yok (temel test)
Her feature satırı için kullanılan kaynak maçların `Date` değeri, hedef maçın `Date` değerinden
**kesinlikle küçük** olmalı. Uygulama: feature üretimi sırasında kullanılan `MatchId` listesini
üret ve tarihlerini karşılaştır. Başarısızlık = derhal durdur.

### T2 — Kaydırma (shift) testi
Aynı feature'ı iki kez üret: (a) normal, (b) her takımın geçmişinden **son maçı çıkararak**.
Feature değerleri (a) ve (b) arasında **değişmelidir**; değişmiyorsa feature geçmişi gerçekten
kullanmıyordur. Değişim yoksa uyarı ver.

### T3 — Sahte hedef testi
Hedef sütununu rastgele karıştır (permütasyon) ve feature'ları yeniden üret.
Feature değerleri **değişmemelidir**. Değişiyorsa feature hedeften besleniyordur → sızıntı.

### T4 — İlk maç testi
Bir takımın veri setindeki **ilk** maçında geçmişe dayalı tüm feature'lar `null` olmalıdır.
Dolu geliyorsa hesap yanlış sıradadır. (Bu durum 709 takımın hepsi için kontrol edilebilir.)

### T5 — Aynı gün testi
Aynı tarihte oynanan iki maçtan biri, diğerinin feature'ına girmemelidir.
Uygulama: aynı `Date` üzerinde iki maçı olan takımları bul (turnuva takvimlerinde nadir ama mümkün),
feature'ın ikisini de saymadığını doğrula.

### T6 — Tie bütünlüğü testi
Bir UEFA eşleşmesinin (aynı sezon, aynı takım çifti) tüm ayakları **aynı** train/test bölümünde
olmalıdır. Aksi halde başarısız.

### T7 — Sezon sınırı testi
`season_points_to_date` gibi sezon-içi feature'lar sezon değişiminde **sıfırlanmalıdır**.
Yeni sezonun ilk maçında değer 0 olmalı.

### T8 — Kaynak kolon karantinası
Feature üretim kodu `HalfTime*`, `ExtraTime*`, `PenaltyShootout*`, `MatchStatus`,
`DataQualityFlag`, `ResultResolution` ve `AlternateReported*` kolonlarını **girdi olarak okumamalı**.
Uygulama: statik kontrol — feature üreticisine verilen dataframe'den bu kolonlar baştan düşürülür
(allow-list yaklaşımı). Bu, T1-T7'nin hepsinden ucuz ve en güvenli olanıdır.

---

## 7. ÖRNEK — bir feature'ın test gereksinimi

```
Feature:      RollingTeamStrength (F102/F103/F108)
Tanım:        Takımın önceki maçlarından hesaplanan atak/defans oranı
Test T1:      Kullanılan her kaynak maç Date < hedef maç Date
Test T2:      Son maç geçmişten çıkarıldığında değer değişmeli
Test T3:      Hedef permüte edildiğinde değer değişmemeli
Test T4:      Takımın ilk maçında null olmalı
Test T8:      Üretici yalnızca izinli kolonları görmeli
Minimum örnek: her iki tarafta >=5 önceki maç (aksi halde null) — veri setinde %93,04
```

---

## 8. ÖRNEKLEM YETERLİLİĞİ — sızıntı değil ama güvenilirlik sınırı

Sızıntı olmayan ama **güvenilmez** feature de zararlıdır. Ölçülen sınırlar:

| Kapsam | Her iki tarafta ≥5 önceki maç | Kendi turnuvasında ≥5 | Sezon içi ≥3 |
|---|---|---|---|
| DOMESTIC_LEAGUE | %97,03 | %96,62 | %91,88 |
| UEFA_MAIN | %95,34 | %75,19 | %64,26 |
| UEFA_QUALIFICATION_PLAYOFF | %83,64 | %56,36 | %33,22 |
| **UEFA_QUALIFIER** | **%54,26** | **%27,06** | **%10,92** |

709 takımın **%19,18'i** veri setinde 5'ten az, **%44,29'u** 20'den az maça sahip.

**Kural:** minimum örneklem sağlanmıyorsa feature `null` bırakılır — varsayılan/ortalama değerle
doldurulup "veri varmış" gibi gösterilmez. Doldurma kararı (varsa) model katmanında açıkça
belgelenir.

---

## 9. ZAMAN AĞIRLIĞI (TIME DECAY) — ilerisi için not

Veri seti 2017/18–2025/26 arasını kapsar. **Eski sezonların yeni sezonlarla eşit ağırlıkta
kullanılacağı varsayılmamıştır.** Uygulanabilir yaklaşımlar (katsayı burada belirlenmez):

* örnek ağırlığı `w = exp(-Δgün / H)` — `H` (yarı ömür) bir model hiper-parametresidir;
* sezon bazlı sabit ağırlık;
* yalnız son N sezonu eğitime almak.

Hangisinin seçileceği walk-forward doğrulamasıyla ölçülmelidir; bu görevde ölçülmedi.

---

## 10. KAPSAM DIŞI REKABET UYARISI (fixture density)

Veri seti FORMAX'ın **17 kilitli kapsamını** içerir. Yerel kupalar, süper kupalar, milli takım
araları ve hazırlık maçları **yoktur**. Bu yüzden:

* `DaysRest` gerçek dinlenmenin **üst sınırıdır** (satırların %8,69'unda 20 günden uzun boşluk
  görünüyor — çoğu kapsam dışı bir turnuvanın izidir);
* `MatchesInLast7/14/21Days` **sistematik olarak eksik sayar**.

Bu iki feature `OPTIONAL` sınıfındadır ve kullanılırsa bu sapma modelin dokümantasyonunda
belirtilmelidir.
