# 03_TASARIM_SISTEMI.md

# FORMAX Design System

Version: 1.0 (LOCKED)

---

# 1. Amaç

Bu doküman, FORMAX ürün ailesinde kullanılacak ortak tasarım sistemini tanımlar.

Amaç;

- Görsel tutarlılığı sağlamak
- Ortak UI dili oluşturmak
- Component tasarımlarını standartlaştırmak
- Yeni ekranlarda tasarım kararı alınmasını önlemektir.

Bu doküman yalnızca görsel sistemi tanımlar.

UX davranışları, Motion ve Experience akışları bu dokümanın kapsamı dışındadır.

---

# 2. Kapsam

Bu doküman aşağıdaki ekranlar için geçerlidir.

- Splash
- Login
- Discovery Feed
- Match Intelligence
- Matches
- Live Match
- News
- AI Comments
- Market Intelligence
- Profile
- Notifications
- Following
- Settings

Tüm yeni ekranlar bu sisteme uymak zorundadır.

---

# 3. Tasarım Felsefesi

FORMAX'ın tasarım dili aşağıdaki ilkeler üzerine kuruludur.

- Premium
- Minimal
- AI First
- Human Centered
- Information First
- Silent Interface

Arayüz dikkat çekmez.

Bilgi dikkat çeker.

AI deneyimi yönlendirir.

---

# 4. Tasarım Prensipleri

Her ekran aşağıdaki prensiplere uymalıdır.

- Tutarlılık
- Basitlik
- Okunabilirlik
- Tek Odak Noktası
- Görsel Denge
- Tahmin Edilebilirlik

Her ekran tek bir ana amacı yerine getirir.

---

# 5. Renk Sistemi

## 5.1 Tema

Dark Theme zorunludur.

Light Theme desteklenmez.

---

## 5.2 Renk Rolleri

Renkler yalnızca aşağıdaki amaçlarla kullanılabilir.

- Primary
- Secondary
- Surface
- Border
- Text
- Success
- Warning
- Error
- Information

Semantik renkler yalnızca sistem durumu veya ürün tarafından tanımlanmış AI sinyallerini göstermek için kullanılabilir.

---

## 5.3 Accent

Accent renk yalnızca kullanıcı odağını yönlendirmek amacıyla kullanılabilir.

Accent dekoratif amaçla kullanılamaz.

---

## 5.4 Arka Plan

Arka plan sade olmalıdır.

Desen kullanılmaz.

Gradient kullanılmaz.

Glow kullanılmaz.

Glass Effect kullanılmaz.

---

# 6. Tipografi Sistemi

Tipografi bilgi hiyerarşisini desteklemek için kullanılır.

## Katmanlar

- Display
- Heading
- Title
- Body
- Caption
- Label

---

## Yazı Kuralları

Metinler kısa ve okunabilir olmalıdır.

Büyük metin bloklarından kaçınılmalıdır.

AI mesajları kolay okunmalıdır.

Evidence metinleri AI mesajlarını desteklemelidir.

---

## Sayısal Veriler

Skorlar

Dakikalar

İstatistikler

Yüzdeler

aynı hizalama mantığı ile gösterilmelidir.

---

# 7. Spacing Sistemi

Tüm arayüz ortak spacing sistemi kullanır.

Dikey ritim korunmalıdır.

Component'ler rastgele boşluk kullanamaz.

Boşluklar hiyerarşi oluşturmalıdır.

---

# 8. Radius Sistemi

Köşe yarıçapları sistem genelinde tutarlı olmalıdır.

Kartlar

Butonlar

Input alanları

Chip'ler

aynı radius ailesini kullanmalıdır.

---

# 9. Elevation Sistemi

FORMAX düz yüzey tasarımını benimser.

Elevation yalnızca katman ayrımı gerektiğinde kullanılabilir.

Gölge dekorasyon amacıyla kullanılamaz.

Z-index yalnızca teknik katman ihtiyaçları için kullanılmalıdır.

---

# 10. İkonografi

Tüm ikonlar aynı stil ailesinden seçilmelidir.

Karışık ikon setleri kullanılmaz.

İkonlar metni desteklemek için kullanılır.

İkon hiçbir zaman tek başına bilgi taşımaz.

---

# 11. Görsel Sistemi

Hero görseller yalnızca ilgili Experience'i temsil eder.

Takım logoları orijinal oranlarını korur.

Oyuncu görselleri kırpılmaz.

Haber görselleri dikkat dağıtmayacak şekilde kullanılır.

Her Experience yalnızca bir Hero Visual kullanır.

---

# 12. Bileşen Tasarım Kuralları

Tüm bileşenler Shared Component mimarisine uymalıdır.

Component'ler kendi tasarım dili oluşturamaz.

Yeni component tasarlanamaz.

Mevcut component genişletilebilir.

Ortak davranış korunmalıdır.

---

# 13. Erişilebilirlik

Arayüz her zaman okunabilir olmalıdır.

Yeterli metin kontrastı sağlanmalıdır.

Dokunulabilir alanlar erişilebilir boyutta olmalıdır.

Metinler farklı ekran boyutlarında okunabilir kalmalıdır.

---

# 14. Responsive Kuralları

Tasarım farklı ekran boyutlarında aynı bilgi hiyerarşisini korur.

Metin taşmaları kontrollü şekilde yönetilir.

Component oranları bozulamaz.

---

# 15. Yasaklar

Aşağıdaki tasarım kararları yasaktır.

- Gradient
- Glow
- Glassmorphism
- Dashboard görünümü
- Gereksiz dekorasyon
- Bilgi kalabalığı
- Gereksiz animasyon
- Aynı ekranda birden fazla ana odak noktası

---

# 16. Son Hüküm

Tüm ekranlar bu tasarım sistemine uymak zorundadır.

Çelişki durumunda belge önceliği aşağıdaki gibidir.

1. 01_URUN_ANAYASASI.md
2. 02_UI_KURAL_KITABI.md
3. 03_TASARIM_SISTEMI.md

Bu doküman Product Owner onayı olmadan değiştirilemez.