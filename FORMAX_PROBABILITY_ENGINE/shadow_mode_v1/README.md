# FORMAX — PRODUCTION PREDICTION SHADOW MODE V1

**Tarih:** 2026-08-22
**Durum:** Migration uygulandı · 65 gerçek maç için tahmin üretildi · 17/17 kabul kontrolü PASS ·
regresyon 4/4 MATCH

> **`PRODUCTION SHADOW READY` DENMEDİ.** §18'in tüm maddeleri geçmedi: gözlem süresi (§17, en az
> 1 gerçek maç haftası) ve gerçek sonuçla settlement henüz **yaşanmadı**, /detail gecikme
> regresyonu **ölçülmedi**. Ayrıntı §9'da.

---

## 0. SON RAPOR

### DATABASE
**Migration PASS.** `20260822081906_PredictionContract_V1` + `20260822090000_PredictionContract_V1_NoDelete`
uygulandı. 2 tablo, 6 CHECK kısıtı, 4 trigger.

### IMMUTABILITY
**PASS** — ölçüldü, varsayılmadı. Ham SQL UPDATE trigger tarafından kendi mesajıyla reddedildi;
EF üzerinden UPDATE reddedildi; DELETE reddedildi (bu koruma bu görevde **eklendi**, §8'e bakın).
Saklanan olasılık ve ContentHash denemeden sonra bit-identical.

### SHADOW
| | |
|---|---|
| Prediction count | **65** |
| Accepted | **65** |
| Rejected | **0** |
| Kapsam | DOMESTIC_LEAGUE 29 · UEFA_QUALIFICATION_PLAYOFF 36 |
| Tarih aralığı | 2026-08-22 … 2026-08-29 |
| Rating kanıtı | 2026-08-19'a kadar |

### REPLAY
**Idempotency PASS.** İkinci koşu: 65 tahmin üretildi, **0 insert**, 65 `AlreadyPublished`,
tabloda hâlâ 65 satır. Ayrıca kazara silinen bir satır yeniden üretildiğinde **birebir aynı**
`PredictionId` ve bit-identical olasılıkla geri geldi (§8).

### SETTLEMENT
| | |
|---|---|
| Settled | **0** |
| Unsettled | **65** |

Sebep: 65 maçın **tamamı gelecekte**. Settlement mekanizması çalışıyor (0 aday ile temiz koştu,
canlı probe ile çift-settlement reddi doğrulandı) ama **gerçek bir maç sonucuyla henüz
çalışmadı**.

### PERFORMANCE
| | |
|---|---|
| Maç başına ortalama | **1,37 ms** |
| Maç başına p95 | **0,28 ms** |
| Maç başına maks | 82,25 ms (ilk maç — JIT + ilk replay) |
| Tam cycle | **4.753 ms** (65 maç, 8 ayrı gün replay'i) |

### MODEL REGRESSION
**MATCH.** Üretim kod yolu, `model_validation_v2` referansını dört segmentte de birebir
üretiyor; sapma ≤ 4,4e-9 (kayan nokta gürültüsü).

### BUGS
Üç gerçek bulgu — §8'de.

### DOĞRULANAMAYAN
Beş madde — §9'da.

---

## 1. ÜRETİM YAPISINA NASIL BAĞLANDI (§1)

Önce okundu: `Formax.slnx` (5 proje), `FormaxDbContext` (780 satır, ~120 DbSet), 62 mevcut
migration, `Program.cs` DI kayıtları, mevcut `BackgroundService` deseni
(`PlayerIntelligenceSyncJob` örnek alındı).

**Büyük refactor yapılmadı.** Üretim tarafındaki değişiklikler:

| Dosya | Değişiklik |
|---|---|
| `Formax.Domain/Entities/Prediction.cs` | **yeni** — 2 entity |
| `Formax.Infrastructure/Data/FormaxDbContext.cs` | +2 DbSet, +2 entity yapılandırması |
| `Formax.Infrastructure/Predictions/` | **yeni** — 3 servis |
| `Formax.Infrastructure/BackgroundJobs/ShadowPredictionJob.cs` | **yeni** — 2 job |
| `Formax.Infrastructure/Formax.Infrastructure.csproj` | +1 ProjectReference |
| `Formax.API/Program.cs` | +6 satır DI (varsayılan KAPALI) |
| `Formax.API/appsettings.json` | +`Predictions` bölümü |
| `Formax.Infrastructure/Migrations/` | +2 migration |

**Mevcut hiçbir uç, use case, servis veya istek yolu değiştirilmedi.**

### Motor neden kopyalanmadı

`Formax.Infrastructure` → `FormaxPredictionContract` ProjectReference'ı eklendi. Zincir:
`FormaxPredictionContract → FormaxPredictionGate → FormaxModelValidation → FormaxDixonColes →
FormaxTeamStrength`. Hepsi net8.0, NuGet bağımlılığı yok.

Sebep §19: "production shadow probability == model_validation_v2 reference, sapma 0". Kod
kopyalansaydı bu eşitlik bir dilek olurdu; **tek kaynak** olduğu için yapısal bir gerçek. Regresyon
bunu ayrıca ölçüyor.

---

## 2. KİMLİK KÖPRÜSÜ — en kritik bulgu

Üretim `Matches`/`Teams` **int** kimlik kullanır; olasılık motoru canonical **`FMXT…`** kimlikle
çalışır. Köprü veride zaten vardı ama bulunması gerekti:

```
Teams.ExternalTeamId  →  FORMAX_HISTORICAL_TEAMS.ProviderTeamId  →  CanonicalTeamId (FMXT…)
```

⚠️ `Teams.ApiFootballTeamId` alanı bu iş için **kullanılamaz**: 9.050 takımın yalnız **4'ünde**
dolu. Doğru alan `ExternalTeamId` (9.036 takımda dolu). Canonical korpustaki 494 sağlayıcı
kimliğinin **458'i** üretimde bulunuyor.

**Eşlenemeyen takım UYDURULMAZ.** Eşleşme yoksa maç kapsam dışıdır ve tahmin üretilmez.

### Kapsam daralması, ölçülmüş

| Aşama | Maç |
|---|---|
| Önümüzdeki 8 günde `NotStarted` | 1.757 |
| İki tarafı da canonical kimliğe çözülen | 128 |
| **Kilitli 11 müsabaka kapsamında** | **65** |

65 ile 128 arasındaki fark, kilitli müsabaka kapsamının dışındaki liglerdir (NB I, Ekstraklasa,
Premiership, Liga I…). Kapsam kuralı doğru çalışıyor.

### CompetitionType uydurulmadı

Tarihsel veri setinin kendi kuralı okundu ve birebir kopyalandı: UEFA turnuvalarında türü
sağlayıcının `Round` alanı belirler (`Qualifying Round`/`Preliminary Round`/`Round 1` →
`UEFA_QUALIFIER`, `Play-offs`/`Playoffs` → `UEFA_QUALIFICATION_PLAYOFF`, kalanı `UEFA_MAIN`);
yerel ligde `Semi-final`/`Final` → `DOMESTIC_PLAYOFF`, kalanı `DOMESTIC_LEAGUE`.

---

## 3. ZAMAN SÖZLEŞMESİ — gölge modun en ince yeri

Bir maç için tahmin, **yalnız o maçtan kesinlikle önce oynanmış** maçlardan üretilmelidir. Naif
uygulama bunu bozar: 65 maçı tek listede motora verirseniz, 23 Ağustos'taki (henüz oynanmamış)
maçın *varsayılan* skoru 25 Ağustos'taki maçın rating'ini besler.

**Uygulanan çözüm:** her yaklaşan maç TARİHİ için ayrı bir replay. Replay listesine yalnız
(a) bitmiş maçlar ve (b) o tarihteki yaklaşan maçlar girer. Motorun kendi kuralı gereği aynı gün
oynanan maçlar birbirini beslemez; sonraki günler listede hiç yoktur. 8 gün = 8 replay.

Motor **değiştirilmedi** — çözüm tamamen çağıran taraftadır.

Ölçülen sonuç: 65 tahminin **hiçbirinde** `EvidenceCutoff >= MatchDate` yok. Örnek satır:
maç 2026-08-22T18:30Z, kanıt kesimi 2026-08-18.

### Rating tazeliği

Araştırma veri seti 2026-05-30'da bitiyor; bugün 2026-08-22. Aradaki ~3 ay, üretimdeki **bitmiş**
maçlardan kapatıldı: canonical kimliğe çözülebilen **595** maç motora eklendi. Rating kanıtı
böylece **2026-08-19**'a kadar taze.

Bu bir model değişikliği **değildir** — aynı motorun güncel veriyle çalışmasıdır. Regresyon
(§7) girdinin sabit tutulduğu ayrı bir koşuyla ölçülür.

---

## 4. VERİTABANI (§2, §3, §4)

```
dbo.Predictions            INSERT-ONLY, 20 sütun, Sequence IDENTITY PK, PredictionId UNIQUE
dbo.PredictionSettlements  1:1, PK = PredictionId → çift settlement ŞEMA düzeyinde imkânsız
```

**6 CHECK kısıtı:** simplex (kabul edilende üç olasılık dolu/[0,1]/toplam 1, reddedilende üçü de
NULL, GateStatus tutarlı), kanıtın maçtan önceki bir **günde** olması, damganın kanıttan sonra
olması, confidence sözlüğü + yayımlanmışta `NONE` yasağı, GateStatus sözlüğü, settlement sonucunun
gollerle tutarlılığı.

**4 trigger:** `Predictions` ve `PredictionSettlements` üzerinde UPDATE ve DELETE → `ROLLBACK` +
açıklayıcı hata.

> **Neden `DENY UPDATE` değil de trigger?** `DENY` bir principal'a bağlıdır ve sysadmin'i bağlamaz;
> FORMAX bağlantısı `Trusted_Connection` ile yönetici olarak geliyor, dolayısıyla tek başına DENY
> bu kurulumda **hiçbir şeyi engellemezdi**. Trigger kim bağlanırsa bağlansın çalışır. `DENY` yine
> de ikinci katman olarak migration'da duruyor ve rol varsa uygulanıyor. §3 zaten "eşdeğer
> immutable yapı"ya izin veriyor.

Canlı kanıt:

```
RAW SQL UPDATE : BLOCKED -> Predictions is INSERT-ONLY: a published prediction may not be updated…
after          : 0.35669509867839105   (değişmedi)
RAW SQL DELETE : BLOCKED -> Predictions is append-only: a published prediction may not be deleted…
```

---

## 5. GÖLGE MODU (§6, §7, §15, §16)

`ShadowPredictionService` → `ShadowPredictionJob` (BackgroundService, 6 saatte bir, 8 gün ufuk).

* **Gerçek** MatchId, PredictionTimestamp, EvidenceCutoff, Probability, Gate, Confidence, versiyonlar.
* Satırlar `ShadowMode = 1` ile yazılır. **Hiçbir uç bu tabloları okumaz**; frontend'e hiçbir şey
  gitmez; UI değişmedi.
* Varsayılan **KAPALI**: `Predictions:ShadowMode:Enabled = false`. Job ancak açıkça açılırsa koşar.
* Gate V1 **aynen** kullanıldı, değiştirilmedi.

### Bu penceredeki dağılım

| CompetitionType | Maç | Ort. Ev | Ort. Beraberlik | Ort. Deplasman |
|---|---|---|---|---|
| DOMESTIC_LEAGUE | 29 | 0,4348 | 0,2494 | 0,3158 |
| UEFA_QUALIFICATION_PLAYOFF | 36 | 0,4492 | 0,2666 | 0,2842 |
| **Toplam** | **65** | 0,4428 | 0,2589 | 0,2983 |

| ConfidenceClass | Maç |
|---|---|
| HIGH | 58 |
| MEDIUM | 6 |
| LOW | 1 |

**`UEFA_MAIN`, `UEFA_QUALIFIER` ve `DOMESTIC_PLAYOFF` bu pencerede hiç yok** — UCL/UEL/UECL
maçlarının tamamı play-off turunda. §7'nin `DOMESTIC_PLAYOFF` için istediği ayrı not: bu pencerede
**0 maç**, dolayısıyla bu tür hakkında hiçbir gözlem yapılamadı.

---

## 6. İDEMPOTENTLİK VE REPLAY (§8, §9)

`PredictionId = SHA256(MatchId | 4 versiyon | EvidenceCutoff | PredictionTimestamp)`.

Aynı koşu ikinci kez çalıştırıldı:

```
predicted 65  (accepted 65 / rejected 0)
inserted  0
already published 65
tabloda satır: 65
```

**Duplicate insert = 0. Probability overwrite = 0.**

`EvidenceCutoff` değişirse `PredictionId` değişir → yeni satır, düzeltme değil. Bu, sözleşme
harness'ında birim testle de kilitli.

---

## 7. MODEL REGRESYONU (§19)

Üretim kod yolu (`TeamStrengthService` → `BacktestV2` → `ContractService`), üretim
yapılandırma dosyalarını okuyarak, **yalnız araştırma veri seti** üzerinde çalıştırıldı ve
`model_validation_v2/model_comparison.csv`'den **okunan** referansla karşılaştırıldı:

| Segment | N | LogLoss | Referans | Sapma | Durum |
|---|---|---|---|---|---|
| TRAIN | 18.472 | 1,006533 | 1,006533 | 1,6e-9 | **MATCH** |
| VALIDATION | 7.636 | 0,987473 | 0,987473 | 4,4e-9 | **MATCH** |
| TEST | 7.793 | 0,993544 | 0,993544 | 4,3e-9 | **MATCH** |
| FULL | 33.901 | 0,999254 | 0,999254 | 3,2e-9 | **MATCH** |

Ayrıca: sözleşmenin yayımladığı olasılıkların ham modelden sapması **0,0e+0** (33.203 kabul
edilmiş tahmin üzerinde).

Model parmak izi (validated config'in SHA-256'sı): `94FC079D00190B341B2AEB6F0A54712E` — hem
regresyonda hem canlı gölge koşusunda **aynı**.

`prediction_contract_v1` harness'ı üretim bağlantısından sonra yeniden çalıştırıldı: **16/16 test
geçiyor.** `Formax.API` derleniyor (0 hata).

---

## 8. BULUNAN GERÇEK BUGLAR

**1. Ad alanı çakışması — derleme kırıldı.**
Araştırma projesinin `Formax.Prediction` ad alanı ile yeni `Prediction` entity'si çakıştı
(`CS0118: 'Prediction' bir ad alanı öğesidir ancak tür olarak kullanılır`). `FormaxDbContext`
içinde tam nitelendirilerek çözüldü; araştırma projelerinin ad alanı değiştirilmedi.

**2. DELETE korunmuyordu — gerçek bir satır kaybedildi.**
Tablo "INSERT-ONLY" olarak tasarlandı ama yalnız UPDATE korunuyordu. Doğrulama sırasında bir test
sorgusundaki `ROLLBACK` autocommit'te işe yaramadı ve **gerçek bir gölge tahmini silindi** (65→64).

Açık teorik değildi, **ölçüldü**. `TR_Predictions_NoDelete` ve
`TR_PredictionSettlements_NoDelete` eklendi (`20260822090000_PredictionContract_V1_NoDelete`).
Kasıtlı temizlik gerekirse trigger açıkça DROP edilir — görünür ve niyetli bir eylem olur.

Silinen satır yeniden üretildiğinde **birebir aynı `PredictionId`** (`FMXP6FB4CD016482EEA6F6B7`)
ve **bit-identical olasılıkla** (`0.35669509867839105`) geri geldi — kazara da olsa §9 replay
determinizminin en güçlü kanıtı.

**3. EF'in UPDATE'i trigger tarafından değil, başka bir nedenle reddediliyor.**
Beklenen: trigger'ın `ROLLBACK`'i. Gerçekleşen:

```
The target table 'Predictions' of the DML statement cannot have any enabled triggers
if the statement contains an OUTPUT clause without INTO clause.
```

EF Core, UPDATE'te `OUTPUT` yan tümcesi kullanıyor ve tetikleyicili tabloda bu yasak. Sonuç aynı
(UPDATE geçmiyor, değer değişmiyor) ama **mekanizma farklı** ve bu fark önemli:

* **Operasyonel risk:** ileride `Predictions` üzerine bir **AFTER INSERT** trigger'ı eklenirse
  EF'in INSERT'leri de aynı hatayla kırılır ve gölge modu durur. Bu tabloya INSERT trigger'ı
  eklenmemelidir; eklenecekse EF tarafında `ToTable(tb => tb.HasTrigger(...))` bildirimi şart.
* Ham SQL yolu zaten trigger tarafından, kendi açıklayıcı mesajıyla engelleniyor (§4).

---

## 9. DOĞRULANAMAYANLAR — `PRODUCTION SHADOW READY` neden denmedi

1. **§17 gözlem süresi: en az 1 gerçek maç haftası.** Yaşanmadı. Tahminler bugün üretildi;
   maçlar 22–29 Ağustos arasında oynanacak. Duplicate / immutable violation / settlement error /
   missing prediction / missing settlement / leakage / latency / crash takibi bir hafta boyunca
   **yapılmadı**.
2. **Gerçek sonuçla settlement.** Settled = 0. Mekanizma çalışıyor (temiz koştu, çift-settlement
   canlı probe ile reddedildi) ama **hiçbir gerçek maç sonucu** henüz iliştirilmedi.
3. **`/detail` gecikme regresyonu = 0.** **Ölçülmedi** — API başlatılmadı. Elimdeki tek dayanak
   yapısal: istek yolundaki hiçbir dosya değiştirilmedi, gölge job varsayılan olarak kapalı, servis
   ortak tabloları `AsNoTracking` ile okuyor. Bu bir argümandır, ölçüm değildir.
4. **Job'ın API host'u altında gerçekten koşması.** `AddHostedService` kaydı yapıldı ve
   `Formax.API` derleniyor, ama host çalıştırılıp job'ın döngüsü **gözlenmedi**. Servisin kendisi
   runner üzerinden gerçek DB'ye karşı çalıştırıldı.
5. **`UEFA_MAIN`, `UEFA_QUALIFIER`, `DOMESTIC_PLAYOFF` türleri.** Bu 8 günlük pencerede hiç maç
   yok; bu türlerde gölge davranışı hakkında **hiçbir şey** ölçülemedi.

Ayrıca gözlemlenmemiş ama tasarımdan bilinen: gölge job 6 saatte bir koşar ve her koşuda 8 günlük
ufku yeniden değerlendirir; `EvidenceCutoff` yeni bir maç bittiğinde değişeceği için aynı maç için
**yeni** bir `PredictionId` üretilecektir. Bu beklenen davranıştır (düzeltme değil, yeni tahmin)
ama gerçek bir hafta boyunca kaç satır birikeceği ölçülmedi.

---

## 10. GÜVENLİK (§20)

* Log'lara API key, bağlantı dizesi veya secret yazılmıyor; servis loglarında yalnız MatchId,
  PredictionId, sayaçlar ve süre var.
* `EnableSensitiveDataLogging(false)`.
* Prediction contract dışarıdan değiştirilemez: `PredictionContract` bir `record`, tüm üyeler
  `init`-only; DB tarafında UPDATE trigger'ı var.
* **Hiçbir uç model parametresi kabul etmiyor** — zaten bu tabloları okuyan bir uç yok.
  `appsettings` içindeki `Predictions` bölümü yalnız motorun **yolunu** ve gölge modun açık olup
  olmadığını söyler; **tek bir model parametresi içermez**.

---

## 11. ÇALIŞTIRMA

```bash
cd FORMAX_PROBABILITY_ENGINE/shadow_mode_v1/FormaxShadowRunner
dotnet build -c Release
dotnet run -c Release -- regress   # üretim kod yolu vs araştırma referansı
dotnet run -c Release -- predict   # yaklaşan gerçek maçlar için tahmin üret ve yaz
dotnet run -c Release -- settle    # biten maçların sonuçlarını iliştir
dotnet run -c Release -- verify    # §18 kabul kriterleri, canlı DB'ye karşı
dotnet run -c Release -- report    # shadow_predictions.csv + shadow_summary.csv
dotnet run -c Release -- all       # regress -> predict -> settle -> report
```

Üretimde aynı servis `ShadowPredictionJob` olarak koşar; açmak için:
`Predictions:ShadowMode:Enabled = true`.

---

## 12. ÇIKTILAR

| Dosya | İçerik |
|---|---|
| `shadow_predictions.csv` | 65 gerçek tahmin: id, versiyonlar, olasılıklar, gate, confidence, kanıt kesimi, takımlar, müsabaka türü, (varsa) sonuç ve log loss |
| `shadow_summary.csv` | Genel + CompetitionType + ConfidenceClass bazında: sayım, kabul/red, settle/unsettle, ortalama olasılıklar, LogLoss/Brier/RPS/Accuracy |
| `shadow_acceptance.csv` | §18'in 17 kabul kontrolü, canlı DB'ye karşı ölçülmüş |

Metrik sütunları şu an boş: settle edilmiş tahmin yok (§9.2).
