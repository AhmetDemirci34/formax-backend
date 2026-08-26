// FORMAX · Takip (Activity Feed + Follow Management) UI tipleri.
// Gerçek kaynaklar: /api/notifications/me (feed), /api/users/me/teams (takımlar),
// /api/follows/me (maçlar). Mock/fake yok.

export type ViewMode = "feed" | "management";

export type FeedFilter = "all" | "matches" | "teams" | "leagues" | "news";
export type ManageFilter = "all" | "teams" | "leagues" | "matches";

/** Feed öğesi — UserNotificationDto'dan türetilir. */
export interface ActivityItem {
  id: number;
  matchId: number;
  title: string;
  description: string;
  createdAt: string;
  isRead: boolean;
  targetUrl: string; // /match/{matchId}
}

export type ManageType = "team" | "league" | "match";

/** Yönetim listesi öğesi — takım/lig/maç ortak görünümü. */
export interface ManageEntity {
  key: string; // `${type}:${id}`
  type: ManageType;
  id: number;
  name: string;
  subtitle: string; // "Takım" | "Lig" | "Maç"
  logoUrl?: string | null;
}
