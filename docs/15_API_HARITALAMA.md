# 15_API_HARITALAMA.md

# FORMAX API Mapping

Version: 1.0 (LOCKED)

---

# 1. Amaç

Bu doküman backend ile frontend arasındaki veri eşleştirme kurallarını tanımlar.

---

# 2. Temel İlkeler

- Frontend iş kuralı üretmez.
- Backend tek doğruluk kaynağıdır.
- DTO yapıları korunur.
- Mapping tek yönlüdür.

---

# 3. Veri Akışı

API

↓

Application

↓

Mapping

↓

UI Model

↓

Shared Components

---

# 4. Mapping Kuralları

Her API alanı tek bir UI karşılığına sahip olmalıdır.

Frontend aynı veriyi yeniden üretmez.

Eksik veri tahmin edilmez.

---

# 5. Null Yönetimi

Null alanlar güvenli şekilde işlenmelidir.

UI hata üretmemelidir.

---

# 6. Tarih ve Saat

Tüm zaman bilgileri standart formatta işlenmelidir.

Yerel gösterim UI katmanında yapılır.

---

# 7. Kimlikler

Entity kimlikleri değiştirilmez.

Frontend yalnızca referans olarak kullanır.

---

# 8. Hata Yönetimi

API hataları kullanıcı deneyimini bozmayacak şekilde yönetilir.

---

# 9. Son Hüküm

Backend sözleşmesi Product Owner onayı olmadan değiştirilemez.

Bu doküman API referansı için temel kaynaktır.