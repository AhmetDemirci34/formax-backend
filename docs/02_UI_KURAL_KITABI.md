# 02_UI_KURAL_KITABI.md

# FORMAX UI Rulebook

Version: 1.0 (LOCKED)

---

# 1. Amaç

Bu doküman, FORMAX ürün ailesindeki tüm kullanıcı arayüzlerinin uyması gereken ortak UI kurallarını tanımlar.

Bu dokümanın amacı yeni tasarım üretmek değil, mevcut tasarım dilinin tutarlı uygulanmasını sağlamaktır.

---

# 2. Kapsam

Bu kurallar aşağıdaki tüm ekranlar için geçerlidir.

- Splash
- Login
- Discovery Feed
- Match Intelligence
- Matches
- Live Match
- Profile
- Notifications
- Following
- Settings

Yeni eklenen tüm ekranlar da bu kurallara uymak zorundadır.

---

# 3. UI İlkeleri

Tüm ekranlar aşağıdaki temel ilkeleri korur.

- AI First
- Information First
- Premium
- Minimal
- Human Centered
- Silent Interface

Arayüz içerikten daha baskın olamaz.

---

# 4. Bilgi Hiyerarşisi

Bilgi her zaman aşağıdaki sırada sunulur.

1. AI
2. Insight
3. Evidence
4. Data
5. Kullanıcı Aksiyonu

Bu sıra ürün genelinde korunur.

---

# 5. Layout Kuralları

Her ekran tek bir ana amaca hizmet eder.

Her ekranda tek ana odak bulunur.

Bilgi dikey akışla ilerler.

Yatay kaydırma yalnızca ilgili component içinde kullanılabilir.

---

# 6. Match Intelligence Yerleşimi

Match Intelligence aşağıdaki sırayı kullanır.

1. Header
2. AI Session
3. AI Action Chips
4. Dynamic Stage
5. AI Insight
6. AI Evidence
7. AI Ask

Bu sıra değiştirilemez.

---

# 7. Component Kuralları

Tüm ekranlar Shared Component mimarisini kullanır.

Component davranışları değiştirilemez.

Yeni component yalnızca Product Owner onayı ile eklenebilir.

---

# 8. Tipografi Kuralları

Metinler okunabilir olmalıdır.

Başlıklar kısa tutulmalıdır.

AI mesajları kısa ve doğrudan olmalıdır.

Evidence metinleri AI yorumunu desteklemelidir.

---

# 9. Renk Kullanımı

Dark Theme zorunludur.

Renkler yalnızca aşağıdaki amaçlarla kullanılabilir.

- Bilgi hiyerarşisi
- Sistem durumları
- AI tarafından üretilen tanımlı sinyaller
- Kullanıcı odağı

Renk dekorasyon amacıyla kullanılamaz.

---

# 10. Spacing Kuralları

Tüm ekranlar ortak spacing sistemi kullanır.

Boşluklar bilgi hiyerarşisini desteklemelidir.

Rastgele boşluk kullanılamaz.

---

# 11. Durum Kuralları

Her ekran aşağıdaki durumları desteklemelidir.

- Loading
- Empty
- Error
- Offline

Bu durumlarda bilgi hiyerarşisi korunmalıdır.

---

# 12. Responsive Kuralları

Farklı ekran boyutlarında bilgi sırası değişmez.

Component oranları korunur.

Metin taşmaları kontrollü yönetilir.

---

# 13. Erişilebilirlik

Yeterli kontrast sağlanmalıdır.

Dokunulabilir alanlar erişilebilir boyutta olmalıdır.

Metin okunabilirliğini kaybetmemelidir.

---

# 14. Yasaklar

Aşağıdaki tasarım kararları yasaktır.

- Gradient
- Glow
- Glassmorphism
- Dashboard görünümü
- Bilgi kalabalığı
- Gereksiz animasyon
- Aynı ekranda birden fazla ana odak

---

# 15. Son Hüküm

Bu doküman FORMAX'ın resmî UI standartlarını tanımlar.

Tüm ekranlar bu kurallara uymak zorundadır.

Çelişki durumunda belge önceliği aşağıdaki gibidir.

1. 01_URUN_ANAYASASI.md
2. 02_UI_KURAL_KITABI.md
3. 03_TASARIM_SISTEMI.md
4. 04_MOTION_SISTEMI.md

Bu doküman Product Owner onayı olmadan değiştirilemez.