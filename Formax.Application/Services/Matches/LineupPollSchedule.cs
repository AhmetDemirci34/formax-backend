using System;
using System.Collections.Generic;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// RESMÎ KADRO YOKLAMA TAKVİMİ — saf karar, ağ ve veritabanı olmadan sınanabilir.
    ///
    /// SLOTLAR: T−90, T−60, T−30, T−15, T−10, T−5 (kickoff'a kalan dakika).
    ///
    /// ÖLÇÜLEN HATA (11.09.2026, Venezia–Fiorentina, fikstür 1550126): eski takvim dört
    /// slottu (T−90/60/30/10), pencere T−5'te kesin kapanıyordu ve fikstür başına 20 dk
    /// soğuma vardı. T−28,5'teki boş cevaptan sonra 5 dakikalık döngü T−9,6'da T−10
    /// slotuna denk geldi ama soğuma (T−8,5'e kadar) rezervasyonu reddetti; bir sonraki
    /// tur T−4,6'daydı ve pencere kapanmıştı. Kadro dünyada T−13'te yayımlıyken FORMAX
    /// son 28 dakikada sağlayıcıya HİÇ sormadı.
    ///
    /// KARAR KURALI — "zamanı gelmiş en yakın denenmemiş slot":
    ///  • Zamanı gelmiş en geç slot <see cref="DueSlot"/>'tur (ör. T−12'de T−15).
    ///  • Son GERÇEK kontrol hangi slota düştüyse (<see cref="SlotOfCheck"/>) o slot ve
    ///    öncesi harcanmıştır. Daha geç bir slotun zamanı geldiyse TEK istek yapılır.
    ///  • Kaçırılan slotlar birikmez: iş T−35'ten T−12'ye kadar çalışmadıysa T−12'de
    ///    tek istek çıkar (T−30 ve T−15 için iki ayrı istek DEĞİL).
    ///  • Aynı slotta ikinci istek çıkmaz; son kontrol anı KALICIDIR (restart sıfırlamaz).
    ///  • Son slot (T−5) zamanında çalışamadıysa kickoff'tan sonra <see cref="CatchUpGrace"/>
    ///    boyunca yakalanabilir; ondan sonra kadro yoklaması biter.
    ///
    /// "Son gerçek kontrol" yalnız sağlayıcı GEÇERLİ cevap verdiğinde (kadro ya da boş)
    /// yazılır. Bütçe/plan/rate-limit engeli kontrol SAYILMAZ: slot açık kalır.
    /// </summary>
    public static class LineupPollSchedule
    {
        /// <summary>Yoklamanın başladığı an: kickoff'a kalan süre bunun altına düştüğünde.</summary>
        public static readonly TimeSpan WindowOpen = TimeSpan.FromMinutes(90);

        /// <summary>
        /// Son slot kaçırıldıysa kickoff'tan sonra yakalanabileceği en geç süre.
        /// Frontend'in açık ekran yoklaması da kickoff+10'da durur.
        /// </summary>
        public static readonly TimeSpan CatchUpGrace = TimeSpan.FromMinutes(10);

        /// <summary>Slot anları — kickoff'a kalan DAKİKA, genişten dara.</summary>
        public static readonly IReadOnlyList<int> SlotMinutesBeforeKickoff = new[] { 90, 60, 30, 15, 10, 5 };

        /// <summary>Toplam slot = bir maç için geçerli cevaplı en fazla gerçek kontrol.</summary>
        public static int SlotCount => SlotMinutesBeforeKickoff.Count;

        /// <summary>
        /// <paramref name="atUtc"/> anına kadar ZAMANI GELMİŞ en geç slotun indeksi;
        /// T−90'dan önce -1.
        /// </summary>
        public static int SlotOfCheck(DateTime kickoffUtc, DateTime atUtc)
        {
            var remaining = (kickoffUtc - atUtc).TotalMinutes;
            var index = -1;
            for (var i = 0; i < SlotMinutesBeforeKickoff.Count; i++)
                if (remaining <= SlotMinutesBeforeKickoff[i]) index = i;
            return index;
        }

        /// <summary>
        /// Şu an çalıştırılabilecek slot (zamanı gelmiş en geç slot); pencere dışında null.
        /// Pencere: T−90 … kickoff + <see cref="CatchUpGrace"/>.
        /// </summary>
        public static int? DueSlot(DateTime kickoffUtc, DateTime nowUtc)
        {
            var remaining = kickoffUtc - nowUtc;
            if (remaining > WindowOpen) return null;
            if (remaining < -CatchUpGrace) return null;
            var slot = SlotOfCheck(kickoffUtc, nowUtc);
            return slot < 0 ? null : slot;
        }

        /// <summary>
        /// GERÇEK İSTEK YAPILMALI MI?
        /// </summary>
        /// <param name="lastRealCheckUtc">
        /// Sağlayıcının GEÇERLİ cevap verdiği son an (kalıcı kayıttan). Engellenen tur
        /// buraya yazılmaz; hiç kontrol yoksa null.
        /// </param>
        public static bool ShouldPoll(
            DateTime kickoffUtc,
            DateTime nowUtc,
            bool lineupAlreadyComplete,
            DateTime? lastRealCheckUtc)
        {
            // Başarılı kadro bir daha istenmez.
            if (lineupAlreadyComplete) return false;

            var due = DueSlot(kickoffUtc, nowUtc);
            if (due == null) return false;

            // Hiç kontrol yoksa zamanı gelmiş slot çalışır.
            if (lastRealCheckUtc is not DateTime last) return true;

            // Son kontrol bu slotta ya da sonrasında yapıldıysa slot harcanmıştır.
            return SlotOfCheck(kickoffUtc, last) < due.Value;
        }
    }
}
