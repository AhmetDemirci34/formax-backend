"use client";

import { motion } from "framer-motion";
import { AppShell } from "@/components/layout/AppShell";
import { MyPicksList } from "@/components/predictions/MyPicksList";

/**
 * TAHMİNLERİM — kullanıcının seçtiği olası sonuçlar.
 *
 * KAYNAK DEĞİŞTİ (06.09.2026): ekran artık backend'deki UserPicks kaydını okur
 * (/api/picks/me). Önceki sürüm localStorage'daki `formax_predictions` anahtarını
 * kullanıyordu; seçim yalnız o tarayıcıda yaşıyor, başka cihazda ve tarayıcı
 * verisi temizlendiğinde yok oluyordu.
 *
 * Kart bir MAÇI temsil eder; kullanıcının o maçtaki bir veya birden fazla seçimi
 * altında listelenir. Karta tıklandığında /match/{matchId} açılır.
 *
 * Eski bileşenler (PredictionTabs, PerformanceSummary, PredictionList) SİLİNMEDİ;
 * bu rota onları artık kullanmıyor.
 */
export default function PredictionsPage() {
  return (
    <AppShell header={<PredictionsHeader />}>
      <motion.div
        initial={{ opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.35, ease: "easeOut" }}
      >
        <MyPicksList />
      </motion.div>
    </AppShell>
  );
}

function PredictionsHeader() {
  return (
    <div className="flex items-center gap-2.5 px-4 pb-2 pt-3">
      <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-goalai-accent text-[18px] font-black italic text-[#0a0e16]">
        F
      </span>
      <h1 className="text-[19px] font-bold uppercase tracking-tight text-text-primary">
        Tahminlerim
      </h1>
    </div>
  );
}
