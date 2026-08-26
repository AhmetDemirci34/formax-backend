using System.Text.Json.Serialization;

namespace Formax.Application.DTOs.MatchIntelligence;

// ─────────────────────────────────────────────────────────────────────────────
// Match Intelligence v2 — Experience bazlı tek response sözleşmesi (Görev #011).
//
// Docs: 05_MATCH_INTELLIGENCE (sayfa yapısı & Experience sırası), 06 (Shared Components),
//       07–14 (Experience'ler), 15_API_HARITALAMA (Backend tek doğruluk; tek yönlü mapping).
//
// Bu DTO, mevcut MatchDetailDto'yu DEĞİŞTİRMEZ; tamamen paraleldir. Frontend Match Intelligence
// UI'ının 8 Experience'ini (+ Header, AI Session, AI Ask) tek response'ta taşır; frontend
// yalnızca render eder, iş kuralı/mapping üretmez.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class MatchIntelligenceDto
{
    public int MatchId { get; init; }
    public MiHeaderDto Header { get; init; } = new();
    public MiSessionDto Session { get; init; } = new();
    public string InitialExperienceId { get; init; } = "expected-lineup";
    public List<MiExperienceDto> Experiences { get; init; } = new();
}

/// Header (05 §3 · 1. eleman).
public sealed class MiHeaderDto
{
    public string Competition { get; init; } = "";
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";
    public string KickoffLabel { get; init; } = "";
    public string Status { get; init; } = "PRE"; // PRE | LIVE | FT
    public string? MinuteLabel { get; init; }
}

/// C01 AI Session — AI önce konuşur (Rule 01).
public sealed class MiSessionDto
{
    public string Greeting { get; init; } = "";
    public string Message { get; init; } = "";
}

/// C10 Hero Media tanımı.
public sealed class MiHeroDto
{
    public string Title { get; init; } = "";
    public string Caption { get; init; } = "";
    public string Motif { get; init; } = "";
}

/// C05 AI Evidence satırı.
public sealed class MiEvidenceDto
{
    public string Label { get; init; } = "";
    public string? Detail { get; init; }
}

/// Ortak güven özeti (Outlook/Market/Live).
public sealed class MiConfidenceDto
{
    public string Label { get; init; } = "Güven";
    public int Value { get; init; }
}

/// Bir Experience'in tam paketi. Experience'e özel veri bloğu yalnız ilgili alanda dolar;
/// diğerleri null kalır (frontend'te generic Hero fallback'e düşer — davranış #001'de tanımlı).
public sealed class MiExperienceDto
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Question { get; init; } = "";
    public MiHeroDto Hero { get; init; } = new();
    public string StageSummary { get; init; } = "";
    public string Insight { get; init; } = "";
    public List<MiEvidenceDto> Evidence { get; init; } = new();
    public string Ask { get; init; } = "";
    public string? Next { get; init; }

    // Experience'e özel yükler (yalnız biri dolu):
    public MiLineupsDto? Lineup { get; init; }              // 07 Expected Lineup
    public MiLineupChangeDto? LineupChange { get; init; }   // 08 Living Lineup
    // camelCase politikası "H2H" → "h2H" üretir; frontend "h2h" bekler → açık isim.
    [JsonPropertyName("h2h")]
    public MiH2HDto? H2H { get; init; }                     // 09 H2H
    public MiMatchLivingDto? Living { get; init; }          // 08 Living Lineup — backend karşılaştırması (Görev #014)
    public MiNewsDto? News { get; init; }                   // 10 News
    public MiMatchOutlookDto? MatchOutlook { get; init; }   // 11 Match Outlook
    public MiMarketDto? Market { get; init; }               // 12 Market Intelligence
    public MiWatchLiveDto? WatchLive { get; init; }         // 13 Watch Live
    public MiLiveDto? Live { get; init; }                   // 14 Live Intelligence
}

// ── 07/08 Lineup ─────────────────────────────────────────────────────────────
public sealed class MiLineupPlayerDto
{
    public int Number { get; init; }
    public string Name { get; init; } = "";
    public string Position { get; init; } = "";
    /// Rol etiketi (Kaleci / Defans / Orta Saha / Forvet) — Görev #013.
    public string Role { get; init; } = "";
    /// Oyuncu güveni (0–100) — başlama sıklığından.
    public int Confidence { get; init; }
}

public sealed class MiUnavailablePlayerDto
{
    public string Name { get; init; } = "";
    public string Status { get; init; } = ""; // Injured | Suspended | Doubtful
    public string Reason { get; init; } = "";
}

public sealed class MiTeamLineupDto
{
    public string TeamName { get; init; } = "";
    /// Diziliş (4-3-3 / 4-2-3-1 / 3-5-2 / 4-4-2) — ExpectedLineupEngine üretir (Görev #013).
    public string Formation { get; init; } = "";
    public List<MiLineupPlayerDto> Players { get; init; } = new();
    /// Takım (diziliş) güveni (0–100).
    public int Confidence { get; init; }
    /// Official | Predicted | Insufficient.
    public string Source { get; init; } = "";
    public List<MiUnavailablePlayerDto> UnavailablePlayers { get; init; } = new();
    public List<string> Reasons { get; init; } = new();
}

public sealed class MiLineupsDto
{
    public MiTeamLineupDto Home { get; init; } = new();
    public MiTeamLineupDto Away { get; init; } = new();
}

public sealed class MiLineupChangeDto
{
    public MiLineupsDto Expected { get; init; } = new();
    public MiLineupsDto Official { get; init; } = new();
}

/// Living Lineup backend karşılaştırması (Görev #014) — home/away LivingLineupDto.
/// Frontend #003 şimdilik lineupChange'i client-side kıyaslar; bu alan backend'in
/// yaptığı otoriter karşılaştırmayı taşır (ileride frontend doğrudan tüketebilir).
public sealed class MiMatchLivingDto
{
    public LivingLineupDto Home { get; init; } = new();
    public LivingLineupDto Away { get; init; } = new();
}

// ── 09 H2H ───────────────────────────────────────────────────────────────────
public sealed class MiH2HMeetingDto
{
    public string Date { get; init; } = "";
    public string Competition { get; init; } = "";
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";
    public int HomeScore { get; init; }
    public int AwayScore { get; init; }
    public string Outcome { get; init; } = "draw"; // home | draw | away (fikstür ev sahibi açısından)
}

public sealed class MiVenueRecordDto
{
    public int Played { get; init; }
    public int Wins { get; init; }
    public int Draws { get; init; }
    public int Losses { get; init; }
}

public sealed class MiScoreNoteDto
{
    public string Score { get; init; } = "";
    public string Note { get; init; } = "";
}

public sealed class MiStreakDto
{
    public string Team { get; init; } = "";
    public int Count { get; init; }
}

public sealed class MiH2HDto
{
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";
    public int Played { get; init; }
    public int HomeWins { get; init; }
    public int Draws { get; init; }
    public int AwayWins { get; init; }
    public int HomeGoals { get; init; }
    public int AwayGoals { get; init; }
    public MiVenueRecordDto HomeVenue { get; init; } = new();
    public MiVenueRecordDto AwayVenue { get; init; } = new();
    public MiScoreNoteDto BiggestScore { get; init; } = new();
    public MiStreakDto UnbeatenStreak { get; init; } = new();
    public MiH2HMeetingDto LastMeeting { get; init; } = new();
    public List<MiH2HMeetingDto> Recent { get; init; } = new();
}

// ── 10 News ──────────────────────────────────────────────────────────────────
public sealed class MiNewsArticleDto
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Source { get; init; } = "";
    public string Time { get; init; } = "";
    public string Type { get; init; } = "";
    public string Importance { get; init; } = "medium"; // critical | high | medium | low
    public string Team { get; init; } = "general";      // home | away | general
    public string Impact { get; init; } = "";
    public bool Hero { get; init; }
}

public sealed class MiNewsDto
{
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";
    public List<MiNewsArticleDto> Articles { get; init; } = new();
    // News Intelligence Engine çıktısı (Görev #015).
    public string AiSummary { get; init; } = "";
    public int Confidence { get; init; }
    public int ImportanceScore { get; init; }
    public string Impact { get; init; } = "";
}

// ── 11 Match Outlook ─────────────────────────────────────────────────────────
public sealed class MiTeamOutlookDto
{
    public string Team { get; init; } = "";
    public List<string> Form { get; init; } = new(); // W | D | L
    public string Performance { get; init; } = "";
    /// <summary>0–100. Yeterli maç yoksa null — "0/100" veri yokluğunu ölçüm gibi gösterirdi.</summary>
    public int? PerformanceScore { get; init; }
    public double GoalsFor { get; init; }
    public double GoalsAgainst { get; init; }
}

public sealed class MiMatchOutlookDto
{
    public MiTeamOutlookDto Home { get; init; } = new();
    public MiTeamOutlookDto Away { get; init; } = new();
    public string GoalTrend { get; init; } = "";
    public string Tempo { get; init; } = "";
    /// <summary>0 ev ↔ 100 deplasman. Yeterli maç yoksa null (hesaplanmadı).</summary>
    public int? Balance { get; init; }
    /// <summary>home | away | balanced | unknown (kanıt yetersiz).</summary>
    public string Momentum { get; init; } = "unknown";
    public MiConfidenceDto ConfidenceSummary { get; init; } = new();
    public string HeroOutlook { get; init; } = "";
    /// Match Outlook Engine AI değerlendirmesi (Görev #016).
    public string AiSummary { get; init; } = "";
}

// ── 12 Market Intelligence ───────────────────────────────────────────────────
public sealed class MiMarketChangeDto
{
    public string Label { get; init; } = "";
    public string Note { get; init; } = "";
    public string Direction { get; init; } = "balanced"; // home | away | draw | balanced
}

public sealed class MiMarketDto
{
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";
    public string MarketTrend { get; init; } = "";
    public string OpeningState { get; init; } = "";
    public string CurrentState { get; init; } = "";
    public string Direction { get; init; } = "";
    public int Lean { get; init; } // 0 ev ↔ 100 deplasman
    public int Stability { get; init; }
    public int Volatility { get; init; }
    public MiMarketChangeDto SignificantChange { get; init; } = new();
    public MiConfidenceDto AiConfidence { get; init; } = new();
    public string HeroSummary { get; init; } = "";
    /// Market Intelligence Engine AI özeti (Görev #017).
    public string AiSummary { get; init; } = "";
}

// ── 13 Watch Live ────────────────────────────────────────────────────────────
public sealed class MiWatchLiveDto
{
    public string Broadcaster { get; init; } = "";
    public string Platform { get; init; } = "";
    public string Availability { get; init; } = "scheduled"; // live | scheduled | available | unavailable
    public string AvailabilityLabel { get; init; } = "";
    public string Region { get; init; } = "";
    public string StreamQuality { get; init; } = "";
    public string MatchCoverage { get; init; } = "";
    public string AiRecommendation { get; init; } = "";
    public string HeroSummary { get; init; } = "";
    /// Watch Live Engine AI özeti (Görev #018).
    public string AiSummary { get; init; } = "";
}

// ── 14 Live Intelligence ─────────────────────────────────────────────────────
public sealed class MiLiveScoreDto
{
    public int Home { get; init; }
    public int Away { get; init; }
}

public sealed class MiLiveKeyEventDto
{
    public string Minute { get; init; } = "";
    public string Type { get; init; } = "";
    public string Description { get; init; } = "";
    public string Team { get; init; } = "home"; // home | away
}

public sealed class MiLiveDto
{
    public string HomeTeam { get; init; } = "";
    public string AwayTeam { get; init; } = "";
    public string MatchMinute { get; init; } = "";
    public MiLiveScoreDto Score { get; init; } = new();
    public string MatchStatus { get; init; } = "live"; // live | halftime | ended
    public string StatusLabel { get; init; } = "";
    public string Momentum { get; init; } = "balanced";
    public int MomentumValue { get; init; }
    public string MatchRhythm { get; init; } = "";
    public MiLiveKeyEventDto KeyEvent { get; init; } = new();
    public string LiveSummary { get; init; } = "";
    public MiConfidenceDto AiConfidence { get; init; } = new();
    /// Live Intelligence Engine AI özeti (Görev #019).
    public string AiSummary { get; init; } = "";
}
