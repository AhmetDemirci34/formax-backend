using System;
using Formax.Domain.Constants;

namespace Formax.Application.Services.Standings
{
    /// <summary>
    /// BİR MAÇIN TURNUVA AŞAMASI. Puan tablosuna YALNIZ <see cref="LeaguePhase"/> girer.
    /// </summary>
    public enum CompetitionPhase
    {
        /// <summary>Aşama kesin çözülemedi. Puan tablosuna ASLA girmez.</summary>
        Unknown = 0,

        /// <summary>1./2./3. eleme turu.</summary>
        Qualifying,

        /// <summary>Eleme play-off'u (lig aşamasına giriş turu) — Ağustos.</summary>
        QualifyingPlayoff,

        /// <summary>Lig aşaması. TABLOYA GİREN TEK AŞAMA.</summary>
        LeaguePhase,

        /// <summary>Son 16 öncesi eleme turu (knockout phase play-off) — Şubat.</summary>
        KnockoutPlayoff,

        RoundOf16,
        QuarterFinal,
        SemiFinal,
        Final,

        /// <summary>Ulusal lig maçı (normal sezon haftası).</summary>
        DomesticLeague
    }

    /// <summary>
    /// AŞAMA ÇÖZÜCÜ — sağlayıcının GERÇEK tur adından (Match.Round) aşama üretir.
    ///
    /// NEDEN MERKEZÎ: "play-off" kelimesine basit <c>contains</c> bakmak İKİ FARKLI aşamayı
    /// birbirine karıştırır — Ağustos'taki ELEME PLAY-OFF'u ile Şubat'taki KNOCKOUT PHASE
    /// PLAY-OFF'u aynı değildir. İkisi de tablo dışıdır ama ayrı aşamalardır ve ayrı
    /// raporlanır. Sınıflandırma tek yerde, açık ve test edilebilir olsun diye buradadır.
    ///
    /// TAHMİN YOK: tanınmayan ya da BOŞ tur adı <see cref="CompetitionPhase.Unknown"/>
    /// döner. Ölçüldü (01.09.2026): depodaki UEFA maçlarının büyük kısmında Round NULL'dur
    /// (Round kolonu sonradan eklendi, eski kayıtlara işlenmedi). Bu maçların lig aşaması
    /// SAYILMASI, eleme sonuçlarından sahte bir puan tablosu üretirdi — tam da düzeltilen
    /// hata buydu. Bu yüzden Unknown, "lig aşaması olabilir" değil, "tabloya giremez"dir.
    /// </summary>
    public static class CompetitionPhaseResolver
    {
        /// <summary>Aşama çözülemediğinde tabloyu engelleyen tanı kodu.</summary>
        public const string PhaseUnresolvedCode = "STANDINGS_PHASE_UNRESOLVED";

        /// <summary>
        /// <paramref name="round"/> = sağlayıcının verdiği ham tur adı (Match.Round).
        /// <paramref name="leagueId"/> ulusal ligse tur adı aranmaz: ulusal lig maçı
        /// zaten tablonun doğal girdisidir.
        /// </summary>
        public static CompetitionPhase Resolve(int leagueId, string? round)
        {
            if (LockedCompetitions.IsDomestic(leagueId))
                return CompetitionPhase.DomesticLeague;

            if (!LockedCompetitions.IsUefa(leagueId))
                return CompetitionPhase.Unknown;

            if (string.IsNullOrWhiteSpace(round))
                return CompetitionPhase.Unknown;

            var r = Normalize(round);

            // ── SIRA ÖNEMLİ ───────────────────────────────────────────────────
            // "knockout ... play-off" ile "qualifying play-off" ayrımı, düz "play-off"
            // kontrolünden ÖNCE yapılmalıdır; aksi hâlde ikisi tek kovaya düşer.
            if (Has(r, "knockout") && IsPlayoffWord(r)) return CompetitionPhase.KnockoutPlayoff;
            if (Has(r, "knockout") && Has(r, "round"))  return CompetitionPhase.KnockoutPlayoff;

            if (Has(r, "qualifying") && IsPlayoffWord(r)) return CompetitionPhase.QualifyingPlayoff;
            if (Has(r, "qualifying") || Has(r, "preliminary")) return CompetitionPhase.Qualifying;

            // Sağlayıcı eleme play-off'unu çıplak "Play-offs" / "Playoff round" olarak da
            // yazıyor (ölçüldü: UCL/UEL "Play-offs", UECL "Playoff round" — hepsi Ağustos,
            // yani lig aşamasından ÖNCE). "knockout"/"qualifying" nitelemesi olmayan çıplak
            // play-off bu ailedendir.
            if (IsPlayoffWord(r)) return CompetitionPhase.QualifyingPlayoff;

            if (Has(r, "league phase") || Has(r, "league stage")) return CompetitionPhase.LeaguePhase;

            if (Has(r, "round of 16") || Has(r, "1/8"))  return CompetitionPhase.RoundOf16;
            if (Has(r, "quarter"))                       return CompetitionPhase.QuarterFinal;
            if (Has(r, "semi"))                          return CompetitionPhase.SemiFinal;
            if (Has(r, "final"))                         return CompetitionPhase.Final;

            return CompetitionPhase.Unknown;
        }

        /// <summary>Bu aşama puan tablosuna girer mi?</summary>
        public static bool CountsTowardStandings(CompetitionPhase phase)
            => phase == CompetitionPhase.LeaguePhase || phase == CompetitionPhase.DomesticLeague;

        /// <summary>
        /// UEFA maçının kullanıcıya puan tablosu gösterilebilir mi?
        /// Knockout aşamalarında SON TAMAMLANMIŞ lig aşaması tablosu gösterilebilir;
        /// eleme aşamalarında tablo YOKTUR.
        /// </summary>
        public static bool IsKnockoutStage(CompetitionPhase phase)
            => phase is CompetitionPhase.KnockoutPlayoff
                     or CompetitionPhase.RoundOf16
                     or CompetitionPhase.QuarterFinal
                     or CompetitionPhase.SemiFinal
                     or CompetitionPhase.Final;

        /// <summary>Eleme ailesi (lig aşaması öncesi).</summary>
        public static bool IsQualifyingStage(CompetitionPhase phase)
            => phase is CompetitionPhase.Qualifying or CompetitionPhase.QualifyingPlayoff;

        // "Regular Season - 3" gibi ulusal tur adları UEFA'da beklenmez; küçük harfe indir,
        // tireleri boşluğa çevir ki "Play-offs"/"play off"/"playoff" aynı kovaya düşsün.
        private static string Normalize(string round)
            => round.Trim().ToLowerInvariant().Replace('-', ' ').Replace('_', ' ');

        private static bool Has(string haystack, string needle)
            => haystack.Contains(needle, StringComparison.Ordinal);

        private static bool IsPlayoffWord(string r)
            => Has(r, "play off") || Has(r, "playoff") || Has(r, "play offs") || Has(r, "playoffs");
    }
}
