using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.OfficialSources
{
    /// <summary>Tek tarafın kadro kararı.</summary>
    public sealed record LineupSideVerdict(bool Accepted, string Reason)
    {
        public const string ReasonAccepted = "Accepted";
        public const string ReasonMissing = "NotPublished";
        public const string ReasonWrongTeam = "WrongTeam";
        public const string ReasonStarterCount = "StarterCountNot11";
        public const string ReasonEmptyName = "EmptyPlayerName";
        public const string ReasonDuplicatePlayer = "DuplicatePlayer";
        public const string ReasonDuplicateShirt = "DuplicateShirtNumber";
        public const string ReasonBenchOverlap = "BenchOverlapsStarters";
    }

    /// <summary>Kadro belgesinin tamamı için karar.</summary>
    public sealed record LineupVerification(
        bool SourceOfficial,
        string? SourceRejectReason,
        LineupSideVerdict Home,
        LineupSideVerdict Away)
    {
        public bool AnySideAccepted => SourceOfficial && (Home.Accepted || Away.Accepted);
        public bool BothSidesAccepted => SourceOfficial && Home.Accepted && Away.Accepted;
    }

    /// <summary>
    /// RESMÎ İÇERİK DOĞRULAMA — kaydedilmeden önce her kadro buradan geçer (saf, ağ/DB yok).
    ///
    /// KADRO KURALLARI:
    ///  • Kaynak kayıt defterinde DOĞRULANMIŞ bir resmî kaynak ve adres o kaynağın HTTPS host'u.
    ///  • Taraf, beklenen takıma ait (kaynağın yazdığı takım adı FORMAX takımıyla eşleşir).
    ///  • TAM 11 başlangıç oyuncusu, boş ad yok; aynı oyuncu ya da aynı forma numarası iki kez yok;
    ///    yedek listesinde ilk 11'deki bir oyuncu yok.
    ///  • Kulüp yalnız kendi ilk 11'ini yayımladıysa YALNIZ o taraf kabul edilir; diğer taraf
    ///    uydurulmaz ("NotPublished").
    /// Eksik kadro (10 oyuncu) tam kadro SAYILMAZ; belirsiz veri kullanıcıya gitmez.
    /// </summary>
    public static class OfficialContentVerificationService
    {
        public const int StartersRequired = 11;

        public static LineupVerification VerifyLineup(
            OfficialLineupDocument document,
            string expectedHomeTeam,
            string expectedAwayTeam)
        {
            var official = OfficialSourceRegistry.IsOfficialUrl(document.SourceKey, document.SourceUrl);
            var src = OfficialSourceRegistry.ByKey(document.SourceKey);
            string? sourceReason = null;
            if (src == null) sourceReason = "UnknownSource";
            else if (src.Status != OfficialSourceStatuses.Verified) sourceReason = "SourceNotVerified";
            else if (!src.Capabilities.Contains(OfficialPurposes.Lineup)) sourceReason = "SourceHasNoLineupCapability";
            else if (!official) sourceReason = "UrlNotOnOfficialHost";

            return new LineupVerification(
                sourceReason == null,
                sourceReason,
                VerifySide(document.Home, expectedHomeTeam),
                VerifySide(document.Away, expectedAwayTeam));
        }

        public static LineupSideVerdict VerifySide(OfficialLineupSide? side, string expectedTeam)
        {
            if (side == null || side.Starters.Count == 0)
                return new(false, LineupSideVerdict.ReasonMissing);

            if (!OfficialTeamNameMatcher.SameTeam(side.TeamName, expectedTeam))
                return new(false, LineupSideVerdict.ReasonWrongTeam);

            if (side.Starters.Count != StartersRequired)
                return new(false, LineupSideVerdict.ReasonStarterCount);

            var all = side.Starters.Concat(side.Bench).ToList();
            if (all.Any(p => string.IsNullOrWhiteSpace(p.Name)))
                return new(false, LineupSideVerdict.ReasonEmptyName);

            var starterKeys = side.Starters.Select(PlayerKey).ToList();
            if (starterKeys.Distinct(StringComparer.Ordinal).Count() != starterKeys.Count)
                return new(false, LineupSideVerdict.ReasonDuplicatePlayer);

            var benchKeys = side.Bench.Select(PlayerKey).ToList();
            if (benchKeys.Distinct(StringComparer.Ordinal).Count() != benchKeys.Count)
                return new(false, LineupSideVerdict.ReasonDuplicatePlayer);
            if (benchKeys.Intersect(starterKeys, StringComparer.Ordinal).Any())
                return new(false, LineupSideVerdict.ReasonBenchOverlap);

            var shirts = all.Where(p => p.ShirtNumber is > 0).Select(p => p.ShirtNumber!.Value).ToList();
            if (shirts.Distinct().Count() != shirts.Count)
                return new(false, LineupSideVerdict.ReasonDuplicateShirt);

            return new(true, LineupSideVerdict.ReasonAccepted);
        }

        /// <summary>Oyuncu tekillik anahtarı: kaynak kimliği varsa o, yoksa katlanmış ad.</summary>
        private static string PlayerKey(OfficialLineupPlayer p)
            => !string.IsNullOrWhiteSpace(p.OfficialPlayerId)
                ? "id:" + p.OfficialPlayerId
                : "n:" + OfficialTeamNameMatcher.Fold(p.Name);
    }
}
