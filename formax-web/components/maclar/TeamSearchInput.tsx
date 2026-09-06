"use client";

import type { SearchScope } from "@/hooks/useTeamSearch";
import { SearchIcon } from "@/components/maclar/icons";

interface Props {
  query: string;
  onChange: (q: string) => void;
  onClear: () => void;
  isSearching: boolean;
}

/**
 * Takım arama kutusu — YAKLAŞAN / SONUÇLAR sekmelerinin ALTINDA,
 * tarih seçicinin ÜSTünde görünür.
 *
 * Placeholder: "Takım ara…"
 * Minimum 2 karakter, 300 ms debounce (hook tarafında).
 * Temizle (×) butonu.
 */
export function TeamSearchInput({ query, onChange, onClear, isSearching }: Props) {
  return (
    <div className="mx-[18px] mb-2">
      <div className="relative">
        <SearchIcon
          size={16}
          className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-muted"
        />
        <input
          type="text"
          value={query}
          onChange={(e) => onChange(e.target.value)}
          placeholder="Takım ara…"
          className="w-full rounded-xl border border-white/10 bg-white/[0.04] py-2 pl-9 pr-9 text-[13px] text-white placeholder:text-text-muted focus:border-[#A855F7]/50 focus:outline-none"
        />
        {query.length > 0 && (
          <button
            type="button"
            onClick={onClear}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-text-muted hover:text-white"
            aria-label="Aramayı temizle"
          >
            {isSearching ? (
              <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent" />
            ) : (
              <span className="text-[16px] leading-none">×</span>
            )}
          </button>
        )}
      </div>
    </div>
  );
}
