import apiClient from "./client";

/**
 * SONUÇLAR API — "Maçlar → SONUÇLAR" sekmesinin tek veri kaynağı.
 *
 * Backend salt DB'den okur: bu istekler api-football'a ya da başka bir sağlayıcıya
 * ÇIKMAZ. Sekmeye geçmek, gün değiştirmek ve karta basmak sağlayıcı kotası harcamaz.
 */

export interface MatchResultTeamDto {
  teamId: number;
  name: string;
  logoUrl?: string | null;
}

export interface MatchResultItemDto {
  matchId: number;
  externalFixtureId?: string | null;
  leagueId: number;
  leagueName: string;
  /** Sağlayıcının ham tur adı. */
  round?: string | null;
  /** Türkçe aşama etiketi ("Play-off Turu", "Lig Maçı · 3. Hafta"). */
  matchTypeLabel?: string | null;
  matchDateUtc: string;
  homeTeam: MatchResultTeamDto;
  awayTeam: MatchResultTeamDto;
  homeScore: number;
  awayScore: number;
  /** İlk yarı — sağlayıcı vermediyse null. "0-0" uydurulmaz. */
  halfTimeHomeScore?: number | null;
  halfTimeAwayScore?: number | null;
  status: string;
  /** true ise kartta küçük "Video var" işareti gösterilir; false ise HİÇ işaret yok. */
  hasPlayableOfficialVideo: boolean;
}

export interface MatchResultDayDto {
  /** Türkiye takvim günü, "yyyy-MM-dd". */
  date: string;
  matchCount: number;
}

/** Bir Türkiye takvim gününün bitmiş maçları. */
export async function getMatchResults(date: string): Promise<MatchResultItemDto[]> {
  const res = await apiClient.get<{ date: string; count: number; results: MatchResultItemDto[] }>(
    "/api/matches/results",
    { params: { date } }
  );
  return res.data.results ?? [];
}

/** Son N Türkiye günü içinde SONUÇ BULUNAN günler (yeniden eskiye). */
export async function getMatchResultDays(days: number): Promise<MatchResultDayDto[]> {
  const res = await apiClient.get<{ days: MatchResultDayDto[] }>("/api/matches/results/days", {
    params: { days },
  });
  return res.data.days ?? [];
}
