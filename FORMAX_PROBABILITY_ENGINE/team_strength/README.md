# FORMAX — DYNAMIC TEAM STRENGTH ENGINE

**Tarih:** 2026-08-20
**Durum:** Çalışır durumda. Build ✅ · 16/16 test ✅ · 33.901 maç işlendi · 67.802 snapshot üretildi.

**Bu bir olasılık modeli DEĞİLDİR.** Dixon-Coles, Bivariate Poisson, Elo'nun nihai modeli,
calibration, ensemble, prediction ve Gemma **yapılmadı**. Bu motor yalnızca, sonraki fazın
girdisi olacak **leakage-safe takım gücü snapshot'ları** üretir.

---

## 1. NE ÜRETİR

Her maç için **iki snapshot** (ev + deplasman), maçtan **önceki** duruma göre:

| Alan | Anlam |
|---|---|
| `TeamId` / `TeamName` | Canonical kimlik (`FMXT…`) |
| `MatchDate` | Snapshot'ın geçerli olduğu maçın tarihi |
| `AttackStrength` | Çarpımsal atak indeksi. 1,0 = havuz ortalaması, yüksek = daha çok gol atar |
| `DefenseStrength` | Çarpımsal **gol yeme** indeksi. 1,0 = ortalama, **düşük = daha iyi savunma** |
| `OverallStrength` | `Attack / Defense`. Yüksek = güçlü |
| `HomeStrength` | Yalnız **ev** maçlarından kurulan indeks. Ev maçı yoksa `null` |
| `AwayStrength` | Yalnız **deplasman** maçlarından kurulan indeks. Deplasman maçı yoksa `null` |
| `MatchesUsed` | Snapshot'ı besleyen önceki maç sayısı (ham) |
| `EffectiveMatches` | Zaman ağırlıklı (decay uygulanmış) maç sayısı |
| `Confidence` | `None / Low / MediumLow / Medium / High` — **decay'li** kanıta göre |
| `ColdStartClass` | `NoHistory / Limited / Developing / Established / Rich` — **ham** maç sayısına göre |
| `PriorSource` | Shrinkage'ın hangi havuza çektiği (**her satırda dolu**) |
| `PriorWeight` | Havuzun ağırlığı (0 = tamamen kendi kanıtı, 1 = tamamen prior) |
| `LastMatchDate` | Snapshot'ı besleyen **en son** maçın tarihi — daima maç tarihinden küçük |

Çıktılar: `output/team_strength_snapshots.csv` (67.802 satır) ve
`output/team_strength_latest.csv` (takım başına son snapshot, 709 satır).

---

## 2. LEAKAGE SÖZLEŞMESİ

```
maçları tarihe göre sırala
her GÜN için:
    o günün TÜM maçları için snapshot yaz        <- yalnız önceki günlerin bilgisi
    SONRA o günün tüm sonuçlarını state'e uygula
```

* Bir maçın kendi sonucu, o maçın snapshot'ına **asla** girmez.
* **Aynı gün** oynanan maçlar birbirini beslemez (kesin `<` kuralı, `<=` değil).
* Snapshot yazma ve state güncelleme **ayrı adımlardır** ve sırası testle korunur.

Gerçek veri üzerinde ölçülen self-audit:
`kendi maç tarihinden büyük/eşit bir maçı kullanan snapshot = 0`.

---

## 3. RATING NASIL ÇALIŞIR

Çarpımsal, çevrimiçi (online) bir güncelleme. Her maçtan sonra:

```
beklenenEv  = baselineEv(compType)  * Atak[Ev]  * Savunma[Dep]
beklenenDep = baselineDep(compType) * Atak[Dep] * Savunma[Ev]

rEv  = (atılanEv  + s) / (beklenenEv  + s)          # s = ratioSmoothing
rDep = (atılanDep + s) / (beklenenDep + s)

Atak[Ev]     *= rEv^η        Savunma[Dep] *= rEv^η        # η = learningRate
Atak[Dep]    *= rDep^η       Savunma[Ev]  *= rDep^η
```

* `baseline*` değerleri **veriden öğrenilir** (CompetitionType başına, genişleyen pencere).
  Yalnızca ilgili tipte `minBaselineSamples` maç görülene kadar config'teki seed kullanılır ve
  bu durum snapshot'ın `PriorSource` alanına yansır.
* Saha avantajı ayrı bir katsayı değildir: `baselineEv` ve `baselineDep` farkı onu zaten taşır ve
  **CompetitionType başına ayrı öğrenilir**.
* İndeksler `[minIndex, maxIndex]` aralığına kırpılır; tek bir uç sonuç rating'i patlatamaz.

**Zaman:** bir takım oynamadığı her gün için state'i `0.5^(gün/halfLifeDays)` ile yaşlandırılır —
hem kanıt miktarı (`EffectiveMatches`) azalır hem de indeks nötr 1,0'a doğru sürüklenir.

**Ev/deplasman:** ana rating'in yanında yalnız ev maçlarından `HomeAtk/HomeDef`, yalnız deplasman
maçlarından `AwayAtk/AwayDef` tutulur; bunlar ayrı (daha küçük) shrinkage sabitiyle raporlanır.

---

## 4. ORTAK RATING — competition-local DEĞİL

Takım başına **tek** rating akışı vardır ve **tüm FORMAX müsabakalarından** beslenir.
Sebep ölçülmüştür: UEFA eleme maçlarında takımların kendi turnuvasında ≥5 geçmiş maça sahip olma
oranı yalnız **%27,06**'dır. Competition-local rating bu maçların çoğunda tanımsız kalırdı.

CompetitionType **bağlam** olarak taşınır: baseline'lar ve shrinkage havuzları tip başına ayrıdır,
rating değildir. Bir takımın Süper Lig'de öğrendiği güç, UEFA elemesinde de görünür
(test: *cross competition*).

Terfi/küme düşmede rating **sıfırlanmaz** (test: *promotion*); seviye farkının ayrı bir
`competition_offset` ile modellenmesi sonraki fazın işidir.

---

## 5. COLD START

`Services/ColdStartPolicy.cs` — dört durum:

| Durum | Davranış |
|---|---|
| **Zengin geçmiş** | Kendi kanıtı baskın (`PriorWeight` küçük) |
| **Az geçmiş** | Kısmi havuzlama: `w = n_eff / (n_eff + k)` |
| **Yeni takım** | Prior + eldeki kanıt |
| **Hiç geçmiş yok** | Snapshot **prior'ın kendisidir**; `PriorWeight = 1` ve `PriorSource` hangi havuz olduğunu yazar |

Havuz, o CompetitionType'ın **geçmiş maçlardan öğrenilmiş** ortalamasıdır (leakage-safe). Havuz
henüz yeterince gözlem görmediyse nötr 1,0 kullanılır ve satır `GLOBAL_NEUTRAL` der.
**Hiçbir takım için rating uydurulmaz** — kanıt yoksa değer prior'dır ve bunu açıkça söyler.

Gerçek veride prior dağılımı: `POOL:DOMESTIC_LEAGUE` 53.638 · `POOL:UEFA_MAIN` 7.094 ·
`POOL:UEFA_QUALIFIER` 5.566 · `POOL:UEFA_QUALIFICATION_PLAYOFF` 1.146 · `GLOBAL_NEUTRAL` 358.

---

## 6. CONFIG

Tüm ayarlar `teamstrength.config.json` içindedir; motorda **hard-code edilmiş katsayı yoktur**.

| Anahtar | Varsayılan | Ne yapar |
|---|---|---|
| `halfLifeDays` | 365 | Zaman ağırlığı yarı ömrü |
| `learningRate` | 0,08 | Güncelleme adım boyu |
| `shrinkageK` | 6 | Kısmi havuzlama sabiti |
| `venueShrinkageK` | 4 | Ev/deplasman indeksleri için aynısı |
| `ratioSmoothing` | 0,60 | Sürpriz oranındaki toplamsal yumuşatma |
| `minIndex` / `maxIndex` | 0,25 / 4,0 | İndeks kırpma |
| `limited/developing/established/richThreshold` | 1/3/5/10 | Etiket eşikleri |
| `seedBaseline*`, `minBaselineSamples` | 1,5 / 1,2 / 50 | Baseline öğrenilene kadarki tohum |
| `useCompetitionTypePool` | true | Cold-start havuzu açık/kapalı |
| `requiredIdentityConfidence` | `CONFIRMED` | Kimliği doğrulanmamış satır motora girmez |
| `acceptedMatchStatuses` | FT, AET, PEN | Hükmen/oynanmamış maç kanıt sayılmaz |

> **Hiçbiri doğrulanmadı.** Bu değerler motorun çalışması için makul başlangıçlardır; seçimleri
> walk-forward doğrulamayla yapılacaktır (sonraki faz).

---

## 7. ÇALIŞTIRMA

```bash
cd FORMAX_PROBABILITY_ENGINE/team_strength/FormaxTeamStrength
dotnet build
dotnet run -- test      # 16 test
dotnet run -- build     # 33.901 maç -> 67.802 snapshot
```

Seçenekler: `--dataset <csv> --config <json> --out <dir>`.

Proje **Formax.slnx'e eklenmemiştir**; üretim derlemesi etkilenmez. NuGet bağımlılığı yoktur
(yalnız BCL), bu yüzden çevrimdışı derlenir ve test edilir.

---

## 8. TESTLER (16/16 PASS)

| Test | Ne kanıtlar |
|---|---|
| snapshot = yalnız önceki maçlardan kurulan state | Sızıntı yok (tam eşitlik, 1e-12) |
| sonraki maçın sonucu değişince önceki snapshot değişmiyor | Gelecek sızmıyor |
| aynı gün maçları birbirini beslemiyor | Kesin `<` kuralı |
| ilk maç → NoHistory, PriorWeight = 1 | Cold-start doğru |
| 3 maçlık takım → Developing, shrinkage güçlü | Kısmi havuzlama |
| 10+ maçlık takım → Rich, kendi kanıtı baskın | Kanıt ağırlığı doğru |
| UEFA-only takım → `POOL:UEFA_QUALIFIER` | Havuz seçimi doğru ve kaydediliyor |
| yerel ligdeki seri UEFA maçında görünüyor | Ortak rating akışı |
| terfi eden takımın rating'i sıfırlanmıyor | Kimlik sürekliliği |
| evde güçlü / deplasmanda zayıf takım ayrışıyor | Ev/deplasman ayrımı |
| oynanmayan mekân indeksi `null` | Uydurma yok |
| iki koşu birebir aynı çıktı | Determinizm |
| 4 yıllık aradan sonra kanıt azalıyor | Zaman decay |
| kimlik kapısı + CSV parser | Veri hijyeni |
| **gerçek veri:** 33.901 maç, NaN/∞/negatif yok, her satırda PriorSource | Üretim taraması |
| **gerçek veri:** hiçbir snapshot kendi tarihinden sonraki maçı kullanmıyor | Sızıntı taraması |

---

## 9. GERÇEK VERİ SONUCU

```
matches into engine : 33901      teams processed : 709
snapshots produced  : 67802      elapsed         : 132 ms
cold start : Rich 61.969 (%91,40) · Established 2.623 (%3,87) · Limited 1.315 (%1,94)
             Developing 1.186 (%1,75) · NoHistory 709 (%1,05)
confidence : High 58.680 (%86,55) · Medium 3.809 · MediumLow 1.815 · Low 1.920 · None 1.578
self-audit : leakage ihlali 0 · prior'ı olmayan satır 0 · geçersiz indeks 0
```

**Yüz geçerliliği** (son snapshot'a göre en güçlüler): Paris Saint-Germain 2,87 · Bayern München 2,87 ·
Arsenal 2,86 · Manchester City 2,53 · Barcelona 2,42 · Inter 2,41 · Roma 2,15 · Real Madrid 2,07.
En zayıflar: Karabükspor 0,36 · Bolton 0,39 · Dijon 0,41 · Rotherham 0,41.

> Bu yalnız **yüz geçerliliğidir** — modelin doğruluğu değildir. Rating'in tahmin gücü henüz
> ölçülmedi; ölçüm walk-forward backtest ile sonraki fazda yapılacaktır.

---

## 10. BİLİNEN SINIRLAR

1. **Katsayılar doğrulanmadı.** halfLife, learningRate, shrinkageK vb. backtest ile seçilmedi.
2. **Kapsam dışı maçlar görünmez.** Yerel kupalar, milli takım araları ve hazırlık maçları veri
   setinde yok; bir takımın gerçek maç yükü ve dinlenmesi eksik görünür.
3. **UEFA-only kulüpler.** 709 takımın 468'i yalnız UEFA'da görünüyor (medyan 10 maç). Bu
   kulüplerin ratingi büyük ölçüde havuz priorına dayanır; `PriorWeight` ve `ColdStartClass`
   bunu her satırda açıkça gösterir.
4. **Seviye farkı henüz modellenmedi.** Championship→Premier League geçişinde rating korunur ama
   lig seviyesi düzeltmesi (`competition_offset`) uygulanmaz — sonraki fazın işi.
5. **İki ayaklı eşleşme bağlamı bu motorda yok.** `tie_leg`/`aggregate` feature katmanına aittir.
6. **Maç istatistikleri kullanılmıyor.** Çekirdek rating yalnız gollerden beslenir; şut/korner
   UEFA'da %0 kapsamda olduğu için bilerek dışarıda tutuldu.

---

## 11. DOSYA YAPISI

```
team_strength/
├── FormaxTeamStrength/
│   ├── FormaxTeamStrength.csproj      # net8.0, NuGet bağımlılığı yok
│   ├── Program.cs                     # CLI: build | test
│   ├── Config/TeamStrengthConfig.cs
│   ├── Models/MatchRecord.cs
│   ├── Models/TeamStrengthSnapshot.cs
│   ├── Services/MatchCsvReader.cs     # salt-okunur CSV okuyucu
│   ├── Services/TeamStrengthService.cs
│   ├── Services/ColdStartPolicy.cs
│   ├── Tests/TestRunner.cs
│   └── Tests/TeamStrengthTests.cs
├── teamstrength.config.json
├── output/team_strength_snapshots.csv
├── output/team_strength_latest.csv
└── README.md
```

**Değiştirilmeyenler:** `FORMAX_HISTORICAL_MASTER/*` (salt okundu), ham kaynaklar, üretim DB'si,
`Formax.slnx`, frontend, AI Maç Analizi, Canlı Takip, video sistemi.
