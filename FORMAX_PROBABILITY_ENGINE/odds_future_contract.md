# FORMAX — ODDS / MARKET DATA CONTRACT

**STATUS: `FUTURE_BENCHMARK`**
**LEAKAGE_SAFE: `UNKNOWN`**
**Tarih:** 2026-08-20

Bu belge, elimizde bulunan bahis oranı verisinin envanteridir. **Hiçbir oran model feature'ı
yapılmamıştır ve bu fazda yapılmayacaktır.**

---

## 1. NE BULUNDU

`Data/Historical/Matches.csv` (football-data.co.uk) içinde oran kolonları gerçekten mevcut.
Ayrı katmana çıkarıldı:

**`FORMAX_HISTORICAL_MATCH_STATS/FORMAX_HISTORICAL_MARKET_DATA.csv` — 23.867 maç.**

| Market | Kolonlar | Katman içi doluluk |
|---|---|---|
| 1X2 | `OddHome`, `OddDraw`, `OddAway` | ~%100 |
| 1X2 en yüksek | `MaxHome`, `MaxDraw`, `MaxAway` | ~%100 |
| Over/Under 2.5 | `Over25`, `Under25`, `MaxOver25`, `MaxUnder25` | ~%100 |
| Asya handikap | `HandicapLine`, `HandicapHome`, `HandicapAway` | ~%100 |

Kapsam (model dataset'ine göre):

| Kapsam | Coverage |
|---|---|
| Tüm model-eligible maçlar | **%70,40** (23.867 / 33.901) |
| DOMESTIC_LEAGUE | **%88,90** |
| UEFA_MAIN / UEFA_QUALIFIER / UEFA_QUALIFICATION_PLAYOFF / DOMESTIC_PLAYOFF | **%0** |
| 2017/18 – 2024/25 | %74,7 – %85,2 |
| **2025/26** | **%0** |

---

## 2. NEDEN ŞİMDİLİK FEATURE DEĞİL — dört gerekçe

### 2.1 Zaman damgası yok → sızıntı durumu bilinmiyor
Kaynak dosya, oranın **hangi anda** alındığını belirtmiyor. football-data yayınları genellikle
**kapanış oranıdır** (maç başlamadan hemen önce), ancak bu dosyada bunu **kanıtlayan bir alan yok**.
Kapanış oranı maç öncesidir ve teknik olarak leakage değildir; fakat **doğrulanamayan bir şey
"güvenli" ilan edilemez.** Bu yüzden her satır `LeakageSafe = UNKNOWN` ile işaretlendi.

### 2.2 Sistematik kapsam boşluğu
UEFA'da **%0**, 2025/26'da **%0**. Oranı feature yapmak, modelin yerel lig maçlarında güçlü,
UEFA maçlarında kör olması demektir — competition type ile korelasyonlu yapay bir sinyal yaratır.

### 2.3 Oran zaten bir tahmindir
Bahis oranı, piyasanın olasılık tahminidir. Modele girdi olarak verilmesi, modelin kendi
tahminini üretmesi yerine piyasayı kopyalamasına yol açar. FORMAX'ın amacı bağımsız olasılık
üretmektir; oran **kıyas ölçütüdür**, girdi değil.

### 2.4 Marj (overround) çıkarılmamış
Ham oranlar bahisçi marjını içerir; olasılığa çevirmek için normalizasyon gerekir. Bu dönüşüm
model fazının kararıdır, veri katmanının değil.

---

## 3. NE İÇİN KULLANILACAK — benchmark

Oranlar **model değerlendirmesinde** referans olarak kullanılacaktır:

* piyasanın ima ettiği olasılık (marj düzeltilmiş) ile FORMAX olasılığının karşılaştırılması,
* Brier / log-loss / RPS gibi metriklerde **piyasa taban çizgisi**,
* modelin piyasadan sistematik olarak saptığı segmentlerin bulunması.

Bu kullanım, oranı **girdi** yapmaz; yalnız **çıktının kalitesini ölçer** ve yalnız oranın mevcut
olduğu %70,40'lık altkümede mümkündür.

---

## 4. DOSYA ŞEMASI

`FORMAX_HISTORICAL_MARKET_DATA.csv` kolonları:

```
MatchId, Season, Competition, CompetitionType, Date, HomeTeam, AwayTeam,
MarketType, OddHome, OddDraw, OddAway, MaxHome, MaxDraw, MaxAway,
Over25, Under25, MaxOver25, MaxUnder25,
HandicapLine, HandicapHome, HandicapAway,
OddsSource, SourceFile, CaptureTimestamp, LeakageSafe, Status
```

* `CaptureTimestamp` — **boş** (kaynak vermiyor). Uydurulmadı.
* `LeakageSafe` — tüm satırlarda `UNKNOWN`.
* `Status` — tüm satırlarda `FUTURE_BENCHMARK`.

`MatchId` ile master'a bağlanır; master şeması değiştirilmedi.

---

## 5. FEATURE OLABİLMESİ İÇİN GEREKENLER

| Gereksinim | Şu an | Ne gerekir |
|---|---|---|
| Yakalama zaman damgası | **YOK** | Oranın maç öncesi alındığını kanıtlayan `CaptureTimestamp` |
| UEFA kapsamı | **%0** | api-football `/odds` veya başka bir kaynak (kota maliyeti ölçülmeli) |
| 2025/26 kapsamı | **%0** | Aynı |
| Marj düzeltmesi | Yok | Normalizasyon yöntemi kararı (model fazı) |
| Kullanım kararı | — | Oranın girdi mi yoksa yalnız benchmark mı olacağı **ürün kararıdır** |

Bu dört madde kapanmadan oran **feature olarak kullanılmayacaktır.**

---

## 6. NE YAPILMADI

* Hiçbir oran model feature'ı yapılmadı.
* Marj düzeltmesi / ima edilen olasılık hesaplanmadı.
* Yeni API çağrısı yapılmadı.
* Master şeması değiştirilmedi.
