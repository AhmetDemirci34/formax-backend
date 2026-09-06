"use client";

import { useEffect, useRef, useState } from "react";
import { useDebounce } from "@/hooks/useDebounce";
import { searchMatchesByTeam, type MatchResultItemDto } from "@/lib/api/matchResults";

export type SearchScope = "upcoming" | "finished";

interface UseTeamSearchReturn {
  query: string;
  setQuery: (q: string) => void;
  results: MatchResultItemDto[];
  isSearching: boolean;
  isActive: boolean;
  clear: () => void;
}

/**
 * Takım arama hook'u — 300 ms debounce, önceki isteği iptal, min 2 karakter.
 *
 * SALT DB: arama yapmak api-football'a HİÇBİR istek üretmez.
 * AbortController ile önceki istek iptal edilir → StrictMode'da çift istek olmaz.
 */
export function useTeamSearch(scope: SearchScope): UseTeamSearchReturn {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<MatchResultItemDto[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const abortRef = useRef<AbortController | null>(null);

  const debouncedQuery = useDebounce(query.trim(), 300);
  const isActive = query.trim().length >= 2;

  useEffect(() => {
    // Önceki isteği iptal et.
    abortRef.current?.abort();

    // 2 karakterden kısa → aramayı temizle, istek atma.
    if (debouncedQuery.length < 2) {
      setResults([]);
      setIsSearching(false);
      return;
    }

    const controller = new AbortController();
    abortRef.current = controller;
    setIsSearching(true);

    searchMatchesByTeam(debouncedQuery, scope, controller.signal)
      .then((data) => {
        // Bileşen unmount olduysa veya yeni istek başladıysa güncelleme YAPMA.
        if (!controller.signal.aborted) {
          setResults(data);
        }
      })
      .catch((err) => {
        if (err?.name !== "AbortError" && err?.name !== "CanceledError") {
          console.error("Team search failed:", err);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsSearching(false);
        }
      });

    return () => controller.abort();
  }, [debouncedQuery, scope]);

  const clear = () => {
    setQuery("");
    setResults([]);
    abortRef.current?.abort();
  };

  return { query, setQuery, results, isSearching, isActive, clear };
}
