# FORMAX — PREDICTION GATE POLICY VALIDATION V1

**Tarih:** 2026-08-21
**Durum:** Build ✅ · 15/15 test ✅ · 18 policy VALIDATION'da ölçüldü · TEST bir kez skorlandı
**Kapsam:** Yalnız araştırma. Model **dokunulmadı**, calibration eklenmedi, production DB'ye
yazılmadı.

---

## 0. SONUÇ — ÖNDEN

# `GATE_POLICY_IMPROVEMENT_NOT_PROVEN`
# `KEEP_CURRENT_GATE`

18 aday politikanın **hiçbiri** önceden ilan edilen kuralı geçemedi. Mevcut politika
(`minPriorMatchesPerTeam=1`, `minCompetitionMatchesObserved=50`) korunuyor.

En önemli iki bulgu:

1. **Kapı modeli değiştiremiyor** — 18 politikanın hepsinde tüm maçlar üzerindeki log loss
   **tek bir değer**: 0,987473. İddia değil, ölçüm.
2. **Mevcut kapının 65 reddi, aynı büyüklükteki rastgele reddden ayırt edilemiyor** (p = 0,118).
   Reddedilen maçların ortalama kaybı 1,052 — yayımlananların 0,987'sinden yüksek, yani yön
   doğru; ama 65 maçlık örneklemde rastgele reddin null aralığı **[0,877 · 1,092]** kadar geniş.
   Bu aralıkta 1,052 ayırt edilemez. Kapı yanlış maçları seçmiyor; **etkisi ölçülemeyecek kadar
   az maça dokunuyor.**

Bu bir başarısızlık değil, kapının tasarım amacına uygun bir sonuç: §10'un dediği gibi amaç "çok
maç reddetmek" değil.

---

## 1. BASELINE OLARAK KAYDEDİLEN MEVCUT POLİTİKA

`prediction_gate_v1/gate.config.json` içinden okundu, değiştirilmedi:

```
minPriorMatchesPerTeam        = 1     (yalnız sıfır geçmişli taraf reddedilir)
minEffectiveMatchesPerTeam    = 0.5   (bayatlık ağı, tamsayı çıtanın altında)
minCompetitionMatchesObserved = 50    (gol baseline'ı öğrenilmiş olmalı)
requiredIdentityConfidence    = CONFIRMED
confidence eşikleri           = 1 / 3 / 5 / 10  (SÜPÜRÜLMEDİ)
lambda clamp, olasılık toleransı, guard rail  (SÜPÜRÜLMEDİ — doğruluk kontrolü, politika değil)
```

Politika adı: **`P1_C50`**. Bütün karşılaştırmalar buna karşı.

---

## 2. SÜPÜRÜLEN ALAN

Yalnız gerçekten ACCEPT/REJECT kararını değiştiren iki eşik:

| Parametre | Adaylar |
|---|---|
| `minPriorMatchesPerTeam` | 0, 1, 2, 3, 5, 10 |
| `minCompetitionMatchesObserved` | 0, 50, 200 |

= **18 politika**. Bilerek küçük tutuldu: 7.636 validation maçında yüz eşik denemek, kazananı
şansla bulmak demektir ve bulunan şey politika değil gürültü olur.

`minEffectiveMatchesPerTeam` bağımsız süpürülmedi; geçmiş çıtasıyla birlikte hareket ediyor
(`0` çıtada `0`, aksi hâlde `0.5`). Nedeni §7'de.

Kimlik, snapshot varlığı, λ guard-rail, normalizasyon ve zaman kontrolleri **her adayda açık**
kaldı — bunlar politika değil, doğruluk kontrolü.

> **Kapıda bir düzeltme gerekti:** `NO_TEAM_HISTORY` kontrolü config'e bakmadan sabit
> çalışıyordu, yani `minPriorMatchesPerTeam = 0` politikası kendini ifade edemiyordu. Kapatılamayan
> bir eşik, eşik değil inançtır. Kontrol config'e bağlandı ve **varsayılan davranışın bit-identical
> kaldığı kanıtlandı**: `prediction_gate_v1` yeniden çalıştırıldı, 27/27 test geçti ve
> `predictions_dto.csv` değişiklikten önceki hâliyle birebir aynı çıktı.

---

## 3. MODEL METRİĞİ ile KAPI METRİĞİ (§11)

Bunlar aynı şey değil ve ayrı ölçüldü:

| | Nedir | 18 politika boyunca |
|---|---|---|
| **Model kalitesi** | Tüm maçlar üzerindeki LogLoss / Brier / RPS | **0,987473 — tek değer, hiç değişmedi** |
| **Kapı kalitesi** | Coverage, selective risk, red kalitesi | politikaya göre değişir |

`gate_policy_comparison.csv` her satırda `AllLogLoss` sütununu taşıyor. Bu sütunun 18 satırda da
aynı olması, kapının modele dokunamadığının doğrudan kanıtı — ve test paketinde ayrıca
doğrulanıyor ("every policy sees byte-identical model probabilities").

---

## 4. COVERAGE / RISK TABLOSU — VALIDATION

| Policy | minPrior | minComp | Yayımlanan | Coverage | Yayımlanan LogLoss | Reddedilen LogLoss | Rastgeleye karşı |
|---|---|---|---|---|---|---|---|
| P0_C0 | 0 | 0 | 7.636 | **100,00%** | 0,987473 | — | red yok |
| P0_C50 / P0_C200 | 0 | 50/200 | 7.626 | 99,87% | 0,987316 | 1,106992 | = rastgele |
| P1_C0 | 1 | 0 | 7.581 | 99,28% | 0,987075 | 1,042329 | = rastgele |
| **P1_C50 (mevcut)** | **1** | **50** | **7.571** | **99,15%** | **0,986917** | **1,052277** | **= rastgele** |
| P2_C50 | 2 | 50 | 7.514 | 98,40% | 0,986940 | 1,020338 | = rastgele |
| P3_C50 | 3 | 50 | 7.454 | 97,62% | 0,987101 | 1,002710 | = rastgele |
| P5_C50 | 5 | 50 | 7.330 | 95,99% | 0,986625 | 1,007788 | = rastgele |
| P10_C50 | 10 | 50 | 7.044 | **92,25%** | **0,985818** | 1,007165 | = rastgele |

### Okunması gereken şey

**Kapı sıkılaştıkça log loss neredeyse hiç iyileşmiyor.** Coverage %100'den %92,25'e düşerken
yayımlanan log loss 0,987473'ten 0,985818'e iniyor: **%7,75 kapsam karşılığında 0,00166 kazanç.**

Ve eğri **monoton bile değil**: P2 (0,986940) P1'den (0,986917) *kötü*, P3 (0,987101) P2'den kötü,
sonra P5 tekrar iyileşiyor. Gerçek bir sinyal olsaydı eğri düzgün inerdi. Bu dalgalanma, farkların
gürültü seviyesinde olduğunun doğrudan göstergesi.

**18 politikanın 18'i de "rastgeleden ayırt edilemez".** Hiçbiri, aynı sayıda maçı rastgele
reddetmenin null dağılımının dışına çıkamadı.

---

## 5. SELECTIVE RISK — MEVCUT POLİTİKA YAKINDAN (§12)

```
coverage 99,15%   (7.571 yayımlandı, 65 reddedildi)

tutulanlar : 0,986917   aynı büyüklükte rastgele tutma [0,986574 · 0,988418]   p = 0,118
reddedilen : 1,052277   aynı büyüklükte rastgele reddetme [0,877463 · 1,092159]
verdict    : INDISTINGUISHABLE FROM RANDOM REFUSAL
```

Bu iki satır, "daha az tahmin yayınlayarak gerçekten daha güvenilir hâle geliyor muyuz?" sorusunun
tam cevabı:

* Reddedilen 65 maçın ortalama kaybı (1,052) yayımlananlardan (0,987) **belirgin şekilde yüksek**.
  Yani kapı **doğru yöne** bakıyor — zor maçları reddediyor.
* Ama 65 maçlık bir örneklemde, rastgele seçilmiş 65 maçın ortalaması **[0,877 · 1,092]** arasında
  herhangi bir yere düşebiliyor. 1,052 bu aralığın içinde.
* Sonuç: kapının seçiciliği bu hacimde **ölçülemez**. "Kapı rastgele davranıyor" demek yanlış
  olurdu; doğru ifade **"etkisi ölçülemeyecek kadar az maça dokunuyor"**.

Test paketi bu testin kendisini de doğruluyor: en kötü maçları reddeden yapay bir kapıya p < 0,05,
en iyi maçları reddedene p > 0,95 ("WORSE THAN CHANCE"), kayıptan bağımsız reddedene ise
0,05 < p < 0,95 veriyor.

---

## 6. NO_HISTORY ÖZEL KONTROLÜ (§6)

**Soru:** Bu grubu yayınlamamak gerçekten gerekli mi?

VALIDATION, 55 maç (zayıf tarafın hiç geçmişi yok):

| Model | LogLoss | Brier | RPS | Accuracy |
|---|---|---|---|---|
| SIMPLE_BASELINE_V1 | 1,077684 | 0,65141 | 0,22992 | 0,4364 |
| TEAM_STRENGTH_BASELINE_V2 | 1,048232 | 0,62988 | 0,21915 | 0,4909 |
| **INDEPENDENT_POISSON_V2** | **1,042329** | **0,62507** | **0,21718** | 0,4909 |
| *(validation'ın geri kalanı, Poisson)* | *0,987075* | *0,58839* | *0,20270* | *0,5238* |

**Cevap: hayır, doğruluk gerekçesiyle gerekli değil.**

Poisson bu grupta hâlâ **elimizdeki en iyi seçenek** — hem rating-only baseline'ı (1,048232) hem
naif frekansı (1,077684) yeniyor. Yani 552 maçı reddetmek doğruluk **kaybettiriyor**.

Grup kötü değil, sadece **zor**: 1,042 vs geri kalanın 0,987'si. Ama 1,042 hâlâ tamamen belirsiz
tahminin (1,0986) altında; yani model bu maçlar hakkında gerçek bilgi taşıyor — o bilgi rakipten
ve müsabakadan geliyor.

**Bu bir sözleşme kararıdır, bir metrik kararı değildir** ve öyle etiketlenmelidir: hiç maçı
olmayan bir takım hakkında yüzde yayımlamak, o yüzdenin o takım hakkında bir şey söylediğini ima
eder. Söylemiyor. `KEEP_CURRENT_GATE` kararı bu tercihi koruyor, ama ücretsiz olmadığını kayda
geçiriyor.

---

## 7. DECAY ARTEFAKTI GERİ GELMEDİ (§9)

Önceki fazda bulunan hata: `minEffectiveMatchesPerTeam = 1.0` iken, zayıf tarafında **tam olarak 1**
maç olan 533 maç reddediliyordu — çünkü `halfLifeDays = 100000` ile 9 yıllık decay
`EffectiveMatches`'i 0,978'e indirip tamsayı sınırın altına düşürüyordu.

Kalıcı korumalar:

* **Test:** `regression: one prior match with a decayed count just under 1 still passes policy 1` —
  1 maçlık, `EffectiveMatches = 0,978` olan takım `P1` altında geçiyor; `P2` altında ise
  (politika gerçekten 2 istediği için) reddediliyor.
* **Test:** `regression: the staleness net travels with the history bar` — bayatlık ağı her zaman
  tamsayı geçmiş çıtasının **altında** kalıyor, ve `P0`'da tamamen kapanıyor.
* **Yapısal:** `Policy.MinEffectiveMatches` bağımsız bir aday değil, `MinPriorMatches`'ten türeyen
  bir değer. İkisi ayrı süpürülseydi artefakt geri gelebilirdi.

Gerçek veride doğrulama: mevcut politikanın 698 reddi arasında **`INSUFFICIENT_HISTORY` kodu hiç
yok** — yalnız `NO_TEAM_HISTORY` (552) ve `COMPETITION_NOT_COVERED` (258).

---

## 8. MÜSABAKA KAPSAMI (§7)

**VALIDATION, mevcut politika:**

| Tür | Maç | Yayımlanan | Coverage | Yayımlanan LogLoss |
|---|---|---|---|---|
| DOMESTIC_LEAGUE | 6.016 | 6.001 | 99,75% | 0,989471 |
| UEFA_MAIN | 814 | 814 | 100,00% | 0,970656 |
| UEFA_QUALIFICATION_PLAYOFF | 152 | 149 | 98,03% | 0,982278 |
| UEFA_QUALIFIER | 644 | 607 | 94,25% | 0,984609 |
| **DOMESTIC_PLAYOFF** | **10** | **0** | **0,00%** | — |

`DOMESTIC_PLAYOFF`: veri setinin tamamında bu türden yalnız 25 maç var, 50'lik kapsam eşiğinin
altında. **N = 10 olduğu için buradan hiçbir sonuç çıkarılamaz** ve bu tür üzerinden politika
kararı verilmedi. Tabloda yalnız eksiksizlik için.

**Sıkı politikaların müsabaka yanlılığı** — seçim kuralının 4. maddesinin neden orada olduğu:

| Policy | UEFA_QUALIFIER coverage | UEFA_QPO coverage |
|---|---|---|
| P1_C50 (mevcut) | 94,25% | 98,03% |
| P5_C50 | 69,6% | — |
| P10_C50 | **44,9%** | 78,3% |

`P10` en iyi toplam log loss'u veriyor ama **UEFA elemelerinin yarısından fazlasını susturuyor**.
Toplam metriği iyileştiren bir politikanın belirli bir müsabakayı yok etmesi, tam olarak §19'un
"competition bazında aşırı dengesiz" dediği durum.

---

## 9. COLD-START KAPSAMI

**VALIDATION, mevcut politika:**

| Sınıf | Maç | Yayımlanan | Coverage | Yayımlanan LogLoss |
|---|---|---|---|---|
| **NoHistory** | 55 | 0 | 0,00% | — |
| Limited | 117 | 117 | 100,00% | 0,975173 |
| Developing | 124 | 124 | 100,00% | 1,015241 |
| Established | 286 | 286 | 100,00% | 1,006497 |
| Rich | 7.054 | 7.044 | 99,86% | 0,985818 |

Dikkat çeken: `Limited` (0,975) `Rich`'ten (0,986) **daha iyi**, `Developing` (1,015) ise en kötü.
Kanıt miktarı ile tahmin zorluğu arasında düzgün bir ilişki yok — 117 ve 124 maçlık örneklemlerde
bu beklenen bir dalgalanma. Cold-start sınıfına göre kademeli bir kapı kurmanın burada bir dayanağı
yok.

---

## 10. RED ANALİZİ (§8)

`gate_rejection_analysis.csv`: 698 reddin **her biri** için MatchId, tarih, segment, sezon,
müsabaka, tür, gate nedeni, iki tarafın cold-start sınıfı ve geçmiş maç sayısı, müsabaka kapsamı,
modelin ham H/D/A olasılığı, gerçek sonuç ve o maçtaki model log loss'u.

**Neden dağılımı ve zorluk:**

| Grup | Maç | Ortalama model log loss |
|---|---|---|
| Yalnız `NO_TEAM_HISTORY` | 440 | 1,020584 |
| Yalnız `COMPETITION_NOT_COVERED` | 146 | 1,070057 |
| İkisi birden | 112 | 0,998757 |
| *(referans: tüm veri seti)* | *33.901* | *0,999254* |

`COMPETITION_NOT_COVERED` grubu belirgin şekilde en zor olan (1,070). `NO_TEAM_HISTORY` grubu da
ortalamanın üstünde (1,021). İkisini birden tetikleyen grup ise ortalama civarında.

**552 `NO_TEAM_HISTORY` reddi nerede:**

| Müsabaka türü | Maç |
|---|---|
| UEFA_QUALIFIER | 357 |
| DOMESTIC_LEAGUE | 162 |
| UEFA_MAIN | 25 |
| UEFA_QUALIFICATION_PLAYOFF | 8 |

| Sezon | 2017/18 | 2018/19 | 2019/20 | 2020/21 | 2021/22 | 2022/23 | 2023/24 | 2024/25 | 2025/26 |
|---|---|---|---|---|---|---|---|---|---|
| Maç | **212** | 70 | 47 | 50 | 45 | 27 | 28 | 44 | 29 |

İki gözlem:

1. **%38'i (212 maç) ilk sezonda.** Bu bir veri seti artefaktı: 2017/18'de herkesin ilk maçı
   sıfır geçmişle oynanıyor. Kararlı hâlde sezon başına ~27–50 maç, yani yılda binde bir civarı.
2. **Kalanların çoğunluğu UEFA elemelerinde** (357/552). Beklendik: bu turnuvaya FORMAX'ın kilitli
   lig kapsamı dışından kulüpler giriyor ve ilk kez görülüyorlar.

---

## 11. SEÇİM KURALI VE NEDEN KİMSE GEÇEMEDİ (§13)

Kural, süpürmeden **önce** ilan edildi ve mekanik olarak uygulandı:

1. VALIDATION yayımlanan log loss, mevcut politikayı en az **0,0010** geçmeli
2. coverage en az **%95** kalmalı
3. redler, aynı büyüklükteki rastgele reddi geçmeli (**p < 0,05**)
4. 100+ maçlı hiçbir müsabaka türü **%80** coverage'ın altına düşmemeli (zaten altında değilse)

En yakın adaylar ve neden düştükleri:

| Policy | Yayımlanan LogLoss | Coverage | Düşme nedeni |
|---|---|---|---|
| P10_C50 | 0,985818 | 92,25% | coverage; p = 0,124; UEFA_QUAL %44,9; UEFA_QPO %78,3 |
| P10_C200 | 0,985818 | 92,25% | aynı |
| P10_C0 | 0,985990 | 92,38% | kazanç 0,00093 (eşik altı); coverage; p = 0,148; UEFA_QUAL %44,9 |
| P5_C50 | 0,986625 | 95,99% | kazanç 0,00029 (eşik altı); p = 0,199; UEFA_QUAL %69,6 |

**Hiçbir aday 4 maddeyi birden geçemedi.** En iyi log loss'u veren politika bile kazancın
yarısını ancak buluyor ve UEFA elemelerini yok ediyor.

---

## 12. TEST — BİR KEZ (§14)

Seçim VALIDATION'da kilitlendikten sonra, TEST **yalnız bir kez** skorlandı. Seçilen politika
mevcut politikayla aynı olduğu için tek satır:

| Policy | Yayımlanan | Coverage | Yayımlanan LogLoss | Reddedilen LogLoss | Tüm maçlar |
|---|---|---|---|---|---|
| **P1_C50** | 7.710 | **98,93%** | **0,993289** | 1,017244 | 0,993544 |

TEST'te müsabaka ve cold-start kırılımı:

| Tür | Maç | Yayımlanan | Coverage | Yayımlanan LogLoss |
|---|---|---|---|---|
| DOMESTIC_LEAGUE | 5.867 | 5.854 | 99,78% | 0,998211 |
| UEFA_MAIN | 1.062 | 1.055 | 99,34% | 0,962951 |
| UEFA_QUALIFICATION_PLAYOFF | 172 | 172 | 100,00% | 0,982216 |
| UEFA_QUALIFIER | 682 | 629 | 92,23% | 1,001383 |
| DOMESTIC_PLAYOFF | 10 | 0 | 0,00% | — |

| Sınıf | Maç | Yayımlanan | Coverage | Yayımlanan LogLoss |
|---|---|---|---|---|
| NoHistory | 73 | 0 | 0,00% | — |
| Limited | 130 | 130 | 100,00% | 1,010886 |
| Developing | 110 | 110 | 100,00% | 1,043291 |
| Established | 255 | 255 | 100,00% | 1,010335 |
| Rich | 7.225 | 7.215 | 99,86% | 0,991606 |

TEST'te de aynı örüntü: reddedilenler (1,017) yayımlananlardan (0,993) zor, kapsam kaybı %1,07.

---

## 13. KARAR

```
GATE_POLICY_IMPROVEMENT_NOT_PROVEN
KEEP_CURRENT_GATE
```

**Gerekçe, sırayla:**

1. Hiçbir aday politika, ilan edilen kuralın dört maddesini birden geçemedi.
2. Sıkılaştırmanın getirisi gürültü seviyesinde: %7,75 kapsam karşılığında 0,00166 log loss ve
   eğri monoton bile değil.
3. Mevcut kapının seçiciliği ölçülemez ama **yönü doğru** — reddettiği maçlar gerçekten daha zor.
4. Sıkı politikalar müsabaka yanlılığı üretiyor (UEFA_QUALIFIER %94 → %45).
5. Mevcut politikanın %2,06'lık reddi, sözleşme gerekçesiyle savunulabilir bir maliyet.

**Daha sıkı kapı otomatik olarak daha iyi değildir** (§10) ve bu veri o iddiayı desteklemiyor.

---

## 14. NE KANITLANMADI

1. **Kapının seçici olduğu.** 65 (validation) / 698 (tüm veri) reddi, seçiciliği istatistiksel
   olarak göstermeye yetmiyor. Yön doğru, güç yok.
2. **NoHistory'yi reddetmenin doğruluk faydası.** Yok — tersine, ölçülebilir bir doğruluk kaybı
   var. Karar sözleşmeye dayanıyor.
3. **Cold-start sınıfına göre kademeli kapı.** Sınıflar arasında düzgün bir zorluk sıralaması
   çıkmadı (`Limited` 0,975 < `Rich` 0,986 < `Developing` 1,015); dayanak yok.
4. **`DOMESTIC_PLAYOFF` hakkında hiçbir şey.** N = 10.
5. **Müsabaka bazında ayrı kapı eşiği.** Denenmedi — bu görevde tek global politika süpürüldü.
   UEFA_QUALIFIER'ın hem en çok redde (357/552) hem en düşük kapsama sahip olması, bir sonraki
   fazın makul sorusu.

---

## 15. ÇALIŞTIRMA

```bash
cd FORMAX_PROBABILITY_ENGINE/prediction_gate_v1_validation/FormaxGatePolicy
dotnet build -c Release
dotnet run -c Release -- test    # 15 test
dotnet run -c Release -- all     # test + tam süpürme (~3 sn)
```

Seçenekler: `--dataset`, `--split`, `--ts-config`, `--dc-config`, `--gate-config`, `--out`.

`Formax.slnx`'e eklenmedi, NuGet bağımlılığı yok. Model ve calibration katmanları
**değiştirilmedi**; `prediction_gate_v1` içindeki tek değişiklik §2'de anlatılan config bağlama
düzeltmesi ve varsayılan çıktının değişmediği kanıtlandı.

---

## 16. ÇIKTILAR

| Dosya | İçerik |
|---|---|
| `gate_policy_comparison.csv` | 18 politika × VALIDATION genel: yayımlanan, reddedilen, coverage, LogLoss/Brier/RPS/Accuracy, reddedilen kümenin log loss'u, tüm maçların log loss'u |
| `gate_policy_validation.csv` | 18 politika × 5 cold-start sınıfı ve 5 müsabaka türü, VALIDATION |
| `gate_policy_test.csv` | Seçilen politika, TEST'te bir kez: genel + cold-start + müsabaka |
| `risk_coverage_curve.csv` | Her politika için coverage, selective risk, rastgele-tutma ve rastgele-reddetme null aralıkları, p değeri, karar |
| `nohistory_analysis.csv` | NoHistory grubunda Simple / TeamStrength / Poisson karşılaştırması + validation'ın geri kalanı |
| `gate_rejection_analysis.csv` | 698 reddin her biri: id, tarih, sezon, müsabaka, neden, cold-start, geçmiş sayısı, ham olasılık, gerçek sonuç, log loss |

`gate_policy_comparison.csv` içindeki `AllLogLoss` sütunu her satırda aynı sayıyı taşır — kapının
modele dokunmadığının dosya üzerinden doğrulanabilir kanıtı.
