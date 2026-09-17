"use client";

import { AnimatePresence } from "framer-motion";
import type { MatchDetailDto } from "@/types/api";
import { ErrorState } from "@/components/ui/ErrorState";
import { archivoNarrow } from "@/components/match-center/fonts";
import type { ActiveView, MatchAction } from "@/components/match-center/aiContext";
import { MatchCenterHeader } from "@/components/match-center/MatchCenterHeader";
import { MatchCenterSkeleton } from "@/components/match-center/MatchCenterSkeleton";
import { isUpcomingMatch, isFinishedMatch } from "@/lib/matches/upcomingOnly";
import { FinishedMatchSummary } from "@/components/match-center/views/FinishedMatchSummary";
import { MatchCenterHero } from "@/components/match-center/MatchCenterHero";
import { MatchCenterBottomNav } from "@/components/match-center/MatchCenterBottomNav";
import { AssistantDashboard } from "@/components/match-center/dashboard/AssistantDashboard";
import { AIAnalysisView } from "@/components/match-center/views/AIAnalysisView";
import { FormStatusView } from "@/components/match-center/views/FormStatusView";
import { LineupView } from "@/components/match-center/views/LineupView";
import { LineupPanel } from "@/components/match-center/lineup/LineupPanel";
import { NewsView } from "@/components/match-center/views/NewsView";

export const MATCH_DETAIL_ERROR_TEXT = "Maç bilgileri şu an yüklenemiyor. Lütfen tekrar dene.";

export type MatchCenterState =
  | { kind: "loading" }
  | { kind: "error" }
  | { kind: "ready"; match: MatchDetailDto };

interface Props {
  state: MatchCenterState;
  activeView: ActiveView;
  onSelect: (action: MatchAction) => void;
  onCloseView: () => void;
  /** Header geri: alt görünüm açıksa dashboard'a, değilse rota geri. */
  onBack: () => void;
  /** Yükleme/hata/bitmiş ekranlarında rota geri. */
  onRouteBack: () => void;
  onRetry: () => void;
  /** Maçlar listesine dön; `replace` geçmişe yeni kayıt eklemez. */
  onGoMatches: (replace?: boolean) => void;
}

/**
 * Maç Detay Merkezi — görünüm katmanı (veri çekmez).
 *
 * AÇILIŞ KURALI (17.09.2026 kök neden): dashboard eskiden framer-motion `initial={{opacity:0}}`
 * ile çiziliyordu. Veri 100 ms'de gelse bile giriş animasyonu bir JS karesi bekler; pencere
 * odaksız/arka planda ya da tarayıcı kareleri kıstığında (güç tasarrufu, gömülü görünüm) kare
 * gelmez ve Hero'nun altı 10–30 sn boş kalır. Artık İLK çizim animasyonsuzdur
 * (`AnimatePresence initial={false}`): Header, Hero ve aksiyonlar detail DTO ile aynı karede
 * görünür. Görünüm geçişleri (kullanıcı tıklaması) animasyonlu kalır.
 *
 * Yükleme sırasında ölçüsüne uygun iskelet, hatada tekrar deneme gösterilir. Bağımsız geç
 * veriler (AI BEKLENTİSİ snapshot'ı, haberler, kadro) kendi bileşenlerinde yüklenir ve bu ekranı
 * bekletmez.
 */
export function MatchCenterScreen({
  state,
  activeView,
  onSelect,
  onCloseView,
  onBack,
  onRouteBack,
  onRetry,
  onGoMatches,
}: Props) {
  const shell = `relative flex h-[100dvh] flex-col overflow-hidden bg-goalai-surface text-white ${archivoNarrow.variable} ${archivoNarrow.className}`;

  if (state.kind === "loading") {
    return (
      <div className={shell}>
        <MatchCenterHeader onBack={onRouteBack} />
        <MatchCenterSkeleton />
        <MatchCenterBottomNav />
      </div>
    );
  }

  if (state.kind === "error") {
    return (
      <div className={shell}>
        <MatchCenterHeader onBack={onRouteBack} />
        <div className="flex min-h-0 flex-1 flex-col justify-center px-4">
          <ErrorState message={MATCH_DETAIL_ERROR_TEXT} onRetry={onRetry} />
        </div>
        <MatchCenterBottomNav />
      </div>
    );
  }

  const { match } = state;

  // ── BİTMİŞ MAÇ: MAÇ ÖZETİ ────────────────────────────────────────────────
  // Aynı rota durum-duyarlıdır. Maç bittiğinde kullanıcı boş bir ekranla
  // karşılaşmaz; kesinleşmiş sonuç ve kaynaktaki olaylar gösterilir. Bu ekran
  // yalnız DB'den okunur — sağlayıcıya istek ÜRETMEZ ve canlı akış İÇERMEZ.
  if (isFinishedMatch(match.status)) {
    return (
      <div className={shell}>
        <MatchCenterHeader onBack={onRouteBack} title="Maç Özeti" />
        <FinishedMatchSummary match={match} />
      </div>
    );
  }

  // KİLİTLİ ÜRÜN KARARI: FORMAX canlı maç GÖSTERMEZ. Henüz bitmemiş ama başlamış
  // (canlı) bir maç doğrudan URL ile açılırsa canlı skor/dakika/olay GÖSTERİLMEZ;
  // mevcut güvenli davranış korunur.
  if (!isUpcomingMatch({ status: match.status, startTime: match.matchDate })) {
    return (
      <div className={shell}>
        <MatchCenterHeader onBack={() => onGoMatches()} />
        <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto px-4 pb-28 pt-4">
          <div className="flex flex-col items-center gap-3 px-4 text-center">
            {/* "Bu maç oynanıyor" DENMEZ: sonuç alımı gecikmişse maç çoktan bitmiş
                olabilir. Saatten canlı durum ÜRETİLMEZ; yalnız doğru olan söylenir. */}
            <p className="text-[14px] leading-relaxed text-white/85">
              Bu maçın başlama saati geçti. FORMAX canlı yayın ve canlı skor göstermez;
              sonuç kesinleştiğinde maç özeti burada yayımlanır.
            </p>
            <button
              type="button"
              onClick={() => onGoMatches(true)}
              className="rounded-[12px] border border-goalai-accent/30 bg-goalai-accent/10 px-4 py-2 text-[13px] font-bold text-goalai-accent"
            >
              Maçlara dön
            </button>
          </div>

          {/* KADRO — maç başlamış olsa bile DB'deki doğrulanmış kadro kaybolmaz; yoksa
              "doğrulanmış kadro bulunamadı" açıkça yazılır. Sağlayıcıya istek YOK. */}
          <section className="w-full rounded-2xl border border-goalai-border bg-goalai-surface-bright/40 p-3">
            <h2 className="mb-2 text-[12px] font-bold uppercase tracking-wide text-white/85">Kadrolar</h2>
            <LineupPanel match={match} />
          </section>
        </div>
      </div>
    );
  }

  return (
    <div className={shell}>
      {/* Sabit: Header + Hero */}
      <MatchCenterHeader onBack={onBack} />
      <MatchCenterHero match={match} />

      {/* Dinamik içerik alanı */}
      <div className="relative min-h-0 flex-1 px-4 py-4">
        {/* initial={false}: ilk çizimde giriş animasyonu YOK → içerik JS karesi beklemeden görünür. */}
        <AnimatePresence mode="wait" initial={false}>
          {activeView === "dashboard" && (
            <AssistantDashboard key="dashboard" onSelect={onSelect} />
          )}
          {activeView === "analysis" && (
            <AIAnalysisView key="analysis" match={match} onClose={onCloseView} />
          )}
          {activeView === "stats" && (
            <FormStatusView key="stats" match={match} onClose={onCloseView} />
          )}
          {activeView === "lineup" && (
            <LineupView key="lineup" match={match} onClose={onCloseView} />
          )}
          {activeView === "news" && (
            <NewsView key="news" match={match} onClose={onCloseView} />
          )}
        </AnimatePresence>
      </div>

      {/* Sabit: Bottom Nav */}
      <MatchCenterBottomNav />
    </div>
  );
}
