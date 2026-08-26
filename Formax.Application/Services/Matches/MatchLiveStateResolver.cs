using System;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// Maçın CANLI DURUMU — "Canlı Takip" ekranının durum kararı.
    ///
    /// TEMEL KURAL: <b>zaman penceresi canlılığın kanıtı DEĞİLDİR.</b> Bir maç ancak
    /// GLOBAL kaynaklar şu anda oynandığını doğruluyorsa "Live" gösterilir. Sağlayıcının
    /// bayat <c>Status='Live'</c> satırı tek başına asla canlı saydırmaz (canlı alım kapalı
    /// olduğu için dünkü maçlar hâlâ "Live" görünüyor).
    ///
    /// Zaman yalnız OLUMSUZLAMA için kullanılır: ilk düdüğün üzerinden
    /// <see cref="MaxPlausibleLiveMinutes"/> dakikadan fazla geçtiyse maç fiziksel olarak
    /// oynanıyor olamaz. Bu, canlılık iddiası değil, canlılık İNKÂRIDIR.
    ///
    /// Doğrulanamayan durum <see cref="Unknown"/>'dır — "CANLI" etiketi GÖSTERİLMEZ.
    /// Yanlış canlı bilgi vermek, eksik bilgi vermekten kötüdür.
    /// </summary>
    public static class MatchLiveStateResolver
    {
        public const string NotStarted = "NotStarted";
        public const string Live       = "Live";
        public const string Finished   = "Finished";
        /// <summary>Maç saati geçti ama ne canlı olduğu ne bittiği doğrulanabildi.</summary>
        public const string Unknown    = "Unknown";

        /// <summary>Bir maçın fiziksel olarak sürebileceği en uzun süre (uzatma + penaltılar).</summary>
        public const int MaxPlausibleLiveMinutes = 210;

        /// <summary>
        /// Bir global gelişmenin "maç ŞU AN oynanıyor" kanıtı sayılabilmesi için azami tazeliği.
        /// Bundan eski içerik maçın hâlâ sürdüğünü kanıtlamaz.
        /// </summary>
        public const int LiveEvidenceFreshnessMinutes = 25;

        /// <summary>Durum kararını besleyen GERÇEK sinyaller — hiçbiri tahmin değildir.</summary>
        public readonly struct Signals
        {
            /// <summary>Maç kaydı bitmiş/iptal olarak işaretli (DB gerçeği).</summary>
            public bool RecordSaysFinished { get; init; }

            /// <summary>Kayıtlı skorun devresi final (FT/AET/PEN) — DB gerçeği.</summary>
            public bool RecordPhaseIsFinal { get; init; }

            /// <summary>Global bir kaynak maçın bittiğini bildiriyor.</summary>
            public bool GlobalSaysFullTime { get; init; }

            /// <summary>
            /// Global bir kaynak, TAZE (bkz. <see cref="LiveEvidenceFreshnessMinutes"/>) ve
            /// maçın içinden bir gelişme yayımladı → maç şu an oynanıyor.
            /// </summary>
            public bool GlobalConfirmsInPlay { get; init; }

            /// <summary>Maç ertelendi (kayıt gerçeği).</summary>
            public bool RecordSaysPostponed { get; init; }
        }

        public static string Resolve(DateTime kickoffUtc, DateTime nowUtc, in Signals signals)
        {
            if (signals.RecordSaysPostponed) return NotStarted;
            if (nowUtc < kickoffUtc)         return NotStarted;

            // BİTTİĞİ DOĞRULANDI (kayıt ya da global kaynak).
            if (signals.RecordSaysFinished || signals.RecordPhaseIsFinal || signals.GlobalSaysFullTime)
                return Finished;

            // Fiziksel olarak sürmesi imkânsız → canlı DEĞİL. Bittiğini de kimse
            // doğrulamadığı için "bitti" DEMEYİZ; durum bilinmiyor.
            if (nowUtc > kickoffUtc.AddMinutes(MaxPlausibleLiveMinutes))
                return Unknown;

            // CANLI ancak global kaynak doğrularsa. Saat penceresi TEK BAŞINA yetmez.
            if (signals.GlobalConfirmsInPlay) return Live;

            return Unknown;
        }

        /// <summary>Kullanıcıya gösterilecek dürüst durum cümlesi.</summary>
        public static string? Message(string state) => state switch
        {
            NotStarted => "Canlı takip henüz başlamadı.",
            Live       => null, // Akışın kendisi konuşur.
            Finished   => "Maç sona erdi.",
            Unknown    => "Maçın canlı olarak devam ettiği global kaynaklardan doğrulanamadı.",
            _          => null
        };
    }
}
