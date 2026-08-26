# FORMAX — PROBABILITY ENGINE DATA CONTRACT

**Tarih:** 2026-08-20
**Girdi:** `FORMAX_HISTORICAL_MASTER/FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv` — **33.901 maç**, 709 takım,
2017-06-27 … 2026-05-30, 9 sezon, 17 kilitli müsabaka kapsamı.

**Bu belge yalnızca envanterdir.** Model kurulmadı: Dixon-Coles, Bivariate Poisson, Elo, xG,
ensemble, calibration, prediction ve Gemma'ya dokunulmadı. Katsayı, ağırlık ve eşik belirlenmedi.
Hiçbir kaynak dosya, üretim DB'si, frontend veya canlı sistem değiştirilmedi.

Amaç tek cümleyle: **Probability Engine'in hangi girdilerle gerçekten kurulabileceğini bilmek.**

---

## 1. ÖZET — üç kategori

Envanterde **58 kalem** var. Erişilebilirliğe göre:

| Availability | Ne demek | Kalem |
|---|---|---|
| **CURRENTLY_AVAILABLE** | Model dosyasında doğrudan var, ölçüldü | **20** |
| **CAN_BE_DERIVED** | Mevcut sonuçlardan üretilebilir | **18** |
| **NOT_AVAILABLE** | Model dosyasında **yok** — %0 | **18** (5'i sahip olduğumuz bir dosyadan içe aktarılabilir) |
| **CURRENTLY_AVAILABLE_IN_SOURCE** | Kaynakta var ama güvenilmez | **2** |

Kullanım sınıfına göre:

| Status | Kalem |
|---|---|
| **CORE** — ilk modelde kullanılabilir | **16** |
| **OPTIONAL** — üretilebilir, katkısı ölçülmeli | **15** |
| **FUTURE** — veri gelirse eklenebilir | **18** |
| **REJECT** — sızıntı/güvenilirlik nedeniyle kullanılmayacak | **9** |

Sızıntı durumu: `LEAKAGE_SAFE` 35 · `LEAKAGE_RISK` 10 · `TARGET` 2 · `NA` (veri yok) 11.

Tam liste: `FORMAX_PROBABILITY_FEATURE_INVENTORY.csv` (58 satır, her biri ölçülmüş coverage ile).

---

## 2. KOLON ENVANTERİ (ölçülmüş)

37 kolon. Tam tablo: `FORMAX_PROBABILITY_COLUMN_INVENTORY.csv` (null sayısı, benzersiz sayısı,
min/max, örnek değerler). Kritik alanların doğrulaması:

| Alan | Tip | Null | Benzersiz | Aralık / örnek | Güvenilirlik |
|---|---|---|---|---|---|
| `MatchId` | string | 0 (%0) | 33.901 | `FMXM…` | Deterministik SHA1; duplicate 0 |
| `Season` | string | 0 | 9 | 2017/18 … 2025/26 | Kanıt temelli sezon ataması |
| `Competition` | string | 0 | 11 | 8 yerel lig + 3 UEFA | Canonical |
| `CompetitionType` | enum | 0 | 5 | DOMESTIC_LEAGUE / DOMESTIC_PLAYOFF / UEFA_MAIN / UEFA_QUALIFIER / UEFA_QUALIFICATION_PLAYOFF | Canonical |
| `Round` | string | 1.318 (%3,89) | 288 | "Matchday 12", "Play-offs", "1st Qualifying Round" | **Serbest metin** — kullanılmadan önce canonical eşleme gerekir |
| `Date` | date | 0 | 2.306 | 2017-06-27 … 2026-05-30 | Tüm zamansal bölmelerin temeli |
| `KickoffLocalTime` | string | 7.122 (%21,01) | 108 | "20:45" | Saat dilimi kaynağa göre değişir |
| `KickoffUtc` | timestamp | 28.237 (%83,29) | 1.778 | `2017-08-15T18:45:00Z` | Yalnız saat dilimi kesin kaynaklarda |
| `HomeTeam` / `AwayTeam` | string | 0 | 698 / 701 | canonical ad | 709 kimlik, hepsi CONFIRMED |
| `HomeTeamId` / `AwayTeamId` | string | 0 | 701 / 705 | `FMXT…` | Canonical kimlik |
| `HomeGoals` / `AwayGoals` | int | 0 | 10 / 11 | 0–9 / 0–10 | **HEDEF** — uzatma dâhil, penaltı hariç |
| `RegularTimeHomeGoals` / `…Away` | int | 0 | 10 / 11 | 0–9 | 90 dakika skoru |
| `HalfTimeHomeGoals` / `…Away` | int | 659 (%1,94) | 8 / 7 | 0–7 | Maç içi — **girdi değil** |
| `ExtraTimeHomeGoals` / `…Away` | int | 33.577 (%99,04) | 5 | yalnız AET/PEN | Maç içi — girdi değil |
| `PenaltyShootoutHome` / `…Away` | int | 33.743 (%99,53) | 12 / 14 | yalnız PEN | Maç içi — girdi değil |
| `MatchStatus` | enum | 0 | 3 | FT / AET / PEN | Girdi olarak sızıntı |
| `Source` | string | 0 | 5 | football-data 23.900 · api-football 5.114 · openfootball-json 2.971 · openfootball-txt 1.574 · fixturedownload 342 | Provenance |
| `SourceCount` / `SourceList` | int/string | 0 | 4 / 17 | 1–4 kaynak | Veri güveni sinyali |
| `ProviderMatchId` | string | 28.575 (%84,29) | 5.326 | api-football fixture id | Gelecekteki zenginleştirmenin join anahtarı |
| `ProviderHome/AwayTeamId` | string | 28.579 (%84,3) | 488 / 491 | api-football team id | Kadro/sakatlık zenginleştirmesinin anahtarı |

`ModelExclusionReason` bu dosyada %100 boştur (tanım gereği: dosya yalnız uygun maçları içerir).

---

## 3. A — CURRENTLY_AVAILABLE (kanıtla)

Aşağıdakiler **ölçülerek** doğrulandı, varsayılmadı:

* **Nihai sonuç ve goller** — %100 (33.901/33.901). Eksik skor 0.
* **90 dakika skoru** — %100. Uzatma/penaltı ayrımı korunmuş, `RegularTime + ExtraTime = HomeGoals`
  tutarsızlığı **0 satır**.
* **Ev/deplasman** — yapısal olarak %100 (`HomeTeamId`/`AwayTeamId`).
* **Tarih** — %100, eksik 0.
* **Sezon / competition / competition type** — %100.
* **Round** — %96,11 (UEFA %100, yerel lig %95,13, yerel play-off %60).
* **Takım kimliği** — %100 canonical ve `CONFIRMED`; 709 takım.
* **Uzatma bilgisi** — %0,96 (324 maç: AET/PEN olanlar). Bu düşük oran eksiklik değil, gerçektir.
* **Penaltı serisi** — %0,47 (158 maç).

---

## 4. B — CAN_BE_DERIVED (ölçülmüş üretilebilirlik)

Hepsi **genişleyen pencere** ile, yalnız önceki maçlardan hesaplandı (bkz. leakage kuralları).

### 4.1 Takım gücü
| Feature | Tanım | Üretilebilirlik | Sınıf |
|---|---|---|---|
| `MatchesPlayedToDate` | Önceki maç sayısı | %100 (değer 0 olabilir) | CORE |
| `GoalsForRate` / `GoalsAgainstRate` | Önceki maçlarda atılan/yenilen gol ortalaması | **%93,04** (her iki tarafta ≥5 önceki maç) | CORE |
| `Win/Draw/LossRate` | Sonuç oranları | %93,04 | CORE |
| `HomeAwaySplitStrength` | Yalnız ev / yalnız deplasman geçmişinden güç | %87,77 (≥10 önceki maç) | CORE |
| `OpponentAdjustedStrength` | Rakip gücüne göre düzeltilmiş atak/defans | %93,04 | OPTIONAL |

Ev sahibi avantajı veride gerçektir: yerel ligde ev galibiyeti **%43,8**, ortalama ev golü 1,54'e
karşı deplasman 1,24.

### 4.2 Form
| Feature | Üretilebilirlik | Sınıf |
|---|---|---|
| `RecentForm_3` | %95,50 | OPTIONAL |
| `RecentForm_5` | %93,04 | OPTIONAL |
| `RecentForm_10` | %87,77 | OPTIONAL |
| `TimeDecayedForm` | %98,37 | OPTIONAL |

**"Son 5 maç" otomatik kabul edilmedi.** Pencere uzunluğu bir hiper-parametredir; katkısı
walk-forward doğrulamayla ölçülecektir. Bu görevde ölçülmedi.

### 4.3 Takvim
| Feature | Üretilebilirlik | Sınıf | Uyarı |
|---|---|---|---|
| `DaysRest` | %98,37 (medyan 7 gün) | OPTIONAL | Gerçek dinlenmenin **üst sınırı** |
| `MatchesInLast7/14/21Days` | %98,37 | OPTIONAL | **Sistematik eksik sayar** |

Kapsam dışı müsabakalar (yerel kupalar, milli takım araları, hazırlık maçları) veri setinde yok.
Satırların **%8,69'unda** 20 günden uzun boşluk görünüyor — bunların çoğu kapsam dışı bir
turnuvanın izidir, gerçek bir dinlenme değildir.

### 4.4 Karşılaşma geçmişi ve tablo
| Feature | Üretilebilirlik | Sınıf |
|---|---|---|
| `HeadToHeadRecord` | ≥1 önceki karşılaşma %78,94 · ≥2 %60,35 · ≥4 %43,05 | OPTIONAL |
| `SeasonToDatePoints/GD` | Yerel ligde %91,88 | CORE (yalnız DOMESTIC_LEAGUE) |
| `SeasonToDateRank` | Yerel ligde %91,88 | OPTIONAL |
| `IsNewToCompetition` | %100 | OPTIONAL |
| `TieLegAndAggregate` | UEFA satırlarının %85,5'i | CORE (yalnız UEFA) |
| `MatchImportanceStage` | %96,11 (Round metninden) | OPTIONAL |

---

## 5. C — NOT_AVAILABLE (veri setinde YOK)

`FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv` içinde bu isimlerle **hiç kolon yoktur** (programatik
olarak arandı, 0 eşleşme):

| Kalem | Master'da | Sahip olduğumuz kaynaklarda | Durum |
|---|---|---|---|
| **xG / xGA** | **%0** | hiçbir kaynakta yok | `NOT_AVAILABLE` |
| Şut | %0 | **football-data dosyasında %99,87** → model satırlarının **%70,40'ına** ulaşılabilir | `PARTIAL (importable)` |
| İsabetli şut | %0 | aynı dosya, aynı oran | `PARTIAL (importable)` |
| Korner | %0 | aynı dosya | `PARTIAL (importable)` |
| Faul / kart | %0 | aynı dosya | `PARTIAL (importable)` |
| Topla oynama | %0 | hiçbir kaynakta yok | `NOT_AVAILABLE` |
| İlk 11 / kadro | %0 | tarihsel kapsam yok | `NOT_AVAILABLE` |
| Sakatlık | %0 | tarihsel kapsam yok | `NOT_AVAILABLE` |
| Ceza / suspension | %0 | tarihsel kapsam yok | `NOT_AVAILABLE` |
| Teknik direktör değişimi | %0 | yok | `NOT_AVAILABLE` |
| Hava durumu | %0 | yok | `NOT_AVAILABLE` |
| Hakem | %0 | yok | `NOT_AVAILABLE` |
| Seyirci / stadyum | %0 | yok | `NOT_AVAILABLE` |
| Doğrulanmış maç öncesi haber (global context) | %0 | yok | `NOT_AVAILABLE_IN_HISTORICAL_MASTER` |
| Bahis oranları | %0 | football-data dosyasında %99,86 → %70,40 ulaşılabilir | `PARTIAL (importable)` |

**Önemli bulgu:** elimizdeki `Data/Historical/Matches.csv` (football-data.co.uk) dosyası şut,
isabetli şut, korner, faul, kart, 1X2 oranı, 2,5 alt/üst ve Asya handikap kolonlarını **zaten
taşıyor** (%99,87 doluluk, 23.900 satır). Bunlar master'a aktarılmamıştı. Ulaşılabilir kapsam:

* tüm model satırlarının **%70,40'ı**,
* yerel lig satırlarının **%89,03'ü**,
* yerel lig sezonlarının 2017/18–2023/24'ünde **%100**, 2024/25'te %98,95,
* **2025/26 yerel sezonunda %0** ve **tüm UEFA maçlarında %0**.

Bu asimetri modelde competition-type ve sezon bazlı sistematik eksikliğe yol açar; bu yüzden
`FUTURE` sınıfındadır, CORE değildir.

### 5.1 api-football tarafında ne çekilmedi
Diskteki tüm ham api-football cevapları yalnız `fixture, league, teams, goals, score` bloklarını
içeriyor. `statistics`, `events`, `lineups`, `players`, `odds` uçları **hiç çağrılmadı** — dolayısıyla
tarihsel istatistik/kadro/oran verisi bu taraftan da mevcut değildir.

### 5.2 Gelecekte eklenirse — zaman damgası şartı
Kadro, sakatlık, ceza, teknik direktör ve maç öncesi haber verileri eklenirse **her kaydın
"bu bilgi ne zaman biliniyordu" damgası zorunludur.** Damgasız bir sakatlık kaydı, maç sonrası
yazılmış olabileceği için doğrudan sızıntıdır. Bu veriler damgasız geldiğinde `REJECT` sayılır.

**Elle puanlama yapılmadı ve yapılmayacak** — "sakatlık = -5" türü sabit katsayılar bu contract'ta
tanımlı değildir; veri geldiğinde ham olgu olarak taşınır, ağırlığı model öğrenir.

---

## 6. COMPETITION-TYPE FARKI (ölçülmüş)

| CompetitionType | Satır | Ev % | Beraberlik % | Deplasman % | Ort. toplam gol |
|---|---|---|---|---|---|
| DOMESTIC_LEAGUE | 26.846 | 43,8 | 25,3 | 30,9 | 2,78 |
| UEFA_MAIN | 3.587 | 47,4 | 21,0 | 31,6 | 2,92 |
| UEFA_QUALIFIER | 2.838 | 48,2 | 21,2 | 30,6 | 2,75 |
| UEFA_QUALIFICATION_PLAYOFF | 605 | 49,9 | 23,1 | 26,9 | 2,75 |
| DOMESTIC_PLAYOFF | 25 | 32,0 | 32,0 | 36,0 | 1,80 |

Bu tablo tanımlayıcıdır (model değil) ve tek bir şeyi kanıtlar: **bu beş tip aynı davranmıyor.**
Beraberlik oranı yerel ligde %25,3 iken UEFA ana turnuvada %21,0; play-off maçlarında gol
ortalaması belirgin biçimde düşük.

**En kritik yapısal fark — örneklem:**

| Kapsam | Her iki tarafta ≥5 önceki maç | Kendi turnuvasında ≥5 | Sezon içi ≥3 |
|---|---|---|---|
| DOMESTIC_LEAGUE | %97,03 | %96,62 | %91,88 |
| UEFA_MAIN | %95,34 | %75,19 | %64,26 |
| UEFA_QUALIFICATION_PLAYOFF | %83,64 | %56,36 | %33,22 |
| **UEFA_QUALIFIER** | **%54,26** | **%27,06** | **%10,92** |

**Sonuç:** UEFA eleme turlarında takım gücü, o turnuvanın kendi geçmişinden **üretilemez**.
709 takımın %19,18'i veri setinde 5'ten az maça sahiptir ve bunların neredeyse tamamı eleme turu
kulüpleridir. Eleme maçları için tek gerçekçi yol, gücü **turnuvalar arası** (yerel lig + UEFA
birlikte) bir ratingden taşımaktır; bu bile veri setinde yerel ligi bulunmayan kulüpler için
çalışmaz. Bu kulüpler için `IsNewToCompetition` bayrağı ve düşük güven işareti şarttır.

Detaylı feature × competition matrisi: `FORMAX_PROBABILITY_COMPETITION_FEATURE_MATRIX.csv`.

---

## 7. LEAKAGE

Tüm kurallar, testler ve reddedilen feature listesi ayrı dosyada:
**`FORMAX_PROBABILITY_FEATURE_LEAKAGE_RULES.md`**

Özet:
* Tek kural: feature yalnız `Date`'ten **kesinlikle önceki** olaylardan hesaplanabilir.
* `LEAKAGE_RISK` işaretli hiçbir alan CORE/OPTIONAL olamaz.
* 8 otomatik test tanımlandı (T1 gelecek referansı, T2 kaydırma, T3 sahte hedef, T4 ilk maç,
  T5 aynı gün, T6 tie bütünlüğü, T7 sezon sınırı, T8 kolon karantinası).
* İki ayaklı UEFA eşleşmelerinde bölme birimi **maç değil eşleşmedir**.
* Rastgele train/test bölmesi yasak; walk-forward zorunlu.

---

## 8. FEATURE CONTRACT — sınıflandırma özeti

### CORE — ilk modelde kullanılabilecek minimal set
Yalnız bunlarla bir Probability Engine kurulabilir; hepsi %100 mevcut veya ≥%87 üretilebilir ve
leakage-safe:

```
match_id, match_date, season, competition, competition_type
home_team_id, away_team_id
home/away_matches_played_to_date
home/away_goals_for_rate, home/away_goals_against_rate
home/away_win_rate, draw_rate, loss_rate
home_home_venue_* , away_away_venue_*                (ev/deplasman ayrımı)
home/away_season_points_to_date, *_goal_difference_to_date   (yalnız DOMESTIC_LEAGUE)
tie_leg_number, tie_aggregate_home_goals, tie_aggregate_away_goals  (yalnız UEFA)
HEDEF: target_home_goals / target_away_goals (veya knockout için target_regular_*)
```

### OPTIONAL — üretilebilir, katkısı ayrıca ölçülmeli
`RecentForm_N`, `TimeDecayedForm`, `OpponentAdjustedStrength`, `DaysRest`,
`MatchesInLast14Days`, `HeadToHead*`, `SeasonToDateRank`, `IsNewToCompetition`,
`StageOrdinal`, `SourceCount`, `KickoffLocalTime/Utc`, `ProviderMatchId/TeamId`.

### FUTURE — veri geldiğinde eklenebilir
Şut/isabetli şut/korner/kart ve 1X2 + alt/üst + handikap oranları (football-data dosyasından
içe aktarma yolu mevcut, %70,4 kapsam), possession ve xG (yeni kaynak gerekir), ilk 11, sakatlık,
ceza, teknik direktör değişimi, hava durumu, hakem, doğrulanmış maç öncesi haber.
**Hepsi için zaman damgası zorunludur.**

### REJECT
`HalfTime*` (girdi olarak), `ExtraTime*` (girdi), `PenaltyShootout*` (girdi), `MatchStatus` (girdi),
football-data'nın hazır `Elo` ve `Form` kolonları, sezon sonu sıralaması, sezon toplam
istatistikleri, `DataQualityFlag`.

---

## 9. TARİHSEL PENCERE VE ZAMAN AĞIRLIĞI

Veri 2017/18 → 2025/26 arasını kapsar; sezon başına 3.536–3.912 maç, dengeli dağılım.

**Eski verinin yeni veriyle eşit ağırlıkta kullanılacağı varsayılmadı.** Uygulanabilir seçenekler
(katsayı burada belirlenmez): üstel zaman ağırlığı `exp(-Δgün/H)`, sezon bazlı sabit ağırlık veya
son N sezonla sınırlama. Seçim walk-forward doğrulamayla yapılmalıdır — bu görevde yapılmadı.

Ayrıca sezonlar arası yapısal kırılmalar vardır ve ağırlıklandırmada dikkate alınmalıdır:
Ligue 1 2023/24'te 20→18 takıma indi; Süper Lig 2020/21'de 21 takım; CL ve EL 2024/25'te lig
aşamasına geçti (125→189 maç); EL eleme yapısı 2021/22'de değişti; 2019/20'de COVID nedeniyle
Ligue 1 (279 maç) ve Eredivisie (232 maç) yarıda kaldı.

---

## 10. ÇIKTI DOSYALARI

| Dosya | İçerik |
|---|---|
| `FORMAX_PROBABILITY_FEATURE_CONTRACT.md` | Bu belge |
| `FORMAX_PROBABILITY_FEATURE_SCHEMA.json` | Canonical feature alan tanımları (yalnız şema, değer yok) |
| `FORMAX_PROBABILITY_FEATURE_INVENTORY.csv` | 58 kalem: tanım, kaynak, coverage, leakage, sınıf |
| `FORMAX_PROBABILITY_FEATURE_COVERAGE.csv` | Veri kalemi bazında yüzdesel kapsam (YOK olanlar açıkça 0) |
| `FORMAX_PROBABILITY_FEATURE_LEAKAGE_RULES.md` | Sızıntı kuralları + 8 otomatik test |
| `FORMAX_PROBABILITY_COMPETITION_FEATURE_MATRIX.csv` | CompetitionType × feature kullanılabilirlik |
| `FORMAX_PROBABILITY_COLUMN_INVENTORY.csv` | 37 kolonun ham istatistiği (kanıt) |
| `FORMAX_PROBABILITY_SOURCE_FIELD_COVERAGE.csv` | football-data kaynağındaki 23 alanın ölçülmüş doluluğu (kanıt) |
| `scripts/` | Ölçümü yeniden üreten PowerShell adımları (p1–p4) |

**Değiştirilen mevcut dosya yoktur.** Tüm çıktılar `FORMAX_PROBABILITY_ENGINE/` altındadır.

---

## 11. BU GÖREVDE YAPILMAYANLAR

Dixon-Coles, Bivariate Poisson, Elo, xG modeli, feature engine implementasyonu, calibration,
ensemble, prediction endpoint değişikliği, Gemma çıktısı — **hiçbiri yapılmadı**.
Feature değeri hesaplanmadı; yalnız hesaplanabilirlik ölçüldü.
