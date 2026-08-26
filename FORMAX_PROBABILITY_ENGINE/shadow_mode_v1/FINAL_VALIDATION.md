# FORMAX — SHADOW MODE FINAL VALIDATION

**Tarih:** 2026-08-22
**Model:** DEĞİŞTİRİLMEDİ — `INDEPENDENT_POISSON_V2` / `TEAM_STRENGTH_V2` / `GATE_V1` / `NONE`,
parmak izi `94FC079D00190B341B2AEB6F0A54712E` (üç ayrı koşuda aynı)

---

# `PRODUCTION_SHADOW_NOT_READY`

§12'nin 11 maddesinden **8'i geçti, 3'ü geçmedi**. Geçmeyenler zamana bağlı ve bu oturumda
kapatılamaz. Öncelik sırasıyla §12 bölümünde.

---

## 1. HOSTED SHADOW JOB — **PASS** (§1)

Job **gerçek API host'u içinde** çalıştırıldı (`Formax.API`, Development, port 5063):

```
[SHADOW] Job started. Horizon 8d, startup delay 00:00:10, loop 00:01:12.
[SETTLEMENT] Job started.
[SHADOW] evidence: 33901 historical + 253 recent production = 34154, through 2026-08-21
[SHADOW] cycle done in 2583 ms — seen 65, predicted 65 (accepted 65/rejected 0),
         inserted 0, already published 65, failed 0
```

Ölçüm penceresinde **3 cycle** koştu (2583 / 2429 / 2293 ms), hepsi idempotent.

Ayrıca yaşam döngüsünün tamamı ayrı bir testle sınandı (`hosttest`) — **8/8 PASS**:

| Kontrol | Sonuç |
|---|---|
| Hosted service başlatma (host'un çağırdığı `StartAsync`) | started |
| Scheduler tekrarlı cycle | 2 cycle |
| DB erişimi (job içinden) | tahmin üretti |
| Duplicate prevention (host içinde) | `inserted 0` |
| Cycle hatası | 0 |
| `StopAsync` istisnasız | stopped |
| **Graceful shutdown loglandı** | `[SHADOW] Job stopped gracefully.` |
| Settlement job durdu | `[SETTLEMENT] Job stopped gracefully before its first cycle.` |

> **Not:** Windows'ta arka plan sürecine Ctrl+C göndermek güvenilir olmadığı için kapanış,
> host'un kendi çağırdığı `IHostedService.StopAsync` ile verildi — yani API'nin kapanışta
> yaptığının aynısı, sinyal katmanı hariç.

---

## 2. EF TRIGGER PROBLEMİ — **ÇÖZÜLDÜ** (§2)

### Bulunan durum

Önceki fazda EF'in UPDATE'i reddediliyordu ama **trigger tarafından değil**: SQL Server,
tetikleyicili bir tabloya INTO'suz `OUTPUT` yan tümceli DML kabul etmiyor ve EF tam olarak onu
üretiyordu. Sonuç doğruydu, mekanizma yanlıştı.

### Ölçüm

Önce INSERT yolunun kırılmadığı **iki ayrı şekilde** doğrulandı:

| Yol | Sonuç |
|---|---|
| EF **tek satırlık** INSERT | `INSERTED (Sequence=71)` — identity geri döndü |
| EF **toplu** INSERT (3 satır) | `INSERTED (Sequences 72/73/74)` |

Bu ayrım önemliydi: gölge job 65 satırı tek `SaveChanges` ile yazıyor, yani **toplu** yolu
kullanıyor. Toplu yolun çalışması tek satırlık yolun çalıştığını göstermez; üretimde bir maç için
tek tahmin yazıldığında o yol devreye girer. İkisi de ayrı ayrı sınandı.

### Çözüm

Trigger'lar EF modeline bildirildi:

```csharp
entity.ToTable("Predictions", tb =>
{
    tb.HasTrigger("TR_Predictions_NoUpdate");
    tb.HasTrigger("TR_Predictions_NoDelete");
});
```

Böylece EF trigger-uyumlu SQL üretiyor, UPDATE **gerçekten trigger'a ulaşıyor** ve trigger'ın
kendi mesajıyla reddediliyor. INSERT yolu etkilenmedi.

### Sonuç — `eftest`, 12/12 PASS

| Kontrol | Öncesi | Sonrası |
|---|---|---|
| EF tek satır INSERT | ACCEPTED | **ACCEPTED** |
| EF toplu INSERT | ACCEPTED | **ACCEPTED** |
| EF UPDATE | reddedildi (EF/SQL uyumsuzluğu) | **reddedildi (TRIGGER)** |
| EF UPDATE'i trigger mı reddetti? | **HAYIR** | **EVET** |
| EF DELETE | reddedildi (uyumsuzluk) | **reddedildi (TRIGGER)** |
| EF settlement INSERT | — | **ACCEPTED** |
| EF çift settlement | — | **reddedildi (PK)** |
| Settlement sonrası tahmin olasılığı | — | **değişmedi** |

---

## 3. IMMUTABILITY — **PASS** (§3)

Gerçek DB'de, gerçek gölge satırı üzerinde:

```
INSERT : ACCEPTED   (tek satır ve toplu, identity geri dönüyor)
UPDATE : REJECTED   "Predictions is INSERT-ONLY: a published prediction may not be updated…"
DELETE : REJECTED   "Predictions is append-only: a published prediction may not be deleted…"
```

Denemeden sonra saklanan değerler **bit-identical**: olasılık `0.67297114`, ContentHash
`EDD4CE3BB593FDF754489CD3FFED5BD9`.

Settlement tahmin satırını değiştiremiyor: ayrı tablo, PK = `PredictionId`, çift settlement PK
ihlaliyle reddediliyor, settlement sonrası tahmin olasılığı ve ConfidenceClass sabit.

---

## 4. GERÇEK SHADOW HAFTASI — **FAIL / YAŞANMADI** (§4)

**Ölçülemedi.** Bugün 2026-08-22 09:21 UTC; tahmin edilen en erken maç 2026-08-22 15:00 UTC.
Bir maç haftası boyunca gözlem bu oturumda mümkün değil.

Şu ana kadar biriken:

| | |
|---|---|
| Prediction count | **69** (65 farklı maç) |
| Accepted / Rejected | **69 / 0** |
| Duplicate PredictionId | **0** |
| Errors | **0** |
| DB insert failures | **0** |
| Missing predictions | **0** (kapsamdaki 65 maçın 65'i) |
| Job restarts | 3 (kontrollü; API iki kez yeniden başlatıldı + hosttest) |

**69 satır / 65 maç** farkı bir hata değil, tasarlanan davranışın canlı kanıtı — §6'ya bakın.

Varsayılan UI kapalı: tüm satırlar `ShadowMode = 1`, hiçbir uç bu tabloları okumuyor.

---

## 5. SETTLEMENT — **FAIL / YAŞANMADI** (§5)

**Gerçek maç sonucuyla settlement gerçekleşmedi.** Settled = 0, çünkü tahmin edilen hiçbir maç
henüz oynanmadı (en erken kickoff'a 5,6 saat var).

Mekanizmanın kendisi doğrulandı ama **sentetik satırlarla**: settlement INSERT kabul ediliyor,
ikinci settlement PK ihlaliyle reddediliyor, settlement tahmin satırını değiştirmiyor.

> **Bu fazda bir hata yaptım ve düzelttim.** `verify` komutu, settlement 1:1 kuralını GERÇEK bir
> gölge tahminine SAHTE bir sonuç (1-0 HomeWin) iliştirip sonra silerek sınıyordu. Append-only
> koruma eklendikten sonra silme de engellendi ve prob, gerçek gölge kaydında sahte bir sonuç
> bıraktı. Sahte satır temizlendi ve prob `verify`'dan **kaldırıldı**: aynı kural artık yalnız
> `eftest` içinde, kendi sentetik satırlarıyla sınanıyor. Doğrulama, gözlemlediği veriyi
> kirletmemeli.

---

## 6. MODEL REGRESSION — **PASS** (§6)

Üretim kod yolu, `model_validation_v2` referansına karşı (dosyadan **okunan** değerler):

| Segment | N | LogLoss | Referans | Sapma | Durum |
|---|---|---|---|---|---|
| TRAIN | 18.472 | 1,006533 | 1,006533 | 1,6e-9 | **MATCH** |
| VALIDATION | 7.636 | 0,987473 | 0,987473 | 4,4e-9 | **MATCH** |
| TEST | 7.793 | 0,993544 | 0,993544 | 4,3e-9 | **MATCH** |
| FULL | 33.901 | 0,999254 | 0,999254 | 3,2e-9 | **MATCH** |

Sözleşmenin yayımladığı olasılıkların ham modelden sapması: **0,0e+0** (33.203 tahmin üzerinde).

Sapmalar ≤ 4,4e-9 kayan nokta gürültüsüdür; motor kodu ProjectReference ile **paylaşılıyor**,
kopyalanmıyor.

### Canlı gölge tahminleri için regresyon

Gölge tahminleri **gelecekteki** maçlar için üretildiği için araştırma veri setinde karşılıkları
yok; satır satır karşılaştırma mümkün değil. Bunun yerine iki şey ölçüldü:

1. Aynı motor, aynı config, **sabit girdiyle** referansı birebir üretiyor (yukarıdaki tablo).
2. Canlı gölge koşusundaki model parmak izi, regresyon koşusundakiyle **aynı**
   (`94FC079D00190B341B2AEB6F0A54712E`).

Canlı koşu motora **daha güncel kanıt** verir (veri setinden sonra biten 253 üretim maçı). Bu bir
model değişikliği değil, aynı modelin güncel veriyle çalışmasıdır; bu yüzden regresyon girdinin
sabit tutulduğu ayrı koşuyla ölçülür.

---

## 7. EVIDENCE CUTOFF — **PASS** (§7)

Canlı DB'deki **69 satırın hepsinde**: `EvidenceCutoff < MatchDate`.

```
evidence dated at or after the match: expected 0, observed 0
```

Ayrıca şema düzeyinde de imkânsız: `CK_Predictions_EvidencePreMatch` kısıtı GÜN karşılaştırması
yapıyor (`CAST(EvidenceCutoff AS DATE) < CAST(MatchDate AS DATE)`), yani aynı gün kanıtı da
reddediliyor.

Örnek satır: maç 2026-08-22T18:30Z, kanıt kesimi 2026-08-18.

---

## 8. /DETAIL REGRESSION — **PASS (regresyon yok)**, ama referans doğrulanamadı (§8)

Gerçek API host'u çalışırken, aynı maç (`/api/matches/42844/detail`), kontrollü A/B:

| | n | ortalama | medyan | p95 | min | maks |
|---|---|---|---|---|---|---|
| **Gölge KAPALI** (kontrol) | 7 | 323,9 ms | 323 ms | 345 ms | 277 | 373 |
| **Gölge AÇIK** (3 cycle koşarken) | 25 | **258,5 ms** | 258 ms | 272 ms | 239 | 319 |

**Fark: −65,3 ms (−%20,2) — gölge AÇIK iken /detail DAHA HIZLI.**

Bir arka plan işi istekleri hızlandıramaz; bu fark ısınma/ölçüm gürültüsüdür (kontrol yalnız 7
örnek ve açılıştan hemen sonra). Doğru okuma: **gölge job'ın /detail üzerinde ölçülebilir bir
etkisi yok.**

Cold start: kontrol 8,4 sn · gölge 12,3 sn — ikisi de ilk çağrı (JIT + EF model + AI bağlam),
tek örnek, karşılaştırmaya uygun değil.

> ⚠️ **Görevdeki "~0,1 sn warm" referansı bu makinede üretilemedi** — gölge tamamen kapalıyken
> bile warm ~324 ms. Bu referansın nereden geldiğini doğrulayacak bir öncesi ölçümüm yok,
> dolayısıyla "0,1 sn korunuyor" **diyemem**. Diyebildiğim: gölge modu açmak /detail'i
> yavaşlatmıyor.

---

## 9. COMPETITION COVERAGE (§9)

Gölge haftasında **gerçekten var olan** türler:

| CompetitionType | Tahmin | Ort. Ev | Ort. Beraberlik | Ort. Deplasman |
|---|---|---|---|---|
| DOMESTIC_LEAGUE | 33 | 0,4348 | 0,2494 | 0,3158 |
| UEFA_QUALIFICATION_PLAYOFF | 36 | 0,4492 | 0,2666 | 0,2842 |

**Veri olmayan türler — `NOT_TESTED_IN_SHADOW`:**

| CompetitionType | Durum | Sebep |
|---|---|---|
| `UEFA_MAIN` | **NOT_TESTED_IN_SHADOW** | Pencerede 0 maç — UCL/UEL/UECL'in tamamı play-off turunda |
| `UEFA_QUALIFIER` | **NOT_TESTED_IN_SHADOW** | Pencerede 0 maç |
| `DOMESTIC_PLAYOFF` | **NOT_TESTED_IN_SHADOW** | Pencerede 0 maç; veri setinde toplam 25 maç |

Confidence dağılımı: HIGH 62 · MEDIUM 6 · LOW 1.

---

## 10. IDENTITY BRIDGE — **PASS** (§10)

Köprü: `Teams.ExternalTeamId` → `ProviderTeamId` → `CanonicalTeamId`.
`ApiFootballTeamId` **kullanılmıyor** (9.050 takımın yalnız 4'ünde dolu).

Gölge maçlarının **tamamında** çözüm başarılı: 65 maçın 65'i için iki taraf da canonical kimliğe
çözüldü — çözülemeyen maç zaten kapsam dışında bırakılıyor, uydurma kimlik üretilmiyor.

Kapsam zinciri: 8 günde 1.757 `NotStarted` → 128 canonical çözülebilir → **65** kilitli 11
müsabaka kapsamında.

---

## 11. PERFORMANCE — **PASS** (§11)

| Ölçüm | Değer |
|---|---|
| Maç başına ortalama tahmin süresi | **0,13 – 1,37 ms** (koşuya göre) |
| Maç başına p95 | **0,28 ms** |
| Maç başına maks | 82 ms (ilk maç — JIT + ilk replay) |
| Job cycle süresi (host içinde) | **2.000 – 4.124 ms** |
| DB insert süresi | 65 satır tek `SaveChanges` içinde, cycle toplamına dâhil |
| Failure / retry | **0** |

Cycle süresinin çoğu tahminde değil, **rating replay'inde** geçiyor: 34.154 maçlık akış, her
yaklaşan maç TARİHİ için bir kez (bu pencerede 8 gün → 8 replay).

Arka plan işi /detail'i bloke etmiyor (§8).

---

## 12. KABUL KRİTERLERİ (§12)

| # | Kriter | Durum |
|---|---|---|
| 1 | hosted shadow job | **PASS** |
| 2 | EF INSERT | **PASS** (tek satır + toplu) |
| 3 | UPDATE rejected | **PASS** (trigger tarafından) |
| 4 | DELETE rejected | **PASS** (trigger tarafından) |
| 5 | replay idempotent | **PASS** (`inserted 0`, `already published 65`) |
| 6 | **en az 1 gerçek maç haftası gözlem** | **FAIL — yaşanmadı** |
| 7 | **en az 1 gerçek settlement** | **FAIL — hiçbir maç oynanmadı** |
| 8 | regression MATCH | **PASS** (4/4, sapma ≤ 4,4e-9) |
| 9 | EvidenceCutoff safe | **PASS** (69/69) |
| 10 | /detail no regression | **PASS** (ölçüldü, −%20) |
| 11 | **zero unexplained failures** | **FAIL — aşağıya bakın** |

### 11. madde neden FAIL

Bu fazda **açıklanmış** üç hata bulundu ve düzeltildi (§13). Açıklanamayan bir hata yok. Ama
kriter "sıfır açıklanamayan hata" değil de bir gözlem penceresi boyunca ölçülmesi gereken bir
şeydir; **gözlem penceresi olmadığı için** bu madde de doğrulanamamış sayılmalıdır. Bir haftalık
koşu, bugün görünmeyen hataları (kota, kilit çekişmesi, uzun süreli bellek davranışı, gece
yeniden başlatmaları) ortaya çıkarabilir.

---

## 13. BU FAZDA BULUNAN VE DÜZELTİLEN HATALAR

**1. Job'lar graceful kapanmıyordu — host'u düşürebilirdi.**
Döngü sonundaki `await Task.Delay(loopDelay, stoppingToken)` `try` bloğunun **dışındaydı**.
Kapanış sinyali vakaların neredeyse tamamında tam oraya düşer; `OperationCanceledException`
`ExecuteAsync`'ten dışarı sızıyor, kapanış log satırına hiç ulaşılmıyordu. .NET 8'in varsayılan
`BackgroundServiceExceptionBehavior.StopHost` davranışıyla bu, host'u düşürme yolu. Hem döngü
beklemesi hem başlangıç gecikmesi iptal-güvenli hâle getirildi; iki job da artık gerçekten
"stopped gracefully" logluyor. **Bu hatayı §1'in graceful shutdown kontrolü ortaya çıkardı.**

**2. EF UPDATE'i trigger değil, EF/SQL uyumsuzluğu reddediyordu.** §2'de çözüldü.

**3. `verify` komutu gerçek gölge verisini kirletiyordu.** §5'te anlatıldı; prob kaldırıldı,
sahte settlement temizlendi.

---

## 14. HATA DEĞİL: 69 satır / 65 maç

Dört maçın **ikişer** tahmini var. İncelendi:

| MatchId | PredictionId | EvidenceCutoff | HomeProbability |
|---|---|---|---|
| 101811 | FMXP519EFB53… | 2026-05-23 | 0,75789521 |
| 101811 | FMXPE6902C69… | **2026-08-21** | 0,75525155 |
| 104925 | FMXP4F85408D… | 2026-05-17 | 0,31926461 |
| 104925 | FMXP082694E4… | **2026-08-21** | 0,29228166 |

Aradan geçen sürede o takımlar maç oynadı → `EvidenceCutoff` değişti → `PredictionId` değişti →
**yeni tahmin yazıldı, eskisi olduğu gibi duruyor.** Sözleşmenin §10 kuralının canlı kanıtı:
düzeltme değil, yeni satır.

---

## 15. KALAN AÇIKLAR — önem sırasıyla

1. **Gerçek maç haftası gözlemi (§4).** En kritik eksik. Gölge açık bırakılıp en az 7 gün
   izlenmeli: prediction count, duplicate, error, latency, insert failure, missing prediction,
   job restart. Bugünkü ölçümler tek oturumluk.
2. **Gerçek settlement (§5).** İlk maçlar bugün 15:00 UTC'den itibaren oynanacak. Settlement job
   3 saatte bir koşuyor; ilk gerçek sonuç bu akşam iliştirilebilir. Doğrulanması gereken:
   `ActualResult` doğru, tahmin satırı değişmedi, aynı tahmin ikinci kez settle edilmedi.
3. **`UEFA_MAIN`, `UEFA_QUALIFIER`, `DOMESTIC_PLAYOFF` türleri (§9).** Bu pencerede hiç maç yok.
   UEFA grup aşaması Eylül'de başlıyor; o zamana kadar bu türlerde gölge davranışı bilinmiyor.
4. **`/detail` için gerçek referans (§8).** "~0,1 sn warm" bu makinede üretilemedi. Gölgenin
   regresyon yaratmadığı ölçüldü, ama mutlak hedefin karşılandığı **doğrulanmadı**.
5. **Gerçek işletim sinyaliyle kapanış.** `StopAsync` ile doğrulandı; işletim sisteminden gelen
   SIGTERM/Ctrl+C ile host kapanışı ayrıca gözlenmedi.
6. **Çok örnekli (scale-out) davranış.** Aynı anda iki API örneği koşarsa iki cycle çakışabilir.
   Benzersiz `PredictionId` ikinci yazımı reddeder (kayıp yok) ama bu senaryo denenmedi;
   diğer job'lardaki gibi bir dağıtık kilit gerekebilir.

---

## 16. ÇALIŞTIRMA

```bash
cd FORMAX_PROBABILITY_ENGINE/shadow_mode_v1/FormaxShadowRunner
dotnet run -c Release -- regress    # üretim kod yolu vs araştırma referansı
dotnet run -c Release -- eftest     # EF + trigger davranışı (sentetik satırlar)
dotnet run -c Release -- hosttest   # hosted service yaşam döngüsü + graceful shutdown
dotnet run -c Release -- verify     # §18/§12 kabul kriterleri, canlı DB (salt okunur + UPDATE probu)
dotnet run -c Release -- predict    # yaklaşan gerçek maçlar için tahmin üret
dotnet run -c Release -- settle     # biten maçların sonuçlarını iliştir
dotnet run -c Release -- report     # shadow_predictions.csv + shadow_summary.csv
```

Üretimde: `Predictions:ShadowMode:Enabled = true`
(`appsettings.json` varsayılanı **false**, `appsettings.Development.json` **true**).

---

## 17. ÇIKTILAR

| Dosya | İçerik |
|---|---|
| `shadow_predictions.csv` | 69 gerçek tahmin, tüm sözleşme alanları + (varsa) sonuç |
| `shadow_summary.csv` | Genel + CompetitionType + ConfidenceClass kırılımı |
| `shadow_acceptance.csv` | §12 kabul kriterleri, canlı DB'ye karşı (15 kontrol) |
| `ef_trigger_probe.csv` | EF + trigger davranışı (12 kontrol) |
| `host_lifecycle.csv` | Hosted service yaşam döngüsü (8 kontrol) |
| `README.md` | Shadow Mode V1 kurulum ve mimari |
| `FINAL_VALIDATION.md` | bu dosya |
