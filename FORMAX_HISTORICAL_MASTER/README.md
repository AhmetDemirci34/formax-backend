# FORMAX_HISTORICAL_MASTER

**Oluşturma tarihi:** 2026-08-20
**Kapsam:** Yalnızca tarihsel veri standardizasyonu. Model / Dixon-Coles / Poisson / calibration /
ensemble / prediction / Gemma işi **YAPILMADI**.
**Hiçbir kaynak dosya değiştirilmedi veya silinmedi. Üretim DB'sine yazılmadı.**
Master kurulumunda api-football'a yeni çağrı yapılmadı (yalnızca `FORMAX_HISTORICAL_GAPS/raw/*.json`
okundu). Veri kalite kapısında 8, kimlik doğrulama turunda 12 olmak üzere toplam **20 hedefli
api-football çağrısı yapıldı** — dökümü §13.1 ve §13.7'de.

---

## 1. ÖZET

| Ölçüt | Değer |
|---|---|
| Toplam kaynak dosya | **145** |
| Toplam ham (staged) satır | **67.781** |
| Benzersiz canonical maç | **33.906** |
| Model eligible maç | **33.901** (dışlanan 5, hepsi hükmen/AWARDED) |
| Duplicate | **0** |
| Conflict | **4 maç** — dördü de bağımsız üçüncü kaynakla çözüldü, çözülemeyen **0** (§13.4) |
| Unresolved | **0** — 709 takım kimliğinin tamamı `CONFIRMED`, açık kalem yok (§13.7) |
| Eksik skor | **0** — 209 eksik skor api-football'tan gerçek veriyle dolduruldu (§13.1) |
| Eksik tarih | **0** |
| Canonical takım | **709** — hepsi CONFIRMED; 43 look-alike çifti provider id ile karara bağlandı (§13.7) |
| Tarih aralığı | 2017-06-27 … 2026-05-30 |

---

## 2. DOSYALAR

| Dosya | İçerik |
|---|---|
| `FORMAX_HISTORICAL_MASTER.csv` | **Canonical master** — maç başına tek satır, 33.906 satır, tüm gerçek kayıtlar |
| `FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv` | **Model dataset** — yalnız `ModelEligible = true`, 33.901 satır |
| `FORMAX_HISTORICAL_MODEL_EXCLUSIONS.csv` | Model dışı 5 maç + sebep |
| `FORMAX_HISTORICAL_TEAM_IDENTITY_RESOLUTION.csv` | 43 look-alike çiftinin provider kimliğiyle kararı + kanıtı |
| `FORMAX_HISTORICAL_TEAM_LOOKALIKES.csv` | Birleştirilmeyen 23 çift, hepsi CONFIRMED_DIFFERENT |
| `FORMAX_HISTORICAL_TEAM_IDENTITY_REVIEW.csv` | Parmak iziyle çözülemeyen 93 bağlantının tek tek sonucu |
| `_gate_verification.txt` | Kabul kriteri doğrulama çıktısı |
| `_api_call_log.txt` + `api_raw/` | Yapılan 20 api-football çağrısı ve ham cevapları |
| `FORMAX_HISTORICAL_DATA_QUALITY.csv` | Competition × CompetitionType × Season kalite tablosu (model eligible sütunlu) |
| `FORMAX_HISTORICAL_CONFLICTS.csv` | 4 skor çelişkisi, çözümü ve gerekçesi |
| `FORMAX_HISTORICAL_SOURCE_MAP.csv` | Her maçın hangi kaynaklardan geldiği |
| `FORMAX_HISTORICAL_TEAMS.csv` | Canonical takım kaydı + tüm alias'lar |
| `FORMAX_HISTORICAL_COVERAGE_MATRIX.csv` | 17 FORMAX kapsamı × 9 sezon matrisi |
| `FORMAX_HISTORICAL_UNRESOLVED.csv` | Çözülemeyen/dışarıda bırakılan her şey, gerekçesiyle |
| `FORMAX_HISTORICAL_SOURCE_FILES.csv` | Okunan 145 kaynak dosya ve her birinden çıkan satır sayısı |
| `season_corrections.csv` | Sezon etiketi düzeltilen 343 satır + kanıtı |
| `cell_primary.csv` | Her (competition, season, type) hücresi için seçilen birincil kaynak |
| `team_links.csv` | Takım alias bağlantıları + hangi yöntemle kuruldukları |
| `scripts/` | Tüm hattı yeniden üreten PowerShell adımları (m1–m5 master, q1–q8 kalite kapısı, r1–r6 kimlik doğrulama) |

**Parquet üretilmedi.** Bu makinede Python/pyarrow kurulu değil (`python` yalnızca Windows Store
stub'ı). CSV tek çıktı formatıdır; parquet gerekirse ayrıca üretilebilir.

---

## 3. SKOR ALANLARININ ANLAMI (sözleşme)

| Alan | Anlam |
|---|---|
| `HomeGoals` / `AwayGoals` | **Maçın nihai skoru — uzatma DAHİL, penaltı atışları HARİÇ.** Model için temel skor budur. |
| `RegularTimeHomeGoals` / `RegularTimeAwayGoals` | 90 dakika sonundaki skor |
| `HalfTimeHomeGoals` / `HalfTimeAwayGoals` | İlk yarı skoru |
| `ExtraTimeHomeGoals` / `ExtraTimeAwayGoals` | **Yalnızca uzatmada atılan goller** (`HomeGoals − RegularTimeHomeGoals`) |
| `PenaltyShootoutHome` / `PenaltyShootoutAway` | Penaltı atışları serisi sonucu — asla `HomeGoals`'a karıştırılmadı |

Not: api-football `score.extratime` alanını sezonlar arasında **tutarsız** dolduruyor (eski
kayıtlarda uzatma sonu toplam skoru, yenilerde yalnız uzatma golleri). Bu yüzden uzatma golü
kolonu iki güvenilir alandan (nihai skor − 90' skoru) yeniden hesaplandı; ham sağlayıcı değeriyle
**30 satırda** ayrıştı, hepsi bu sözleşmeye göre normalize edildi. Uydurma yok, aritmetik var.

Doğrulama: `RegularTime + ExtraTime ≠ HomeGoals` olan satır **0**; penaltı skoru olup statüsü PEN
olmayan satır **0**; statüsü AET olup uzatma skoru olmayan satır **0**.

---

## 4. MATCH STATUS

Kapı sonrası: `FT` 33.577 · `AET` 166 · `PEN` 158 · `AWARDED` 5 · `UNKNOWN` **0**.
(Kapı öncesi 209 `UNKNOWN` satır vardı; hepsi gerçek sağlayıcı sonucuyla kapatıldı — §13.1.)
`AWARDED` satırlar sahada oynanmadıkları için model dataset'ine alınmadı.

Oynanmadığı kaynakta açıkça belirtilen **352 fikstür master'a HİÇ alınmadı**
(`FIXTURE_NEVER_PLAYED_EXCLUDED`, çoğu Ligue 1 2019/20 ve Eredivisie 2019/20 COVID iptalleri).

---

## 5. COMPETITION TYPE

| Değer | Satır |
|---|---|
| `DOMESTIC_LEAGUE` | lig maçları (8 lig) |
| `DOMESTIC_PLAYOFF` | 25 — Championship terfi play-off'ları (2021/22–2025/26) |
| `UEFA_MAIN` | ana turnuva (grup/lig aşaması → final; Şubat'taki *Knockout Round Play-offs* dâhil) |
| `UEFA_QUALIFIER` | eleme turları (Preliminary / 1st / 2nd / 3rd Qualifying Round) |
| `UEFA_QUALIFICATION_PLAYOFF` | ana turnuva öncesi Ağustos play-off turu |

### Play-off tuzağı nasıl kapatıldı
* **UEFA:** `cl/el/conf` dosyaları yalnız ana turnuvayı, `clq/elq/confq` dosyaları yalnız eleme
  fazını taşır → aşama **dosya düzeyinde** belirlendi. Bu yüzden `cl.txt` içindeki "Playoffs"
  bölümü (Şubat) `UEFA_MAIN`, `clq.txt` içindeki "Play-offs" bölümü (Ağustos)
  `UEFA_QUALIFICATION_PLAYOFF` oldu. api-football tarafında `Knockout Round Play-offs` ayrı bir
  round metni olduğu için `Play-offs` ile karıştırılmadı; ayrıca her sezonda qualification
  play-off tarihlerinin ilk ana-aşama maçından önce geldiği kontrol edildi (0 ihlal).
* **Yerel lig:** 8 FORMAX ligi saf çift devreli lig; bir (ev, deplasman) sırası sezonda **en fazla
  bir kez** olabilir. Aynı sıralı çiftin tekrarı lig maçı olamaz → tarih sırasına göre ikincisi
  `DOMESTIC_PLAYOFF` işaretlendi. Böylece terfi play-off'ları lig performansına karışmıyor ama
  veri de kaybolmuyor.

---

## 6. KAYNAKLAR VE BİRİNCİL KAYNAK SEÇİMİ

Okunan kaynaklar (`FORMAX_HISTORICAL_SOURCE_FILES.csv` tam listedir):

| SourceKey | Nereden | Rol |
|---|---|---|
| `football-data` | `Data/Historical/Matches.csv` (football-data.co.uk) | 8 lig, 2017/18–2024/25 birincil |
| `openfootball-json` | `football.json-master.zip` | 2025/26 8 lig birincil; diğer sezonlarda doğrulayıcı; Championship play-off birincil |
| `openfootball-txt` | `europe-master.zip` + `champions-league-master.zip` | UEFA 2024/25–2025/26 birincil; diğerlerinde doğrulayıcı |
| `openfootball-csv` | `eng.2.csv`, `nl.1.csv`, `tr.1.csv` | 2017/18 doğrulayıcı |
| `fixturedownload` | `champions-league-2017/2025`, `europa-league-2019/2025`, `conference-league-2025`, `super-lig-2021/2022` | EL & Conference 2025/26 ana turnuva birincil; kalanlar doğrulayıcı |
| `api-football` | `FORMAX_HISTORICAL_GAPS/raw/*.json` | UEFA 2017/18–2023/24 (CL, EL) ve 2021/22–2023/24 (Conference) birincil |

**Duplicate kaynak dosyalar:** `... (1).csv` kopyalarının tamamı orijinaliyle **byte-byte aynı**
(md5 doğrulandı) → tek kaynak sayıldı, iki kez işlenmedi.

**Kapsam dışı bırakılanlar:** `europe-master` içindeki 40+ diğer ülke ligi, `football.json`
içindeki at.1/pt.1/mx.1 vb., `cache.leagues-master.zip` (maç değil lig metadata'sı). FORMAX'ın
kilitli 17 kapsamı dışındaki hiçbir satır master'a alınmadı.

**Birincil kaynak modeli:** her (Competition, Season, CompetitionType) hücresi için tek bir
birincil kaynak seçilir; master satırları oradan gelir. Diğer kaynaklar **doğrulayıcıdır**:
eşleşen satırlar `SourceList`'e eklenir, skorlar karşılaştırılır. Bu sayede aynı maç iki kez
eklenemez (duplicate = 0, yapısal garanti). Birincilde olmayan gerçek maçlar ise kaybolmasın
diye ikincil kaynaktan **terfi ettirildi** (31 satır, `ADDED_FROM_SECONDARY_SOURCE`).

Kaç kaynak doğruladı (kapı sonrası): 1 kaynak 7.040 · 2 kaynak 20.291 · 3 kaynak 6.445 · 4 kaynak 125.

---

## 7. TAKIM KİMLİĞİ

İsim benzerliğine güvenilmedi. Sıra:

1. **Fikstür parmak izi (2.446 bağlantı):** bir takımın sezon boyunca oynadığı
   `(ev/deplasman, attığı, yediği, tarih)` kümesi kaynaklar arasında aynıdır. İki kaynaktaki iki
   ad, parmak izleri örtüşüyorsa aynı kulüptür — dilden ve yazımdan bağımsız kanıt.
   Kabul eşiği: örtüşme ≥ %50 **ve** en iyi aday ikinciden ≥ 2 kat iyi.
2. **Aynı kaynak içinde bölünmüş yazım:** football-data aynı sezonda hem `MGladbach` hem
   `M'gladbach` yazabiliyor. Birden çok birincil ad, tek bir takımın sezonunun **ayrık** parçaları
   ise hepsi birleştirildi.
3. **Normalize isim eşleşmesi (93 bağlantı):** diakritik + `FC/AFC/SC…` ekleri temizlenip birebir
   eşitlik arandı.
4. **RAKİP KORUMASI:** gerçek bir maçta **birbirine karşı oynamış** iki ad asla birleştirilmez.
   Bu koruma `Paris` (=PSG, fixturedownload kısaltması) ile `Paris FC`'nin yanlış birleşmesini
   engelledi — tek reddedilen birleştirme (`FORMAX_HISTORICAL_UNRESOLVED.csv`,
   `TEAM_MERGE_REJECTED`).

Sonuç: 763 canonical takım, aynı normalize ada düşen ikinci kimlik **0** (bölünmüş kimlik yok).
Canonical ad seçiminde öncelik api-football adıdır (FORMAX üretimindeki sağlayıcı ile hizalı).

**UNRESOLVED (89):** parmak izi bağlantısı reddedilen adlar. Büyük çoğunluğu, karşılaştırılan iki
kaynağın **farklı maçları** kapsamasından (ör. Conference 2025/26 ana turnuva CSV'si ile eleme
turu txt'si ortak fikstür içermiyor) kaynaklanır; bu adlar 3. adımdaki normalize isim eşleşmesiyle
doğru kimliğe bağlandı. Hiçbiri sessizce yanlış eşleştirilmedi; liste dosyada.

---

## 8. TARİH VE SEZON

* Tüm tarihler ISO `YYYY-MM-DD`.
* `KickoffLocalTime` + `SourceTimeZone` kaynağın verdiği saat ve saat dilimini korur.
* `KickoffUtc` yalnız saat dilimi kesin bilinen kaynaklarda dolu (api-football ve `-UTC` işaretli
  fixturedownload dosyaları). Bilinmeyen için uydurulmadı → NULL.
* Sezon **takvim yılına göre atanmadı**. Sezonu açıkça bildiren kaynaklardan (openfootball dosya
  adı/başlığı, api-football `season`) alındı. Sezon bildirmeyen tek kaynak football-data.co.uk;
  onun satırları, sezonu bildiren kaynakların gerçek tarih pencerelerine göre yerleştirildi.
  Bu, COVID sezonlarını düzeltti: **343 satır** (PL 66, Championship 77, La Liga 57, Serie A 98,
  Süper Lig 45) 2020/21'den 2019/20'ye alındı — çünkü bu maçlar Temmuz 2020'de oynanan
  2019/20 sezonu maçlarıdır. Her düzeltme kanıtıyla `season_corrections.csv` içinde.
  İki sezon arasındaki boşluğa düşen tarihler yalnız boşluk ≤ 120 gün ise komşu sezona bağlandı;
  kaynağın hiç kapsamadığı sezonlarda tahmin yapılmadı.

---

## 9. DUPLICATE VE ÇELİŞKİ

* Canonical anahtar: `Competition | Season | Date | HomeTeamId | AwayTeamId`
  → **duplicate 0**, tekrarlanan `MatchId` **0**.
* İki ayaklı UEFA eşleşmelerinin her ayağı **ayrı MatchId**; hiçbir tie birleştirilmedi.
* `MatchId` = `FMXM` + SHA1(`Competition|Season|CompetitionType|Date|HomeTeamId|AwayTeamId`)
  ilk 16 hex — deterministik, yeniden üretilebilir.

### 4 gerçek çelişki (otomatik seçim YAPILMADI)

| Maç | Kaynak A | Kaynak B |
|---|---|---|
| Hellas Verona – Roma, 2020-09-19 (Serie A) | football-data 0-0 | openfootball 3-0 |
| Union Berlin – Bochum, 2024-12-14 (Bundesliga) | football-data 1-1 | openfootball 0-2 |
| NEC Nijmegen – Vitesse, 2023-10-01 (Eredivisie) | football-data 1-3 | openfootball 1-2 |
| Akhisar – Beşiktaş, 2019-01-18 (Süper Lig) | football-data 1-3 | openfootball 0-3 |

Dördü de aynı desende: **sahadaki sonuç ile disiplin kurulunun hükmen verdiği sonuç** farklı.
Master birincil kaynağın değerini korur, satır `DATA_CONFLICT` ile işaretlidir; kök neden
belirlenmeden hiçbiri değiştirilmedi.

---

## 10. MAÇ SAYISI KONTROLÜ

Beklenen sayı körlemesine kullanılmadı; her lig-sezon için **veriden** takım sayısı `n` bulunup
`n×(n-1)` ile karşılaştırıldı. 72 lig-sezon hücresinin 70'i birebir tutuyor. Tutmayan 2 hücre:

| Hücre | Takım | Maç | Beklenen | Neden |
|---|---|---|---|---|
| Ligue 1 2019/20 | 20 | 279 | 380 | COVID — sezon 28. haftada iptal edildi, kalan maçlar **hiç oynanmadı** |
| Eredivisie 2019/20 | 18 | 232 | 306 | COVID — sezon iptal edildi |

Bunlar hata değil gerçektir; oynanmayan maçlar uydurulmadı.

Değişen lig büyüklükleri de doğrulandı: Ligue 1 2023/24'ten itibaren 18 takım (306), Süper Lig
2020/21'de 21 takım (420), 2022/23'te 19 takım (342).

---

## 11. KAPSAM MATRİSİ (17 kilitli kapsam × sezon)

| # | Competition | Type | 17/18 | 18/19 | 19/20 | 20/21 | 21/22 | 22/23 | 23/24 | 24/25 | 25/26 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Premier League | LEAGUE | 380 | 380 | 380 | 380 | 380 | 380 | 380 | 380 | 380 |
| 2 | La Liga | LEAGUE | 380 | 380 | 380 | 380 | 380 | 380 | 380 | 380 | 380 |
| 3 | Serie A | LEAGUE | 380 | 380 | 380 | 380 | 380 | 380 | 380 | 380 | 380 |
| 4 | Bundesliga | LEAGUE | 306 | 306 | 306 | 306 | 306 | 306 | 306 | 306 | 306 |
| 5 | Ligue 1 | LEAGUE | 380 | 380 | **279** | 380 | 380 | 380 | 306 | 306 | 306 |
| 6 | Süper Lig | LEAGUE | 306 | 306 | 306 | 420 | 380 | 342 | 380 | 342 | 306 |
| 7 | Championship | LEAGUE | 552 | 552 | 552 | 552 | 552 | 552 | 552 | 552 | 552 |
| 8 | Eredivisie | LEAGUE | 306 | 306 | **232** | 306 | 306 | 306 | 306 | 306 | 306 |
| 9 | UEFA Champions League | MAIN | 125 | 125 | 119 | 125 | 125 | 125 | 125 | 189 | 189 |
| 10 | UEFA Europa League | MAIN | 205 | 205 | 197 | 205 | 141 | 141 | 141 | 189 | 189 |
| 11 | UEFA Conference League | MAIN | – | – | – | – | 141 | 141 | 141 | 153 | 153 |
| 12 | UEFA Champions League | QUALIFIER | 74 | 79 | 79 | 41 | 81 | 77 | 77 | 76 | 78 |
| 13 | UEFA Champions League | QUAL. PLAY-OFF | 20 | 12 | 12 | 12 | 12 | 12 | 12 | 14 | 14 |
| 14 | UEFA Europa League | QUALIFIER | 224 | 272 | 272 | 136 | 16 | 14 | 14 | 56 | 58 |
| 15 | UEFA Europa League | QUAL. PLAY-OFF | 44 | 42 | 42 | 21 | 20 | 20 | 20 | 24 | 24 |
| 16 | UEFA Conference League | QUALIFIER | – | – | – | – | 238 | 230 | 232 | 208 | 208 |
| 17 | UEFA Conference League | QUAL. PLAY-OFF | – | – | – | – | 44 | 44 | 44 | 48 | 48 |

`–` = **o sezon o turnuva yoktu** (Conference League 2021/22'de başladı). Sıfır satır uydurulmadı.

Format değişimleri gerçektir: CL/EL 2024/25'te lig aşamasına geçti (125→189), EL elemesi
2021/22'de küçüldü (erken turlar Conference'a taşındı), 2020/21 elemeleri COVID nedeniyle tek maçlıydı.

---

## 12. DOĞRULANAMAYANLAR / EKSİKLER (açıkça)

1. **Süper Lig 2025/26 — 207 maçın skoru yok.** Elimizdeki iki kaynak da (football.json `tr.1`
   2025-26 ve europe-master `2025-26_tr1.txt`) yalnız fikstür taşıyor; sonuçlar ~Kasım 2025'ten
   sonrası için doldurulmamış. football-data.co.uk arşivi 2025-05'te bitiyor. **Skor uydurulmadı**;
   satırlar `MatchStatus=UNKNOWN` ve skor NULL olarak duruyor. Kapatmanın tek yolu api-football'dan
   çekmektir — bu görevde yeni API çağrısı yasak olduğu için yapılmadı.
2. **Ligue 1 2025/26 ve Eredivisie 2025/26'da 1'er maçın skoru yok** (kaynakta iptal/boş).
3. **Championship terfi play-off'ları yalnız 2021/22–2025/26 için var.** openfootball `en.2`
   dosyaları 2018/19–2020/21 sezonlarında play-off maçlarını hiç içermiyor, football-data zaten
   içermiyor. 2017/18–2020/21 play-off'ları hiçbir kaynakta yok → eklenmedi.
4. **1.318 satırda `Round` boş.** Bunlar 2017/18 Championship, Eredivisie ve Süper Lig hücreleri:
   birincil kaynak football-data'nın round kolonu yok ve o sezonlar için round taşıyan ikincil
   kaynak da yok. Diğer 22.585 satırda round eşleşen ikincil kaynaktan dolduruldu
   (`ROUND_FILLED_FROM_SECONDARY`).
5. **872 satırda ilk yarı skoru yok**, 7.124 satırda başlama saati yok — kaynaklar vermiyor.
6. **`KickoffUtc` çoğu satırda NULL.** Kaynak yerel saat dilimini bildirmediği için UTC'ye
   çevirmek tahmin olurdu; yapılmadı.
7. **173 ikincil satır master'a eklenmedi** (`SECONDARY_NOT_PROMOTED`): aynı maçın zaten master'da
   olan kaydıyla çakışıyor (aynı takım çifti, farklı tarihleme). Bunların çoğu openfootball'un
   PL/Championship 2019/20 dosyalarındaki **yer tutucu tarihlerdir** (COVID sonrası maçlara
   `2020-06-30` gibi sabit tarih yazılmış). Master gerçek tarihi olan kaydı tutar.
8. **89 takım-bağlantısı parmak izi ile çözülemedi** (bkz. §7) — hepsi listelendi.

---

## 13. SON VERİ KALİTE KAPISI (2026-08-20)

Master oluşturulduktan sonra modellemeye hazırlık kapısı çalıştırıldı. **Kaynak dosyalara
dokunulmadı, master'dan tek satır silinmedi, hiçbir skor veya takım kimliği uydurulmadı.**

### 13.1 Eksik skorlar kapatıldı — 209 → 0

Hedefli api-football çekimi, **toplam 8 çağrı** (tüm tarihsel set yeniden çekilmedi):

| Çağrı | Amaç |
|---|---|
| `fixtures?league=203&season=2025` | Süper Lig 2025/26 — 207 eksik skor. Bu satırlar openfootball kaynaklı olduğu için provider fixture id'leri yoktu; maç başına çağrı imkânsızdı, ~30 ayrı tarih çağrısı yerine 1 çağrı yapıldı |
| `fixtures?league=61&season=2025&date=2026-05-17` | Ligue 1 tek eksik |
| `fixtures?league=88&season=2025&date=2026-05-10` + `fixtures?league=88&season=2025` | Eredivisie tek eksik (maç 2026-05-11'e ertelenmiş) |
| 4 × `fixtures?league=..&season=..&date=..` | 4 çelişki için bağımsız üçüncü kaynak |

Sonuç: **209 eksik skorun tamamı gerçek sağlayıcı verisiyle dolduruldu**; `NOT_AVAILABLE_FROM_API`
kalemi çıkmadı. 109 satırda openfootball'un fikstür tarihi gerçek oynanma tarihinden farklıydı →
tarih api verisine göre düzeltildi (`DATE_CORRECTED_FROM_API`). İki Süper Lig maçı (22.03.2026 →
08/09.04.2026) ertelenmişti; çift devreli ligde bir (ev, deplasman) sırası sezonda tek kez
bulunabildiği için eşleşme kesindi.

### 13.2 Türkçe I tuzağı — kimlik bölünmesi düzeltildi

**Kök neden:** PowerShell `-replace` büyük/küçük harfe duyarsızdır ve **tr-TR culture'da `[A-Z]`
sınıfı, içinde 'I' geçen metinlerle eşleşmez.** `"(GRE)"` temizleniyor ama `"(ITA)"`, `"(ISR)"`,
`"(BIH)"` temizlenmiyordu; bu yüzden `Bologna FC 1909` ile `Bologna FC 1909 (ITA)` iki ayrı
kimlik olarak kalmıştı. Tüm regex'ler `[regex]::Replace` (culture-invariant) ile değiştirildi ve
kimlik çözümü **kaynağından yeniden çalıştırıldı**: 763 → **747** canonical takım.

**Önemli:** Bu düzeltmenin ilk denemesinde "token subset" kuralı da denendi ve
`Athletic Club` ↔ `Wigan Athletic`, `Rangers` ↔ `Queens Park Rangers`, `FK Sarajevo` ↔
`Željezničar` gibi **yanlış birleştirmeler** ürettiği görülünce **tamamen geri alındı**.
Nihai kural yalnızca şudur:

* **fikstür parmak izi** (aynı maç kümesi) — dilden bağımsız sert kanıt, veya
* **normalize isim tam eşitliği** + iki koruma: (a) iki ad gerçek bir maçta karşı karşıya
  gelmemiş olacak, (b) iki ad **aynı kaynağın aynı sezonunda** birlikte geçmeyecek (geçiyorsa
  iki farklı kulüptür).

İsim benzerliğine dayanan hiçbir birleştirme yapılmadı.

### 13.3 Takım kimlik güveni

| Sınıf | Takım |
|---|---|
| `CONFIRMED` | **709** |
| `PROBABLE` | 0 |
| `UNRESOLVED` | 0 |

Kanıt türü her takım için `FORMAX_HISTORICAL_TEAMS.csv` → `Evidence` kolonunda: api-football
provider team id / fikstür parmak izi bağlantısı / korpusta tek yazım / tüm yazımların tek isme
normalize olması (koruma testinden geçmiş).

Parmak izi ile çözülemeyen **93 bağlantının** tamamı
`FORMAX_HISTORICAL_TEAM_IDENTITY_REVIEW.csv` içinde tek tek, hangi canonical kimliğe hangi
kanıtla bağlandığıyla listelidir; hepsi `CONFIRMED`.

### 13.4 4 çelişki — bağımsız üçüncü kaynakla çözüldü

Oylama **kaynak ailesi** bazında yapıldı (openfootball-json + openfootball-txt aynı projedir,
tek oy sayılır): `football-data`, `openfootball`, `fixturedownload`, `api-football`.

| Maç | football-data | openfootball | api-football | Master (official) | Not |
|---|---|---|---|---|---|
| Hellas Verona – Roma, 2020-09-19 | 0-0 | 3-0 | **3-0** | **3-0** | Roma kadro dışı oyuncu oynattı; resmî kayıt hükmen 3-0. Sahadaki skor 0-0 `OnPitchResult`'ta |
| NEC – Vitesse, 2023-10-01 | 1-3 | 1-2 | **1-3** | **1-3** | Maç durdurulup sonradan tamamlandı; 1-2 durdurulma anındaki skor (`OnPitchResult`) |
| Union Berlin – Bochum, 2024-12-14 | 1-1 | 0-2 | **1-1** | **1-1** | 1-1 sahadaki skor (2 bağımsız aile). openfootball'daki 0-2 disiplin kararına karşılık geliyor ama ikinci kaynak doğrulamıyor → `AlternateReportedResult` |
| Akhisar – Beşiktaş, 2019-01-18 | 1-3 | 0-3 | **0-3** | **0-3** | İki bağımsız kaynak 0-3; football-data'nın 1-3'ü tek başına kalan tutarsızlık (hükmen karar değil) |

**Çözülemeyen conflict: 0.** Hiçbiri rastgele seçilmedi; her biri en az iki bağımsız kaynak
ailesinin ortak değerine dayanıyor, azınlık değeri kaybedilmedi.

**Kolon anlamları:** `OfficialFinalResult` = master'daki `HomeGoals/AwayGoals`.
`OnPitchResult` yalnız sahadaki skorun farklı olduğu **doğrulanabilen** 2 maçta dolu.
Azınlık kaynağın değeri her zaman `AlternateReportedResult` (conflicts dosyası) ve master'daki
`AlternateReportedHomeGoals/AwayGoals` kolonlarında durur.

**Prediction target önerisi:** gol modeli için **`HomeGoals` / `AwayGoals`** (uzatma dâhil,
penaltı serisi hariç). `OnPitchResult` dolu olan 2 maçta resmî skor sahadaki oyunu yansıtmaz;
gol üretim modeli eğitiliyorsa bu 2 maç `OnPitchResult` ile değiştirilebilir veya dışarıda
bırakılabilir — karar modelleme aşamasına ait, veri her iki seçeneği de taşıyor.

### 13.5 Model dataset ayrımı

| Dosya | Satır |
|---|---|
| `FORMAX_HISTORICAL_MASTER.csv` | **33.906** — her gerçek tarihsel kayıt (veri kaybı yok) |
| `FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv` | **33.901** — yalnız `ModelEligible = true` |
| `FORMAX_HISTORICAL_MODEL_EXCLUSIONS.csv` | **5** — sebebiyle birlikte |

Dışarıda bırakılan 5 maçın tamamı **`INVALID_STATUS` = hükmen (AWARDED)**, yani sahada
oynanmamış sonuçlar:

| Maç | Not |
|---|---|
| RB Leipzig – Spartak Moskova 2022-03-10 + rövanş 2022-03-17 (UEL) | Rusya kulüplerinin turnuvadan çıkarılması, maçlar oynanmadı |
| Dnipro-1 – Puskás 2024-07-25 + rövanş 2024-08-01 (UECL) | hükmen 3-0 |
| Nantes – Toulouse 2026-05-17 (Ligue 1) | api-football `AWD` |

Model uygunluk kuralı: `MatchStatus ∈ {FT, AET, PEN}` **ve** skor dolu **ve** tarih dolu **ve**
competition/season/type dolu **ve** her iki takım kimliği `CONFIRMED` **ve** çözülmemiş conflict yok.
Sebep kodları: `MISSING_SCORE`, `MISSING_DATE`, `OUT_OF_SCOPE`, `INVALID_STATUS`,
`IDENTITY_UNRESOLVED`, `RESULT_CONFLICT`.

### 13.6 Kapı doğrulaması (`_gate_verification.txt`)

```
duplicate canonical keys ........ 0        duplicate MatchIds .............. 0
missing date (master) ........... 0        missing score (master) .......... 0
missing score (model eligible) .. 0        missing date  (model eligible) .. 0
identity not CONFIRMED (eligible) 0        status not FT/AET/PEN (eligible)  0
unresolved conflicts in eligible  0        teams not CONFIRMED ............. 0 / 709
RegularTime + ExtraTime <> HomeGoals ... 0
shootout score but status <> PEN ....... 0
status AET/PEN without ET score ........ 0
negative or absurd goals (>15) ......... 0
```

Model dataset bileşimi: DOMESTIC_LEAGUE 26.846 · UEFA_MAIN 3.587 · UEFA_QUALIFIER 2.838 ·
UEFA_QUALIFICATION_PLAYOFF 605 · DOMESTIC_PLAYOFF 25 · 709 takım · 2017-06-27 … 2026-05-30 ·
FT 33.577 / AET 166 / PEN 158 · 5.326 satırda `ProviderMatchId` · kaynak derinliği
1 kaynak 7.040, 2 kaynak 20.291, 3 kaynak 6.445, 4 kaynak 125.

### 13.7 Look-alike kimlikler — KAPATILDI (2026-08-20, ikinci tur)

Önceki turda açık bırakılan **43 belirsiz kimlik çifti**, api-football provider kimliğiyle tek tek
doğrulandı. İsim benzerliği hiçbir kararda kanıt olarak kullanılmadı.

**Yöntem — 12 API çağrısı:**

| Çağrı | Amaç |
|---|---|
| 8 × `teams?league=&season=` | Eksik tarafın provider kimliğini o lig-sezonun resmî takım listesinden bulmak (140/2025, 2/2024, 3/2024, 3/2025, 40/2022, 40/2025, 848/2024, 848/2025) |
| 2 × `teams?id=` | Katalogda olmayan iki provider kimliğinin ülke/kuruluş/stadyum detayı (2248, 853) |
| 2 × `fixtures?league=&season=` | İsimle ayrışmayan iki tarafı **fikstür kanıtıyla** çözmek (140/2025, 848/2025) |

43 çiftin 42'sinde bir taraf zaten provider id taşıyordu; yalnız eksik taraflar sorgulandı.
Hiç `events`, `lineups`, `statistics`, `odds` çekilmedi.

**İsimle ayrışmayan iki vaka — fikstür kanıtı:**

* `RCD Espanyol de Barcelona`: "Espanyol" ve "Barcelona" adaylarının ikisi de isim token'ıyla
  eşleşiyordu (tehlikeli!). La Liga 2025/26'daki **38 maçın 38'i** provider 540 (Espanyol) ile
  birebir aynı; ikinci aday 0,03 örtüşme → **540**.
* `Víkingur (FRO)`: "Víkingur Reykjavík" (ISL) ile "Víkingur Gøta" (FRO) adayları skor deseniyle
  berabere kalıyordu. Rakip kanıtı kesin: master `2025-08-07 Víkingur v Linfield 2-1`,
  api-football `2025-08-07 Vikingur Gota (580) v Linfield (583) 2-1` → **580**.

**Sonuç:**

| Karar | Çift |
|---|---|
| `CONFIRMED_SAME` | **38** |
| `CONFIRMED_DIFFERENT` | **5** (+ önceki turda veriyle kanıtlanmış 21 çift = toplam 26) |
| `UNRESOLVED` | **0** |

Tam döküm: `FORMAX_HISTORICAL_TEAM_IDENTITY_RESOLUTION.csv` (her çift için provider id, ülke,
kuruluş yılı, stadyum, kanıt cümlesi, güven ve kontrol zamanı).

**Yanlış birleştirme testi (§8) — 38 `CONFIRMED_SAME` kararının her biri için:**
(a) iki kimlik gerçek bir maçta karşı karşıya gelmemiş, (b) tek bir kaynak aynı sezonda iki yazımı
birlikte kullanmamış, (c) provider ülke bilgileri aynı. Üçünden biri düşse karar `UNRESOLVED`
olurdu; hiçbiri düşmedi.

**Kritik negatif kontroller — hepsi ayrı kaldı:**

| Çift | Provider id | Ülke | Karar |
|---|---|---|---|
| Athletic Club ↔ Wigan Athletic FC | 531 ↔ 61 | Spain ↔ England | `CONFIRMED_DIFFERENT` |
| Athletic Club ↔ Charlton Athletic FC | 531 ↔ 1335 | Spain ↔ England | `CONFIRMED_DIFFERENT` |
| Athletic Club ↔ St Patrick's Athletic | 531 ↔ 3843 | Spain ↔ Ireland | `CONFIRMED_DIFFERENT` |
| Queens Park Rangers ↔ Rangers | 72 ↔ 257 | England ↔ Scotland | `CONFIRMED_DIFFERENT` |
| CSKA 1948 Sofia ↔ CSKA Sofia | 1426 ↔ 853 | Bulgaria ↔ Bulgaria (farklı kulüp) | `CONFIRMED_DIFFERENT` |
| FK Sarajevo ↔ Željezničar Sarajevo | — | — | `CONFIRMED_DIFFERENT` (aynı sezonda ikisi de yer alıyor) |

**Birleştirilenlerden örnekler:** HNK Rijeka=Rijeka (561) · Ilves=Ilves Tampere (1163) ·
RCD Espanyol=Espanyol (540) · GNK Dinamo=Dinamo Zagreb (620) · Hajduk Split=HNK Hajduk Split (608) ·
Slovan Bratislava=S. Bratislava (656) · Víkingur=Víkingur Gøta (580) · Vitória Guimarães=Vitória SC (224).

**Master'a etkisi:** canonical takım sayısı **747 → 709**. Maç sayısı değişmedi (33.906),
coverage matrisi birebir aynı, birleştirme sonrası **duplicate 0** ve **kendi kendine maç 0**
(iki kontrol de çalıştırıldı). 709 takımın 496'sı artık provider team id taşıyor.

Geriye kalan 23 look-alike çiftinin tamamı `CONFIRMED_DIFFERENT` — açık kalem yok.

### 13.8 Bilerek kapatılmayanlar

1. **1.318 satırda `Round` boş** — 2017/18 Championship / Eredivisie / Süper Lig. Round taşıyan
   kaynak yok; tahminle doldurulmadı, model için zorunlu alan değil.
2. **663 satırda ilk yarı skoru, 7.124 satırda başlama saati yok** — kaynak vermiyor, uydurulmadı.
3. **`KickoffUtc` 5.667 satırda dolu**; saat dilimi kesin bilinmeyen kaynaklarda NULL.
4. **Championship terfi play-off'ları yalnız 2021/22–2025/26** (25 maç, `DOMESTIC_PLAYOFF`);
   daha eski play-off'lar hiçbir kaynakta yok.
5. **Ligue 1 2019/20 (279) ve Eredivisie 2019/20 (232)** COVID'de iptal edilen sezonlar;
   oynanmamış maçlar üretilmedi.

---

## 14. KABUL KRİTERİ DURUMU

| Zorunlu kriter | Durum |
|---|---|
| Duplicate = 0 | ✅ 0 (canonical anahtar ve MatchId) |
| Missing Date = 0 | ✅ 0 |
| Model eligible maçlarda Missing Score = 0 | ✅ 0 |
| Model eligible maçlarda Team Identity = CONFIRMED | ✅ 33.901/33.901 · 709 takımın tamamı CONFIRMED |
| Model eligible maçlarda Competition/Season kesin | ✅ 0 boş |
| Çözülemeyen conflict model dışında | ✅ çözülemeyen conflict yok; 5 hükmen maç dışarıda |
| Eksik skorlar uydurulmamış | ✅ hepsi api-football'dan gerçek veriyle geldi |
| Takım identity uydurulmamış | ✅ 38 birleştirmenin her biri provider id + 3 güvenlik testi; isim benzerliğiyle 0 birleştirme |
| Team Identity Unresolved = 0 | ✅ 0 |
| Source provenance korunuyor | ✅ `Source`, `SourceCount`, `SourceList`, `ProviderMatchId` |
| 17 kapsam + play-off ayrımı korunuyor | ✅ coverage matrisi (§11) değişmedi + `DOMESTIC_PLAYOFF` ayrı |

**HISTORICAL DATA READY FOR MODELING**

Modelleme (Dixon-Coles, Bivariate Poisson, Elo, xG, calibration, ensemble, prediction, Gemma)
bu aşamada **yapılmadı**.

