"use client";

import { useEffect, useMemo, useState } from "react";
import { useMatchResultDays } from "./useMatchResults";
import { istanbulDay, oldestSelectableDay, pickInitialDay } from "@/lib/matches/resultDays";

/**
 * SONUÇLAR sekmesinin GÜN SEÇİMİ — tek sahip.
 *
 * NEDEN AYRI KANCA: tarih seçici sticky başlığın İÇİNDE, liste ise <main> içinde
 * yaşar. İkisi aynı günü göstermek zorunda ama farklı ağaç dallarındalar. Gün
 * durumunu iki yerde tutmak, kullanıcının seçtiği günle listenin gösterdiği günün
 * ayrışmasına yol açardı; bu kanca ikisini tek kaynaktan besler.
 *
 * Seçim sekme kapalıyken de KORUNUR: kullanıcı YAKLAŞAN'a gidip geri döndüğünde
 * aynı güne düşer, liste başa zıplamaz.
 */
export function useResultDaySelection(active: boolean) {
  const [day, setDay] = useState<string | null>(null);
  const daysQuery = useMatchResultDays(active);
  const daysWithResults = useMemo(() => daysQuery.data ?? [], [daysQuery.data]);

  // İLK AÇILIŞ: HER ZAMAN BUGÜN (Europe/Istanbul). Bugün sonuç yoksa dürüst boş durum
  // gösterilir; düne OTOMATİK geçilmez (ürün kararı 13.09.2026). "Son sonuçlar"
  // yardımcı butonu kullanıcının kendi seçimiyle önceki sonuçlu güne götürür.
  // Gün seçimi gün listesi sorgusunu BEKLEMEZ.
  useEffect(() => {
    if (day !== null || !active) return;
    setDay(pickInitialDay(istanbulDay()));
  }, [day, active]);

  const nearestResultDay = useMemo(() => {
    const today = istanbulDay();
    const oldest = oldestSelectableDay(today);
    const inWindow = daysWithResults
      .filter((d) => d.matchCount > 0 && d.date <= today && d.date >= oldest)
      .map((d) => d.date)
      .sort();
    return inWindow.length > 0 ? inWindow[inWindow.length - 1] : null;
  }, [daysWithResults]);

  return { day, setDay, nearestResultDay, windowHasAnyResult: nearestResultDay !== null };
}
