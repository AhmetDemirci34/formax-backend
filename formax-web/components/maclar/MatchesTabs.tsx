"use client";

// FORMAX · Maçlar ekranının üst sekmeleri — YAKLAŞAN | SONUÇLAR.
//
// Varsayılan YAKLAŞAN'dır: ekranın kilitli kimliği maç öncesidir, sonuçlar ikinci
// yoldur. Neon yeşil YALNIZ seçili sekmede kullanılır (tasarım dili: vurgu tek yerde).

export type MatchesTab = "upcoming" | "results";

interface Props {
  value: MatchesTab;
  onChange: (next: MatchesTab) => void;
}

const TABS: Array<{ id: MatchesTab; label: string }> = [
  { id: "upcoming", label: "YAKLAŞAN" },
  { id: "results", label: "SONUÇLAR" },
];

export function MatchesTabs({ value, onChange }: Props) {
  return (
    <div
      role="tablist"
      aria-label="Maç listesi görünümü"
      className="mx-[18px] mb-2.5 flex max-w-full gap-1 rounded-[12px] bg-bg-glass p-1"
    >
      {TABS.map((t) => {
        const active = t.id === value;
        return (
          <button
            key={t.id}
            type="button"
            role="tab"
            aria-selected={active}
            onClick={() => onChange(t.id)}
            className={`min-w-0 flex-1 truncate rounded-[9px] px-2 py-[7px] text-[11.5px] font-bold tracking-wide transition-colors ${
              active
                ? "bg-neon/[0.14] text-neon"
                : "text-text-muted hover:text-text-secondary"
            }`}
          >
            {t.label}
          </button>
        );
      })}
    </div>
  );
}
