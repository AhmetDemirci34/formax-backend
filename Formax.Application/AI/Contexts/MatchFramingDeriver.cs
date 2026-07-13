using System;

namespace Formax.Application.AI.Contexts
{
    /// <summary>
    /// GDP fikstür alanlarından (lig adı + tur etiketi) maç çerçevesini TÜRETİR.
    /// Saf fonksiyon: yan etki yok, DB/IO yok → tek merkezde test edilebilir.
    /// Heuristikler çok dillidir (İngilizce/Almanca/Türkçe/İspanyolca/İtalyanca/Fransızca),
    /// çünkü GDP ücretsiz kaynakları farklı dillerde etiket üretir
    /// (ör. football-data.co.uk = EN, OpenLigaDb = DE).
    ///
    /// Sınır: ImportanceLevel şu an puan durumu (standings) OLMADAN hesaplanır; standings
    /// bağlandığında (P1) bu skor rafine edilebilir. Sahte kesinlik iddia edilmez.
    /// </summary>
    public static class MatchFramingDeriver
    {
        public static MatchFraming Derive(string? competitionName, string? round)
        {
            var competitionType = ResolveCompetitionType(competitionName);
            var isFinal = IsFinalRound(round);
            var isElimination = isFinal || IsEliminationRound(round);
            var importance = ResolveImportance(competitionType, isFinal, isElimination);

            return new MatchFraming
            {
                CompetitionType = competitionType,
                IsFinal = isFinal,
                IsElimination = isElimination,
                ImportanceLevel = importance
            };
        }

        private static CompetitionType ResolveCompetitionType(string? name)
        {
            var n = Normalize(name);
            if (n.Length == 0)
                return CompetitionType.League;

            // Şampiyonlar Ligi (kupa kontrolünden ÖNCE — "cup" içermez ama özel).
            if (Contains(n, "champions league", "uefa champions", "sampiyonlar ligi", "champions-league"))
                return CompetitionType.ChampionsLeague;

            // Kupa / eleme turnuvası kalıpları.
            if (Contains(n,
                    "cup", "kupa", "pokal", "copa", "coppa", "coupe",
                    "trophy", "dfb", "beker", "taca", "supercup", "super cup", "supercopa"))
                return CompetitionType.Cup;

            return CompetitionType.League;
        }

        /// <summary>Final turu mu? "Semi/Quarter final" YANLIŞ pozitifini eler.</summary>
        private static bool IsFinalRound(string? round)
        {
            var r = Normalize(round);
            if (r.Length == 0)
                return false;

            // Alt eleme turları "final/finale" kelimesini İÇERİR (ör. Almanca "Achtelfinale" = son 16,
            // İspanyolca "Cuartos de Final" = çeyrek). Gerçek finali ayırmak için önce bunları ele.
            if (Contains(r,
                    "semi", "quarter", "yari final", "ceyrek",
                    "halbfinale", "viertelfinale", "achtelfinale", "sechzehntelfinale",  // DE: yarı/çeyrek/son16/son32
                    "octavos", "cuartos", "sedicesimi", "ottavi", "quarti",             // ES/IT alt turlar
                    "round of", "last 16", "last 8",
                    "1/2", "1/4", "1/8", "1/16"))
                return false;

            return Contains(r, "final", "finale", "final maci");
        }

        /// <summary>Eleme (knockout) turu mu? Final dahil değil (çağıran ekler).</summary>
        private static bool IsEliminationRound(string? round)
        {
            var r = Normalize(round);
            if (r.Length == 0)
                return false;

            return Contains(r,
                "semi", "quarter", "knockout", "round of", "last 16", "last 8",
                "play-off", "play off", "playoff", "elimination", "eleme", "tur",
                "halbfinale", "viertelfinale", "achtelfinale", "yari final", "ceyrek final",
                "1/2", "1/4", "1/8", "1/16", "octavos", "cuartos", "semifinal", "ottavi", "quarti");
        }

        private static ImportanceLevel ResolveImportance(CompetitionType type, bool isFinal, bool isElimination)
        {
            if (isFinal)
                return ImportanceLevel.High;
            if (isElimination)
                return ImportanceLevel.High;
            if (type == CompetitionType.ChampionsLeague)
                return ImportanceLevel.Medium;
            if (type == CompetitionType.Cup)
                return ImportanceLevel.Medium;
            return ImportanceLevel.Low;
        }

        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var lowered = value.Trim().ToLowerInvariant();
            // Türkçe/diyakritik sadeleştirme (heuristik eşleşme için yeterli).
            return lowered
                .Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g')
                .Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c')
                .Replace('é', 'e').Replace('á', 'a').Replace('ó', 'o');
        }

        private static bool Contains(string haystack, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if (haystack.IndexOf(needle, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }
    }
}
