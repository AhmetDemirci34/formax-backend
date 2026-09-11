using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// RESMÎ KADRO YOKLAMA TAKVİMİ — saf karar, ağ ve veritabanı olmadan sınanabilir.
    ///
    /// ÖLÇÜLEN HATA (06.09.2026): pencere <c>[kickoff−45dk, kickoff+10dk]</c> idi ve
    /// arayüz "Kadrolar maçtan 1 saat önce açıklanacak" diye KESİN bir söz veriyordu.
    /// İkisi birbiriyle çelişiyordu: maça 50 dakika kalmışken sistem henüz hiç
    /// sormamış oluyor, kullanıcı ise "1 saat önce açıklanır" yazısını okuyup boş
    /// ekrana bakıyordu. Kadro her zaman tam 1 saat önce yayımlanmaz — yayıncıya ve
    /// lige göre T−90 ile T−20 arasında değişir.
    ///
    /// YENİ PENCERE: T−90 → T−5. Dört slot: T−90, T−60, T−30, T−10.
    ///
    /// SLOT MANTIĞI (neden "her turda bir kez" değil): iş 5 dakikada bir dönüyor.
    /// Sınır yalnız soğumaya bırakılsaydı, kadrosu hiç yayımlanmayan bir maç
    /// pencerede 18 kez yoklanırdı. Slotlar, kickoff'a kalan süreyi dört kovaya
    /// böler; her kovadan EN FAZLA BİR gerçek istek çıkar. Hangi kovanın harcandığı
    /// KALICI deftere yazılır — restart, harcanmış slotu geri getirmez.
    /// </summary>
    public static class LineupPollSchedule
    {
        /// <summary>Yoklamanın başladığı an: kickoff'a kalan süre bunun altına düştüğünde.</summary>
        public static readonly TimeSpan WindowOpen = TimeSpan.FromMinutes(90);

        /// <summary>Yoklamanın bittiği an: kickoff'a bundan az kaldıysa artık sorulmaz.</summary>
        public static readonly TimeSpan WindowClose = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Slot sınırları — kickoff'a kalan DAKİKA cinsinden, genişten dara.
        /// Kalan süre 90..61 → slot 0, 60..31 → slot 1, 30..11 → slot 2, 10..5 → slot 3.
        /// </summary>
        public static readonly IReadOnlyList<int> SlotBoundariesMinutes = new[] { 90, 60, 30, 10 };

        /// <summary>Toplam slot sayısı = bir maç için üretilebilecek en fazla gerçek istek.</summary>
        public static int SlotCount => SlotBoundariesMinutes.Count;

        /// <summary>
        /// Kickoff'a <paramref name="nowUtc"/> anında kalan süre penceredeyse, o anın
        /// düştüğü slot indeksini döndürür; pencere dışındaysa null.
        /// </summary>
        public static int? SlotFor(DateTime kickoffUtc, DateTime nowUtc)
        {
            var remaining = kickoffUtc - nowUtc;

            // T−90'dan ERKEN: hiçbir sağlayıcı kadroyu bu kadar önce yayımlamaz.
            if (remaining > WindowOpen) return null;

            // Kickoff'a 5 dakikadan az kaldıysa / kickoff geçtiyse: kadro yoklaması biter.
            if (remaining < WindowClose) return null;

            var minutes = remaining.TotalMinutes;
            for (var i = 0; i < SlotBoundariesMinutes.Count; i++)
            {
                var upper = SlotBoundariesMinutes[i];
                var lower = i + 1 < SlotBoundariesMinutes.Count
                    ? SlotBoundariesMinutes[i + 1]
                    : (int)WindowClose.TotalMinutes;

                if (minutes <= upper && minutes > lower) return i;
                // Son slotta alt sınır DAHİLDİR (tam 5 dk kala hâlâ sorulabilir).
                if (i == SlotBoundariesMinutes.Count - 1 && minutes <= upper && minutes >= lower) return i;
            }
            return null;
        }

        /// <summary>
        /// GERÇEK İSTEK YAPILMALI MI?
        ///
        /// Üç koşul birlikte aranır:
        ///  • kickoff'a kalan süre penceredeyse (bir slota düşüyorsa),
        ///  • kadro DB'de HENÜZ TAM DEĞİLSE (başarılı kadro bir daha istenmez),
        ///  • bu maç için harcanmış slot sayısı, içinde bulunulan slotun indeksinden
        ///    küçükse veya eşitse — yani bu slot henüz kullanılmamışsa.
        ///
        /// <paramref name="attemptsSoFar"/> KALICI defterden okunur; süreç belleğinden
        /// değil. Restart bu sayıyı sıfırlamaz.
        /// </summary>
        public static bool ShouldPoll(
            DateTime kickoffUtc,
            DateTime nowUtc,
            bool lineupAlreadyComplete,
            int attemptsSoFar)
        {
            if (lineupAlreadyComplete) return false;
            if (attemptsSoFar >= SlotCount) return false;

            var slot = SlotFor(kickoffUtc, nowUtc);
            if (slot == null) return false;

            // Slot atlanmışsa (iş o aralıkta çalışmadıysa) hak yanmaz: bu slotta
            // yapılan istek, harcanan slot sayısını mevcut slota kadar ilerletir.
            return attemptsSoFar <= slot.Value;
        }
    }
}
