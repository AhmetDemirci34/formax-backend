// FORMAX · Günün AI Kombini (Combo) — view-model tipleri + referans demo verisi.
// Yapı gerçek DTO ile çalışacak şekilde: backend geldiğinde yalnızca veri kaynağı değişir.

export interface ComboTeamVM {
  name: string;
  logoUrl?: string | null;
}

export interface ComboLegVM {
  id: string;
  time: string;
  home: ComboTeamVM;
  away: ComboTeamVM;
  /** Bahis tipi etiketi: "2.5 Üst" · "KG Var" · "MS 1". */
  betLabel: string;
  odds: string;
}

export interface ComboVM {
  legs: ComboLegVM[];
  totalOdds: string;
  /** AI güven yüzdesi ("%78"). */
  confidence: string;
  /** 0–5 dolu yıldız. */
  stars: number;
  /** Özet kart trend çizgisi. */
  trend: number[];
}

// Referans (Home görseli) — statik.
export const COMBO_DEMO: ComboVM = {
  legs: [
    { id: "mci-liv", time: "Bugün 20:30", home: { name: "Man City" }, away: { name: "Liverpool" }, betLabel: "2.5 Üst", odds: "1.56" },
    { id: "rma-vil", time: "Bugün 22:45", home: { name: "Real Madrid" }, away: { name: "Villarreal" }, betLabel: "KG Var", odds: "1.62" },
    { id: "bay-rbl", time: "Bugün 21:00", home: { name: "Bayern Münih" }, away: { name: "Leipzig" }, betLabel: "2.5 Üst", odds: "1.70" },
    { id: "psg-mon", time: "Bugün 19:00", home: { name: "PSG" }, away: { name: "Monaco" }, betLabel: "MS 1", odds: "1.48" },
  ],
  totalOdds: "8.78",
  confidence: "%78",
  stars: 5,
  trend: [40, 44, 48, 52, 60, 68, 80],
};
