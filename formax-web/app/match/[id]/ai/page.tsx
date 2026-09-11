import { redirect } from "next/navigation";

interface PageProps {
  params: Promise<{ id: string }>;
}

/**
 * KALDIRILDI — /match/[id]/ai
 *
 * Bu ikinci ekran ("AI İncele" başlığı + uzun düz analiz metni + Maçın Hikâyesi /
 * Neden Bu Maç blokları) asıl Maç Detay ekranıyla AYNI Match Intelligence verisini
 * başka bir yerleşimde tekrar ediyordu; kullanıcıya yeni bilgi vermiyordu.
 *
 * Asıl ekran: app/match/[id]/page.tsx (Header + Hero + Asistan görünümleri +
 * MatchCenterBottomNav). Aynı analiz orada "AI Analizi" görünümünde zaten var.
 *
 * Eski adres kırılmasın diye 404 yerine KALICI YÖNLENDİRME bırakıldı: uygulamada
 * bu rotaya giden bağlantı kalmadı, ama dışarıda paylaşılmış/yer imlerine eklenmiş
 * bir URL doğrudan asıl ekrana düşer.
 */
export default async function RemovedMatchAiRoute({ params }: PageProps) {
  const { id } = await params;
  const matchId = Number.parseInt(id, 10);
  redirect(Number.isFinite(matchId) ? `/match/${matchId}` : "/maclar");
}
