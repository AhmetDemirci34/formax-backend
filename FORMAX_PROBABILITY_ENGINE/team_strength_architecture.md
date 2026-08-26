# FORMAX — DYNAMIC TEAM STRENGTH MİMARİSİ (tasarım)

**Tarih:** 2026-08-20
**Durum:** Yalnızca mimari tasarım. **Kod yazılmadı, katsayı seçilmedi, rating hesaplanmadı.**
Dixon-Coles / Bivariate Poisson / Elo nihai modeli / ensemble / calibration / prediction YOK.

---

## 1. NEDEN "W/D/L" YETMEZ

Kazanma oranı tek başına üç bilgiyi kaybeder:

1. **Gol üretimi ile gol yeme ayrı yeteneklerdir.** 2-1 kazanan takım ile 1-0 kazanan takım aynı
   değildir; Poisson ailesindeki her model zaten `attack` ve `defence` parametrelerini ayrı ister.
2. **Rakip gücü.** Lider karşısında alınan beraberlik ile son sıradaki takıma karşı alınan
   beraberlik aynı kanıt değildir.
3. **Saha.** Ölçülen veri: yerel ligde ev sahibi galibiyeti **%43,8**, ortalama ev golü **1,54**,
   deplasman golü **1,24**. Bu fark competition type'a göre de değişiyor (UEFA ana turnuva %47,4).

Bu yüzden Team Strength en az beş kavramı taşımalıdır:

| Bileşen | Tanım |
|---|---|
| `attack_strength` | Beklenenden fazla/az gol atma eğilimi |
| `defence_strength` | Beklenenden az/fazla gol yeme eğilimi |
| `overall_strength` | İkisinin birleşimi (tek skalar özet) |
| `home_strength` | Ev sahibiyken sapma |
| `away_strength` | Deplasmandayken sapma |

---

## 2. TEK BİR ORTAK RATING — competition-local DEĞİL

### 2.1 Ölçülmüş zorunluluk

| Kapsam | Kendi turnuvasında ≥5 önceki maç |
|---|---|
| DOMESTIC_LEAGUE | %96,62 |
| UEFA_MAIN | %75,19 |
| UEFA_QUALIFICATION_PLAYOFF | %56,36 |
| **UEFA_QUALIFIER** | **%27,06** |

UEFA eleme maçlarının **%73'ünde** taraflardan biri o turnuvada 5 maça bile sahip değil.
**Competition-local rating tek başına kullanılamaz** — bu bir tercih değil, verinin dayattığı sonuç.

### 2.2 Tasarım: tek havuz + competition offset

```
strength(team, t)            -> takımın zamana bağlı tek ratingi (tüm kapsamdan beslenir)
competition_offset(comp)     -> o müsabakanın seviye/karakter düzeltmesi
home_advantage(comp_type)    -> saha avantajı, competition type başına ayrı
```

Beklenen gol yapısı kavramsal olarak:

```
lambda_home = f( attack(home,t), defence(away,t), home_advantage(competition_type), competition_offset(comp) )
lambda_away = f( attack(away,t), defence(home,t), competition_offset(comp) )
```

`f`'in biçimi (log-lineer, Poisson, Dixon-Coles düzeltmesi vb.) **bu belgenin kapsamı dışındadır.**
Burada kilitlenen tek şey: **rating tek havuzdan gelir, müsabaka farkı ayrı parametreyle taşınır.**

### 2.3 Neden ayrı offset

Aynı takım hem Süper Lig'de hem UEFA elemesinde oynar. Rating ortak olmazsa eleme maçı için veri
yoktur. Rating ortak olur ama offset olmazsa, farklı seviyedeki müsabakalar aynı sayılır. İkisi
birlikte gerekir.

`competition_offset` başlangıçta müsabaka bazında (11 müsabaka) veya competition type bazında
(5 tip) tanımlanabilir; hangisinin daha iyi olduğu **validation ile ölçülecektir**, burada seçilmedi.

---

## 3. ZAMAN BOYUTU

Rating **statik değil**, her maçtan sonra güncellenen bir durumdur. İki uygulanabilir aile:

| Yaklaşım | Nasıl çalışır | Notlar |
|---|---|---|
| **Sıralı güncelleme** (Elo/Glicko ailesi) | Her maçtan sonra rating adım adım güncellenir | Doğal olarak leakage-safe: güncelleme maçtan *sonra* yapılır |
| **Pencereli yeniden fit** (ağırlıklı en küçük kareler / Poisson regresyon) | Tahmin anına kadarki tüm maçlarla yeniden fit edilir, eski maçlar daha az ağırlıklı | Genişleyen pencere zorunlu |

Her ikisi de aşağıdaki zaman ağırlığını destekler:

```
w(match) = exp( -(t_pred - t_match) / H )
```

`H` (yarı ömür) **bu belgede belirlenmemiştir**; hiper-parametredir ve walk-forward doğrulamayla
seçilecektir. Sabit bir sayı yazmak, ölçmeden karar vermek olurdu.

**Sezon sınırı:** rating sezon başında sıfırlanmaz — takım kimliği süreklidir. Ancak sezonlar arası
kadro değişimi için bir "yaz aralığı gevşemesi" (rating'i havuz ortalamasına doğru bir miktar
çekme) parametresi tasarıma dâhildir; değeri yine ölçülecektir.

---

## 4. GÜNCELLEME SIRASI (leakage kuralı)

```
maçları Date artan sırala
her maç için:
    features = f( rating_state )        # maçtan ÖNCEKİ durum yazılır
    satırı yaz
    rating_state = update(rating_state, maç sonucu)   # ANCAK BUNDAN SONRA güncelle
```

Bu sıra tersine çevrilirse her satır kendi sonucunu görür. Aynı tarihe düşen maçlar birbirini
beslememelidir (kesin `<` karşılaştırması; saat bilgisi satırların yalnız %79'unda var ve saat
dilimi karışık).

---

## 5. ÇOKLU ÖLÇEK — hangi girdiler rating'i besler

| Girdi | Durum | Not |
|---|---|---|
| Gol (atılan/yenilen) | **MEVCUT %100** | Rating'in çekirdeği |
| Maç sonucu (W/D/L) | MEVCUT %100 | Golün türevi, ayrı bilgi değil |
| Ev/deplasman | MEVCUT %100 | Ayrı parametre |
| Rakip kimliği | MEVCUT %100 | Rakip düzeltmesinin temeli |
| Şut / isabetli şut / korner | **KISMİ** — yalnız yerel lig 2017/18–2024/25 (bkz. coverage dosyası) | Rating'in *çekirdeğine* konulamaz: UEFA'da %0 olduğu için aynı rating iki farklı bilgi tabanından beslenir hale gelir |
| xG | **YOK (%0)** | Gelecek |

**Karar:** çekirdek rating **yalnız gollerden** beslenir. Şut/korner temelli sinyaller *ayrı*
bir "performans düzeltmesi" katmanı olarak, yalnız kapsandığı yerde ve **ayrı feature** olarak
denenir. Böylece UEFA maçlarında rating tanımsız hale gelmez.

---

## 6. ÇIKTI ALANLARI (schema adayı)

Rating katmanı, her maç için iki taraf × şu alanları üretir (değerler bu görevde hesaplanmadı):

```
{team}_attack_strength
{team}_defence_strength
{team}_overall_strength
{team}_home_strength          # yalnız ev sahibi tarafı için anlamlı
{team}_away_strength          # yalnız deplasman tarafı için anlamlı
{team}_strength_sample_size   # rating'i besleyen önceki maç sayısı
{team}_strength_confidence    # LOW / MEDIUM / HIGH  (bkz. eşikler)
{team}_strength_last_updated  # rating durumunun tarihi
```

`sample_size` ve `confidence` **zorunludur**: rating'in kendisi kadar, ne kadar kanıta dayandığı
da modele taşınmalıdır. Ölçülen dağılım: 709 takımın %19,18'i 5'ten az, %44,29'u 20'den az maça
sahip.

---

## 7. GÜVEN EŞİKLERİ (rapor amaçlı, model kararı değil)

| Önceki maç sayısı | Etiket | Anlam |
|---|---|---|
| 0 | `NO_HISTORY` | Rating üretilemez → prior gerekir (bkz. cold-start belgesi) |
| 1–2 | `LOW` | Rating üretilir ama havuz ortalamasına güçlü biçimde çekilmelidir |
| 3–4 | `MEDIUM_LOW` | Kullanılabilir, shrinkage yüksek |
| 5–9 | `MEDIUM` | Normal |
| 10+ | `HIGH` | Stabil |

Bu eşikler **feature availability/confidence raporlaması içindir**; modelin bunları nasıl
kullanacağı (ağırlık, shrinkage katsayısı) sonraki fazın kararıdır.

---

## 8. HOME ADVANTAGE — ayrı parametre

Saha avantajı takım ratinginin içine gömülmez; **ayrı ve ölçülebilir** tutulur:

```
home_advantage(competition_type)   # 5 tip için ayrı ölçülebilir yapı
home_advantage(competition)        # 11 müsabaka için ayrı ölçülebilir yapı
home_advantage(team)               # takıma özel sapma (opsiyonel, veri yeterse)
```

Ölçülen ham fark (tanımlayıcı, model değil):

| CompetitionType | Ev % | Beraberlik % | Deplasman % | Ort. toplam gol |
|---|---|---|---|---|
| DOMESTIC_LEAGUE | 43,8 | 25,3 | 30,9 | 2,78 |
| UEFA_MAIN | 47,4 | 21,0 | 31,6 | 2,92 |
| UEFA_QUALIFIER | 48,2 | 21,2 | 30,6 | 2,75 |
| UEFA_QUALIFICATION_PLAYOFF | 49,9 | 23,1 | 26,9 | 2,75 |
| DOMESTIC_PLAYOFF | 32,0 | 32,0 | 36,0 | 1,80 |

**Uyarı:** UEFA rakamları saf saha avantajı değildir — eleme turlarında güçlü takım genellikle
ilk maçı deplasmanda oynar ve eşleşmeler dengesizdir. Bu yüzden saha avantajı, rakip gücü
düzeltmesi *ile birlikte* tahmin edilmelidir. Tek başına yüzdeye bakıp katsayı yazmak yanlış olur.

**DOMESTIC_PLAYOFF satırı 25 maçtır** — hiçbir sonuç çıkarmak için yeterli değildir, tabloda
yalnız bütünlük için yer alıyor.

---

## 9. İKİ AYAKLI EŞLEŞME

UEFA satırlarının **%85,5'i** iki ayaklı bir eşleşmenin parçası (4.001 eşleşmenin 2.984'ü).
İkinci ayak bağımsız bir maç değildir:

* Takımlar toplam skora göre oynar (öndeki savunur, gerideki basar) → gol beklentisi değişir.
* Uzatma yalnız ikinci ayakta oynanır.

Tasarım: `tie_leg_number`, `tie_aggregate_home_goals`, `tie_aggregate_away_goals` ayrı feature
olarak taşınır (ilk ayak sonucu, ikinci ayak için **leakage-safe** bilgidir). Modelin bunları
nasıl kullanacağı sonraki fazın kararıdır.

---

## 10. NE YAPILMADI

* Rating hesaplanmadı, tek bir sayı üretilmedi.
* Decay/half-life, shrinkage, prior, home-advantage katsayıları **seçilmedi**.
* Elo, Dixon-Coles, Bivariate Poisson **kurulmadı**.
* Model kodu yazılmadı.

Bu belge yalnız **mimariyi ve veri gereksinimini** kilitler.
