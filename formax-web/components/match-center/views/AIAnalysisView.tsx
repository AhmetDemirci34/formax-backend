"use client";

import { motion } from "framer-motion";
import type { MatchDetailDto } from "@/types/api";
import { useMatchDecision } from "@/hooks/useMatchDecision";
import { useMatchPicks } from "@/hooks/useMatchPicks";
import { AnalysisSections, ScenarioReasonLines } from "../analysis/AnalysisSections";
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
 * /decision'dan YALNIZ sayısal Olası Sonuçlar okunur; her satırın gerekçesi analizdeki aynı
 * market kaydından gelir (destekleyen veri / zayıflatan risk).
 */
export function AIAnalysisView({ match, onClose }: { match: MatchDetailDto; onClose: () => void }) {
  const { data: decision, isLoading, isError } = useMatchDecision(match.matchId);

  // Decision'dan YALNIZ sayısal karar verisi okunur (anlatı okunmaz).
  //
  // SUNUM FİLTRESİ (hesap DEĞİL): backend 16 market üretir; ekranda %30 altı sonuçlar
  // gürültüdür. Değerler backend'in; burada yalnız hangi satırların gösterileceği ve
  // sırası belirlenir — probability/odd ÜRETİLMEZ, DÖNÜŞTÜRÜLMEZ.
  //   filtre: probability >= 30 · sıra: probability DESC · en fazla: 5
  const probabilities = [...(decision?.probabilities ?? [])]
    .filter((p) => p.probability >= 30)
    .sort((a, b) => b.probability - a.probability)
    .slice(0, 5);


  return (
    <ViewShell title="AI Maç Analizi" onClose={onClose}>
      <div className="space-y-3 pb-4">
        {/* Kanıta dayalı analiz — /detail ile birlikte geldi (DB); hazır değilse durum yazılır. */}
        <AnalysisSections analysis={match.analysis} />

        {isLoading && (
          <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 py-3 text-[13px] text-white/55">
            AI analizi yükleniyor…
          </div>
        )}

        {isError && (
          <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 py-3 text-[13px] text-white/55">
            AI analizi şu an yüklenemedi.
          </div>
        )}

        {!isLoading && !isError && (
          <>
            {/* AI Güven Endeksi bloğu KULLANICIYA GÖSTERİLMEZ (ürün kararı):
                skor/sinyal sayısı/veri kalitesi ürünün iç mekanizmasıdır ve kullanıcıyı
                "AI'ya ne kadar güvenmeliyim?" sorusuna itiyordu. Backend confidence
                hesabı, endpoint ve DTO YERİNDE DURUR — yalnız bu render kaldırıldı.
                (decision.confidence başka yüzeylerde kullanılmaya devam ediyor.) */}

            {/* Olası Sonuçlar — senaryo motoru çıktısı (market adı/sırası backend'in),
                her satırda "Senin seçimin" kontrolü. */}
            {probabilities.length > 0 && (
              <PossibleResultsBlock match={match} probabilities={probabilities} />
            )}

            {/* "AI İncele — analizi rahat oku" bağlantısı KALDIRILDI (ürün kararı):
                ikinci ekran aynı Match Intelligence/Gemma verisini başka yerleşimde
                tekrar ediyordu, kullanıcıya yeni bilgi vermiyordu. Analiz tek ekranda.
                /match/[id]/ai ROTASI ARTIK KALDIRILDI: Maçlar satırı, AI İncele ve
                Takip sayfası asıl Maç Detay ekranına (/match/[id]) gider. Eski adres
                yalnız kalıcı yönlendirme bırakır. Analiz TEK ekranda — burada. */}

          </>
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
function PossibleResultsBlock({
  match,
  probabilities,
}: {
  match: MatchDetailDto;
  probabilities: Array<{ market: string; probability: number; odd?: number | null }>;
}) {
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

      <div className="space-y-2">
        {probabilities.map((p, i) => {
          const selected = selectedByLabel.get(p.market);
          const busy = toggle.isPending && toggle.variables?.marketLabel === p.market;
          const disabled = !isLoggedIn || (started && !selected) || busy;

          return (
            <motion.div
              key={`${p.market}-${i}`}
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.25, delay: Math.min(i, 8) * 0.04, ease: "easeOut" }}
              className={`rounded-xl border px-3.5 py-2.5 transition-colors ${
                selected
                  ? "border-formax-green/50 bg-formax-green/[0.08]"
                  : "border-goalai-border/70 bg-black/20"
              }`}
            >
              {/* Market · olasılık · oran. Gerekçe satırın altında, analizdeki aynı market
                  kaydından gelir (ayrı bir bölümde TEKRARLANMAZ). */}
              <div className="flex items-start justify-between gap-3">
                <span className="min-w-0 text-sm leading-snug text-white/90">{p.market}</span>

                <span className="flex shrink-0 items-center gap-3">
                  <span className="flex flex-col items-end gap-0.5 leading-none">
                    <span className="text-base font-bold tabular-nums text-goalai-accent">
                      %{p.probability}
                    </span>
                    {/* ORAN — olasılığın ALTINDA. GERÇEK market oranı (backend
                        MatchMarketOdds → sağlayıcı). Karşılığı yoksa satır hiç çıkmaz;
                        frontend oran ÜRETMEZ, 1/probability YAPMAZ. */}
                    {p.odd != null && (
                      <span className="text-[15px] font-extrabold tabular-nums text-white">
                        {p.odd.toFixed(2)}
                      </span>
                    )}
                  </span>

                  <button
                    type="button"
                    disabled={disabled}
                    aria-pressed={!!selected}
                    aria-label={selected ? `${p.market} seçimini kaldır` : `${p.market} seç`}
                    onClick={() =>
                      toggle.mutate({
                        matchId: match.matchId,
                        marketLabel: p.market,
                        probabilityPercent: p.probability,
                        odd: p.odd ?? null,
                      })
                    }
                    className={`flex h-8 min-w-[32px] items-center justify-center rounded-lg px-2 text-[12px] font-bold transition-colors disabled:opacity-40 ${
                      selected
                        ? "bg-formax-green/20 text-formax-green"
                        : "bg-white/[0.06] text-white/70 hover:bg-white/[0.12]"
                    }`}
                  >
                    {busy ? "…" : selected ? "✓" : "+"}
                  </button>
                </span>
              </div>

              {/* OLASI SENARYONUN GEREKÇESİ — analizdeki aynı market kaydı (kanıtlı). */}
              <ScenarioReasonLines analysis={match.analysis} market={p.market} />

              {/* KULLANICININ SEÇİMİ MODEL TAHMİNİ GİBİ GÖSTERİLMEZ. */}
              {selected && (
                <p className="mt-1.5 text-[10.5px] font-semibold uppercase tracking-wide text-formax-green/85">
                  Senin seçimin · seçim anındaki olasılık %{selected.probabilityPercent}
                </p>
              )}
            </motion.div>
          );
        })}
      </div>

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
