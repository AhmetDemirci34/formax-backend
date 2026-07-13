"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/context/AuthContext";
import { useAllTeams, useSetMyTeams } from "@/hooks/useTeams";
import type { TeamDto } from "@/types/api";

export default function OnboardingTeamsPage() {
  const router = useRouter();
  const { isLoggedIn } = useAuth();
  const { data: teams = [], isLoading } = useAllTeams();
  const { mutateAsync: saveTeams, isPending } = useSetMyTeams();

  const [selected, setSelected] = useState<Set<number>>(new Set());

  useEffect(() => {
    if (!isLoggedIn) {
      router.replace("/auth/login");
    }
  }, [isLoggedIn, router]);

  function toggle(id: number) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  async function handleContinue() {
    await saveTeams([...selected]);
    router.push("/");
  }

  function handleSkip() {
    router.push("/");
  }

  return (
    <div className="min-h-screen bg-bg-base flex flex-col">
      {/* Header */}
      <div className="px-6 pt-10 pb-4">
        <div className="text-accent font-black text-xl tracking-tight mb-1">FORMAX</div>
        <h1 className="text-text-primary text-2xl font-bold mt-4 mb-1">
          Takımlarını seç
        </h1>
        <p className="text-text-muted text-sm">
          İlgilendiğin takımları seç. İstediğin zaman değiştirebilirsin.
        </p>
      </div>

      {/* Team grid */}
      <div className="flex-1 overflow-y-auto px-4 pb-32">
        {isLoading ? (
          <div className="grid grid-cols-2 gap-3 mt-2">
            {Array.from({ length: 8 }).map((_, i) => (
              <div
                key={i}
                className="h-14 rounded-xl bg-bg-card border border-border animate-pulse"
              />
            ))}
          </div>
        ) : (
          <div className="grid grid-cols-2 gap-3 mt-2">
            {teams.map((team: TeamDto) => {
              const isSelected = selected.has(team.id);
              return (
                <button
                  key={team.id}
                  onClick={() => toggle(team.id)}
                  className={`flex items-center gap-3 px-4 py-3 rounded-xl border text-left transition-all ${
                    isSelected
                      ? "bg-accent/10 border-accent text-accent"
                      : "bg-bg-card border-border text-text-primary hover:border-border-active"
                  }`}
                >
                  <span className="flex-1 text-sm font-medium leading-tight truncate">
                    {team.name}
                  </span>
                  {isSelected && (
                    <span className="text-accent text-xs font-bold shrink-0">✓</span>
                  )}
                </button>
              );
            })}
          </div>
        )}
      </div>

      {/* Fixed bottom bar */}
      <div className="fixed bottom-0 left-0 right-0 bg-bg-base border-t border-border-dim px-4 py-4 flex gap-3">
        <button
          onClick={handleSkip}
          className="flex-1 py-3 rounded-xl text-sm font-medium text-text-muted border border-border hover:text-text-secondary hover:border-border-active transition-colors"
        >
          Atla
        </button>
        <button
          onClick={handleContinue}
          disabled={selected.size === 0 || isPending}
          className="flex-2 px-8 py-3 rounded-xl text-sm font-semibold bg-accent text-white disabled:opacity-40 disabled:cursor-not-allowed hover:bg-accent-dim transition-colors"
        >
          {isPending
            ? "Kaydediliyor..."
            : `Devam Et${selected.size > 0 ? ` (${selected.size})` : ""}`}
        </button>
      </div>
    </div>
  );
}
