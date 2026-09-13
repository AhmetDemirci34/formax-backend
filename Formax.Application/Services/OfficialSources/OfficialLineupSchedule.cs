using System;
using System.Collections.Generic;

namespace Formax.Application.Services.OfficialSources
{
    /// <summary>
    /// RESMÎ KADRO ARAMA TAKVİMİ — saf karar, ağ ve DB yok.
    ///
    /// SLOTLAR (kickoff'a kalan dakika): T−60, T−45, T−30, T−20, T−15, T−10, T−5 ve
    /// gerekirse kickoff+10 son kontrol. Arama kickoff'tan 60 dakika önce BAŞLAR.
    ///
    /// KARAR KURALI — "zamanı gelmiş en yakın denenmemiş slot" (API-Football döneminden
    /// korunan ve Venezia–Fiorentina hatasıyla kanıtlanan kural):
    ///  • Son GERÇEK kontrol hangi slota düştüyse o slot ve öncesi harcanmıştır.
    ///  • Kaçırılan slotlar birikmez: tek istek çıkar.
    ///  • Kadro bulunduysa (iki taraf doğrulandı) arama durur.
    ///  • "Gerçek kontrol" yalnız kaynak GEÇERLİ cevap verdiğinde yazılır (kadro ya da boş);
    ///    ağ/güvenlik/biçim hatası kontrol SAYILMAZ, slot açık kalır.
    ///  • Boş cevap başarı DEĞİLDİR ve sonraki slotu engellemez.
    /// </summary>
    public static class OfficialLineupSchedule
    {
        /// <summary>Aramanın başladığı an: kickoff'a 60 dakika kala.</summary>
        public static readonly TimeSpan WindowOpen = TimeSpan.FromMinutes(60);

        /// <summary>Son kontrol slotu: kickoff + 10 dakika.</summary>
        public static readonly TimeSpan FinalCheckAfterKickoff = TimeSpan.FromMinutes(10);

        /// <summary>Son slotun geç kalan tur tarafından yakalanabileceği pay.</summary>
        public static readonly TimeSpan FinalCheckTolerance = TimeSpan.FromMinutes(5);

        /// <summary>Slot anları — kickoff'a kalan DAKİKA; negatif değer kickoff sonrasıdır.</summary>
        public static readonly IReadOnlyList<int> SlotMinutesBeforeKickoff = new[] { 60, 45, 30, 20, 15, 10, 5, -10 };

        public static int SlotCount => SlotMinutesBeforeKickoff.Count;

        /// <summary>Anına kadar zamanı gelmiş en geç slotun indeksi; T−60'tan önce -1.</summary>
        public static int SlotOfCheck(DateTime kickoffUtc, DateTime atUtc)
        {
            var remaining = (kickoffUtc - atUtc).TotalMinutes;
            var index = -1;
            for (var i = 0; i < SlotMinutesBeforeKickoff.Count; i++)
                if (remaining <= SlotMinutesBeforeKickoff[i]) index = i;
            return index;
        }

        /// <summary>Şu an çalıştırılabilecek slot; pencere dışında null.</summary>
        public static int? DueSlot(DateTime kickoffUtc, DateTime nowUtc)
        {
            var remaining = kickoffUtc - nowUtc;
            if (remaining > WindowOpen) return null;
            if (remaining < -(FinalCheckAfterKickoff + FinalCheckTolerance)) return null;
            var slot = SlotOfCheck(kickoffUtc, nowUtc);
            return slot < 0 ? null : slot;
        }

        /// <summary>Resmî kaynağa şimdi kadro sorulmalı mı?</summary>
        public static bool ShouldCheck(DateTime kickoffUtc, DateTime nowUtc, bool lineupComplete, DateTime? lastRealCheckUtc)
        {
            if (lineupComplete) return false;
            var due = DueSlot(kickoffUtc, nowUtc);
            if (due == null) return false;
            if (lastRealCheckUtc is not DateTime last) return true;
            return SlotOfCheck(kickoffUtc, last) < due.Value;
        }

        /// <summary>Kadro arama penceresi açık mı (T−60 … kickoff+10)?</summary>
        public static bool IsWindowOpen(DateTime kickoffUtc, DateTime nowUtc)
        {
            var remaining = kickoffUtc - nowUtc;
            return remaining <= WindowOpen && remaining >= -FinalCheckAfterKickoff;
        }
    }
}
