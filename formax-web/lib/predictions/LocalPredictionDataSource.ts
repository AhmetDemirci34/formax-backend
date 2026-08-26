import type { PredictionDataSource } from "./PredictionDataSource";
import type { Prediction, PredictionInput, PredictionTeam } from "./types";

const KEY = "formax_predictions";

/** localStorage'da tutulan ham kayıt — yeni + eski şema (geri uyum). */
interface RawRecord {
  predictionId?: string;
  matchId?: number;
  market?: string;
  selection?: string;
  odds?: number | null;
  status?: string;
  createdAt?: string;
  homeTeam?: PredictionTeam;
  awayTeam?: PredictionTeam;
  league?: string;
  // eski şema
  home?: string;
  away?: string;
  savedAt?: string;
}

function normalize(r: RawRecord): Prediction | null {
  if (r.matchId == null || !r.market) return null;
  return {
    predictionId: r.predictionId ?? `${r.matchId}:${r.market}`,
    matchId: r.matchId,
    market: r.market,
    selection: r.selection ?? "",
    odds: r.odds ?? null,
    status: (r.status as Prediction["status"]) ?? "pending",
    createdAt: r.createdAt ?? r.savedAt ?? new Date().toISOString(),
    homeTeam: r.homeTeam ?? (r.home ? { name: r.home } : undefined),
    awayTeam: r.awayTeam ?? (r.away ? { name: r.away } : undefined),
    league: r.league,
  };
}

/**
 * LocalPredictionDataSource — tahmin verisini tarayıcı localStorage'ından okur/yazar.
 * Bu, PredictionDataSource'un BUGÜNKÜ tek implementasyonudur. Backend hazır olunca
 * yerine ApiPredictionDataSource yazılır; bu dosya silinebilir.
 */
export class LocalPredictionDataSource implements PredictionDataSource {
  async getAll(): Promise<Prediction[]> {
    if (typeof window === "undefined") return [];
    try {
      const raw = window.localStorage.getItem(KEY);
      const arr: RawRecord[] = raw ? JSON.parse(raw) : [];
      if (!Array.isArray(arr)) return [];
      return arr.map(normalize).filter((p): p is Prediction => p !== null);
    } catch {
      return [];
    }
  }

  async add(items: PredictionInput[]): Promise<number> {
    if (typeof window === "undefined") return 0;
    const existing = await this.getAll();
    const seen = new Set(existing.map((p) => `${p.matchId}:${p.market}`));
    const merged = [...existing];
    const now = new Date().toISOString();
    let added = 0;
    for (const it of items) {
      const key = `${it.matchId}:${it.market}`;
      if (seen.has(key)) continue;
      seen.add(key);
      merged.push({
        predictionId: key,
        matchId: it.matchId,
        market: it.market,
        selection: it.selection ?? "",
        odds: it.odds ?? null,
        status: "pending",
        createdAt: it.createdAt ?? now,
        homeTeam: it.homeTeam,
        awayTeam: it.awayTeam,
        league: it.league,
      });
      added += 1;
    }
    window.localStorage.setItem(KEY, JSON.stringify(merged));
    return added;
  }

  async setForMatch(it: PredictionInput): Promise<"added" | "updated" | "removed"> {
    if (typeof window === "undefined") return "removed";

    const existing = await this.getAll();
    const current = existing.find((p) => p.matchId === it.matchId);
    const rest = existing.filter((p) => p.matchId !== it.matchId);

    // Aynı market tekrar seçildi → seçim iptal edilir.
    if (current && current.market === it.market) {
      window.localStorage.setItem(KEY, JSON.stringify(rest));
      return "removed";
    }

    const record: Prediction = {
      predictionId: `${it.matchId}:${it.market}`,
      matchId: it.matchId,
      market: it.market,
      selection: it.selection ?? "",
      odds: it.odds ?? null,
      status: "pending",
      createdAt: it.createdAt ?? new Date().toISOString(),
      homeTeam: it.homeTeam,
      awayTeam: it.awayTeam,
      league: it.league,
    };

    window.localStorage.setItem(KEY, JSON.stringify([...rest, record]));
    return current ? "updated" : "added";
  }
}
