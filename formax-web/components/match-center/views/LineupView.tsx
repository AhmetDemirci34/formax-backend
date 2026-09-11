"use client";

import type { MatchDetailDto } from "@/types/api";
import { ViewShell } from "./ViewShell";
import { LineupPanel } from "@/components/match-center/lineup/LineupPanel";

/**
 * LineupView (activeView === 'lineup') — maç öncesi "Kadro Bilgisi" görünümü.
 *
 * İçerik ortak <see LineupPanel/>'dedir: formasyon, sahada ilk 11, yedekler ve son
 * kontrol. Başlamış ve bitmiş maç ekranları da aynı paneli kullanır.
 *
 * Kaydırılabilir kabuk: yedek listesi eklendiği için içerik tek ekrana sığmayabilir.
 */
export function LineupView({ match, onClose }: { match: MatchDetailDto; onClose: () => void }) {
  return (
    <ViewShell title="Kadro Bilgisi" onClose={onClose}>
      <LineupPanel match={match} />
    </ViewShell>
  );
}
