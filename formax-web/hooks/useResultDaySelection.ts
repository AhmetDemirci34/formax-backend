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

  // İLK AÇILIŞ: bugün sonuç yoksa pencere içindeki en yakın sonuçlu güne düş.
  // Kullanıcıyı boş bir "bugün" ekranında bırakmak, hiç sonuç yok sanmasına yol açar.
  //
  // KAPI isSuccess'tir, isLoading DEĞİL: sekme kapalıyken sorgu DEVRE DIŞIDIR ve
  // devre dışı bir sorgu "yükleniyor" demez. isLoading'e bakan bir sürüm, sekme daha
  // açılmadan boş listeyle çalışıp günü "bugün"e sabitliyordu; sekme açılıp gerçek
  // günler geldiğinde ise gün zaten seçilmiş olduğu için bir daha düzelmiyordu.
  // (Ölçüldü 03.09.2026: sekme "Bugün" ve 0 kartla açılıyordu.)
  useEffect(() => {
    if (day !== null || !daysQuery.isSuccess) return;
    setDay(pickInitialDay(daysWithResults, istanbulDay()));
  }, [day, daysQuery.isSuccess, daysWithResults]);

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
