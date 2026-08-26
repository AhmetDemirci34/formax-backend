# FORMAX — SHADOW MODE SÜREKLİ ÇALIŞMA (DEPLOYMENT)

**Kapsam:** Gölge tahmin işinin geliştirici PC'sinden bağımsız, sürekli çalışması.
**Model:** `INDEPENDENT_POISSON_V2` / `TEAM_STRENGTH_V2` / `GATE_V1` / `CALIBRATION=NONE` — **DEĞİŞTİRİLMEDİ.**
**Prediction Contract V1:** değiştirilmedi. Şema, PredictionId türetimi, immutability aynı.
**Kullanıcıya tahmin:** **AÇILMADI.** Satırlar `ShadowMode=1` ile yazılır; kullanıcıya açılan hiçbir uç `Predictions` tablosunu okumaz.

---

## 1. MEVCUT DURUM — NEDEN PC'YE BAĞLI

| Ölçüm | Bulgu |
|---|---|
| Barındırma biçimi | `dotnet run` ile başlatılan **etkileşimli konsol süreci** |
| Windows Service | **YOK** (`Get-Service *formax*` → boş) |
| Zamanlanmış görev | **YOK** (`Get-ScheduledTask *formax*` → boş) |
| Oturum açılışında başlatma | **YOK** (`HKCU:\...\Run` içinde FORMAX kaydı yok) |
| Shadow job'ın sahibi | `ShadowPredictionJob : BackgroundService` — **API host sürecinin içinde** |
| Veritabanı | `localhost\SQLEXPRESS`, Windows kimlik doğrulama, **aynı PC** |
| Log | yalnız **konsol** — süreç kapanınca kaybolur |

**Sonuç:** gölge iş, API sürecinin bir parçasıdır; süreç ölünce iş de ölür. Süreci ayakta
tutan hiçbir servis/görev tanımı yoktur, dolayısıyla PC kapanınca **hiçbir şey** çalışmaz.

### Kanıt (ölçüldü)

`Predictions` tablosundaki son yazma `2026-08-22 09:40:36Z`. Aynı gün 14:16'da yapılan ölçümde
`MAX(CreatedAt)` hâlâ 09:40 — yani aradaki ~4,5 saatte **tek bir cycle koşmadı**; çünkü job'ın
yaşadığı süreç kapatılmıştı. Cycle aralığı 6 saat olsa bile job canlı olsaydı en az bir cycle
daha görülürdü; görülmedi. Süreç listesi de bunu doğrular: ölçüm anında makinede FORMAX'a ait
hiçbir `dotnet`/`Formax.API` süreci yoktu.

---

## 2. SEÇENEK KARŞILAŞTIRMASI VE KARAR

| Seçenek | Gereken değişiklik | Karar |
|---|---|---|
| **A. Windows VPS + Windows Service** | publish + servis kaydı. Bağlantı dizesi, dosya yolları, timezone, SQL Express **aynen** çalışır. Kodda tek ekleme: `UseWindowsService()` (konsolda no-op). | **SEÇİLDİ** |
| B. Linux VPS + Docker + SQL Server for Linux | Dockerfile + compose, SQL kimlik doğrulama değişimi, yol düzeni, imaj içine 12 MB motor verisi, yeni operasyon zinciri | Reddedildi — yeni mimari |
| C. Azure App Service + Azure SQL | Always-On, bağlantı/uyumluluk doğrulaması, maliyet, migration gözden geçirme | Reddedildi — kapsam dışı |
| D. Mevcut PC + servis + "uykuya alma" | PC kapalıyken yine çalışmaz | Hedefi karşılamıyor |

**Seçim gerekçesi:** hedef "PC kapalıyken de çalışsın" idi; bunu sağlayan en az değişiklikli
yol, aynı işletim sistemi ve aynı veritabanı motoruyla çalışan bir Windows sunucusudur. Docker
ya da bulut PaaS, çalışan mimariyi değiştirmeyi gerektirirdi.

### Sunucu boyutu

| | |
|---|---|
| İşletim sistemi | Windows Server 2019/2022 |
| vCPU | 2 (cycle 68 maçta ~4 sn; darboğaz değil) |
| RAM | 8 GB (motor replay'i 34 bin maçlık kanıt kümesini belleğe alır) |
| Disk | 80 GB SSD (DB bugün **400 MB**, motor verisi 12 MB, log 30 gün) |
| Veritabanı | SQL Server Express (10 GB sınırı; büyüme izlenmeli) veya Standard |

---

## 3. SUNUCUDAKİ KLASÖR DÜZENİ

`Predictions:EngineRoot` Production profilinde **boş** bırakılır; kod o zaman
`ContentRoot`'un bir üstünü kullanır. Bu yüzden düzen şöyle olmalıdır:

```
C:\FORMAX\
├── app\                                  ← publish çıktısı (ContentRoot)
│   ├── Formax.API.exe
│   ├── appsettings.json
│   ├── appsettings.Production.json
│   └── Logs\                             ← kalıcı log (30 gün)
├── FORMAX_HISTORICAL_MASTER\
│   ├── FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv
│   └── FORMAX_HISTORICAL_TEAMS.csv
└── FORMAX_PROBABILITY_ENGINE\
    ├── model_validation_v2\validated_teamstrength_config.json
    ├── model_validation_v2\split.config.json
    ├── dixon_coles\dixoncoles.config.json
    └── prediction_gate_v1\gate.config.json
```

Motor yükü toplam **12 MB**, altı dosya. Bunlar KİLİTLİ girdilerdir; sunucuda düzenlenmez.

> `appsettings.json` içindeki `Predictions:EngineRoot` değeri geliştirici makinesine ait
> mutlak bir yoldur (`C:/Users/dikim/...`). Production profili bunu **bilerek boşa çeker**;
> aksi hâlde sunucuda motor dosyaları bulunamaz ve job **sessizce devre dışı kalırdı**.

---

## 4. DAĞITIM ADIMLARI

### 4.1 Yayınla (geliştirici makinesinde)

```bash
dotnet publish Formax.API/Formax.API.csproj -c Release -o C:\FORMAX_publish\app
```

Sonra `FORMAX_HISTORICAL_MASTER` ve `FORMAX_PROBABILITY_ENGINE` altındaki **altı dosyayı**
yukarıdaki düzene kopyalayın ve tümünü sunucuya taşıyın.

### 4.2 Sunucuyu hazırla

1. SQL Server Express (veya Standard) kurulur.
2. Veritabanı taşınır: geliştirici PC'sinde `FormaxDB` yedeği alınır, sunucuda geri yüklenir.
   (Alternatif: boş DB — uygulama açılışta `Database.Migrate()` çalıştırır, ancak o zaman
   tarihsel `Matches` verisi olmadığı için gölge ilk günlerde az maç görür.)
3. .NET 8 **ASP.NET Core Runtime** kurulur (self-contained yayın yapılmadıysa).

### 4.3 Servisi kur (Yönetici PowerShell)

```bash
powershell -File deploy\install-shadow-service.ps1 -AppDir "C:\FORMAX\app" -ConnectionString "Server=localhost\SQLEXPRESS;Database=FormaxDB;Trusted_Connection=True;TrustServerCertificate=True" -ApiFootballKey "<anahtar>" -JwtKey "<anahtar>"
```

Betik şunları yapar:
- motor dosyalarının **varlığını doğrular** (eksikse kurulumu durdurur),
- servisi `start= auto` ile kaydeder,
- **otomatik yeniden başlatma** yapılandırır: 30 sn → 60 sn → 120 sn, sayaç 24 saatte sıfırlanır,
- sıfır olmayan çıkış kodunu da hata sayar (`failureflag 1`),
- **sırları servis ortam değişkenlerine** yazar; `appsettings*.json` içine sır konmaz.

```bash
sc.exe start FormaxShadow
```

### 4.4 Doğrula

```bash
powershell -Command "Invoke-RestMethod http://localhost:5063/admin/shadow/health | ConvertTo-Json -Depth 5"
```

`status: RUNNING` ve `worker.cyclesCompleted >= 1` bekleyin (ilk cycle, `StartupDelaySeconds`
+ cycle süresi kadar sonra gelir; varsayılan 120 sn + ~5 sn).

---

## 5. SIRLAR

`appsettings.json` bugün **düz metin sır taşıyor**: `Jwt:Key` ve `ApiFootball:ApiKey`.
Sunucuya taşınırken bunlar ortam değişkeniyle geçersiz kılınır (kurulum betiği yapar):

| Ayar | Ortam değişkeni |
|---|---|
| `ConnectionStrings:FormaxDB` | `ConnectionStrings__FormaxDB` |
| `Jwt:Key` | `Jwt__Key` |
| `ApiFootball:ApiKey` | `ApiFootball__ApiKey` |
| `Llm:ApiKey` | `Llm__ApiKey` |

Ortam değişkeni JSON'un üzerine biner. **Depodaki düz metin anahtarların döndürülmesi ayrı ve
açık bir iştir; bu görevde yapılmadı.**

Log tarafında sır sızıntısı ölçüldü: üretilen dosya logunda API anahtarı, JWT anahtarı,
bağlantı dizesi veya `Password=` geçmiyor (`grep` ile doğrulandı, 0 eşleşme). Sağlık ucundaki
`lastError` alanı yalnız istisna **tipi + mesajın ilk satırı** (300 karakterle sınırlı) taşır.

---

## 6. YENİDEN BAŞLATMA GÜVENLİĞİ

| Gereksinim | Mekanizma |
|---|---|
| Job otomatik tekrar başlasın | Servis `start= auto`; SCM recovery 30/60/120 sn |
| Duplicate üretmesin | Cycle başında `Predictions.PredictionId` kümesi belleğe alınır; var olan id **eklenmez** (`AlreadyPublished`) |
| Aynı PredictionId tekrar yazılmasın | `PredictionId` deterministiktir: `SHA256(MatchId · 4 versiyon · EvidenceCutoff · PredictionTimestamp)`; `PredictionTimestamp` maç gününün 00:00 UTC'sidir, duvar saati **değildir**. Ayrıca `PredictionId` üzerinde **benzersiz index** vardır |
| Kaldığı yerden devam etsin | Cycle içinde tek `SaveChanges` vardır: kesinti hâlinde hiçbir satır yazılmaz, sonraki cycle aynı işi baştan yapar ve eksikleri tamamlar |

**Bilinen davranış (kusur değil, sınır):** bir cycle'da tek bir satır benzersizlik ihlaline
yol açarsa EF o `SaveChanges` çağrısının **tamamını** reddeder; o cycle'ın yeni satırları
yazılmaz ve `conflicted` olarak raporlanır. Bir sonraki cycle mevcut id kümesini yeniden
okur ve kalan satırları yazar — yani en fazla bir cycle gecikmeyle kendini onarır.

---

## 7. HEALTH CHECK

| Uç | Amaç |
|---|---|
| `GET /health` | **Liveness.** Hiçbir bağımlılığa dokunmaz (DB dahil). Supervisor/uptime kontrolü için |
| `GET /admin/shadow/health` | **Gölge işin durumu + kalıcı sayaçlar** |

`/admin/shadow/health` alanları:

- `status`: `RUNNING` · `STALE` · `FAILING` · `NOT_STARTED` · `DISABLED`
- `worker.shadowJobStartedUtc` — **startup**
- `worker.lastSuccessfulCycleUtc` + `minutesSinceLastSuccessfulCycle` — **last successful cycle**
- `worker.lastError` + `lastErrorUtc` + `consecutiveFailures` — **last error**
- `predictions.total` / `shadow` / `distinctMatches` / `settled` / `lastWriteUtc` — **prediction count** (DB'den; süreç yeniden başlasa da doğru)
- `processCounters.*` — bu süreçteki `inserted` / `alreadyPublished` / `conflicted` / `failedMatches`
- `worker.modelFingerprint` — kilitli motorun parmak izi (`94FC079D00190B341B2AEB6F0A54712E`)

`STALE` eşiği: beklenen cycle aralığının 2,5 katı + 10 dakika.

> `/admin/*` uçları kimlik doğrulaması istemez (mevcut proje deseni). Sunucuda uygulama
> yalnız `localhost`'a bağlanır (`Hosting:Urls`), dolayısıyla bu yüzey dışarıya açık değildir.
> Dışarıdan erişim istenirse güvenlik duvarı ve kimlik doğrulama **bilinçli** olarak eklenmelidir.

---

## 8. LOG

Kalıcı dosya logu Production profilinde **açıktır**: `app\Logs\formax-YYYYMMDD.log`, UTC,
30 gün saklama. Konsol logu korunur (geliştirmede davranış değişmedi).

Gölge satırı örneği:

```
[SHADOW] cycle done in 4044 ms — seen 68, predicted 68 (accepted 68/rejected 0),
         inserted 0, already published 68, conflicted 0, failed 0, evidence through 2026-08-21
```

İstenen alanların tamamı var: **cycle** · **inserted** · **already published** · **conflicted**
· **errors** (`[SHADOW] cycle failed` satırı + `failed N`).

Log yazımı ayrı bir iş parçacığındadır ve kuyruk dolarsa satır düşürülür: **loglama uygulamayı
yavaşlatmaz veya düşürmez.**

---

## 9. VERİTABANI

Yeni tablo, yeni entity, yeni migration **yoktur**. Mevcut `Predictions` ve
`PredictionSettlements` tabloları kullanılır. Immutability korunur: `TR_Predictions_NoUpdate`,
`TR_Predictions_NoDelete`, benzersiz `PredictionId`, `PK_PredictionSettlements`.

---

## 10. GERİ ALMA

```bash
sc.exe stop FormaxShadow
```

Gölge durur. Yazılmış satırlar **silinmez** (append-only). Tamamen kaldırmak için
`sc.exe delete FormaxShadow`. Uygulama tarafında geri almak için
`Predictions:ShadowMode:Enabled=false` yeterlidir.
