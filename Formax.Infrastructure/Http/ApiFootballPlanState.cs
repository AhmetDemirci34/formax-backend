using System;

namespace Formax.Infrastructure.Http
{
    /// <summary>
    /// ABONELİK PLANI YETENEK DURUMU — sağlayıcının "bu parametreye erişiminiz yok" reddi GEÇİCİ DEĞİLDİR.
    ///
    /// ÖLÇÜLDÜ (18.09.2026, gerçek cevaplar):
    ///   fixtures?team={id}&amp;next=20  → {"plan":"Free plans do not have access to the Next parameter."}
    ///   fixtures?team={id}&amp;last=5   → {"plan":"Free plans do not have access to the Last parameter."}
    ///   fixtures?league=2&amp;season=2026 → {"plan":"Free plans do not have access to this season, try from 2022 to 2024."}
    ///   fixtures?date=2026-09-20      → {"plan":"Free plans do not have access to this date, try from 2026-09-17 to 2026-09-19."}
    ///
    /// KÖK NEDEN: takım Timeline ayağı (last=/next=) bu reddi ALGILAMIYORDU; boş cevabı "o takımın maçı yok"
    /// sayıp 6 saat ÖNBELLEĞE ALIYOR, işi de "kapsam-dışı" diye damgalıyordu. Yani her tur, hiçbir zaman
    /// veri getirmeyecek isteklere kota harcanıyor ve teşhis yanlış görünüyordu (plan reddi ≠ kapsam yok).
    ///
    /// Bu sınıf o reddi SÜREÇ ÖMRÜ boyunca hatırlar; aynı desen tarih penceresi için zaten vardı
    /// (<c>LearnPlanWindow</c>). Plan yükseltilirse tek bir restart yeterlidir: durum sıfırlanır ve
    /// istekler kendiliğinden yeniden denenir.
    /// </summary>
    public sealed class ApiFootballPlanState
    {
        private readonly object _gate = new();

        /// <summary>
        /// <c>fixtures?team=&amp;last=/next=</c> (takım Timeline penceresi) plan tarafından kapatıldı mı?
        /// Kapalıysa bu uca İSTEK ÜRETİLMEZ.
        /// </summary>
        public bool TeamWindowBlocked { get; private set; }

        /// <summary>Sağlayıcının maskelenmiş ret metni (teşhis).</summary>
        public string? TeamWindowDetail { get; private set; }

        /// <summary>Reddin ilk ölçüldüğü an.</summary>
        public DateTime? TeamWindowBlockedAtUtc { get; private set; }

        /// <summary>İlk kez öğrenildiğinde true döner (çağıran yalnız o zaman uyarı yazar).</summary>
        public bool BlockTeamWindow(string? detail, DateTime nowUtc)
        {
            lock (_gate)
            {
                if (TeamWindowBlocked) return false;
                TeamWindowBlocked = true;
                TeamWindowDetail = string.IsNullOrWhiteSpace(detail) ? null : detail;
                TeamWindowBlockedAtUtc = nowUtc;
                return true;
            }
        }

        /// <summary>Sağlayıcı ret metni takım penceresi (last=/next=) parametresini mi kapatıyor?</summary>
        public static bool IsTeamWindowRestriction(string? detail)
            => !string.IsNullOrWhiteSpace(detail)
               && detail!.IndexOf("do not have access to the", StringComparison.OrdinalIgnoreCase) >= 0
               && (detail.IndexOf("Next parameter", StringComparison.OrdinalIgnoreCase) >= 0
                   || detail.IndexOf("Last parameter", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
