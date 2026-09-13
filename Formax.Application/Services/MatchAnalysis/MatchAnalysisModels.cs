using System;
using System.Collections.Generic;

namespace Formax.Application.Services.MatchAnalysis
{
    /// <summary>Takımın tek tamamlanmış maçı — takım perspektifinden (atılan/yenen).</summary>
    public sealed record TeamMatchFact(
        int MatchId,
        DateTime DateUtc,
        bool IsHome,
        int GoalsFor,
        int GoalsAgainst,
        int? Shots = null,
        int? ShotsOnTarget = null);

    public sealed record StandingFact(int Position, int Points, int Played);

    public sealed record LineupFact(string? Formation, string? Coach, int Starters);

    /// <summary>
    /// ANALİZ GİRDİSİ — yalnız doğrulanmış DB verisi. Maç listeleri: AYNI sezon, AYNI lig,
    /// sonucu kesinleşmiş (Finished), bu maçın başlama anından ÖNCE; kupa/hazırlık/canlı/gelecek
    /// maç bu listelere hiç girmez (çağıran sezon kapsamlı lig sorgusuyla doldurur).
    /// </summary>
    public sealed record AnalysisInput(
        int MatchId,
        string HomeName,
        string AwayName,
        DateTime KickoffUtc,
        string LeagueName,
        IReadOnlyList<TeamMatchFact> HomeMatches,
        IReadOnlyList<TeamMatchFact> AwayMatches,
        StandingFact? HomeStanding = null,
        StandingFact? AwayStanding = null,
        bool StandingsComplete = false,
        LineupFact? HomeLineup = null,
        LineupFact? AwayLineup = null,
        DateTime? HomePreviousMatchUtc = null,
        DateTime? AwayPreviousMatchUtc = null);

    /// <summary>Tek kanıt: anahtar + sayısal değerler (+ gerekirse metin değerleri).</summary>
    public sealed record EvidenceItem(
        string Key,
        string Kind,
        IReadOnlyDictionary<string, double> Values,
        IReadOnlyDictionary<string, string>? Texts = null);

    /// <summary>Kullanıcıya gidecek tek cümle — en az bir kanıt anahtarı ZORUNLU.</summary>
    public sealed record AnalysisSentence(string Text, IReadOnlyList<string> EvidenceKeys);

    /// <summary>Olası sonucun gerekçesi — destek ve risk ayrı cümle, ayrı kanıt.</summary>
    public sealed record ScenarioReason(string Market, AnalysisSentence? Support, AnalysisSentence? Risk);

    /// <summary>Üretilmiş analiz belgesi (bölümler en fazla bu kadardır).</summary>
    public sealed class MatchAnalysisDocument
    {
        public List<AnalysisSentence> WhyWatch { get; set; } = new();
        public List<AnalysisSentence> KeyBattle { get; set; } = new();
        public List<AnalysisSentence> LineupImpact { get; set; } = new();
        public AnalysisSentence? Uncertainty { get; set; }
        public List<ScenarioReason> Scenarios { get; set; } = new();

        public IEnumerable<AnalysisSentence> AllSentences()
        {
            foreach (var s in WhyWatch) yield return s;
            foreach (var s in KeyBattle) yield return s;
            foreach (var s in LineupImpact) yield return s;
            if (Uncertainty != null) yield return Uncertainty;
            foreach (var r in Scenarios)
            {
                if (r.Support != null) yield return r.Support;
                if (r.Risk != null) yield return r.Risk;
            }
        }

        public bool IsEmpty => !AllSentences().GetEnumerator().MoveNext();
    }
}
