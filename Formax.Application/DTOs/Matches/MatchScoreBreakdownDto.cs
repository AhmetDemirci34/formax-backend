using Formax.Domain.Entities;

namespace Formax.Application.DTOs.Matches
{
    /// <summary>
    /// MAÇ SKOR KIRILIMI — İY / 2Y / MS.
    ///
    /// NEDEN BACKEND'DE: ikinci yarı skoru bir ÇIKARMA işlemidir ve tek bir yerde
    /// yapılmalıdır. Frontend'e bırakılırsa her yüzey kendi kuralını yazar, eksik veride
    /// 0-0 uydurma riski her ekranda ayrı ayrı doğar.
    ///
    /// DÜRÜSTLÜK KURALI: hiçbir alan tahmin edilmez.
    ///  • İY yoksa <see cref="HalfTime"/> null → ekran "İY —" der.
    ///  • MS yoksa <see cref="FullTime"/> null → "MS —".
    ///  • 2Y YALNIZ ikisi de geçerliyse hesaplanır; aksi hâlde null.
    ///  • Çıkarma negatif çıkarsa VERİ HATASIDIR: null döner, ekranda gösterilmez.
    ///
    /// UZATMA/PENALTI: bu tip 90 DAKİKA sonucunu taşır. Uzatma ve penaltı ayrı
    /// alanlardır ve 90 dakika skoruyla KARIŞTIRILMAZ (depoda ayrı kaynak gerektirir;
    /// kaynak yoksa null kalır, uydurulmaz).
    /// </summary>
    public sealed class MatchScoreBreakdownDto
    {
        /// <summary>İlk yarı. Sağlayıcı vermediyse null.</summary>
        public ScoreDto? HalfTime { get; init; }

        /// <summary>Yalnız ikinci yarıda atılan goller (MS − İY). Türetilemiyorsa null.</summary>
        public ScoreDto? SecondHalf { get; init; }

        /// <summary>90 dakika sonu skoru. Maç bitmediyse/skor yoksa null.</summary>
        public ScoreDto? FullTime { get; init; }

        /// <summary>Uzatma sonucu — ayrı kaynak gerektirir; yoksa null.</summary>
        public ScoreDto? ExtraTime { get; init; }

        /// <summary>Penaltı seri sonucu — ayrı kaynak gerektirir; yoksa null.</summary>
        public ScoreDto? Penalties { get; init; }

        /// <summary>
        /// Bir maçtan kırılımı üretir. <paramref name="resultIsFinal"/> false ise
        /// (maç bitmemiş) MS ve 2Y üretilmez — oynanmamış maçın 0-0'ı sonuç değildir.
        /// </summary>
        public static MatchScoreBreakdownDto From(Match match, bool resultIsFinal)
        {
            ScoreDto? half = match.HalfTimeHomeScore.HasValue && match.HalfTimeAwayScore.HasValue
                ? new ScoreDto { Home = match.HalfTimeHomeScore.Value, Away = match.HalfTimeAwayScore.Value }
                : null;

            ScoreDto? full = resultIsFinal
                ? new ScoreDto { Home = match.HomeScore, Away = match.AwayScore }
                : null;

            ScoreDto? second = null;
            if (half != null && full != null)
            {
                var h = full.Home - half.Home;
                var a = full.Away - half.Away;
                // NEGATİF = VERİ HATASI (İY skoru MS'den büyük olamaz). Gösterilmez.
                if (h >= 0 && a >= 0) second = new ScoreDto { Home = h, Away = a };
            }

            return new MatchScoreBreakdownDto
            {
                HalfTime   = half,
                SecondHalf = second,
                FullTime   = full,
                // Uzatma/penaltı için depoda ayrı alan YOKTUR; uydurulmaz.
                ExtraTime  = null,
                Penalties  = null
            };
        }
    }
}
