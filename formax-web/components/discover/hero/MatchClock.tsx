"use client";

import { useEffect, useState } from "react";
import { computeMatchClock, kickoffMsOf, type MatchClockState } from "@/lib/matchClock";

/**
 * FORMAX · MatchClock — tüm maç kartları için ortak zaman/geri sayım sistemi.
 *
 * MVP KİLİTLİ KARAR: Bu bileşen maç saatine bakarak CANLI/BİTTİ ÜRETMEZ. Canlı durum
 * YALNIZ backend'in gerçek isLive alanından gelir. Saat yalnız planlanan zamanı gösterir:
 *
 *  • backend isLive = true         → 🔴 CANLI (varsa dakika ile)
 *  • kickoff'a > 6 saat            → normal saat (Bugün/Yarın/tarih · HH:MM)
 *  • kickoff'a ≤ 6 saat            → saniyeli geri sayım (HH:MM:SS)
 *  • kickoff geçmiş & backend canlı DEMEDİ → yine planlanan saat (asla "Bitti"/"CANLI")
 *
 * Kaynak: backend kickoff ISO tarihi (matchDate/kickoffTime) + backend isLive/liveMinute.
 * Veri yoksa hiçbir şey uydurulmaz → null döner.
 */

// Hesap TEK ORTAK yerde: lib/matchClock.ts. Maç Merkezi Hero'su da AYNI modülü kullanır →
// aynı maç için iki farklı zaman/durum hesabı artık yoktur. Bu bileşen maç saatinden
// CANLI/BİTTİ ÜRETMEZ (MVP kararı); "live" YALNIZ backend'in isLive alanından gelir.

interface MatchClockProps {
  kickoff?: string | null;
  /** Backend canlı durumu (öncelikli; verilmezse tarihten türetilir). */
  isLive?: boolean;
  /** Backend canlı dakika (ör. 67 → "67'"). Yalnız backend gönderirse gösterilir. */
  liveMinute?: number | null;
}

/** Sadece geri sayım/canlı durumda saniyelik tik gerekir; diğer hâllerde interval kurulmaz. */
export function MatchClock({ kickoff, isLive, liveMinute }: MatchClockProps) {
  const kickoffMs = kickoffMsOf(kickoff);
  const valid = Number.isFinite(kickoffMs);

  const [now, setNow] = useState(() => Date.now());
  const derived: MatchClockState | null = valid ? computeMatchClock(kickoffMs, now) : null;

  // Canlı durum YALNIZ backend'in gerçek isLive alanından gelir — saat CANLI/BİTTİ ÜRETMEZ.
  const live = isLive === true;
  // Canlı dakika backend'den gelir → tik gerekmez; yalnız geri sayımda saniyelik tik.
  const ticking = !live && derived?.mode === "countdown";

  useEffect(() => {
    if (!ticking) return;
    const id = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(id);
  }, [ticking]);

  if (live) {
    return (
      <span className="inline-flex items-center gap-1.5">
        <span className="relative flex h-2 w-2">
          <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-signal-red opacity-75" />
          <span className="relative inline-flex h-2 w-2 rounded-full bg-signal-red" />
        </span>
        {liveMinute != null ? (
          <span className="text-[13px] font-bold tabular-nums text-signal-red">{liveMinute}&apos;</span>
        ) : null}
        <span className="text-[13px] font-bold uppercase tracking-wide text-signal-red">CANLI</span>
      </span>
    );
  }

  if (!derived) return null;
  const state = derived;

  const tone =
    state.mode === "countdown" ? "text-neon" : "text-text-secondary";
  return (
    <span className={`text-[13px] font-semibold tabular-nums tracking-wide ${tone}`}>
      {state.label}
    </span>
  );
}
