import { HomeGate } from "@/components/auth/HomeGate";
import { AppShell } from "@/components/layout/AppShell";
import { DiscoverHeader } from "@/components/discover/DiscoverHeader";
import { MatchDiscoveryFeed } from "@/components/discover/MatchDiscoveryFeed";
import { AIComboSection } from "@/components/discover/combo/AIComboSection";
import { FeaturedMatchesSection } from "@/components/discover/featured/FeaturedMatchesSection";

/**
 * FORMAX · Discover (Keşfet) ekranı — TAMAMEN gerçek backend feed'i.
 *
 * Açılış akışı ürün tasarımına uygundur: HomeGate → Splash → (giriş yoksa) Login → Keşfet.
 * Kök rota artık authenticated kullanıcı ister; anonim doğrudan-Keşfet davranışı kaldırıldı.
 *
 * Tüm bölümler /api/home/recommendations'tan beslenir (mock yok):
 *  • MatchDiscoveryFeed  → swipe'lı gerçek Hero + aktif maçın AI Olası Sonuçları
 *  • AIComboSection      → en yüksek karar paketi skorlu maçlardan türetilen Günün Kombini
 *  • FeaturedMatchesSection → kişisel öneriler (Sana Özel) + Tüm önerileri gör → Maçlar
 */
export default function DiscoverScreen() {
  return (
    <HomeGate>
      <AppShell header={<DiscoverHeader />}>
        {/* Sıra: Sana Özel → Keşfet feed → Günün AI Kombini.
            Üstteki kategori ikonları (Bugünkü Maçlar / Yüksek Olasılıklar /
            Kupon Önerileri / Sürpriz Maçlar) kaldırıldı. */}
        <FeaturedMatchesSection compact />
        <MatchDiscoveryFeed />
        <AIComboSection />
      </AppShell>
    </HomeGate>
  );
}
