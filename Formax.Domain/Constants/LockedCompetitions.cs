using System.Collections.Generic;
using System.Linq;

namespace Formax.Domain.Constants
{
    /// <summary>
    /// KİLİTLİ MÜSABAKA KAPSAMI — 11 organizasyonun TEK merkezî kaynağı.
    ///
    /// NEDEN VAR: aynı 11 id job'larda, resolver'da, metadata sorgularında ve raporlarda
    /// tekrar tekrar elle yazılıyordu; "7 lig" ifadesi (yalnız metadata kaydı olan ligler)
    /// yanlışlıkla ÜRÜN KAPSAMI gibi kullanılıyordu. Kapsam artık burada tanımlıdır ve
    /// başka hiçbir yerde yeniden listelenmez.
    ///
    /// ÖNEMLİ AYRIM:
    ///  • <see cref="Domestic"/> (8) NORMAL LİG TABLOSU üretir.
    ///  • <see cref="Uefa"/> (3) üretmez: bu turnuvalarda yalnız LİG AŞAMASI maçları
    ///    puan tablosuna girer; eleme ve eleme sonrası turlar girmez.
    ///
    /// Buradaki liste ÜRÜN KARARIDIR (kilitli). Çalışma zamanı daraltması ayrı bir
    /// konudur ve <c>Coverage:LeagueAllowList</c> ile yapılır.
    /// </summary>
    public static class LockedCompetitions
    {
        // ── Ulusal ligler (normal puan tablosu) ────────────────────────────────
        public const int PremierLeague = 39;
        public const int Championship  = 40;
        public const int LaLiga        = 140;
        public const int SerieA        = 135;
        public const int Bundesliga    = 78;
        public const int Ligue1        = 61;
        public const int SuperLig      = 203;
        public const int Eredivisie    = 88;

        // ── UEFA turnuvaları (yalnız lig aşaması tablosu) ──────────────────────
        public const int ChampionsLeague  = 2;
        public const int EuropaLeague     = 3;
        public const int ConferenceLeague = 848;

        /// <summary>Normal lig tablosu üreten 8 ulusal lig.</summary>
        public static readonly IReadOnlyList<int> Domestic = new[]
        {
            PremierLeague, Championship, LaLiga, SerieA,
            Bundesliga, Ligue1, SuperLig, Eredivisie
        };

        /// <summary>Aşama-duyarlı işlenen 3 UEFA turnuvası.</summary>
        public static readonly IReadOnlyList<int> Uefa = new[]
        {
            ChampionsLeague, EuropaLeague, ConferenceLeague
        };

        /// <summary>Kilitli kapsamın tamamı — 11 organizasyon.</summary>
        public static readonly IReadOnlyList<int> All =
            Domestic.Concat(Uefa).ToArray();

        private static readonly HashSet<int> DomesticSet = Domestic.ToHashSet();
        private static readonly HashSet<int> UefaSet     = Uefa.ToHashSet();
        private static readonly HashSet<int> AllSet      = All.ToHashSet();

        public static bool IsLocked(int leagueId)   => AllSet.Contains(leagueId);

        /// <summary>Bu lig NORMAL puan tablosu üretir mi?</summary>
        public static bool IsDomestic(int leagueId) => DomesticSet.Contains(leagueId);

        /// <summary>
        /// Bu lig UEFA turnuvası mı? true ise puan tablosu YALNIZ lig aşamasından üretilir;
        /// eleme/knockout maçları tabloya ve tamlık hesabına GİRMEZ.
        /// </summary>
        public static bool IsUefa(int leagueId)     => UefaSet.Contains(leagueId);
    }
}
