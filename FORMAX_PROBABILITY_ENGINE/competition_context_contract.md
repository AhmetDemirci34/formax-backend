# FORMAX — COMPETITION CONTEXT CONTRACT

**Tarih:** 2026-08-20
**Durum:** Tasarım. Model kurulmadı, katsayı seçilmedi.

Takım gücü **ortaktır** (bkz. `team_strength_architecture.md`), ancak **maç bağlamı ayrıdır.**
Bu belge, bağlamın hangi alanlarla taşınacağını kilitler.

---

## 1. BEŞ BAĞLAM TİPİ

| CompetitionType | Satır | Yapı | Model açısından farkı |
|---|---|---|---|
| `DOMESTIC_LEAGUE` | 26.846 | Çift devreli lig | Sezon içi tablo anlamlı; tekrarlı eşleşmeler; dengeli takvim |
| `DOMESTIC_PLAYOFF` | 25 | Terfi play-off'u (Championship) | Tek eleme; **örneklem 25 maç — hiçbir parametre buradan tahmin edilemez** |
| `UEFA_MAIN` | 3.587 | Grup/lig aşaması + eleme | Karışık: grup maçları tekrarlı, eleme iki ayaklı |
| `UEFA_QUALIFIER` | 2.838 | Ön eleme turları | Neredeyse tamamı iki ayaklı; takımların çoğunun kapsam içinde geçmişi yok |
| `UEFA_QUALIFICATION_PLAYOFF` | 605 | Ağustos play-off turu | Ana turnuvaya giriş; iki ayaklı |

**Kural:** bu beş tip aynı dağılıma sahip değildir ve model tarafından ayırt edilebilmelidir.
Ölçülmüş fark (tanımlayıcı):

| Tip | Ev % | Beraberlik % | Deplasman % | Ort. toplam gol |
|---|---|---|---|---|
| DOMESTIC_LEAGUE | 43,8 | 25,3 | 30,9 | 2,78 |
| UEFA_MAIN | 47,4 | 21,0 | 31,6 | 2,92 |
| UEFA_QUALIFIER | 48,2 | 21,2 | 30,6 | 2,75 |
| UEFA_QUALIFICATION_PLAYOFF | 49,9 | 23,1 | 26,9 | 2,75 |
| DOMESTIC_PLAYOFF | 32,0 | 32,0 | 36,0 | 1,80 |

Beraberlik oranındaki 4 puanlık fark (yerel %25,3 ↔ UEFA ana %21,0) tek başına bile bağlamın
modele girmesini zorunlu kılar.

---

## 2. BAĞLAM ALANLARI (schema)

### 2.1 Her maç için zorunlu

| Alan | Tip | Kaynak | Coverage |
|---|---|---|---|
| `competition` | enum(11) | master | %100 |
| `competition_type` | enum(5) | master | %100 |
| `season` | string | master | %100 |
| `is_neutral_venue` | boolean | **YOK** | %0 — final/tek maçlı turnuva aşamalarında saha avantajı yoktur ama veri setinde bu bilgi bulunmuyor |

`is_neutral_venue` **uydurulmayacaktır.** Şu an tüm maçlar ev/deplasman varsayımıyla işlenir;
UEFA finalleri (sezon başına 1–3 maç) bu varsayımı bozar. Etkilenen satır sayısı küçüktür ama
belgelenmiştir. Kapatma yolu: api-football `fixture.venue` + turnuva aşaması eşleştirmesi (gelecek).

### 2.2 UEFA eleme/knockout için

| Alan | Tip | Türetme | Coverage |
|---|---|---|---|
| `tie_id` | string | competition + season + sıralı takım çifti | UEFA satırlarının %100'ü |
| `tie_leg_number` | int | eşleşme içindeki tarih sırası (1 veya 2) | UEFA satırlarının **%85,5'i** çok ayaklı bir eşleşmeye ait |
| `tie_aggregate_home_goals` | int | önceki ayakların toplamı — ilk ayakta null | ikinci ayaklarda %100 |
| `tie_aggregate_away_goals` | int | aynı | ikinci ayaklarda %100 |
| `tie_is_home_second_leg` | boolean | bu takım ikinci ayağı evinde mi oynuyor | %100 |

**Leakage durumu:** ilk ayak, ikinci ayaktan **önce** oynanır → aggregate bilgisi ikinci ayak için
`LEAKAGE_SAFE`'tir ve kullanılmalıdır. İlk ayak satırında bu alanlar `null` olur; ikinci ayağın
sonucu asla ilk ayağa taşınmaz.

**Bölme kuralı:** train/test bölmesinin birimi **maç değil `tie_id`'dir.** Bir eşleşmenin iki ayağı
farklı taraflara düşerse sızıntı olur (bkz. leakage kuralları T6).

### 2.3 Yerel lig için

| Alan | Tip | Coverage |
|---|---|---|
| `season_matchday_index` | int | Round metninden %95,13 (yerel) — 288 serbest metin etiketi canonical eşleme ister |
| `season_progress_ratio` | float | oynanan maç / toplam maç — tablo verisi olan satırlarda %91,88 |
| `home_season_points_to_date` vb. | int | %91,88 (her iki tarafta ≥3 maç) |

Sezon-sonu sıralaması **kullanılmayacaktır** (leakage).

---

## 3. COMPETITION OFFSET / SEVİYE

Ortak rating + müsabaka düzeltmesi (`competition_offset`) yaklaşımı `team_strength_architecture.md`
§2'de tanımlıdır. Bu belgede kilitlenen kısım: **offset'in granülerliği bir hiper-parametredir.**

İki aday:
* `competition_type` bazında (5 parametre) — az veri, sağlam;
* `competition` bazında (11 parametre) — daha ince, UEFA eleme için hâlâ yeterli veri var (2.838 satır).

Hangisinin kullanılacağı walk-forward doğrulamayla seçilecektir. **Şimdi seçilmedi.**

---

## 4. TERFİ / KÜME DÜŞME GEÇİŞLERİ

Bir takım Championship'ten Premier League'e çıktığında:

* **Rating sıfırlanmaz** — takım kimliği süreklidir, geçmiş maçları gerçektir.
* **Rating birebir taşınmaz** — alt ligdeki performans üst ligde aynı seviyeyi göstermez.

Gereken mekanizma: `competition_offset` farkı kadar bir **seviye düzeltmesi** + geçiş sonrası ilk
maçlarda **shrinkage** (bkz. cold-start belgesi §4). Katsayı burada seçilmemiştir.

Ölçülebilirlik: veri setinde 8 lig × 9 sezon boyunca terfi/düşme geçişleri mevcuttur, dolayısıyla
seviye farkı **veriden tahmin edilebilir** — elle atanmasına gerek yoktur.

---

## 5. FEATURE × BAĞLAM UYGULANABİLİRLİĞİ

Ayrıntılı matris: `FORMAX_PROBABILITY_COMPETITION_FEATURE_MATRIX.csv`. Özet kurallar:

| Feature ailesi | DOMESTIC_LEAGUE | UEFA_MAIN | UEFA_QUALIFIER | UEFA_QUAL_PLAYOFF | DOMESTIC_PLAYOFF |
|---|---|---|---|---|---|
| Ortak team strength | ✔ | ✔ | ✔ (düşük güven) | ✔ (orta güven) | ✔ |
| Sezon-içi tablo | ✔ %91,9 | ✖ (%64 ama tablo anlamsız) | ✖ %10,9 | ✖ %33,2 | ✖ |
| Kendi turnuvası içi form | ✔ %96,6 | ~ %75,2 | ✖ %27,1 | ~ %56,4 | ✔ |
| H2H | ✔ %79 | ~ | ✖ (çoğu ilk karşılaşma) | ~ | ✔ |
| Tie context | ✖ | ✔ | ✔ | ✔ | ✖ |
| Maç istatistikleri (şut vb.) | ✔ kısmi (bkz. coverage) | ✖ %0 | ✖ %0 | ✖ %0 | ✖ %0 |

✔ kullanılabilir · ~ dikkatli · ✖ kullanılamaz/anlamsız

---

## 6. NE YAPILMADI

Bağlam katsayıları, offset değerleri, saha avantajı sayıları **belirlenmedi**. Bu belge yalnız
hangi bağlam alanının hangi kapsamda hangi doluluğa sahip olduğunu ve nasıl taşınacağını tanımlar.
