"use client";

/**
 * FORMAX · Bugünün Seçkisi — ekran başlığı.
 *
 * "Günün AI Kombini" kimliği kaldırıldı: bu ekran kupon değil, FORMAX'ın günlük maç
 * seçkisidir. Dekoratif F+ kutusu ve büyük neon glow kaldırıldı (kupon hissini besliyordu).
 *
 * MAÇ SAYISI SABİT DEĞİLDİR: `matchCount` ekranda gerçekten render edilen ayak sayısıdır
 * (selectComboLegs çıktısının uzunluğu). UI hiçbir yerde 4 varsaymaz — 2 ayak varsa "2 MAÇ"
 * yazar. Frontend maç SEÇMEZ, yalnız backend'in verdiğini sayar.
 */
export function AIComboHero({ matchCount }: { matchCount: number }) {
  return (
    <section className="rounded-[24px] border border-white/10 bg-white/[0.03] px-5 py-5">
      {/* Başlık YALNIZ sticky header'da durur — burada tekrarlanmaz (aynı bilgi iki kez
          gösterilmez). Bu blok seçkinin açıklaması ve kapsamıdır. */}
      <p className="max-w-[30ch] text-[13.5px] leading-relaxed text-white/60">
        FORMAX bugün izlemeye değer maçları senin için seçti.
      </p>

      <p className="mt-4 flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wide">
        <span className="tabular-nums text-neon">{matchCount} Maç</span>
        <span aria-hidden className="h-1 w-1 rounded-full bg-white/25" />
        <span className="text-white/45">Bugün + Yakın Günler</span>
      </p>
    </section>
  );
}
