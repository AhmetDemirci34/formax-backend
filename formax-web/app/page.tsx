import { AppShell } from "@/components/layout/AppShell";
import { DiscoverHeader } from "@/components/discover/DiscoverHeader";
import { AICategorySelector } from "@/components/discover/AICategorySelector";
import { MatchDiscoveryFeed } from "@/components/discover/MatchDiscoveryFeed";
import { AIComboSection } from "@/components/discover/combo/AIComboSection";
import { FeaturedMatchesSection } from "@/components/discover/featured/FeaturedMatchesSection";

/**
 * FORMAX · Discover (Keşfet) ekranı.
 *
 * Eski swipe-feed ekranı `app/page.swipe.tsx.bak` olarak yedeklendi.
 * Akış: kategori seçici → MatchDiscoveryFeed (swipe'lı Hero + maça bağlı AI Olası Sonuçlar)
 *       → Günün AI Kombini → Sana Özel Maçlar.
 */
export default function DiscoverScreen() {
  return (
    <AppShell header={<DiscoverHeader />}>
      <AICategorySelector />
      <MatchDiscoveryFeed />
      <AIComboSection />
      <FeaturedMatchesSection />
    </AppShell>
  );
}
