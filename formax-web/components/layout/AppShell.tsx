import type { ReactNode } from "react";

interface AppShellProps {
  /** Üstte sabit (sticky) duran başlık alanı — Header buraya verilir. */
  header?: ReactNode;
  /** Kaydırılabilir içerik (section'lar). */
  children: ReactNode;
}

/**
 * FORMAX Discover · AppShell (01)
 *
 * Ekran düzeyi iskelet. Telefon çerçevesi (max-width, kenarlık) ve global
 * BottomNav `app/layout.tsx` tarafından sağlanır — AppShell bunları TEKRARLAMAZ.
 *
 * Görevi:
 *  • Header için safe-area duyarlı, sticky bir üst bölge,
 *  • Section'ları dikey ritimle (--section-gap) dizen, alt navigasyonu
 *    örtmeyecek şekilde padding'li kaydırma alanı.
 *
 * Tüm ölçüler Layout System token'larından (globals.css) gelir — magic number yok.
 */
export function AppShell({ header, children }: AppShellProps) {
  return (
    <div className="relative flex min-h-[100dvh] flex-col">
      {/* Ambient zemin — çok hafif radial gradient + premium his (dekoratif) */}
      <div className="fx-ambient pointer-events-none absolute inset-0" aria-hidden />

      {header ? (
        <div className="sticky top-0 z-40 pt-[var(--safe-top)] backdrop-blur-md">
          {header}
        </div>
      ) : null}

      <main className="relative flex flex-1 flex-col gap-6 px-4 pb-[calc(var(--bottom-nav-height)+var(--safe-bottom))] pt-2">
        {children}
      </main>
    </div>
  );
}
