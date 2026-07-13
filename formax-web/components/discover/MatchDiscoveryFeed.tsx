"use client";

import { useState } from "react";
import { HeroSection } from "@/components/discover/hero/HeroSection";
import { AIPredictionsSection } from "@/components/discover/AIPredictionsSection";
import { MATCHES } from "@/components/discover/hero/heroData";

/**
 * FORMAX · MatchDiscoveryFeed (05)
 * Hero swipe'ı ile AI Olası Sonuçlar'ı ortak "aktif maç" state'inde birleştirir.
 * Kullanıcı Hero'yu kaydırınca hem Hero hem AI Olası Sonuçlar yeni maça güncellenir.
 * Bölümler arası dikey ritim (gap-6) AppShell ile aynı korunur.
 */
export function MatchDiscoveryFeed() {
  const [index, setIndex] = useState(0);
  const active = MATCHES[index];

  return (
    <div className="flex flex-col gap-6">
      <HeroSection matches={MATCHES} onIndexChange={setIndex} />
      <AIPredictionsSection predictions={active.predictions} matchKey={active.id} />
    </div>
  );
}
