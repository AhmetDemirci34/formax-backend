# FORMAX — Tarihsel Maç Verisi Boşluk Kapatma (api-football)

**Tarih:** 2026-08-19
**Kapsam:** Yalnızca eksik tarihsel maç omurgası. Model/tahmin/kalibrasyon işi YAPILMADI.
**Üretim DB'sine yazılmadı.** Mevcut hiçbir dataset değiştirilmedi/silinmedi. Bu klasör bağımsız çıktıdır.

---

## 1. Keşif bulguları (varsayım değil, gerçek API cevabı)

### 1.1 Eleme/play-off için AYRI league id YOK
`GET /leagues?search=Qualif` → dönen 30 sonucun tamamı **milli takım** elemeleri
(World Cup Qualification, Euro Qualification, U21, Kadınlar vb.).
UEFA kulüp turnuvalarının eleme/play-off turları için ayrı bir league id **bulunmamaktadır**.

Eleme + play-off maçları ana turnuvanın league id'si altında, `league.round` alanıyla ayrışır:

| Competition | League ID |
|---|---|
| UEFA Champions League | 2 |
| UEFA Europa League | 3 |
| UEFA Europa Conference League | 848 |

### 1.2 Gerçek round sözlüğü (API cevabından çıkarıldı, uydurulmadı)

**QUALIFIER olarak sınıflanan round değerleri:**
`Preliminary Round`, `Preliminary round`, `Preliminary round 1`, `Preliminary round 2`,
`Preliminary Round - Semi-finals`, `Preliminary Round - Final`,
`1st Qualifying Round`, `2nd Qualifying Round`, `3rd Qualifying Round`

**QUALIFICATION_PLAYOFF olarak sınıflanan round değeri:**
`Play-offs` (Temmuz/Ağustos, grup aşamasından ÖNCE)

**TUZAK — MAIN sayılan, play-off DEĞİL:**
`Knockout Round Play-offs` (Şubat ayı, turnuva içi eleme). Bu değer ayrı bir string olarak
gelir ve `Play-offs` ile karıştırılmamıştır. Ek güvenlik kontrolü: her sezonda
QUALIFICATION_PLAYOFF satırlarının tarihi, o sezonun ilk ana-aşama maçından önce olmak
zorunda — 17 sezonun tamamı bu kontrolü geçti (0 ihlal).

### 1.3 Coverage (leagues endpoint)
League 2 → 2016–2025 sezonları mevcut; League 3 → 2016–2025; League 848 → 2021–2025.
Tüm ilgili sezonlarda `coverage.fixtures.events = true`.

---

## 2. Mevcut veri taraması — TEKRAR ÇEKİLMEYENLER

Taranan kaynaklar: `Data/Historical/Matches.csv` (230.557 satır, football-data.co.uk şeması),
`Data/Historical/EloRatings.csv`, `veri setleri/` klasöründeki 4 ZIP
(`football.json-master`, `europe-master`, `champions-league-master`, `cache.leagues-master`)
ve 11 gevşek CSV.

| Talep edilen boşluk | Gerçek durum | Kanıt |
|---|---|---|
| Championship 2017/18 | **ZATEN VAR** | `Data/Historical/Matches.csv` Division=E1, 2017-08-04→2018-05-06, **552 maç** + `veri setleri/eng.2.csv` 552 maç |
| Eredivisie 2017/18 | **ZATEN VAR** | Division=N1, 2017-08-11→2018-05-06, **306 maç** + `veri setleri/nl.1.csv` 306 maç |
| Süper Lig 2017/18 | **ZATEN VAR** | Division=T1, 2017-08-11→2018-05-19, **306 maç** + `veri setleri/tr.1.csv` 306 maç |
| CL / EL / Conf **Qualifier + Play-off 2025/26** | **ZATEN VAR** | `champions-league-master/2025-26/clq.txt` (92), `elq.txt` (82), `confq.txt` (256) — hepsi `1./2./3. Round` + `Play-offs` içeriyor |
| CL / EL / Conf Qualifier + Play-off 2024/25 | **ZATEN VAR** | `2024-25/clq.txt` (90), `elq.txt` (80), `confq.txt` (256) |
| Europa League ana turnuva 2019/20+ | **ZATEN VAR** | `europa-league-2019-...csv` (199), `2020-21..2023-24/el.txt`, `europa-league-2025-UTC.csv` |

Bu kalemler için **hiç API çağrısı yapılmadı**.

---

## 3. SONUÇ TABLOSU — indirilen boşluklar

| Competition | Season | Requested Round | API League ID | API Season | Fixture Count | Existing | Missing | Status |
|---|---|---|---|---|---|---|---|---|
| Europa League (ana turnuva) | 2017/18 | Group→Final | 3 | 2017 | 205 | 0 | 205 | **FOUND** |
| Europa League (ana turnuva) | 2018/19 | Group→Final | 3 | 2018 | 205 | 0 | 205 | **FOUND** |
| Champions League Qualifier | 2017/18 | 1st/2nd/3rd Qualifying | 2 | 2017 | 74 | 0 | 74 | **FOUND** |
| Champions League Qualifier | 2018/19 | Preliminary + 1st/2nd/3rd | 2 | 2018 | 79 | 0 | 79 | **FOUND** |
| Champions League Qualifier | 2019/20 | 1st/2nd/3rd Qualifying | 2 | 2019 | 79 | 0 | 79 | **FOUND** |
| Champions League Qualifier | 2020/21 | Preliminary 1-2 + 1st/2nd/3rd | 2 | 2020 | 41 | 0 | 41 | **FOUND** (COVID: tek maçlı turlar) |
| Champions League Qualifier | 2021/22 | Preliminary + 1st/2nd/3rd | 2 | 2021 | 81 | 0 | 81 | **FOUND** |
| Champions League Qualifier | 2022/23 | Preliminary + 1st/2nd/3rd | 2 | 2022 | 77 | 0 | 77 | **FOUND** |
| Champions League Qualifier | 2023/24 | Preliminary + 1st/2nd/3rd | 2 | 2023 | 77 | 0 | 77 | **FOUND** |
| Champions League Qualification Play-off | 2017/18 | Play-offs | 2 | 2017 | 20 | 0 | 20 | **FOUND** |
| Champions League Qualification Play-off | 2018/19 | Play-offs | 2 | 2018 | 12 | 0 | 12 | **FOUND** |
| Champions League Qualification Play-off | 2019/20 | Play-offs | 2 | 2019 | 12 | 0 | 12 | **FOUND** |
| Champions League Qualification Play-off | 2020/21 | Play-offs | 2 | 2020 | 12 | 0 | 12 | **FOUND** |
| Champions League Qualification Play-off | 2021/22 | Play-offs | 2 | 2021 | 12 | 0 | 12 | **FOUND** |
| Champions League Qualification Play-off | 2022/23 | Play-offs | 2 | 2022 | 12 | 0 | 12 | **FOUND** |
| Champions League Qualification Play-off | 2023/24 | Play-offs | 2 | 2023 | 12 | 0 | 12 | **FOUND** |
| Europa League Qualifier | 2017/18 | 1st/2nd/3rd Qualifying | 3 | 2017 | 224 | 0 | 224 | **FOUND** |
| Europa League Qualifier | 2018/19 | Preliminary + 1st/2nd/3rd | 3 | 2018 | 272 | 0 | 272 | **FOUND** |
| Europa League Qualifier | 2019/20 | Preliminary + 1st/2nd/3rd | 3 | 2019 | 272 | 0 | 272 | **FOUND** |
| Europa League Qualifier | 2020/21 | Preliminary + 1st/2nd/3rd | 3 | 2020 | 136 | 0 | 136 | **FOUND** (COVID: tek maçlı turlar) |
| Europa League Qualifier | 2021/22 | 3rd Qualifying | 3 | 2021 | 16 | 0 | 16 | **FOUND** (2021'den itibaren erken turlar Conference'a geçti) |
| Europa League Qualifier | 2022/23 | 3rd Qualifying | 3 | 2022 | 14 | 0 | 14 | **FOUND** |
| Europa League Qualifier | 2023/24 | 3rd Qualifying | 3 | 2023 | 14 | 0 | 14 | **FOUND** |
| Europa League Qualification Play-off | 2017/18 | Play-offs | 3 | 2017 | 44 | 0 | 44 | **FOUND** |
| Europa League Qualification Play-off | 2018/19 | Play-offs | 3 | 2018 | 42 | 0 | 42 | **FOUND** |
| Europa League Qualification Play-off | 2019/20 | Play-offs | 3 | 2019 | 42 | 0 | 42 | **FOUND** |
| Europa League Qualification Play-off | 2020/21 | Play-offs | 3 | 2020 | 21 | 0 | 21 | **FOUND** (tek maçlı) |
| Europa League Qualification Play-off | 2021/22 | Play-offs | 3 | 2021 | 20 | 0 | 20 | **FOUND** |
| Europa League Qualification Play-off | 2022/23 | Play-offs | 3 | 2022 | 20 | 0 | 20 | **FOUND** |
| Europa League Qualification Play-off | 2023/24 | Play-offs | 3 | 2023 | 20 | 0 | 20 | **FOUND** |
| Conference League Qualifier | 2021/22 | 1st/2nd/3rd Qualifying | 848 | 2021 | 238 | 0 | 238 | **FOUND** |
| Conference League Qualifier | 2022/23 | 1st/2nd/3rd Qualifying | 848 | 2022 | 230 | 0 | 230 | **FOUND** |
| Conference League Qualifier | 2023/24 | 1st/2nd/3rd Qualifying | 848 | 2023 | 232 | 0 | 232 | **FOUND** |
| Conference League Qualification Play-off | 2021/22 | Play-offs | 848 | 2021 | 44 | 0 | 44 | **FOUND** |
| Conference League Qualification Play-off | 2022/23 | Play-offs | 848 | 2022 | 44 | 0 | 44 | **FOUND** |
| Conference League Qualification Play-off | 2023/24 | Play-offs | 848 | 2023 | 44 | 0 | 44 | **FOUND** |

**NOT_AVAILABLE_IN_API_FOOTBALL kalemi YOKTUR.** Talep edilen tüm eksik sezon/tur
kombinasyonları api-football'da gerçekten mevcuttu.

---

## 4. Çıktı dosyaları

| Dosya | Satır | İçerik |
|---|---|---|
| `champions_league_qualifiers.csv` | 508 | CL eleme turları, 2017/18–2023/24 |
| `champions_league_qualification_playoffs.csv` | 92 | CL qualification play-off, 2017/18–2023/24 |
| `europa_league_qualifiers.csv` | 948 | EL eleme turları, 2017/18–2023/24 |
| `europa_league_qualification_playoffs.csv` | 209 | EL qualification play-off, 2017/18–2023/24 |
| `conference_league_qualifiers.csv` | 700 | Conference eleme turları, 2021/22–2023/24 |
| `conference_league_qualification_playoffs.csv` | 132 | Conference qualification play-off, 2021/22–2023/24 |
| `europa_league_2017_18.csv` | 205 | EL ana turnuva 2017/18 (grup→final) |
| `europa_league_2018_19.csv` | 205 | EL ana turnuva 2018/19 (grup→final) |
| `_coverage_report.csv` | 51 | league × season × stage kapsam özeti |
| `_api_call_log.txt` | 22 | yapılan her API çağrısı |
| `raw/` | 22 JSON | ham API cevapları (yeniden çekmeye gerek kalmasın diye) |

**TOPLAM: 2.999 maç, 467 farklı takım.**

Her dosyada `Season` alanı vardır (çok sezonlu dosyalar için zorunlu).

### Kolonlar
`FixtureId, Season, SeasonLabel, Competition, CompetitionId, CompetitionType, Stage, Round,
Date (UTC …Z), MatchDate, HomeTeam, AwayTeam, HomeTeamId, AwayTeamId, HomeGoals, AwayGoals,
HomeGoalsHalfTime, AwayGoalsHalfTime, HomeGoalsFullTime, AwayGoalsFullTime,
HomeGoalsExtraTime, AwayGoalsExtraTime, HomeGoalsPenalty, AwayGoalsPenalty,
Status, StatusShort, Venue, City, ProviderFixtureId, DuplicateKey`

`CompetitionType` canonical değerleri (API round'undan türetildi):
`CHAMPIONS_LEAGUE_QUALIFIER`, `CHAMPIONS_LEAGUE_QUALIFICATION_PLAYOFF`,
`EUROPA_LEAGUE`, `EUROPA_LEAGUE_QUALIFIER`, `EUROPA_LEAGUE_QUALIFICATION_PLAYOFF`,
`CONFERENCE_LEAGUE_QUALIFIER`, `CONFERENCE_LEAGUE_QUALIFICATION_PLAYOFF`

---

## 5. Doğrulama

**İki ayaklı eşleşmeler:** her ayak AYRI satır. Birleştirme yapılmadı, aggregate hesaplanmadı.
Örnek (2019/20 CL 1. eleme turu):
```
2019-07-10  Dundalk 0-0 Riga
2019-07-17  Riga    0-0 Dundalk   (pen 4-5)
2019-07-24  Dundalk 1-1 Qarabag        <- 2. tur, tur atlama tutarlı
2019-07-31  Qarabag 3-0 Dundalk
```

**Uzatma / penaltı korundu:** 179 maçta uzatma skoru, 92 maçta penaltı atışı skoru ayrı
kolonlarda tutuluyor (`AET`=87, `PEN`=92 statü). Normal süre `HomeGoalsFullTime`,
uzatma `…ExtraTime`, seri `…Penalty` kolonlarında ayrı.

**Duplicate kontrolü:** 5.099 mevcut yerel UEFA maç satırı (openfootball .txt + 5 fixture CSV)
tarih + normalize takım adı ile indekslendi. 2.999 çıktı satırının 19'u mevcut bir maçla aynı
TARİHE düşüyor, ancak hiçbirinde takım eşleşmesi yok → **gerçek duplicate: 0**.
Dosya içi duplicate: `DuplicateKey` 0, `FixtureId` 0.

**UNRESOLVED: 0.** Tüm satırlarda provider `HomeTeamId`/`AwayTeamId` ve skor dolu; iptal /
hükmen / ertelenmiş satır yok. Takım kimliği api-football team id ile taşındığı için isim
tabanlı otomatik birleştirme YAPILMADI — mevcut dataset ile eşleme bir sonraki (birleştirme)
aşamasının işidir.

**Veri kalitesi notu:** 4 maçta sağlayıcı ilk yarı skorunu vermiyor (maç sonu skorları dolu):
Drita–Linfield 2020-08-11, KI Klaksvik–Slovan Bratislava 2020-08-21,
Cracovia–Dunajska Streda 2019-07-18, Lincoln Red Imps–Prishtina 2020-08-22.

---

## 6. API kullanımı

**Toplam 22 çağrı:** 1 × `/status`, 4 × `/leagues`, 17 × `/fixtures?league=&season=`.
`events`, `lineups`, `statistics`, `players`, `odds` çekilmedi. Aynı league+season ikinci kez
çağrılmadı (ham JSON `raw/` altında önbelleklendi).
