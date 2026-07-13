# FORMAX GDP — Provider Catalog

> **Amaç:** FORMAX Global Data Platform için kullanılabilecek *tüm potansiyel* veri kaynaklarının resmi değerlendirme kataloğu.
> **Durum:** Yalnızca analiz. Hiçbir provider seçilmedi, implemente edilmedi veya projeye eklenmedi.
> **Karar:** Provider seçimi kullanıcının mimari kararıdır; bu katalog yalnızca girdi sağlar.

## ⚠️ Önemli Uyarılar (okumadan seçim yapma)

1. **Doğrulama zorunlu:** Fiyatlandırma, rate limit, ücretsiz kota ve endpoint kapsamları sağlayıcılar tarafından sık değiştirilir. Aşağıdaki değerler bilinen/yaklaşık durumdur ve **seçim öncesi resmi siteden doğrulanmalıdır**.
2. **Yasal / ToS riski:** "Resmi olmayan (unofficial)" API'ler ve "scraping" tabanlı kaynaklar, ilgili sitenin Kullanım Şartları'nı ihlal edebilir; erişim habersiz kesilebilir. Bu kaynaklar katalogda **açıkça işaretlenmiştir**; üretimde kullanımı hukuki/operasyonel risk taşır.
3. **FORMAX çekirdek kuralı:** Sistem ücretsiz kaynaklarla tam çalışabilmeli; ücretli/freemium kaynaklar opsiyonel olmalı ([[project_data_engine_v1]] mimarisi, `ProviderCategory`).
4. **HARİÇ:** TheSportsDB kullanıcı kararı gereği **kullanılmayacaktır**; katalogda yalnızca bütünlük için, "HARİÇ" etiketiyle listelenmiştir.

## Veri Türü Kısaltmaları

`Fix`=Fixture · `Live`=Live · `News`=News · `Team` · `Play`=Player · `Coach` · `Ven`=Venue · `Ref`=Referee · `Line`=Lineup · `Stat`=Statistics · `Stand`=Standings · `H2H` · `Inj`=Injuries · `Susp`=Suspensions · `Trans`=Transfers · `Wx`=Weather

Kapsam: ✅ tam · 🟡 kısmi/dolaylı · ❌ yok

---

## 1. Kapsam Matrisi (Özet)

<div style="overflow-x:auto">

| Provider | Tür | Ücret | Key | Fix | Live | News | Team | Play | Coach | Ven | Ref | Line | Stat | Stand | H2H | Inj | Susp | Trans | Wx |
|---|---|---|---|--|--|--|--|--|--|--|--|--|--|--|--|--|--|--|--|
| API-Football (API-Sports) | API | Freemium | ✔ | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | ✅ | ✅ | 🟡 | ✅ | ❌ |
| SportMonks | API | Freemium | ✔ | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 🟡 |
| football-data.org | API | Freemium | ✔ | ✅ | 🟡 | ❌ | ✅ | 🟡 | 🟡 | 🟡 | ❌ | ❌ | 🟡 | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| Sportradar | API | Ücretli | ✔ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 🟡 |
| Opta / Stats Perform | API | Ücretli | ✔ | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| Genius Sports | API | Ücretli | ✔ | ✅ | ✅ | ❌ | ✅ | ✅ | 🟡 | 🟡 | 🟡 | ✅ | ✅ | ✅ | ✅ | 🟡 | 🟡 | ❌ | ❌ |
| TheSportsDB **(HARİÇ)** | API | Freemium | ✔ | ✅ | 🟡 | ❌ | ✅ | ✅ | 🟡 | ✅ | ❌ | ✅ | 🟡 | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| OpenLigaDB | API/Açık | Ücretsiz | ✖ | ✅ | 🟡 | ❌ | ✅ | 🟡 | ❌ | 🟡 | ❌ | ❌ | 🟡 | ✅ | 🟡 | ❌ | ❌ | ❌ | ❌ |
| Football-Data.co.uk | Açık/CSV | Ücretsiz | ✖ | ✅ | ❌ | ❌ | 🟡 | ❌ | ❌ | ❌ | 🟡 | ❌ | 🟡 | 🟡 | ✅ | ❌ | ❌ | ❌ | ❌ |
| StatsBomb Open Data | Açık/JSON | Ücretsiz | ✖ | ✅ | ❌ | ❌ | ✅ | ✅ | 🟡 | 🟡 | 🟡 | ✅ | ✅ | 🟡 | 🟡 | ❌ | ❌ | ❌ | ❌ |
| ESPN (unofficial) ⚠️ | API | Ücretsiz | ✖ | ✅ | ✅ | ✅ | ✅ | 🟡 | 🟡 | 🟡 | 🟡 | 🟡 | ✅ | ✅ | 🟡 | 🟡 | ❌ | ❌ | ❌ |
| SofaScore (unofficial) ⚠️ | API | Ücretsiz | ✖ | ✅ | ✅ | 🟡 | ✅ | ✅ | 🟡 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | 🟡 | 🟡 | ❌ | 🟡 |
| FotMob (unofficial) ⚠️ | API | Ücretsiz | ✖ | ✅ | ✅ | ✅ | ✅ | ✅ | 🟡 | 🟡 | 🟡 | ✅ | ✅ | ✅ | ✅ | 🟡 | 🟡 | 🟡 | ❌ |
| FBref / Sports-Reference ⚠️ | Scraping | Ücretsiz | ✖ | ✅ | ❌ | ❌ | ✅ | ✅ | 🟡 | 🟡 | 🟡 | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| Transfermarkt (unofficial) ⚠️ | Scraping | Ücretsiz | ✖ | 🟡 | ❌ | 🟡 | ✅ | ✅ | ✅ | 🟡 | 🟡 | ❌ | 🟡 | 🟡 | ❌ | ✅ | ✅ | ✅ | ❌ |
| Wikidata | Açık/SPARQL | Ücretsiz | ✖ | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | 🟡 | ❌ |
| Wikipedia REST | Açık | Ücretsiz | ✖ | ❌ | ❌ | 🟡 | 🟡 | 🟡 | 🟡 | 🟡 | 🟡 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| OpenStreetMap / Nominatim | Açık | Ücretsiz | ✖ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Open-Meteo | API | Ücretsiz | ✖ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| OpenWeatherMap | API | Freemium | ✔ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Meteostat | API/Açık | Ücretsiz | 🟡 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| MET Norway (Yr) | API | Ücretsiz | ✖ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| RSS Feeds (BBC/Sky/Guardian…) | RSS | Ücretsiz | ✖ | ❌ | ❌ | ✅ | 🟡 | 🟡 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | 🟡 | 🟡 | 🟡 | ❌ |
| NewsAPI.org | API | Freemium | ✔ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| GNews | API | Freemium | ✔ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| GDELT Project | API/Açık | Ücretsiz | ✖ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| PhysioRoom | Web/Feed | Ücretsiz | ✖ | ❌ | ❌ | 🟡 | 🟡 | 🟡 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | 🟡 | ❌ | ❌ |

</div>

---

## 2. Kategori A — Kapsamlı Futbol API'leri

### A1. API-Football (API-Sports)
- **Resmi site:** api-football.com / dashboard.api-football.com (ayrıca RapidAPI üzerinden)
- **Erişim türü:** REST API (JSON)
- **Ücret:** Freemium (ücretsiz plan + ücretli katmanlar)
- **API Key:** Evet (zorunlu)
- **Kullanım kısıtları:** Ücretsiz plan ~100 istek/gün; ücretli planlarda günlük/dakikalık limitler artar. Ticari kullanımda plan yükseltmesi gerekir.
- **Sağladığı veri türleri:** Fixture, Live, Team, Player, Coach, Venue, Lineup, Statistics, Standings, H2H, Injuries, Transfers, Odds (+Referee fixture alanı olarak). News ve Weather yok.
- **Güvenilirlik:** Yüksek — geniş lig kapsamı, olgun dokümantasyon, yaygın topluluk.
- **Güncelleme sıklığı:** Canlı skorlar ~15sn–1dk; diğerleri periyodik.
- **Rate limit:** Plana bağlı (ücretsiz ~100/gün, ~10 istek/dk); doğrulanmalı.
- **Avantajlar:** Tek kaynaktan çok geniş kapsam (16 türün çoğu); iyi dokümantasyon; makul ücretsiz kota.
- **Dezavantajlar:** Ücretsiz kota düşük; ticari ölçekte ücretli; News/Weather yok; referee sınırlı.
- **FORMAX uygunluğu:** **Yüksek aday.** Freemium olduğu için çekirdek kurala göre opsiyonel katman olarak konumlanmalı; tek başına birçok capability'yi karşılar.

### A2. SportMonks Football API
- **Resmi site:** sportmonks.com
- **Erişim türü:** REST API (JSON)
- **Ücret:** Freemium (ücretsiz plan sınırlı ligler: Danimarka/İskoçya) + ücretli planlar
- **API Key:** Evet
- **Kullanım kısıtları:** Ücretsiz plan yalnızca belirli ligler; tam kapsam ücretli. Rate limit plana bağlı (genelde ~3000 istek/gün başlangıç).
- **Sağladığı veri türleri:** Fixture, Live, Team, Player, Coach, Venue, Lineup, Statistics, Standings, H2H, Injuries+Suspensions (sidelined), Transfers, fixture Weather raporu, Referee (kısmi).
- **Güvenilirlik:** Yüksek — kurumsal seviye, iyi SLA.
- **Güncelleme sıklığı:** Canlı ~sn seviyesi (plana bağlı).
- **Rate limit:** Plana bağlı; doğrulanmalı.
- **Avantajlar:** Çok kapsamlı (fixture içi hava durumu + sidelined dahil); esnek "includes" modeli.
- **Dezavantajlar:** Ücretsiz plan lig açısından dar; tam kapsam pahalı.
- **FORMAX uygunluğu:** **Yüksek aday (ücretli).** Kapsam genişliği güçlü; maliyet-kapsam dengesi değerlendirilmeli.

### A3. football-data.org
- **Resmi site:** football-data.org
- **Erişim türü:** REST API (JSON)
- **Ücret:** Freemium (ücretsiz kişisel plan + ücretli)
- **API Key:** Evet (ücretsiz kayıt)
- **Kullanım kısıtları:** Ücretsiz plan ~10 istek/dk ve sınırlı üst düzey ligler (PL, La Liga, Serie A, Bundesliga, CL vb.).
- **Sağladığı veri türleri:** Fixture, Standings, Team, Squad/Player (kısmi), Coach (kısmi), H2H, sınırlı Statistics; canlı skor kısıtlı. News/Lineup/Injuries/Transfers/Weather yok.
- **Güvenilirlik:** Orta-Yüksek — stabil ama dar kapsam.
- **Güncelleme sıklığı:** Maç dakikası güncellemeleri (sınırlı).
- **Rate limit:** Ücretsiz ~10 istek/dk.
- **Avantajlar:** Basit, ücretsiz başlanabilir, temiz veri; üst ligler için yeterli fikstür/puan durumu.
- **Dezavantajlar:** Kapsam dar; derin istatistik/kadro/sakatlık yok.
- **FORMAX uygunluğu:** **Orta aday.** Fixture/Standings/H2H için sağlam, ücretsiz omurga adayı; derin veriler için başka kaynakla tamamlanmalı.

### A4. Sportradar
- **Resmi site:** sportradar.com
- **Erişim türü:** REST API (JSON/XML), push feed
- **Ücret:** Ücretli (kurumsal)
- **API Key:** Evet
- **Kullanım kısıtları:** Sözleşmeli; deneme erişimi sınırlı; lisans ve kullanım alanı kısıtlamaları.
- **Sağladığı veri türleri:** Neredeyse tümü (Fixture, Live, Team, Player, Coach, Venue, Referee, Lineup, Statistics, Standings, H2H, Injuries, Suspensions, Transfers, kısmi Weather).
- **Güvenilirlik:** Çok yüksek — resmi lig ortağı, düşük gecikmeli canlı veri.
- **Güncelleme sıklığı:** Gerçek zamanlı (push).
- **Rate limit:** Sözleşmeye bağlı.
- **Avantajlar:** Endüstri standardı doğruluk ve hız; resmi veri.
- **Dezavantajlar:** Pahalı; kurumsal sözleşme; erişim bariyeri yüksek.
- **FORMAX uygunluğu:** **Düşük öncelik (maliyet).** Çekirdek kurala aykırı biçimde zorunlu olamaz; ancak premium canlı doğruluk gerekirse opsiyonel üst katman.

### A5. Opta / Stats Perform
- **Resmi site:** statsperform.com
- **Erişim türü:** REST API / feed
- **Ücret:** Ücretli (kurumsal, premium)
- **API Key:** Evet
- **Kullanım kısıtları:** Sözleşmeli, lisanslı; en pahalı segment.
- **Sağladığı veri türleri:** Sektörün en derini — tüm türler + detaylı olay/xG/gelişmiş metrikler.
- **Güvenilirlik:** Çok yüksek (endüstri referansı).
- **Güncelleme sıklığı:** Gerçek zamanlı.
- **Rate limit:** Sözleşmeye bağlı.
- **Avantajlar:** En kapsamlı ve en doğru gelişmiş istatistik.
- **Dezavantajlar:** Çok pahalı; erişim ve entegrasyon bariyeri yüksek.
- **FORMAX uygunluğu:** **Düşük öncelik (maliyet).** Yalnızca ileri analiz gereksinimi kesinleşirse opsiyonel.

### A6. Genius Sports
- **Resmi site:** geniussports.com
- **Erişim türü:** REST API / feed
- **Ücret:** Ücretli (kurumsal, bahis/resmi veri odaklı)
- **API Key:** Evet
- **Kullanım kısıtları:** Sözleşmeli, lisanslı.
- **Sağladığı veri türleri:** Fixture, Live, Statistics, Standings, Lineup, Team, Player, H2H (bahis/resmi odaklı).
- **Güvenilirlik:** Yüksek (resmi veri hakları).
- **Güncelleme sıklığı:** Gerçek zamanlı.
- **Rate limit:** Sözleşmeye bağlı.
- **Avantajlar:** Resmi/bahis kalitesinde canlı veri.
- **Dezavantajlar:** Pahalı; bahis odaklı; metadata kapsamı ikincil.
- **FORMAX uygunluğu:** **Düşük öncelik (maliyet).**

### A7. TheSportsDB — ⛔ HARİÇ (kullanıcı kararı)
- **Not:** Kullanıcı kararı gereği **kullanılmayacaktır**. Yalnızca katalog bütünlüğü için listelenmiştir; aday değildir. Değerlendirme yapılmayacaktır.

---

## 3. Kategori B — Açık / Ücretsiz Futbol Verisi

### B1. OpenLigaDB
- **Resmi site:** openligadb.de (api.openligadb.de)
- **Erişim türü:** REST API (JSON), topluluk/açık veri
- **Ücret:** Ücretsiz (açık)
- **API Key:** Hayır
- **Kullanım kısıtları:** Topluluk katkılı; kapsam ağırlıklı Almanya (Bundesliga) + seçili ligler; SLA yok.
- **Sağladığı veri türleri:** Fixture, Standings, Team, Goals/kısmi Statistics; canlı polling ile kısmi; H2H dolaylı.
- **Güvenilirlik:** Orta — topluluk kaynaklı, gecikme/eksik olabilir.
- **Güncelleme sıklığı:** Maç günü periyodik (community).
- **Rate limit:** Belirgin resmi limit yok (nazik kullanım beklenir).
- **Avantajlar:** Tamamen ücretsiz/anahtarsız; Almanya için sağlam; açık.
- **Dezavantajlar:** Dar coğrafi kapsam; SLA yok; derin veri az.
- **FORMAX uygunluğu:** **Orta aday (niş/ücretsiz).** Almanya ligleri için ücretsiz tamamlayıcı olabilir; global omurga değil.

### B2. Football-Data.co.uk
- **Resmi site:** football-data.co.uk
- **Erişim türü:** Açık veri (CSV indirme)
- **Ücret:** Ücretsiz
- **API Key:** Hayır
- **Kullanım kısıtları:** API değil, dosya indirme; geçmiş sezonlar + güncel sezon; kişisel/araştırma kullanımı.
- **Sağladığı veri türleri:** Tarihsel maç sonuçları + bahis oranları; H2H/backtesting için ideal. Canlı/kadro/sakatlık yok.
- **Güvenilirlik:** Yüksek (tarihsel doğruluk) ama gerçek zamanlı değil.
- **Güncelleme sıklığı:** Haftalık/periyodik CSV güncellemesi.
- **Rate limit:** Yok (dosya).
- **Avantajlar:** Zengin tarihsel + oran verisi; ücretsiz; H2H/model eğitimi için değerli.
- **Dezavantajlar:** Canlı değil; API değil; yalnızca sonuç+oran.
- **FORMAX uygunluğu:** **Orta aday (tarihsel/H2H).** Canlı boru hattına değil, tarihsel/analitik katmana uygun.

### B3. StatsBomb Open Data
- **Resmi site:** github.com/statsbomb/open-data
- **Erişim türü:** Açık veri (JSON, GitHub)
- **Ücret:** Ücretsiz
- **API Key:** Hayır
- **Kullanım kısıtları:** Lisans (kaynak gösterimi, ticari kısıtlar okunmalı); yalnızca seçili tarihsel turnuvalar.
- **Sağladığı veri türleri:** Derin olay-düzeyi Statistics, Lineup, Player, Team (tarihsel).
- **Güvenilirlik:** Yüksek (tarihsel doğruluk).
- **Güncelleme sıklığı:** Statik/periyodik (canlı değil).
- **Rate limit:** Yok (repo).
- **Avantajlar:** Ücretsiz, çok derin olay verisi; analiz için eşsiz.
- **Dezavantajlar:** Canlı yok; sınırlı turnuva; lisans dikkati.
- **FORMAX uygunluğu:** **Niş aday (derin analiz).** Canlı GDP için değil; gelişmiş istatistik/AI eğitimi için opsiyonel.

### B4. ESPN (unofficial hidden API) — ⚠️ resmi değil
- **Resmi site:** espn.com (site.api.espn.com — belgelenmemiş)
- **Erişim türü:** REST API (belgelenmemiş, resmi olmayan)
- **Ücret:** Ücretsiz (fiili)
- **API Key:** Hayır
- **Kullanım kısıtları:** **ToS riski**; resmi olmayan; habersiz değişebilir/kesilebilir; ticari kullanım belirsiz.
- **Sağladığı veri türleri:** Fixture, Live, News, Team, Standings, Statistics; kısmi Player/Venue/Referee/Lineup/H2H/Injuries.
- **Güvenilirlik:** Orta — teknik olarak stabil ama sözleşmesiz/garantisiz.
- **Güncelleme sıklığı:** Canlı skorbord ~dk.
- **Rate limit:** Belgelenmemiş.
- **Avantajlar:** Ücretsiz, anahtarsız, geniş; News dahil.
- **Dezavantajlar:** Resmi değil → hukuki/stabilite riski; garanti yok.
- **FORMAX uygunluğu:** **Riskli aday.** Yasal/ToS riski nedeniyle üretimde önerilmez; yalnızca kullanıcı riski kabul ederse.

### B5. SofaScore (unofficial) — ⚠️ resmi değil
- **Erişim türü:** Belgelenmemiş REST (resmi olmayan)
- **Ücret:** Ücretsiz (fiili) · **API Key:** Hayır
- **Kısıtlar:** **ToS/anti-scraping riski**; resmi izin yok.
- **Veri türleri:** Çok geniş — Fixture, Live, Lineup, Statistics, H2H, Standings, Player, Venue, Referee (kısmi Injuries).
- **Güvenilirlik:** Orta (garantisiz).
- **Güncelleme:** Gerçek zamanlıya yakın.
- **Rate limit:** Belgelenmemiş; agresif kullanım engellenir.
- **Avantajlar:** Kapsam çok geniş, ücretsiz.
- **Dezavantajlar:** Resmi değil; yüksek ToS/engellenme riski.
- **FORMAX uygunluğu:** **Riskli aday.** Üretimde önerilmez.

### B6. FotMob (unofficial) — ⚠️ resmi değil
- **Erişim türü:** Belgelenmemiş REST · **Ücret:** Ücretsiz (fiili) · **Key:** Hayır
- **Kısıtlar:** **ToS riski**; resmi değil.
- **Veri türleri:** Fixture, Live, News, Team, Player, Lineup, Statistics, Standings, H2H (kısmi Injuries/Transfers).
- **Güvenilirlik:** Orta (garantisiz). **Güncelleme:** Gerçek zamanlıya yakın. **Rate limit:** Belgelenmemiş.
- **Avantajlar:** Geniş + News. **Dezavantajlar:** Resmi değil; risk.
- **FORMAX uygunluğu:** **Riskli aday.** Üretimde önerilmez.

### B7. FBref / Sports-Reference — ⚠️ scraping
- **Resmi site:** fbref.com
- **Erişim türü:** Web (scraping; resmi API yok)
- **Ücret:** Ücretsiz (içerik) · **Key:** Yok
- **Kısıtlar:** **Scraping ToS riski**; rate/robots kısıtı; ticari kullanım dikkati.
- **Veri türleri:** Derin Statistics, Standings, Player, Team, H2H (tarihsel/detay).
- **Güvenilirlik:** Yüksek (veri doğruluğu) ama erişim yöntemi kırılgan.
- **Güncelleme:** Maç sonrası. **Rate limit:** Scraping için nazik gecikme şart.
- **Avantajlar:** Çok derin ücretsiz istatistik.
- **Dezavantajlar:** Scraping riski/kırılganlığı; canlı yok.
- **FORMAX uygunluğu:** **Riskli/niş.** Yasal netlik olmadan üretim önerilmez.

### B8. Transfermarkt (unofficial) — ⚠️ scraping
- **Resmi site:** transfermarkt.com
- **Erişim türü:** Web (resmi API yok; topluluk unofficial API'leri + scraping)
- **Ücret:** Ücretsiz (içerik) · **Key:** Yok
- **Kısıtlar:** **ToS/scraping riski**.
- **Veri türleri:** Transfers, Injuries, Suspensions, market değerleri, Player, Team, Coach (transfer odaklı en zengin kaynak).
- **Güvenilirlik:** Yüksek (transfer/piyasa verisi referansı) ama erişim resmi değil.
- **Güncelleme:** Sürekli (editoryal). **Rate limit:** Scraping için kısıtlı.
- **Avantajlar:** Transfer/sakatlık/ceza için en zengin ücretsiz içerik.
- **Dezavantajlar:** Resmi API yok; hukuki/stabilite riski.
- **FORMAX uygunluğu:** **Riskli/niş.** Transfers/Injuries/Suspensions boşluğunu doldurur ama yasal netlik gerektirir.

---

## 4. Kategori C — Referans / Metadata (Açık)

### C1. Wikidata
- **Site:** wikidata.org · **Erişim:** SPARQL + REST · **Ücret:** Ücretsiz · **Key:** Hayır
- **Kısıtlar:** Adil kullanım; SPARQL zaman aşımı limitleri.
- **Veri türleri:** Team, Player, Coach, Venue, Referee metadata (statik); dolaylı Transfers geçmişi.
- **Güvenilirlik:** Orta-Yüksek (topluluk düzenli); güncellik değişken.
- **Güncelleme:** Sürekli ama editoryal. **Rate limit:** SPARQL adil kullanım.
- **Avantajlar:** Ücretsiz, yapısal, entity eşleştirme/kimlik için değerli.
- **Dezavantajlar:** Canlı/maç verisi yok; güncellik garanti değil.
- **FORMAX uygunluğu:** **Destekleyici aday.** [[project_data_engine_v1]] Match Identity / entity zenginleştirme için ücretsiz metadata kaynağı.

### C2. Wikipedia / Wikimedia REST
- **Site:** wikipedia.org (REST API) · **Ücret:** Ücretsiz · **Key:** Hayır
- **Veri türleri:** Team/Player/Coach/Venue/Referee açıklama metinleri, dolaylı News/özet.
- **Güvenilirlik:** Orta. **Güncelleme:** Editoryal. **Rate limit:** Adil kullanım.
- **Avantajlar:** Ücretsiz zengin metin/metadata. **Dezavantajlar:** Yapısal değil; maç verisi yok.
- **FORMAX uygunluğu:** **Destekleyici aday (metadata/içerik).**

### C3. OpenStreetMap / Nominatim
- **Site:** openstreetmap.org / nominatim.org · **Ücret:** Ücretsiz · **Key:** Hayır
- **Kısıtlar:** Nominatim adil kullanım (1 istek/sn); yoğun kullanımda kendi sunucusu önerilir.
- **Veri türleri:** Venue (stat konumu/koordinat/geocoding).
- **Güvenilirlik:** Yüksek (coğrafi). **Güncelleme:** Sürekli topluluk. **Rate limit:** ~1/sn (public).
- **Avantajlar:** Ücretsiz venue koordinatı → Weather eşleştirmesi için köprü.
- **Dezavantajlar:** Yalnızca coğrafi; spor verisi yok.
- **FORMAX uygunluğu:** **Destekleyici aday (venue→coord→weather zinciri).**

---

## 5. Kategori D — Hava Durumu

### D1. Open-Meteo
- **Site:** open-meteo.com · **Erişim:** REST · **Ücret:** Ücretsiz (non-commercial açık; ticari için plan) · **Key:** Hayır
- **Kısıtlar:** Ücretsiz kullanım adil kullanım kotası; ticari yoğun kullanımda ücretli.
- **Veri türleri:** Weather (tahmin + geçmiş), koordinat bazlı.
- **Güvenilirlik:** Yüksek. **Güncelleme:** Saatlik/model bazlı. **Rate limit:** Adil kullanım (yüksek).
- **Avantajlar:** Ücretsiz, anahtarsız, açık; geçmiş+tahmin.
- **Dezavantajlar:** Koordinat gerekir (venue geocoding'e bağımlı).
- **FORMAX uygunluğu:** **Yüksek aday (Weather).** Ücretsiz/anahtarsız → çekirdek kurala en uygun hava durumu kaynağı.

### D2. OpenWeatherMap
- **Site:** openweathermap.org · **Ücret:** Freemium · **Key:** Evet
- **Kısıtlar:** Ücretsiz ~60 çağrı/dk, 1M/ay; bazı ürünler ücretli.
- **Veri türleri:** Weather (güncel/tahmin/geçmiş).
- **Güvenilirlik:** Yüksek. **Rate limit:** ~60/dk (ücretsiz).
- **Avantajlar:** Olgun, geniş. **Dezavantajlar:** Key gerekir; freemium.
- **FORMAX uygunluğu:** **Alternatif aday (Weather).** Open-Meteo'ya yedek.

### D3. Meteostat · D4. MET Norway (Yr)
- **Meteostat:** meteostat.net — ücretsiz/açık geçmiş hava; API (RapidAPI) veya Python. Key kısmi.
- **MET Norway (api.met.no):** ücretsiz, anahtarsız; User-Agent zorunlu; adil kullanım.
- **Veri türleri:** Weather. **Uygunluk:** Ücretsiz alternatif/yedek adaylar.

---

## 6. Kategori E — Haber

### E1. RSS Feeds (BBC Sport, Sky Sports, Guardian Football, ESPN, lig resmi)
- **Erişim:** RSS/Atom · **Ücret:** Ücretsiz · **Key:** Hayır
- **Kısıtlar:** Yayıncı telif/atıf; tam metin değil özet.
- **Veri türleri:** News (kısmi Team/Player/Transfer/Injury bağlamı).
- **Güvenilirlik:** Yüksek (yayıncı kaynaklı). **Güncelleme:** Dakikalar. **Rate limit:** Nazik polling.
- **Avantajlar:** Ücretsiz, stabil, kaliteli kaynak; anahtarsız.
- **Dezavantajlar:** Yapısal eşleştirme (haber→maç) gerekir; özet metin.
- **FORMAX uygunluğu:** **Yüksek aday (News).** Ücretsiz/anahtarsız → çekirdek kurala uygun News omurgası ([[project_data_engine_v2_news]] ile uyumlu).

### E2. NewsAPI.org · E3. GNews · E4. GDELT · E5. Mediastack
- **NewsAPI.org:** Freemium, key; ücretsiz plan geliştirme/geriye dönük kısıtlı.
- **GNews:** Freemium, key; ücretsiz ~100 istek/gün.
- **GDELT:** Ücretsiz/açık, key yok; global haber olay veritabanı (çok geniş, gürültülü).
- **Mediastack:** Freemium, key.
- **Veri türleri:** News. **Uygunluk:** GDELT ücretsiz/açık → aday; diğerleri freemium alternatif.

---

## 7. Kategori F — Uzman / Niş

### F1. PhysioRoom (Injuries)
- **Site:** physioroom.com · **Erişim:** Web/feed (resmi API sınırlı) · **Ücret:** Ücretsiz içerik · **Key:** Hayır
- **Veri türleri:** Injuries (ağırlıklı İngiltere), kısmi Suspensions.
- **Güvenilirlik:** Orta (editoryal). **Kısıtlar:** Yapısal API yok; scraping riski.
- **FORMAX uygunluğu:** **Niş aday (Injuries).** Yasal netlik gerektirir.

> **Not — Transfers / Injuries / Suspensions / Referee / Coach**: Bu türler için *ücretsiz + resmi + yapısal* kaynak azdır.
> En zengin içerik (Transfermarkt, PhysioRoom, FBref) resmi API sunmaz → scraping/ToS riski.
> Yapısal ücretsiz seçenek isteniyorsa bu türler için kapsam **API-Football / SportMonks (freemium)** ya da **Wikidata (metadata)** ile sınırlıdır.

---

## 8. Değerlendirme Sentezi (seçime girdi, karar değil)

**Kapsam-maliyet-risk üçgeni:**

- **Ücretsiz + resmi + anahtarsız (en düşük risk):** OpenLigaDB (DE fikstür/puan), Football-Data.co.uk (tarihsel/H2H), Open-Meteo (hava), RSS/GDELT (haber), Wikidata/OSM (metadata/venue-coord). → Global canlı kapsam sınırlı ama risksiz.
- **Freemium + resmi + key (orta maliyet, düşük risk):** API-Football, SportMonks, football-data.org. → Tek/iki kaynakla 16 türün çoğu; çekirdek kurala göre *opsiyonel* katman.
- **Ücretli + resmi (yüksek maliyet, en yüksek doğruluk):** Sportradar, Opta/Stats Perform, Genius Sports. → Yalnızca premium canlı doğruluk gerekirse.
- **Ücretsiz ama resmi değil (yüksek ToS/stabilite riski):** ESPN, SofaScore, FotMob, FBref, Transfermarkt. → Üretimde önerilmez; kullanıcı riski açıkça kabul etmedikçe.

**Öne çıkan gözlemler:**
- Hiçbir *tek* ücretsiz+resmi kaynak 16 türün tamamını global karşılamıyor → çok-provider yaklaşımı ([[project_data_engine_v1]]) doğru mimari.
- `News` ve `Weather` için ücretsiz/anahtarsız güçlü seçenekler var (RSS/GDELT, Open-Meteo).
- `Transfers/Injuries/Suspensions/Referee/Coach` en zayıf halkalar; ücretsiz+resmi+yapısal boşluk burada.
- TheSportsDB **hariç** (kullanıcı kararı).

---

## 9. Sonraki Adım

Bu katalog yalnızca **girdi**dir. Provider seçimi kullanıcı tarafından yapılacaktır.
Bir sonraki sprintte birlikte:
1. Hangi veri türlerinin öncelikli olduğunu,
2. Ücretsiz omurga + (opsiyonel) freemium/ücretli katman dengesini,
3. Resmi olmayan/scraping kaynakların kabul edilip edilmeyeceğini
belirleyip **yalnızca onaylanan** provider'ları platforma ekleyeceğiz.
