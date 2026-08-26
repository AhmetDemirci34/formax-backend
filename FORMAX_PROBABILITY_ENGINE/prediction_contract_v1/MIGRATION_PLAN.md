# FORMAX — PREDICTION CONTRACT: PRODUCTION DB MIGRATION **PLAN**

> **Bu bir plandır. Uygulanmadı.** Görev §17 gereği yalnız hazırlandı: bu fazda production DB'ye
> yazılmadı, EF migration üretilmedi, `FormaxDbContext` değiştirilmedi, mevcut prediction sistemine
> bağlanmadı. Kod preview/araştırma modunda.

**Hedef sağlayıcı:** SQL Server (`UseSqlServer`, `Formax.Infrastructure/Data/FormaxDbContextFactory.cs`)
**Uygulanacak yer:** `Formax.Infrastructure/Migrations/` (EF Core), mevcut adlandırma düzeniyle
`{yyyyMMddHHmmss}_PredictionContract_V1.cs`

---

## 1. NEDEN İKİ TABLO

Sözleşmenin çekirdek kuralı: **yayımlanmış bir tahmin değişmez** (§10) ve **settlement tahmini
değiştirmez** (§11).

Bunu tek tabloda `ActualResult` sütunu ekleyerek yapmak, kuralı bir konvansiyona indirger — herhangi
bir `UPDATE` olasılığı geri gelir. İki tablo, kuralı **şemanın kendisine** taşır:

| Tablo | Yazma modeli | İçerik |
|---|---|---|
| `Predictions` | **INSERT-only** | Tahminin kendisi. Hiçbir `UPDATE` yolu yok. |
| `PredictionSettlements` | INSERT-only, 1:1 | Maç sonucu. `PredictionId`'ye PK ile bağlı. |

Böylece "olasılık sonradan değişti mi?" sorusu bir denetim işi olmaktan çıkıp **imkânsız** hâle
gelir: `Predictions` üzerinde `UPDATE` yetkisi verilmez.

---

## 2. TABLO: `Predictions`

```sql
CREATE TABLE dbo.Predictions
(
    Sequence              BIGINT IDENTITY(1,1) NOT NULL,
    PredictionId          CHAR(24)        NOT NULL,   -- FMXP + 20 hex
    MatchId               VARCHAR(32)     NOT NULL,   -- FORMAX_MATCH_ID
    MatchDate             DATE            NOT NULL,
    PredictionTimestamp   DATETIME2(0)    NOT NULL,   -- UTC
    EvidenceCutoff        DATE            NULL,       -- NULL = hiç kanıt yoktu

    ModelVersion          VARCHAR(64)     NOT NULL,
    TeamStrengthVersion   VARCHAR(64)     NOT NULL,
    GateVersion           VARCHAR(64)     NOT NULL,
    CalibrationVersion    VARCHAR(64)     NOT NULL,

    HomeProbability       FLOAT           NULL,       -- REJECTED ise NULL
    DrawProbability       FLOAT           NULL,
    AwayProbability       FLOAT           NULL,

    PredictionEligible    BIT             NOT NULL,
    ConfidenceClass       VARCHAR(16)     NOT NULL,   -- NONE|LOW|MEDIUMLOW|MEDIUM|HIGH
    GateStatus            VARCHAR(16)     NOT NULL,   -- ACCEPTED|REJECTED
    GateReason            VARCHAR(256)    NOT NULL,   -- 'OK' veya kod|kod
    ContentHash           CHAR(32)        NOT NULL,   -- yayım anındaki parmak izi

    CreatedAtUtc          DATETIME2(3)    NOT NULL CONSTRAINT DF_Predictions_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Predictions PRIMARY KEY CLUSTERED (Sequence),
    CONSTRAINT UQ_Predictions_PredictionId UNIQUE (PredictionId)
);
```

### Sütun notları

* **`PredictionId`** — `SHA256(MatchId | 4 version | EvidenceCutoff | PredictionTimestamp)`'in ilk
  20 hex'i, `FMXP` önekiyle. Deterministik: aynı girdiler aynı id'yi verir, bu yüzden yeniden yayım
  **idempotent**tir. Yeni kanıt → yeni cutoff → **yeni id**, yani düzeltme değil yeni tahmin (§10).
* **`Sequence`** — yayım sırası. "Hangi tahmin güncel?" sorusunun tek doğru cevabı bu; aynı maçın
  iki tahmini aynı `PredictionTimestamp`'i taşıyabilir (replay'de ikisi de maç günü). Uygulama
  katmanında bu ayrım bir hata olarak yakalandı ve düzeltildi; şema aynı ayrımı taşıyor.
* **`FLOAT`** (IEEE 754 double) — `DECIMAL` **değil**. Olasılık ondalıktır ve tam çift duyarlıkla
  saklanmalıdır; `DECIMAL(5,4)` sessizce yuvarlar ve §2'nin yasakladığı şeyi şema düzeyinde yapar.
* **`ContentHash`** — yayım anında hesaplanır. "Bu satır yayımlandığı gibi mi?" sorusu, satırdan
  yeniden hesaplayıp karşılaştırarak cevaplanır.

### Kısıtlar — kural yorum değil, `CHECK`

```sql
ALTER TABLE dbo.Predictions ADD CONSTRAINT CK_Predictions_Simplex CHECK (
    (PredictionEligible = 0
        AND HomeProbability IS NULL AND DrawProbability IS NULL AND AwayProbability IS NULL
        AND GateStatus = 'REJECTED')
 OR (PredictionEligible = 1
        AND HomeProbability IS NOT NULL AND DrawProbability IS NOT NULL AND AwayProbability IS NOT NULL
        AND HomeProbability BETWEEN 0 AND 1
        AND DrawProbability BETWEEN 0 AND 1
        AND AwayProbability BETWEEN 0 AND 1
        AND ABS(HomeProbability + DrawProbability + AwayProbability - 1.0) <= 1e-12
        AND GateStatus = 'ACCEPTED'
        AND GateReason = 'OK')
);

-- kanıt maçtan kesinlikle önce olmalı
ALTER TABLE dbo.Predictions ADD CONSTRAINT CK_Predictions_EvidencePreMatch CHECK (
    EvidenceCutoff IS NULL OR EvidenceCutoff < MatchDate
);

-- tahmin kendi kanıtından önce damgalanamaz
ALTER TABLE dbo.Predictions ADD CONSTRAINT CK_Predictions_TimestampAfterEvidence CHECK (
    EvidenceCutoff IS NULL OR EvidenceCutoff <= CAST(PredictionTimestamp AS DATE)
);

ALTER TABLE dbo.Predictions ADD CONSTRAINT CK_Predictions_Confidence CHECK (
    ConfidenceClass IN ('NONE','LOW','MEDIUMLOW','MEDIUM','HIGH')
    -- yayımlanmış bir tahmin "hiç kanıt yok" diyemez
    AND (PredictionEligible = 0 OR ConfidenceClass <> 'NONE')
);
```

> Bu `CHECK`'lerin dördü de uygulama katmanında zaten test ediliyor. Şemaya konmalarının nedeni,
> gelecekte bir toplu betiğin veya elle bir `INSERT`'in kuralı atlayamaması. Uygulama kodu
> değiştirilebilir; `CHECK` kısıtı değiştirmek migration ister ve gözden geçirilir.

### İndeksler

```sql
CREATE INDEX IX_Predictions_Match_Sequence ON dbo.Predictions (MatchId, Sequence DESC);
CREATE INDEX IX_Predictions_MatchDate      ON dbo.Predictions (MatchDate) INCLUDE (MatchId, PredictionEligible);
CREATE INDEX IX_Predictions_Versions       ON dbo.Predictions (ModelVersion, TeamStrengthVersion, GateVersion, CalibrationVersion);
```

`IX_Predictions_Match_Sequence` "bu maçın güncel tahmini" sorgusunun tamamını karşılar:

```sql
SELECT TOP 1 * FROM dbo.Predictions WHERE MatchId = @matchId ORDER BY Sequence DESC;
```

---

## 3. TABLO: `PredictionSettlements`

```sql
CREATE TABLE dbo.PredictionSettlements
(
    PredictionId         CHAR(24)     NOT NULL,
    ActualHomeGoals      INT          NOT NULL,
    ActualAwayGoals      INT          NOT NULL,
    ActualResult         VARCHAR(8)   NOT NULL,   -- HomeWin|Draw|AwayWin
    SettlementTimestamp  DATETIME2(0) NOT NULL,

    CONSTRAINT PK_PredictionSettlements PRIMARY KEY CLUSTERED (PredictionId),
    CONSTRAINT FK_PredictionSettlements_Predictions
        FOREIGN KEY (PredictionId) REFERENCES dbo.Predictions (PredictionId),
    CONSTRAINT CK_PredictionSettlements_Result CHECK (
        ActualResult = CASE
            WHEN ActualHomeGoals > ActualAwayGoals THEN 'HomeWin'
            WHEN ActualHomeGoals = ActualAwayGoals THEN 'Draw'
            ELSE 'AwayWin' END
    ),
    CONSTRAINT CK_PredictionSettlements_Goals CHECK (ActualHomeGoals >= 0 AND ActualAwayGoals >= 0)
);
```

**PK = `PredictionId`** olması, "bir tahmin iki kez settle edilemez" kuralını şema düzeyinde
uygular — ikinci `INSERT` PK ihlali verir. Uygulama katmanındaki `AlreadySettled` sayacı bunun
sessiz karşılığıdır.

`ActualResult` sütunu türetilebilir olduğu hâlde saklanıyor çünkü sorgular onu filtreliyor;
`CHECK` kısıtı gollerle tutarsız kalmasını engelliyor.

---

## 4. YETKİLER — kuralı roller taşır

```sql
-- prediction yazan servis: yalnız ekleyebilir
GRANT SELECT, INSERT ON dbo.Predictions            TO formax_prediction_writer;
GRANT SELECT, INSERT ON dbo.PredictionSettlements  TO formax_prediction_writer;
DENY  UPDATE, DELETE  ON dbo.Predictions           TO formax_prediction_writer;
DENY  UPDATE, DELETE  ON dbo.PredictionSettlements TO formax_prediction_writer;

-- okuyan her şey (API, rapor, Gemma girdisi)
GRANT SELECT ON dbo.Predictions           TO formax_reader;
GRANT SELECT ON dbo.PredictionSettlements TO formax_reader;
```

`DENY UPDATE ON dbo.Predictions` — §10'un tek satırlık uygulaması. Uygulama katmanı hata yapsa bile
veritabanı yazmayı reddeder.

---

## 5. GERİYE DÖNÜK UYUM

**Mevcut tabloların hiçbirine dokunulmaz.** `Matches`, `MatchMarketOdds`, `TeamPlayerIntelligence`,
`MatchNewsArticles` ve diğerleri değişmez; `Predictions` bunlara `MatchId` ile bakar ama **FK
konmaz** — tahmin, henüz `Matches`'e girmemiş bir fikstür için de üretilebilmeli.

Mevcut prediction uçları çalışmaya devam eder. Bu tablolar **paralel** yazılır; kesme (cut-over)
ayrı bir karardır ve bu planın kapsamında değildir.

---

## 6. UYGULAMA SIRASI

| # | Adım | Geri alınabilir mi |
|---|---|---|
| 1 | `Predictions` + `PredictionSettlements` oluştur, `CHECK` ve indeksler dâhil | evet (`DROP TABLE`) |
| 2 | Rolleri ve `DENY`'leri uygula | evet |
| 3 | `FormaxDbContext`'e iki `DbSet` ekle, **yalnız okuma + ekleme** olacak şekilde yapılandır | evet |
| 4 | Yazıcıyı gölge modda çalıştır: tahminler yazılır, hiçbir uç okumaz | evet |
| 5 | Gölge modda ≥1 tam maç haftası: `ContentHash` doğrulaması ve settlement kapanışı ölç | — |
| 6 | Okuma ucunu bağla | ayrı karar, bu planın dışında |

4. ve 5. adım bilerek ayrı: sözleşmenin canlı davranışı (idempotent yeniden yayım, settlement
kapanma oranı, red oranı) **üretimde ölçülmeden** hiçbir uç bu tablolara bağlanmamalı.

---

## 7. DOĞRULAMA SORGULARI

Migration'dan sonra bunlar **her zaman 0 satır** döndürmeli:

```sql
-- simplex ihlali (CHECK zaten engelliyor; bu, kısıtın kurulduğunun kanıtı)
SELECT COUNT(*) FROM dbo.Predictions
WHERE PredictionEligible = 1
  AND ABS(HomeProbability + DrawProbability + AwayProbability - 1.0) > 1e-12;

-- reddedilmiş ama sayı taşıyan
SELECT COUNT(*) FROM dbo.Predictions
WHERE PredictionEligible = 0 AND HomeProbability IS NOT NULL;

-- kanıt sızıntısı
SELECT COUNT(*) FROM dbo.Predictions WHERE EvidenceCutoff >= MatchDate;

-- iki kez settle (PK zaten engelliyor)
SELECT PredictionId FROM dbo.PredictionSettlements GROUP BY PredictionId HAVING COUNT(*) > 1;

-- settlement'ı olan ama tahmini olmayan (FK zaten engelliyor)
SELECT s.PredictionId FROM dbo.PredictionSettlements s
LEFT JOIN dbo.Predictions p ON p.PredictionId = s.PredictionId
WHERE p.PredictionId IS NULL;
```

Ve performans metriği (§12) tek sorguyla:

```sql
SELECT p.ModelVersion, p.TeamStrengthVersion, p.GateVersion, p.CalibrationVersion,
       COUNT(*) AS N,
       AVG(-LOG(CASE s.ActualResult
                    WHEN 'HomeWin' THEN p.HomeProbability
                    WHEN 'Draw'    THEN p.DrawProbability
                    ELSE                p.AwayProbability END)) AS LogLoss
FROM dbo.Predictions p
JOIN dbo.PredictionSettlements s ON s.PredictionId = p.PredictionId
WHERE p.PredictionEligible = 1
GROUP BY p.ModelVersion, p.TeamStrengthVersion, p.GateVersion, p.CalibrationVersion;
```

Version alanlarına göre gruplama, model değiştiğinde eski ve yeni sürümün performansının
**aynı log üzerinden** karşılaştırılabilmesi demektir — §7'nin asıl amacı bu.

---

## 8. BU PLANDA OLMAYANLAR

1. **EF migration dosyası.** Yazılmadı. Yazıldığında yukarıdaki DDL'i birebir üretmeli.
2. **Kesme (cut-over) planı.** Mevcut prediction ucunun bu tablolara geçmesi ayrı bir karar.
3. **Arşivleme / bölümleme.** Sezon başına ~34.000 satır; yıllarca bölümlemeye gerek yok, ama
   `MatchDate` üzerinde bölümleme ileride tek adımda eklenebilir.
4. **Frontend sözleşmesi.** Bu tablolar API şekli değil; uç tasarımı ayrı iş.
