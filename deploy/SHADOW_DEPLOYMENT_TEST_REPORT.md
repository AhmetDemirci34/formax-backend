# FORMAX — SHADOW DEPLOYMENT TEST RAPORU

**Tarih:** 2026-08-22 · **Ölçüm penceresi:** 14:16–14:37 UTC
**Model parmak izi:** `94FC079D00190B341B2AEB6F0A54712E` — **her koşuda aynı**
**Model / kapı / kalibrasyon / sözleşme:** DEĞİŞTİRİLMEDİ

---

# `SHADOW_DEPLOYMENT_NOT_READY`

**Neden:** yazılım tarafı hazır ve 7/7 test gerçek ortamda geçti; ancak **üzerinde çalışacağı
sunucu henüz yok**. Dağıtım yapılmadı, servis kurulmadı, sırlar hâlâ depoda düz metin.
Kalan maddelerin tamamı **kullanıcı kararı / erişimi** gerektirir; kod tarafında engel yoktur.

---

## 1. TEST ORTAMI

Testler **gerçek yayın çıktısı** (`dotnet publish -c Release`) üzerinde, **Production profiliyle**,
**gerçek SQL Server** ve **gerçek `FormaxDB` verisiyle** koşturuldu — geliştirme sunucusu değil.

| | |
|---|---|
| Konak | `Formax.API.exe` (publish çıktısı), `ASPNETCORE_ENVIRONMENT=Production` |
| Klasör düzeni | hedef sunucudaki düzenin aynısı: `…\FORMAX\app` + bir üstte motor dosyaları |
| `Predictions:EngineRoot` | **boş** — kod ContentRoot'un üstünü buldu (dağıtım davranışı doğrulandı) |
| Veritabanı | `localhost\SQLEXPRESS` / `FormaxDB` (80 mevcut gölge satırı) |
| Cycle aralığı | test için 2 dk / 1 dk (üretimde 6 saat) — **model ayarı değildir** |
| Dış dünya | api-football `BaseUrl` `127.0.0.1:9`'a çevrildi → **test hiçbir kota harcamadı** |

---

## 2. SONUÇLAR

| # | Test | Sonuç | Kanıt |
|---|---|---|---|
| 1 | server start | **PASS** | `GET /health` → `{"status":"ok"}`; `/admin/shadow/health` → `RUNNING` |
| 2 | prediction cycle | **PASS** | 4 ardışık cycle: 14:23:53 · 14:25:55 · 14:27:56 · 14:28:51 — döngü kendini tekrar etti |
| 3 | server restart | **PASS** | Kapanış 14:28:20 → yeniden başlatma 14:28:43 → cycle 14:28:51 (2.953 ms) |
| 4 | duplicate prevention | **PASS** | 6 cycle boyunca `inserted 0 / already published 68`; DB: **80 satır, 80 benzersiz id**, `MAX(CreatedAt)` hiç değişmedi |
| 5 | DB connection failure/recovery | **PASS** | DB OFFLINE 14:32:38 → cycle failed 14:33:19 & 14:34:19, `status=DB_UNREACHABLE`; DB ONLINE 14:35:02 → cycle done 14:35:19, `consecutiveFailures=0` — **müdahalesiz kurtarma** |
| 6 | graceful shutdown | **PASS** | `[SHADOW] Job stopped gracefully.` + `[SETTLEMENT] Job stopped gracefully before its first cycle.` (14:28:20) |
| 7 | automatic restart | **PASS (süreç düzeyi)** | Süreç 14:36:18'de **zorla öldürüldü** → 14:36:23 yeniden başladı → 14:36:33 cycle tamam; DB hâlâ 80/80 |

### Ek doğrulamalar

| Kontrol | Sonuç |
|---|---|
| Veri bütünlüğü (`integrity`, canlı DB) | **13/13 PASS** — ContentHash ve PredictionId satırdan yeniden hesaplandı, 0 uyumsuzluk |
| Model parmak izi | 6 koşuda da `94FC079D00190B341B2AEB6F0A54712E` |
| Kanıt kesimi | `EvidenceCutoff` ihlali **0** |
| Log'da sır | API anahtarı / JWT anahtarı / bağlantı dizesi / `Password=` → **0 eşleşme** |
| Liveness DB kesintisinde | `GET /health` → **200** (bağımlılığı yok) |
| Diğer işlerin hatası gölgeyi düşürdü mü | **hayır** — api-football tamamen erişilemezken gölge cycle'ları sorunsuz koştu |

---

## 3. §7 LOG ALANLARI — GERÇEK ÇIKTI

```
2026-08-22 14:28:51.315Z [INF] …ShadowPredictionJob — [SHADOW] cycle done in 2953 ms —
  seen 68, predicted 68 (accepted 68/rejected 0), inserted 0, already published 68,
  conflicted 0, failed 0, evidence through 2026-08-21

2026-08-22 14:33:19.935Z [ERR] …ShadowPredictionJob — [SHADOW] cycle failed — retry in 00:00:59.
```

**cycle · inserted · already published · conflicted · errors** — hepsi var, dosyada kalıcı.

---

## 4. §6 HEALTH CHECK — GERÇEK ÇIKTI

Sağlıklı:

```json
{"status":"RUNNING",
 "worker":{"shadowJobStartedUtc":"2026-08-22T14:23:44.866Z",
           "lastSuccessfulCycleUtc":"2026-08-22T14:25:55.503Z",
           "minutesSinceLastSuccessfulCycle":1.2,
           "cyclesCompleted":2,"cyclesFailed":0,"consecutiveFailures":0,
           "lastError":null,
           "modelFingerprint":"94FC079D00190B341B2AEB6F0A54712E"},
 "processCounters":{"inserted":0,"alreadyPublished":68,"conflicted":0,"failedMatches":0},
 "predictions":{"total":80,"shadow":80,"distinctMatches":76,"settled":0,
                "lastWriteUtc":"2026-08-22T09:40:36.689Z"}}
```

Veritabanı düşükken:

```json
{"status":"DB_UNREACHABLE","databaseReachable":false,
 "databaseError":"InvalidOperationException: An exception has been raised that is likely due to a transient failure…",
 "worker":{"cyclesFailed":2,"consecutiveFailures":2,"lastError":"InvalidOperationException: …"}}
```

> Bu davranış **bu testte bulunup düzeltildi**: uç ilk hâlinde DB sayaçlarını koşulsuz okuyordu,
> yani veritabanı düştüğünde 500 dönüyordu — arızayı görmesi gereken kişiden arıza bilgisini
> saklıyordu. Artık sayaçlar boş gelir, `lastError` okunabilir kalır.

---

## 5. KALAN ENGELLER (yalnız bunlar)

| # | Engel | Kimde |
|---|---|---|
| 1 | **Sunucu yok.** Windows VPS/sunucu temin edilmedi. | Kullanıcı kararı |
| 2 | **Servis kurulmadı.** `deploy\install-shadow-service.ps1` yazıldı ve söz dizimi doğrulandı, ama **Yönetici hakkı** gerektirdiği için bu makinede çalıştırılmadı. SCM'in yeniden başlatma politikası (`sc qfailure`) gerçek sunucuda doğrulanmalı. | Kullanıcı / sunucu erişimi |
| 3 | **Veritabanı taşınmadı.** `FormaxDB` (400 MB) sunucuya yedek/geri yükleme ile taşınmalı. | Kullanıcı |
| 4 | **Sırlar hâlâ depoda düz metin.** `appsettings.json` içinde `Jwt:Key` ve `ApiFootball:ApiKey`. Kurulum betiği bunları ortam değişkeniyle geçersiz kılar, ama **anahtarların döndürülmesi ayrı bir iştir** ve bu görevde yapılmadı. | Kullanıcı kararı |

Bunların dışında test edilmemiş hiçbir madde kalmadı.

---

## 6. AYRI OLARAK KAYDEDİLEN RİSKLER (READY engeli değil)

1. **`EnableRetryOnFailure` yok.** EF geçici SQL hatalarını yeniden denemiyor. Testte
   görüldüğü gibi cycle hata alır ve **bir sonraki cycle'a** kadar bekler — üretimde bu
   **6 saat** demektir. Kalıcı veri kaybı yoktur (sonraki cycle eksikleri tamamlar), ama
   tazelik kaybı olur. Düzeltmesi tek satırdır; **uygulamanın tamamını** etkilediği için
   bilinçli bir karar olmadan yapılmadı.
2. **Cycle'da tek `SaveChanges`.** Tek bir çakışma, o cycle'ın **tüm** yeni satırlarını geri
   alır (`conflicted` olarak raporlanır). Bir sonraki cycle kendini onarır.
3. **Dağıtık kilit yok.** İki örnek aynı işi tekrar yapar (CPU israfı); benzersiz
   `PredictionId` sayesinde veriyi çoğaltamaz. (Önceki fazda ölçüldü: `NOT_SUPPORTED_BUT_SAFE`.)
4. **SQL Express 10 GB sınırı.** DB bugün 400 MB; `Matches` 107 bin satır ve büyüyor.
5. **`/admin/*` uçları kimlik doğrulaması istemiyor** (mevcut proje deseni). Uygulama yalnız
   `localhost`'a bağlandığı için bu yüzey dışarı kapalıdır; dışarı açılacaksa kimlik
   doğrulama **eklenmelidir**.
6. **Önceki fazın GO/NO-GO engelleri açık:** en az 1 gerçek maç haftası gözlemi ve en az 1
   gerçek settlement hâlâ yaşanmadı (`settled = 0`). Bunlar dağıtımın değil, **gölge modun
   üretim onayının** koşuludur — ve zaten sürekli çalışma sağlanmadan karşılanamazlar.

---

## 7. KULLANICIYA TAHMİN AÇILDI MI?

**Hayır.** Yazılan 80 satırın tamamı `ShadowMode = 1`. Kullanıcıya açılan hiçbir uç
`Predictions` / `PredictionSettlements` tablolarını okumaz. `Predictions:ShadowMode:Enabled`
yalnız **iki arka plan işini** başlatır; hiçbir DTO, hiçbir controller, hiçbir UI yolu
eklenmedi.
