# 04_MOTION_SISTEMI.md

# FORMAX Motion System

Version: 1.0 (LOCKED)

---

# 1. Amaç

Bu doküman, FORMAX ürününde kullanılacak hareket (Motion) sistemini tanımlar.

Motion dekorasyon değildir.

Motion yalnızca kullanıcı deneyimini desteklemek için kullanılır.

---

# 2. Motion Felsefesi

FORMAX'ta hareket;

- Bilgiyi açıklamak
- Kullanıcı etkileşimini desteklemek
- AI'ın konuşmasını güçlendirmek
- Veri değişimini göstermek

amacıyla kullanılır.

Motion hiçbir zaman dikkat çekmek için kullanılmaz.

---

# 3. Motion İlkeleri

Tüm animasyonlar aşağıdaki ilkelere uymalıdır.

- Doğal
- Sessiz
- Akıcı
- Kısa
- Tutarlı
- Tahmin edilebilir

---

# 4. Motion Tetikleyicileri

Motion yalnızca aşağıdaki durumlarda çalışır.

- Kullanıcı etkileşimi
- Yeni veri gelmesi
- AI mesajı
- Maç olayı
- Experience değişimi
- Sayfa açılışı
- Sayfa kapanışı

Bunların dışında animasyon kullanılmaz.

---

# 5. Sayfa Geçişleri

Geçişler kısa olmalıdır.

Ani sıçramalar kullanılmaz.

Sayfa geçişleri kullanıcıyı yönlendirmelidir.

---

# 6. Component Motion

Her Shared Component aynı hareket dilini kullanır.

Component kendi animasyon sistemini oluşturamaz.

Motion davranışı ortak olmalıdır.

---

# 7. Dynamic Stage Motion

Dynamic Stage ekran değiştirmez.

Yalnızca içerik dönüşür.

Hero Visual Experience ile birlikte değişir.

Geçişler doğal olmalıdır.

---

# 8. AI Motion

AI konuşurken ekran bunu destekler.

AI mesajı görünürken dikkat AI üzerinde kalmalıdır.

AI sustuğunda kullanıcı keşfe devam eder.

---

# 9. Live Motion

Canlı veri değişimleri kullanıcıyı rahatsız etmemelidir.

Sadece değişen alan hareket eder.

Tüm ekran yeniden çizilmez.

---

# 10. Gesture Motion

Swipe

Scroll

Drag

Bottom Sheet

Pull

aynı hareket prensiplerini kullanmalıdır.

---

# 11. Feedback Motion

Başarılı işlem

Hata

Uyarı

Yükleniyor

durumları sade şekilde gösterilmelidir.

Abartılı efekt kullanılmaz.

---

# 12. Performance

Motion performansı her zaman önceliklidir.

Animasyon performansı için içerik kalitesinden ödün verilmez.

Düşük performans oluşturan animasyonlar kullanılmaz.

---

# 13. Yasaklar

Aşağıdaki hareketler kullanılmaz.

- Sürekli animasyon
- Sonsuz döngü
- Dekoratif hareket
- Gereksiz bounce
- Flash efekti
- Dikkat dağıtan geçişler
- Rastgele hareketler

---

# 14. Son Hüküm

Tüm Motion davranışları bu dokümana uymak zorundadır.

Çelişki durumunda belge önceliği aşağıdaki gibidir.

1. 01_URUN_ANAYASASI.md
2. 02_UI_KURAL_KITABI.md
3. 03_TASARIM_SISTEMI.md
4. 04_MOTION_SISTEMI.md

Bu doküman Product Owner onayı olmadan değiştirilemez.