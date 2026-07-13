// FORMAX · Sana Özel Maçlar (Featured) — view-model tipleri + referans demo verisi.
// Yapı gerçek DTO ile çalışacak şekilde: backend geldiğinde yalnızca veri kaynağı değişir.

export type FeaturedTagTone = "neon" | "purple" | "amber" | "red" | "blue" | "muted";

export interface FeaturedTeamVM {
  name: string;
  logoUrl?: string | null;
}

export interface FeaturedTagVM {
  label: string;
  tone: FeaturedTagTone;
}

export interface FeaturedMatchVM {
  id: string;
  time: string;
  home: FeaturedTeamVM;
  away: FeaturedTeamVM;
  tags: FeaturedTagVM[];
  /** İlgi skoru (0–100). */
  interestScore: number;
}

// Referans (görsel) — statik.
export const FEATURED_DEMO: FeaturedMatchVM[] = [
  {
    id: "ajax-fey",
    time: "Bugün 17:30",
    home: { name: "Ajax" },
    away: { name: "Feyenoord" },
    tags: [
      { label: "Hızlı Oyun", tone: "purple" },
      { label: "Derbi", tone: "amber" },
    ],
    interestScore: 92,
  },
  {
    id: "mil-ata",
    time: "Bugün 18:00",
    home: { name: "Milan" },
    away: { name: "Atalanta" },
    tags: [{ label: "Gol Potansiyeli", tone: "neon" }],
    interestScore: 88,
  },
  {
    id: "bvb-lev",
    time: "Bugün 20:45",
    home: { name: "Dortmund" },
    away: { name: "Leverkusen" },
    tags: [{ label: "Tempo Yüksek", tone: "red" }],
    interestScore: 85,
  },
  {
    id: "por-ben",
    time: "Bugün 21:00",
    home: { name: "Porto" },
    away: { name: "Benfica" },
    tags: [{ label: "Bol Pozisyon", tone: "blue" }],
    interestScore: 83,
  },
];
