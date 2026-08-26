/**
 * FORMAX · Auth Gate bayrağı — GEÇİCİ.
 *
 * UI henüz bitmediği için giriş zorunluluğu KAPALI: kök (/), bildirimler ve
 * onboarding ekranları kullanıcı adı/şifre istemeden açılır.
 *
 * TEKRAR AKTİFLEŞTİRMEK İÇİN: aşağıdaki değeri `true` yap. Başka hiçbir dosyayı
 * değiştirmek gerekmez — guard'ların hepsi bu tek bayrağı okur.
 *
 * Bayrak KAPALIYKEN neler etkilenir:
 *  • /auth/login rotası ve AuthContext DURUYOR (silinmedi) — sadece zorunlu değil.
 *  • Token gerektiren uçlar (takip/bildirim/abonelik hook'ları `enabled: isLoggedIn`)
 *    giriş yapılmadığı sürece veri çekmez; bu kasıtlıdır, backend 401 döndürmesin diye.
 *  • 401 yanıtı login'e YÖNLENDİRMEZ (`lib/api/client.ts`): süresi dolmuş token sadece
 *    temizlenir. Yönlendirme açıkken, localStorage'da kalmış eski bir token tek bir
 *    401 ile kullanıcıyı açılışta login ekranına fırlatıyordu.
 *  • Takip düğmesi anonimken login'e atmaz, sessizce yok sayar (`hooks/useFollow.ts`).
 */
export const AUTH_GATE_ENABLED = false;
