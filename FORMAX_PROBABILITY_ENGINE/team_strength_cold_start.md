# FORMAX — TEAM STRENGTH COLD START (tasarım)

**Tarih:** 2026-08-20
**Durum:** Tasarım + ölçüm. **Prior değeri seçilmedi, shrinkage katsayısı belirlenmedi, rating
hesaplanmadı.** Veri olmayan takım için uydurma rating üretilmedi.

---

## 1. SORUNUN GERÇEK BÜYÜKLÜĞÜ (ölçülmüş)

| Ölçüm | Değer |
|---|---|
| Veri setindeki canonical takım | **709** |
| Bunlardan kilitli 8 yerel ligde **en az bir maçı olan** | **241** |
| **Yalnız UEFA'da görünen (yerel lig geçmişi hiç yok)** | **468 — %66** |
| UEFA-only takımların toplam maç görünürlüğü | 10.012 taraf-maç, takım başına **medyan 10** |
| Takımların 5'ten az maça sahip olma oranı | %19,18 |
| Takımların 20'den az maça sahip olma oranı | %44,29 |

Maç düzeyinde:

| Durum | Maç | Oran |
|---|---|---|
| Taraflardan birinin **hiç** önceki maçı yok | 552 | %1,63 |
| Taraflardan birinin 3'ten az önceki maçı var | 1.527 | %4,50 |
| **Taraflardan birinin hiç yerel lig maçı yok** | **6.176** | **%18,22** |

Bunun competition type dağılımı:

| CompetitionType | Yerel lig geçmişi olmayan taraf içeren maç | O tipteki toplam | Oran |
|---|---|---|---|
| **UEFA_QUALIFIER** | **2.828** | 2.838 | **%99,6** |
| UEFA_MAIN | 2.594 | 3.587 | %72,3 |
| UEFA_QUALIFICATION_PLAYOFF | 592 | 605 | %97,9 |
| DOMESTIC_LEAGUE | 162 | 26.846 | %0,6 |

**Sonuç:** UEFA eleme maçlarının neredeyse tamamında, taraflardan en az biri FORMAX'ın kilitli 8
yerel liginde hiç oynamamıştır (Malta, Faroe, Andorra, Ermenistan, Kosova… ligleri kapsam dışı).
Bu takımlar için güç **yalnızca UEFA maçlarından** öğrenilebilir ve bu da takım başına medyan
10 maçtır.

---

## 2. DÖRT SINIF VE ELE ALIŞ BİÇİMİ

### A — Zengin geçmişi olan takım
**Tanım:** ≥10 önceki in-scope maç. (Maçların %87,77'sinde her iki taraf bu sınıfta.)
**Ele alış:** normal dinamik rating; shrinkage minimum.
**Ek veri gerekmez.**

### B — Az maçı olan takım
**Tanım:** 1–9 önceki maç.
**Ele alış:** **partial pooling / shrinkage** — takımın kendi kanıtı ile ait olduğu havuzun
ortalaması arasında, kanıt miktarına göre ağırlıklandırma. Kavramsal biçim:

```
rating_used = w * rating_own + (1 - w) * pool_mean
w = n / (n + k)        # n = önceki maç sayısı, k = shrinkage sabiti
```

`k` **seçilmemiştir** — hiper-parametredir, walk-forward doğrulamayla belirlenecektir.

**Havuz (`pool`) seçimi** kritik ve veriden belirlenebilir:
* aynı **müsabaka** (ör. Conference League eleme) ortalaması,
* aynı **ülke** ortalaması — *ülke bilgisi şu an master'da YOK, `FORMAX_HISTORICAL_TEAMS.csv`
  içinde yalnız provider kimliği doğrulanmış 496 takım için ülke mevcut (bkz. §5)*,
* genel havuz ortalaması.

### C — Yeni / yeni terfi etmiş takım
**Tanım:** bu müsabakada ilk kez oynuyor ama başka müsabakada geçmişi var
(`IsNewToCompetition = true`, geçmiş maç sayısı > 0).
**Ele alış:** rating korunur + **seviye düzeltmesi** (`competition_offset` farkı) + geçiş
shrinkage'ı. Rating **sıfırlanmaz**.
**Ölçülebilirlik:** 8 lig × 9 sezonluk terfi/düşme geçişleri veri setinde mevcut olduğundan seviye
farkı elle atanmaz, **veriden tahmin edilir**.

### D — UEFA eleme takımı, kapsam içinde neredeyse hiç geçmiş yok
**Tanım:** yalnız UEFA maçları var (468 takım) veya hiç önceki maçı yok (552 maçta bir taraf).
**Ele alış — üç seçenek, hiçbiri bu görevde seçilmedi:**

| Seçenek | Ne gerektirir | Veri durumu |
|---|---|---|
| **D1 — UEFA-içi havuz priorı** | Ek veri gerekmez. Takım, kendi UEFA maçlarından öğrenilir; kanıt azken müsabaka havuzunun ortalamasına çekilir | **Şimdi uygulanabilir** |
| **D2 — Ülke/lig katsayısı priorı** | Takımın ülkesi + o ülke liginin seviye tahmini | Ülke bilgisi 709 takımın **496'sında** var (provider kimliği doğrulanmış olanlar); UEFA ülke katsayısı **YOK** |
| **D3 — Kapsam genişletme** | Bu kulüplerin kendi yerel liglerini veri setine eklemek | **Veri yok** — FORMAX kapsamı 17 müsabakayla kilitli; kapsam kararı kullanıcıya aittir |

**Kural:** D sınıfındaki takım için **sabit bir rating uydurulmayacaktır.** Prior kullanılacaksa
bu prior veriden (havuz ortalaması) türetilir ve `strength_confidence = LOW` ile işaretlenir.

---

## 3. ZORUNLU ÇIKTI ALANLARI

Cold-start ele alışı görünmez olmamalıdır. Rating katmanı her taraf için şunları da yayınlar:

```
{team}_strength_sample_size        # kaç önceki maçtan geldi
{team}_strength_confidence         # NO_HISTORY | LOW | MEDIUM_LOW | MEDIUM | HIGH
{team}_strength_pool_weight        # shrinkage'ta havuzun ağırlığı (1 - w)
{team}_strength_pool_used          # hangi havuz: competition / country / global
{team}_is_cold_start               # boolean
```

Model bu alanları girdi olarak kullanabilir; en azından **raporlamada** kullanılmalıdır — çünkü
`%1,63` maçta bir tarafın hiç geçmişi yoktur ve bu maçların tahmini diğerleriyle aynı güvende değildir.

---

## 4. TERFİ / KÜME DÜŞME (özel durum C)

| Gereksinim | Durum |
|---|---|
| Takım kimliğinin lig değişiminde korunması | **Sağlandı** — 709 canonical kimlik, lig bağımsız |
| Alt lig → üst lig seviye farkı | **Veriden tahmin edilebilir** (Championship↔Premier League geçişleri 9 sezon boyunca mevcut) |
| Geçiş sonrası belirsizlik | Shrinkage ile ele alınır; katsayı seçilmedi |

Rating'in otomatik sıfırlanması **yasaktır**; alt lig gücünün üst lige birebir taşınması da yanlıştır.
İkisinin arası `competition_offset` + shrinkage ile kurulur.

---

## 5. GELECEK VERİ İHTİYACI (D2 için)

| Veri | Şu an | Nereden gelebilir |
|---|---|---|
| Takım ülkesi | 709 takımın **496'sında** var (`FORMAX_HISTORICAL_TEAMS.csv` → provider kimliği doğrulananlar) | api-football `teams?id=` — kalan 213 takım için ~213 çağrı |
| Takım kuruluş yılı / stadyum | Aynı 496 takım | Aynı uç |
| UEFA ülke katsayısı | **YOK** | Harici kaynak; FORMAX kapsamında değil |
| Kulübün kendi yerel ligi | **YOK** | Kapsam genişletme kararı gerekir |

Bunların hiçbiri bu görevde çekilmedi.

---

## 6. NE YAPILMADI

* Prior değeri, havuz ortalaması, shrinkage sabiti `k` **seçilmedi**.
* Hiçbir takım için rating hesaplanmadı.
* Uydurma/varsayılan rating atanmadı.
* Kapsam genişletme yapılmadı, yeni API çağrısı yapılmadı.
