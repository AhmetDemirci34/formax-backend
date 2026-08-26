"use client";

import { motion } from "framer-motion";
import type { MatchDetailDto } from "@/types/api";
import { useMatchDecision } from "@/hooks/useMatchDecision";
import { NarrativeBlocks, hasNarrativeContent } from "../narrative/NarrativeBlocks";
import { ViewShell } from "./ViewShell";

/**
 * AIAnalysisView (activeView === 'analysis') — FORMAX'ın merkez ekranı.
 *
 * ANLATININ TEK KAYNAĞI: Match Intelligence (Gemma) → match.aiNarrative.
 * Sayfa zaten /detail'i yüklediği için EK İSTEK ATILMAZ; Keşfet ve AI İncele
 * de aynı react-query cache'ini (["match", matchId]) okur → ikinci AI üretimi olmaz.
 *
 * KİLİTLİ KARAR (16.08): Eski Decision/MatchReadingEngine ANLATISI kullanıcıya
 * GÖSTERİLMEZ. Ölçüldü: o katman maçta yer almayan takımdan söz ediyor
 * ("Galatasaray ile aradaki fark" — Kasımpaşa–Trabzonspor maçında), aynı cümleyi
 * tekrarlıyor ve bozuk ek üretiyordu. Endpoint ve backend kodu YERİNDE DURUR;
 * bu ekran /decision'dan yalnız SAYISAL karar verisini okur:
 *   • probabilities → Olası Sonuçlar (market adı/yüzdesi/oranı backend'indir)
 *
 * KULLANICIYA GÖSTERİLMEYENLER (ürün kararı, backend'de silinmedi):
 *   • decision.confidence → AI Güven endeksi bloğu (iç mekanizma)
 *   • "AI İncele" ikinci ekranına bağlantı (aynı veriyi tekrar ediyordu)
 *
 * Boş blok tamamen gizlenir — placeholder yok, uydurma metin yok, frontend
 * hesaplaması yok.
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

  // Gemma anlatısı — /detail yanıtından birebir; burada hiçbir alan türetilmez.
  const narrative = match.aiNarrative ?? null;
  const hasNarrative = hasNarrativeContent(narrative);

  return (
    <ViewShell title="AI Maç Analizi" onClose={onClose}>
      <div className="space-y-3 pb-4">
        {/* Match Intelligence anlatısı — /detail ile birlikte geldi, beklemez. */}
        {hasNarrative && narrative && <NarrativeBlocks narrative={narrative} />}

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

            {/* Olası Sonuçlar — senaryo motoru çıktısı (market adı/sırası backend'in) */}
            {probabilities.length > 0 && (
              <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright p-4">
                <h3 className="mb-3 text-xs font-semibold uppercase tracking-[0.16em] text-goalai-accent">
                  Olası Sonuçlar
                </h3>
                <div className="space-y-2">
                  {probabilities.map((p, i) => (
                    <motion.div
                      key={`${p.market}-${i}`}
                      initial={{ opacity: 0, y: 8 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ duration: 0.25, delay: Math.min(i, 8) * 0.04, ease: "easeOut" }}
                      className="rounded-xl border border-goalai-border/70 bg-black/20 px-3.5 py-2.5"
                    >
                      {/* Market · olasılık · oran. Gerekçe BU BÖLÜMDE tekrarlanmaz —
                          o "Olası Sonuçların Gerekçesi" bölümünün işidir (tek görev kuralı). */}
                      <div className="flex items-start justify-between gap-3">
                        <span className="min-w-0 text-sm leading-snug text-white/90">{p.market}</span>
                        <span className="flex shrink-0 flex-col items-end gap-0.5 leading-none">
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
                      </div>
                    </motion.div>
                  ))}
                </div>
              </div>
            )}

            {/* "AI İncele — analizi rahat oku" bağlantısı KALDIRILDI (ürün kararı):
                ikinci ekran aynı Match Intelligence/Gemma verisini başka yerleşimde
                tekrar ediyordu, kullanıcıya yeni bilgi vermiyordu. Analiz tek ekranda.
                /match/[id]/ai ROTASI SİLİNMEDİ — Maçlar ekranı (app/maclar/page.tsx)
                hâlâ oraya yönlendiriyor; rotayı silmek o akışı kırardı. */}

            {!hasNarrative && probabilities.length === 0 && (
              <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 py-3 text-[13px] text-white/55">
                Bu maç için AI analizi henüz üretilmedi.
              </div>
            )}
          </>
        )}
      </div>
    </ViewShell>
  );
}
