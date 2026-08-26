# FORMAX — PRE-MATCH ROLLING FEATURE CONTRACT

**Tarih:** 2026-08-20
**Durum:** Sözleşme + veri katmanı. **Feature değerleri üretilmedi, model kurulmadı, pencere
uzunluğu ve ağırlık katsayısı seçilmedi.**

---

## 1. TEMEL YASAK — maçın kendi istatistiği feature olamaz

`FORMAX_HISTORICAL_MATCH_STATS.csv` içindeki her satır **o maçın kendi** istatistiğidir:

```
2024-10-10  X v Y   HomeShots = 14
```

Bu değer, **aynı maçın** tahmininde kullanılamaz — maç oynanmadan bilinmez. Aynısı goller,
kornerler, kartlar, fauller ve maç statüsü için de geçerlidir.

Kullanım biçimi tektir: **önceki maçların istatistiklerinden rolling feature üretmek.**

```
feature(match M, team T) = g( T'nin, M'nin tarihinden KESİNLİKLE ÖNCE oynadığı maçların istatistikleri )
```

Üretim sırası (leakage kuralı):

```
maçları Date artan sırala
her maç için:
    feature satırını yaz          # yalnız o ana kadarki geçmişten
    SONRA bu maçı geçmişe ekle
```

Bu sıra ters çevrilirse her satır kendi sonucunu görür.

---

## 2. VERİ KATMANI — ne gerçekten var

### 2.1 Match stats katmanı (yeni)

`FORMAX_HISTORICAL_MATCH_STATS/FORMAX_HISTORICAL_MATCH_STATS.csv` — **23.869 maç**.

Kaynak: `Data/Historical/Matches.csv` (football-data.co.uk). Master şeması **değiştirilmedi**;
bu ayrı bir katmandır ve `MatchId` ile master'a bağlanır.

**Eşleşme denetimi:** kaynaktaki 23.900 satırın **23.900'ü** (%100) canonical anahtar
(`Competition|Date|HomeTeamId|AwayTeamId`) ile master satırına birebir bağlandı. Eşleşmeyen satır 0.
31 satırda istatistik kolonları kaynağın kendisinde boş olduğu için stats katmanına girmedi.

**Kapsam:**

| Kapsam | Coverage |
|---|---|
| Tüm model-eligible maçlar | **%70,41** (23.869 / 33.901) |
| DOMESTIC_LEAGUE | **%88,91** (23.869 / 26.846) |
| UEFA_MAIN | **%0** |
| UEFA_QUALIFIER | **%0** |
| UEFA_QUALIFICATION_PLAYOFF | **%0** |
| DOMESTIC_PLAYOFF | **%0** |
| Sezon 2017/18 … 2024/25 | %74,67 – %85,18 |
| **Sezon 2025/26** | **%0** |

Alanlar (kaynakta gerçekten bulunanlar, uydurulmuş kolon yok):
`HomeShots, AwayShots, HomeShotsOnTarget, AwayShotsOnTarget, HomeCorners, AwayCorners,
HomeFouls, AwayFouls, HomeYellow, AwayYellow, HomeRed, AwayRed`.

Katman içi doluluk: bu 12 alanın hepsi 23.869 satırın ~%100'ünde dolu (ayrıntı:
`historical_match_stats_coverage.csv`).

**YOK olanlar:** xG, xGA, possession, pas, kurtarış, ofsayt — hiçbir kaynakta bulunmuyor.

### 2.2 Gol katmanı (master)

Goller **%100** mevcuttur ve tüm competition type'ları kapsar. Bu yüzden gol temelli rolling
feature'lar tüm veri setinde üretilebilir; şut/korner temelli olanlar **yalnız yerel ligde ve
yalnız 2024/25'e kadar** üretilebilir.

---

## 3. FEATURE AİLELERİ

Her aile için: iki taraf (home/away) × pencere seçeneği. **Pencere sabitlenmedi.**

### 3.1 Scoring (gol) — tüm kapsamda üretilebilir

| Feature | Tanım | Kaynak | Üretilebilirlik |
|---|---|---|---|
| `goals_for_rate` | Önceki maçlarda atılan gol / maç | master | %93,04 (her iki tarafta ≥5 önceki maç) |
| `goals_against_rate` | Önceki maçlarda yenilen gol / maç | master | %93,04 |
| `goal_difference_rate` | Farkın maç başına ortalaması | master | %93,04 |
| `clean_sheet_rate` | Gol yemeden bitirme oranı | master | %93,04 |
| `btts_rate` | Karşılıklı gol oranı | master | %93,04 |
| `over25_rate` | Toplam >2,5 gol oranı | master | %93,04 |

### 3.2 Attacking volume — yalnız stats katmanı olan maçlarda

| Feature | Tanım | Kaynak | Üretilebilirlik |
|---|---|---|---|
| `shots_for_avg` | Önceki maçlarda atılan şut ortalaması | stats katmanı | yerel lig ≤2024/25 |
| `shots_against_avg` | Rakiplerin attığı şut ortalaması | stats katmanı | aynı |
| `shots_on_target_for_avg` | İsabetli şut | stats katmanı | aynı |
| `shots_on_target_against_avg` | Rakip isabetli şut | stats katmanı | aynı |
| `shot_conversion_rate` | Gol / şut | stats + master | aynı |
| `shot_accuracy_rate` | İsabetli şut / şut | stats katmanı | aynı |

### 3.3 Set pieces / territory

`corners_for_avg`, `corners_against_avg` — stats katmanı, aynı kapsam.

### 3.4 Discipline

`fouls_for_avg`, `fouls_against_avg`, `yellow_avg`, `red_avg`, `red_rate` — stats katmanı, aynı kapsam.

### 3.5 Form (sonuç temelli, tüm kapsamda)

`points_per_match_last_n`, `wins_last_n`, `goal_diff_last_n`, `form_vs_opponent_strength`
(rakip gücüne göre düzeltilmiş form), `home_form` / `away_form` (yalnız ev / yalnız deplasman
maçlarından).

**Form yalnız `W W L W W` değildir**: sonuç + gol farkı + atılan/yenilen gol + rakip gücü +
ev/deplasman bağlamı birlikte taşınır.

### 3.6 Schedule

`days_rest`, `matches_last_7d`, `matches_last_14d`, `matches_last_21d`.

**Uyarı (ölçülmüş):** veri seti yalnız FORMAX'ın 17 kilitli müsabakasını içerir. Yerel kupalar,
milli takım araları ve hazırlık maçları **yoktur**. `days_rest` gerçek dinlenmenin **üst
sınırıdır** (satırların %8,69'unda 20 günden uzun sahte boşluk görünüyor) ve
`matches_last_*` **sistematik olarak eksik sayar**. Bu aile **CORE değildir**.

### 3.7 H2H

`h2h_matches`, `h2h_home_win_rate`, `h2h_avg_total_goals`, `h2h_last_n` (n = 1, 2, 4).
Kapsam: ≥1 önceki karşılaşma %78,94 · ≥2 %60,35 · ≥4 %43,05.
**Otomatik CORE değildir** — eski sezonlardan gelen H2H'nin bugünkü gücü ne kadar temsil ettiği
backtest ile ölçülmelidir.

### 3.8 Domestic season-to-date (yalnız DOMESTIC_LEAGUE)

`season_points_to_date`, `season_goal_difference_to_date`, `season_table_position_to_date`,
`season_matches_played`. Kapsam: %91,88 (her iki tarafta ≥3 maç).
**Sezon sonu sıralaması kullanılmayacaktır.**

---

## 4. PENCERE SEÇENEKLERİ — kilitlenmedi

Desteklenen adaylar, her feature için:

| Pencere | Kapsam (her iki tarafta yeterli geçmiş) |
|---|---|
| `rolling_3` | %95,50 |
| `rolling_5` | %93,04 |
| `rolling_10` | %87,77 |
| `time_decayed` | %98,37 (≥1 önceki maç) |

**Hangisinin kullanılacağı bu belgede seçilmemiştir.** Seçim walk-forward doğrulamayla yapılacaktır.
`time_decayed` için yarı ömür (`H`) bir hiper-parametredir; değeri burada belirlenmedi.

**Pencere kapsamı seçenekleri** (ayrıca test edilecek):
* `all_competitions` — tüm in-scope maçlar (UEFA eleme takımları için tek gerçekçi seçenek),
* `same_competition_only` — yalnız aynı müsabaka (yerel ligde %96,62; UEFA elemede **%27,06**),
* `same_competition_type` — ara çözüm.

---

## 5. MINIMUM HISTORY VE GÜVEN

| Önceki maç | Etiket | Davranış |
|---|---|---|
| 0 | `NO_HISTORY` | Feature **null** — varsayılan değerle doldurulmaz |
| 1–2 | `LOW` | Üretilir, `confidence = LOW` |
| 3–4 | `MEDIUM_LOW` | Üretilir |
| 5–9 | `MEDIUM` | Normal |
| 10+ | `HIGH` | Stabil |

Her rolling feature yanında **iki denetim alanı zorunludur**:

```
{feature}_sample_size     # feature'ı besleyen önceki maç sayısı
{feature}_confidence      # yukarıdaki etiket
```

Bu eşikler **model kararı değildir**; feature availability/confidence raporlamasıdır.
Eksik feature'ın nasıl doldurulacağı (veya doldurulmayacağı) sonraki fazın kararıdır.

---

## 6. DENETLENEBİLİRLİK — source row takibi

Her feature satırı için, onu besleyen **kaynak maç kimlikleri** izlenebilir olmalıdır:

```
{feature}_source_match_ids   # opsiyonel çıktı: feature'ı üreten MatchId listesi
```

Bu alan üretimde kapatılabilir (boyut), ancak **leakage testleri (T1) için en az bir kez
üretilmelidir**: her kaynak maçın `Date` değeri hedef maçın `Date` değerinden kesinlikle küçük
olmalıdır.

---

## 7. KAYNAK HARİTASI

| Feature ailesi | Kaynak dosya |
|---|---|
| Goller, sonuç, form, H2H, sezon tablosu, takvim | `FORMAX_HISTORICAL_MASTER/FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv` |
| Şut, isabetli şut, korner, faul, kart | `FORMAX_HISTORICAL_MATCH_STATS/FORMAX_HISTORICAL_MATCH_STATS.csv` |
| Takım kimliği, provider id, ülke (496 takım) | `FORMAX_HISTORICAL_MASTER/FORMAX_HISTORICAL_TEAMS.csv` |
| Bahis oranları | `FORMAX_HISTORICAL_MATCH_STATS/FORMAX_HISTORICAL_MARKET_DATA.csv` — **feature değil, benchmark** |
| xG, possession, kadro, sakatlık, hava, hakem, global context | **NOT_AVAILABLE** |

---

## 8. REDDEDİLENLER (bu katmanda üretilmeyecek)

* Maçın kendi istatistiği (şut, korner, kart, faul, gol, ilk yarı skoru, statü) — girdi olarak.
* football-data dosyasındaki hazır `HomeElo/AwayElo` (%98,71) ve `Form3/Form5` (%100) kolonları —
  üçüncü tarafın hesabı, maçın kendisini içerip içermediği **kanıtlanamıyor**. Kendi ratingimizi
  genişleyen pencereyle üretmek serbesttir.
* `C_LTH, C_LTA, C_VHD, C_VAD, C_HTB, C_PHB` — kaynakta bulunan ama **anlamı belgelenmemiş**
  kolonlar. Anlamı doğrulanmadan feature yapılmayacaktır.
* Sezon sonu sıralaması, sezon toplam istatistikleri.
* `DataQualityFlag` gibi veri hattı izleri.

---

## 9. NE YAPILMADI

33.901 maç için feature **değerleri üretilmedi**. Bu belge yalnız hangi feature'ın hangi kaynaktan,
hangi kapsamda ve hangi kuralla üretileceğini kilitler. Model, pencere seçimi ve katsayılar
sonraki fazın işidir.
