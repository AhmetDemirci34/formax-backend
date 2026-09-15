"use client";

import type { MatchDetailDto } from "@/types/api";
import type { OutcomeSnapshotDto } from "@/types/outcomes";
import { useMatchOutcomes } from "@/hooks/useMatchOutcomes";
import { useMatchPicks } from "@/hooks/useMatchPicks";
import { outcomeViewState } from "@/lib/outcomes/outcomeView";
import { OutcomeCards } from "@/components/outcomes/OutcomeCards";
import { AnalysisSections } from "../analysis/AnalysisSections";
import { ViewShell } from "./ViewShell";

/**
 * AIAnalysisView (activeView === 'analysis') — FORMAX'ın merkez ekranı.
 *
 * ANALİZİN TEK KAYNAĞI (13.09.2026, kilitli): /detail → match.analysis. Analiz ARKA PLANDA
 * backend'in deterministik kanıtından üretilir, doğrulanır ve DB'ye yazılır; bu ekran yalnız
 * hazır kaydı gösterir. Sayfa açılışı LLM çağırmaz, ek istek atmaz.
 *
 * KALDIRILANLAR (ürün kararı):
 *  • Eski Match Intelligence anlatısı (aiNarrative) — maça özel olmayan, kanıtsız ve maçtan
 *    maça tekrar eden kalıp cümleler üretiyordu ("Maç çevresinde konuşulacak gelişmeler var").
 *  • "Bu Sezon Form" bloğundaki ham teknik satır (O · G · B · M · AG · YG · AV). Form sayıları
 *    artık analiz cümlelerinin içinde, takım adıyla ve örneklem büyüklüğüyle birlikte geçer.
 *
 * OLASI SONUÇLAR (15.09.2026): arka planda üretilen snapshot (GET /api/matches/{id}/outcomes) — Keşfet ile AYNI
 * SnapshotId. Üç ana kart üç farklı aileden; çifte şans yalnız "Tüm Olasılıklar" içinde. Frontend sıralama/filtre yapmaz.
 */
export function AIAnalysisView({ match, onClose }: { match: MatchDetailDto; onClose: () => void }) {
  const { data: outcomes, isLoading, isError } = useMatchOutcomes(match.matchId);
  const view = outcomeViewState(outcomes);

  return (
    <ViewShell title="AI Maç Analizi" onClose={onClose}>
      <div className="space-y-3 pb-4">
        {/* Kanıta dayalı analiz — /detail ile birlikte geldi (DB); hazır değilse durum yazılır. */}
        <AnalysisSections analysis={match.analysis} />

        {isLoading && (
          <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 py-3 text-[13px] text-white/55">
            AI olası sonuçları yükleniyor…
          </div>
        )}

        {isError && (
          <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 py-3 text-[13px] text-white/55">
            AI olası sonuçları şu an yüklenemedi.
          </div>
        )}

        {!isLoading && !isError && view.kind === "ready" && <PossibleResultsBlock match={match} snapshot={view.snapshot} />}
        {!isLoading && !isError && view.kind !== "ready" && (
          <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 py-3 text-[13px] text-white/60">{view.text}</div>
        )}
      </div>
    </ViewShell>
  );
}


/**
 * OLASI SONUÇLAR — model olasılıkları + "SENİN SEÇİMİN" kontrolü.
 *
 * KURALLAR:
 *  • Frontend OLASILIK ÜRETMEZ. Yüzde ve oran backend'in; burada yalnız hangi
 *    satırların gösterileceği ve seçim durumu yönetilir.
 *  • ÇAKIŞMA KURALI BACKEND'İNDİR. Aynı market grubundan ikinci bir seçim
 *    yapıldığında eskisini backend siler ve GÜNCEL listeyi döner; arayüz o listeyi
 *    olduğu gibi gösterir. Kural iki yerde ayrı ayrı yazılsaydı er geç ayrışırdı.
 *  • MAÇ BAŞLADIYSA yeni seçim yapılamaz — kontrol gizlenmez, DEVRE DIŞI kalır ve
 *    sebebi yazılır.
 *  • Kullanıcının seçimi modelin tahmini gibi ETİKETLENMEZ: rozet açıkça
 *    "Senin seçimin" der.
 */
function PossibleResultsBlock({ match, snapshot }: { match: MatchDetailDto; snapshot: OutcomeSnapshotDto }) {
  const { selections, isLoggedIn, toggle, rejection } = useMatchPicks(match.matchId);

  const selectedByLabel = new Map(selections.map((s) => [s.label, s]));

  // Maç başladı mı? Kaynak tek gerçektir: backend durumu + kickoff saati.
  const kickoff = match.matchDate ? new Date(match.matchDate).getTime() : NaN;
  const started =
    match.status?.toLowerCase() === "finished" ||
    match.status?.toLowerCase() === "live" ||
    (!Number.isNaN(kickoff) && Date.now() >= kickoff);

  return (
    <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright p-4">
      <div className="mb-3 flex items-baseline justify-between gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-[0.16em] text-goalai-accent">
          Olası Sonuçlar
        </h3>
        {selections.length > 0 && (
          <span className="text-[10.5px] font-semibold uppercase tracking-wide text-white/45">
            {selections.length} seçim
          </span>
        )}
      </div>

      <OutcomeCards
        snapshot={snapshot}
        selectedMarkets={new Set(selections.map((x) => x.label))}
        selectionDisabled={!isLoggedIn || toggle.isPending}
        onSelect={(c) => {
          const already = selectedByLabel.has(c.market);
          if (started && !already) return;
          toggle.mutate({ matchId: match.matchId, marketLabel: c.market, probabilityPercent: c.probability, odd: null });
        }}
      />

      {/* DURUM AÇIKÇA SÖYLENİR — sessiz devre dışı bırakma yok. */}
      {!isLoggedIn && (
        <p className="mt-2.5 text-[11.5px] leading-snug text-white/45">
          Sonuç seçebilmek için giriş yapmalısın. Seçimlerin hesabına kaydedilir.
        </p>
      )}
      {isLoggedIn && started && (
        <p className="mt-2.5 text-[11.5px] leading-snug text-white/45">
          Maç başladığı için yeni seçim yapılamaz.
        </p>
      )}
      {rejection === "MATCH_ALREADY_STARTED" && (
        <p className="mt-2.5 text-[11.5px] leading-snug text-formax-amber/85">
          Maç başladığı için bu seçim kaydedilmedi.
        </p>
      )}
      {rejection === "UNKNOWN_MARKET" && (
        <p className="mt-2.5 text-[11.5px] leading-snug text-formax-amber/85">
          Bu market için seçim kaydı desteklenmiyor.
        </p>
      )}
      {toggle.isError && (
        <p className="mt-2.5 text-[11.5px] leading-snug text-formax-amber/85">
          Seçim kaydedilemedi, tekrar dene.
        </p>
      )}
    </div>
  );
}
