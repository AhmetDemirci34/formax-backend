// ─────────────────────────────────────────────────────────────────────────────
// formatMatchTime — Human-readable match time display
// Converts an ISO matchDate into a user-facing string + optional countdown.
//
// Tier rules:
//   past          → "Dün oynandı" / "3 gün önce oynandı"
//   < 15 min      → "Az kaldı · 22:00"
//   15–60 min     → "38 dakika sonra"
//   today, < 6h   → "Bu akşam 22:00"  +  secondary "3 saat sonra"
//   today, > 6h   → "Bu akşam 22:00"
//   tomorrow      → "Yarın 19:00"
//   2–7 days      → "Cumartesi 22:00"
//   > 7 days      → "14 Haziran · Cumartesi"
// ─────────────────────────────────────────────────────────────────────────────

export interface MatchTimeDisplay {
  /** Primary label: "Bu akşam 22:00", "38 dakika sonra", … */
  primary: string;
  /** Secondary label (optional): "3 saat sonra", shown smaller below primary */
  secondary?: string;
  /** True when match starts within the hour — drives accent highlight */
  isImminent: boolean;
}

function cap(s: string): string {
  return s.charAt(0).toUpperCase() + s.slice(1);
}

export function formatMatchTime(matchDateStr: string): MatchTimeDisplay {
  const match = new Date(matchDateStr);
  const now   = new Date();

  const diffMs   = match.getTime() - now.getTime();
  const diffMin  = Math.round(diffMs / 60_000);
  const diffHours = diffMs / 3_600_000;
  const diffDays  = diffMs / 86_400_000;

  const timeStr = match.toLocaleTimeString("tr-TR", {
    hour: "2-digit",
    minute: "2-digit",
  });

  // ── Past (give 5-min buffer for matches in progress) ──────────────────────
  if (diffMs < -300_000) {
    const d = Math.abs(Math.floor(diffDays));
    if (d === 0) return { primary: "Bugün oynandı",     isImminent: false };
    if (d === 1) return { primary: "Dün oynandı",       isImminent: false };
    return           { primary: `${d} gün önce oynandı`, isImminent: false };
  }

  // ── Less than 15 minutes ──────────────────────────────────────────────────
  if (diffMin < 15) {
    return { primary: `Az kaldı · ${timeStr}`, isImminent: true };
  }

  // ── 15–60 minutes ─────────────────────────────────────────────────────────
  if (diffMin < 60) {
    return {
      primary:   `${diffMin} dakika sonra`,
      secondary: timeStr,
      isImminent: true,
    };
  }

  // ── Same calendar day ─────────────────────────────────────────────────────
  const isSameDay = match.toDateString() === now.toDateString();
  const matchHour = match.getHours();
  const dayLabel  = matchHour >= 18 ? "Bu akşam"
                  : matchHour >= 12 ? "Bugün öğleden sonra"
                  : "Bugün";

  if (isSameDay && diffHours < 6) {
    const h     = Math.floor(diffHours);
    const label = h === 1 ? "1 saat sonra" : `${h} saat sonra`;
    return {
      primary:   `${dayLabel} ${timeStr}`,
      secondary: label,
      isImminent: false,
    };
  }

  if (isSameDay) {
    return { primary: `${dayLabel} ${timeStr}`, isImminent: false };
  }

  // ── Tomorrow ──────────────────────────────────────────────────────────────
  const tomorrow = new Date(now);
  tomorrow.setDate(now.getDate() + 1);
  if (match.toDateString() === tomorrow.toDateString()) {
    return { primary: `Yarın ${timeStr}`, isImminent: false };
  }

  // ── 2–7 days ──────────────────────────────────────────────────────────────
  if (diffDays <= 7) {
    const day = match.toLocaleDateString("tr-TR", { weekday: "long" });
    return { primary: `${cap(day)} ${timeStr}`, isImminent: false };
  }

  // ── > 7 days ──────────────────────────────────────────────────────────────
  const date    = match.toLocaleDateString("tr-TR", { day: "numeric", month: "long" });
  const weekday = match.toLocaleDateString("tr-TR", { weekday: "long" });
  return { primary: `${date} · ${cap(weekday)}`, isImminent: false };
}
