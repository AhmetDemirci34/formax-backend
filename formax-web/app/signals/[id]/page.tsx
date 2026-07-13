"use client";

import { useParams, useRouter } from "next/navigation";
import { useMatchDetail } from "@/hooks/useMatchDetail";
import { LoadingCard } from "@/components/ui/LoadingState";

type Row = { label: string; value: string; layer: string; color: string };

// FORMAX Intelligence Dashboard — aktif maçın TÜM gerçek sinyalleri, kaynak katmanıyla.
// Uydurma yüzde yok: yalnız Match Detail'de gerçekten üretilen metrikler listelenir.
export default function SignalsDashboard() {
  const params = useParams();
  const router = useRouter();
  const id = Number(params?.id ?? 0);
  const { data: d, isLoading } = useMatchDetail(id);

  const home = d?.homeTeam?.name ?? "Ev sahibi";
  const away = d?.awayTeam?.name ?? "Deplasman";
  const rows: { title: string; items: Row[] }[] = [];

  if (d) {
    // Olasılıklar — AI Consensus (Form + H2H + Sapma)
    const probs: Row[] = (d.probabilities ?? []).map((p) => ({
      label: p.market,
      value: `%${p.probability}`,
      layer: "AI Consensus · Form + H2H",
      color: "text-[#A855F7]",
    }));
    if (probs.length) rows.push({ title: "AI Olasılık Tahminleri", items: probs });

    // Piyasa Sapması — Market Intelligence
    if (d.sapma?.sapmaBolgesi) {
      rows.push({
        title: "Piyasa & Sapma",
        items: [
          { label: "Sapma Bölgesi", value: d.sapma.sapmaBolgesi, layer: "Market Intelligence", color: "text-[#3B82F6]" },
          { label: "Sapma Skoru", value: `%${d.sapma.sapma}`, layer: "Oynanma vs Güç farkı", color: "text-[#3B82F6]" },
          { label: "Oynanma Yönü", value: dirLabel(d.sapma.oynanmaYonu), layer: "Kullanıcı yoğunluğu", color: "text-text-secondary" },
        ],
      });
    }

    // Form — Form Intelligence
    const c = d.comparison;
    if (c?.home && c?.away) {
      rows.push({
        title: "Form Intelligence",
        items: [
          { label: `${home} · Gol Atma`, value: `%${c.home.goalScoringRate}`, layer: "Son 10 maç", color: "text-formax-green" },
          { label: `${away} · Gol Atma`, value: `%${c.away.goalScoringRate}`, layer: "Son 10 maç", color: "text-formax-green" },
          { label: `${home} · Clean Sheet`, value: `%${c.home.cleanSheetRate}`, layer: "Son 10 maç", color: "text-formax-amber" },
          { label: `${away} · Clean Sheet`, value: `%${c.away.cleanSheetRate}`, layer: "Son 10 maç", color: "text-formax-amber" },
        ],
      });
    }

    // Risk — Risk Intelligence
    if (d.riskIntelligence?.homeRiskLabel || d.riskIntelligence?.awayRiskLabel) {
      rows.push({
        title: "Risk Intelligence",
        items: [
          { label: home, value: d.riskIntelligence.homeRiskLabel, layer: d.riskIntelligence.homeRiskDetail, color: "text-formax-red" },
          { label: away, value: d.riskIntelligence.awayRiskLabel, layer: d.riskIntelligence.awayRiskDetail, color: "text-formax-red" },
        ],
      });
    }

    // H2H
    const h2hTotal = d.h2h ? d.h2h.homeWins + d.h2h.awayWins + d.h2h.draws : 0;
    if (h2hTotal > 0) {
      rows.push({
        title: "Geçmiş Karşılaşmalar",
        items: [
          { label: "Toplam Maç", value: `${h2hTotal}`, layer: "H2H Intelligence", color: "text-white" },
          { label: home, value: `${d.h2h.homeWins} G`, layer: "Galibiyet", color: "text-formax-green" },
          { label: away, value: `${d.h2h.awayWins} G`, layer: "Galibiyet", color: "text-formax-green" },
        ],
      });
    }

    // Live xG (yalnız gerçekten varsa)
    const stats = d.live?.stats;
    if (stats && (stats.xgHome != null || stats.xgAway != null)) {
      rows.push({
        title: "Canlı xG",
        items: [
          { label: home, value: `${stats.xgHome ?? 0}`, layer: "Live Intelligence", color: "text-[#A855F7]" },
          { label: away, value: `${stats.xgAway ?? 0}`, layer: "Live Intelligence", color: "text-[#A855F7]" },
        ],
      });
    }
  }

  return (
    <div className="flex min-h-screen flex-col bg-bg-base pb-10">
      <header className="sticky top-0 z-10 flex items-center gap-3 border-b border-white/[0.06] bg-bg-base/95 px-4 py-3 backdrop-blur">
        <button onClick={() => router.back()} aria-label="Geri" className="text-text-secondary">
          ‹ Geri
        </button>
        <div className="min-w-0">
          <h1 className="truncate text-sm font-bold text-white">Intelligence Dashboard</h1>
          <p className="truncate text-[11px] text-text-muted">{home} — {away}</p>
        </div>
      </header>

      <main className="flex flex-col gap-5 px-4 pt-4">
        {isLoading && <LoadingCard />}

        {!isLoading && rows.length === 0 && (
          <p className="pt-16 text-center text-sm text-text-muted">Bu maç için sinyal verisi henüz oluşmadı.</p>
        )}

        {rows.map((group) => (
          <section key={group.title}>
            <h2 className="mb-2 px-0.5 text-[11px] font-bold uppercase tracking-[0.15em] text-text-muted">
              {group.title}
            </h2>
            <div className="overflow-hidden rounded-2xl border border-white/[0.07] bg-white/[0.02]">
              {group.items.map((r, i) => (
                <div
                  key={r.label + i}
                  className={`flex items-center justify-between gap-3 px-3.5 py-3 ${i === group.items.length - 1 ? "" : "border-b border-white/[0.05]"}`}
                >
                  <div className="min-w-0">
                    <div className="truncate text-[13px] font-semibold text-white">{r.label}</div>
                    <div className="truncate text-[10px] text-text-muted">{r.layer}</div>
                  </div>
                  <div className={`shrink-0 text-sm font-bold tabular-nums ${r.color}`}>{r.value}</div>
                </div>
              ))}
            </div>
          </section>
        ))}

        {!isLoading && rows.length > 0 && (
          <p className="px-1 text-[10px] leading-relaxed text-text-muted/70">
            Tüm değerler FORMAX Intelligence motorunun mevcut katmanlarından üretilmiştir. Haber, sosyal, hava ve
            hakem katmanları veri akışı oluştukça bu panele eklenecektir.
          </p>
        )}
      </main>
    </div>
  );
}

function dirLabel(v: string): string {
  if (v === "Home") return "Ev Sahibi";
  if (v === "Away") return "Deplasman";
  return "Dengede";
}
