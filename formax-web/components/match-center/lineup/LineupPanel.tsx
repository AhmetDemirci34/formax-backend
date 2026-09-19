"use client";

import { motion } from "framer-motion";
import type { MatchDetailDto, LineupPlayerDto } from "@/types/api";
import { formatLastCheck, hasVerifiedLineup, lineupWaitingText } from "@/lib/lineup/lineupStatus";

/**
 * KADRO PANELİ — tek ortak içerik. Maç öncesi "Kadro Bilgisi" görünümü, başlamış maç
 * ekranı ve bitmiş maç özeti AYNI paneli kullanır; üç ayrı kopya er geç ayrışırdı.
 *
 * VERİ: /api/matches/{id}/detail → lineup (DB). Bu panel sağlayıcıya istek ATMAZ.
 *
 * KURALLAR
 *  • Kadro yalnız DOĞRULANMIŞSA gösterilir (açıklandı + ilk 11 dolu). Uydurma YOK.
 *  • Maç başlamış ya da bitmiş olsa bile DB'deki doğrulanmış kadro KAYBOLMAZ.
 *  • Diziliş TAHMİN EDİLMEZ; saha konumu sağlayıcının grid'inden gelir.
 *  • "Son kontrol" backend'in kalıcı defterinden gelir; yoksa satır hiç çıkmaz.
 *  • Resmî kaynak saha konumu vermediyse (grid yok) saha ÇİZİLMEZ; ilk 11 liste olarak
 *    gösterilir. Konum uydurulmaz.
 *  • Metin kontrastı mobilde okunur olmalı: bekleme metni ≥ %85, yardımcı satır ≥ %70
 *    beyaz (koyu zemin üzerinde).
 */
export function LineupPanel({ match, pitchHeight = 440 }: { match: MatchDetailDto; pitchHeight?: number }) {
  const l = match.lineup;
  const lastCheck = formatLastCheck(l?.lastCheckedUtc);

  if (!hasVerifiedLineup(l)) {
    return (
      <div className="flex flex-col items-center justify-center gap-2 px-6 py-8 text-center" data-lineup-state="waiting">
        <p className="max-w-[300px] text-[14px] leading-relaxed text-white/90">{lineupWaitingText(l)}</p>
        {lastCheck && <p className="text-[12px] tabular-nums text-white/75">{lastCheck}</p>}
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-2" data-lineup-state="verified">
      {/* Diziliş künyesi — takım adı + GERÇEK formation (yoksa "mevcut değil") */}
      <div className="shrink-0 space-y-1 rounded-xl border border-goalai-border bg-goalai-surface-bright px-3 py-2">
        <FormationRow name={match.awayTeam?.name} formation={l.awayFormation} away />
        <FormationRow name={match.homeTeam?.name} formation={l.homeFormation} />
      </div>

      {hasPitchPositions(l.homeStartingXI, l.awayStartingXI) ? (
        <div style={{ height: pitchHeight }} className="flex">
          <Pitch home={l.homeStartingXI} away={l.awayStartingXI} />
        </div>
      ) : (
        <StartersList
          homeName={match.homeTeam?.name}
          awayName={match.awayTeam?.name}
          home={l.homeStartingXI ?? []}
          away={l.awayStartingXI ?? []}
        />
      )}

      <BenchSection
        homeName={match.homeTeam?.name}
        awayName={match.awayTeam?.name}
        home={l.homeBench ?? []}
        away={l.awayBench ?? []}
      />

      {(l.homeCoach || l.awayCoach) && (
        <p className="px-1 text-[12px] text-white/80">
          Teknik direktör: {[l.homeCoach, l.awayCoach].filter(Boolean).join(" · ")}
        </p>
      )}
      {l.source && <p className="px-1 text-[12px] text-white/75">Resmî kaynak: {l.source}</p>}
      {lastCheck && <p className="px-1 text-[12px] tabular-nums text-white/75">{lastCheck}</p>}
    </div>
  );
}

/**
 * MEVKİ ETİKETİ — yalnız resmî kaynağın verdiği G/D/M/F kodundan. Kaynak vermediyse ya da
 * tanınmayan bir değer geldiyse etiket HİÇ ÇIKMAZ; mevki tahmin edilmez.
 */
export function positionLabel(position?: string | null): string | null {
  switch (position?.trim().toUpperCase()) {
    case "G":
      return "KL";
    case "D":
      return "DEF";
    case "M":
      return "ORT";
    case "F":
      return "FOR";
    default:
      return null;
  }
}

/**
 * Sahaya yerleştirme ancak İLK 11 oyuncularının tamamı kaynaktan gelen grid taşıyorsa yapılır.
 * Tek bir eksik konum bile sahayı yanıltıcı yapar → liste görünümü.
 */
export function hasPitchPositions(home?: LineupPlayerDto[], away?: LineupPlayerDto[]): boolean {
  const all = [...(home ?? []), ...(away ?? [])];
  return all.length > 0 && all.every((p) => typeof p.grid === "string" && /^\d+\s*:\s*\d+$/.test(p.grid.trim()));
}

/** İLK 11 LİSTESİ — saha konumu olmayan resmî kadrolar için (ör. TFF). */
export function StartersList({
  homeName,
  awayName,
  home,
  away,
}: {
  homeName?: string;
  awayName?: string;
  home: LineupPlayerDto[];
  away: LineupPlayerDto[];
}) {
  return (
    <section className="rounded-xl border border-goalai-border bg-goalai-surface-bright px-3 py-2.5" data-lineup-view="list">
      <h3 className="mb-2 text-[11px] font-bold uppercase tracking-wide text-white/85">İlk 11</h3>
      <div className="grid grid-cols-2 gap-3">
        <BenchColumn title={homeName} players={home} accent emptyText="İlk 11 açıklanmadı" />
        <BenchColumn title={awayName} players={away} emptyText="İlk 11 açıklanmadı" />
      </div>
    </section>
  );
}

/** YEDEKLER — iki sütun; forma numarasına göre (backend sıralaması korunur). */
export function BenchSection({
  homeName,
  awayName,
  home,
  away,
}: {
  homeName?: string;
  awayName?: string;
  home: LineupPlayerDto[];
  away: LineupPlayerDto[];
}) {
  if (home.length === 0 && away.length === 0) return null;
  return (
    <section className="rounded-xl border border-goalai-border bg-goalai-surface-bright px-3 py-2.5">
      <h3 className="mb-2 text-[11px] font-bold uppercase tracking-wide text-white/85">Yedekler</h3>
      <div className="grid grid-cols-2 gap-3">
        <BenchColumn title={homeName} players={home} accent />
        <BenchColumn title={awayName} players={away} />
      </div>
    </section>
  );
}

function BenchColumn({
  title,
  players,
  accent = false,
  emptyText = "Yedek bilgisi yok",
}: {
  title?: string;
  players: LineupPlayerDto[];
  accent?: boolean;
  emptyText?: string;
}) {
  return (
    <div className="min-w-0">
      <p className={`mb-1 truncate text-[10.5px] font-semibold uppercase ${accent ? "text-goalai-accent" : "text-white/80"}`}>
        {title ?? "—"}
      </p>
      {players.length === 0 ? (
        <p className="text-[11px] text-white/70">{emptyText}</p>
      ) : (
        <ul className="flex flex-col gap-0.5">
          {players.map((p, i) => (
            <li key={`${p.shirtNumber}-${i}`} className="flex min-w-0 items-baseline gap-1.5 text-[11.5px] text-white/90">
              <span className="w-5 shrink-0 text-right font-bold tabular-nums text-white/75">{p.shirtNumber}</span>
              <span className="truncate">{p.playerName}</span>
              {positionLabel(p.position) && (
                <span className="ml-auto shrink-0 text-[10px] uppercase tracking-wide text-white/60">
                  {positionLabel(p.position)}
                </span>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function FormationRow({
  name,
  formation,
  away = false,
}: {
  name?: string;
  formation?: string | null;
  away?: boolean;
}) {
  const value = formation && formation.trim().length > 0 ? formation.trim() : null;
  return (
    <div className="flex items-baseline justify-between gap-3">
      <span className="flex min-w-0 items-center gap-1.5">
        <span
          aria-hidden
          className={`h-2 w-2 shrink-0 rounded-full ${away ? "bg-white" : "bg-goalai-accent"}`}
        />
        <span className="min-w-0 truncate text-[11px] font-semibold uppercase tracking-wide text-white/85">
          {name ?? "—"}
        </span>
      </span>
      <span
        className={`shrink-0 text-[12px] font-bold tabular-nums ${
          value ? "text-goalai-accent" : "text-white/70"
        }`}
      >
        {value ?? "Diziliş bilgisi mevcut değil"}
      </span>
    </div>
  );
}

/** "2:4" → { line: 2, slot: 4 }. Biçim beklenmedikse null (konum ÜRETİLMEZ). */
function parseGrid(grid?: string | null): { line: number; slot: number } | null {
  if (!grid) return null;
  const m = /^(\d+)\s*:\s*(\d+)$/.exec(grid.trim());
  if (!m) return null;
  const line = Number(m[1]);
  const slot = Number(m[2]);
  if (!Number.isFinite(line) || !Number.isFinite(slot) || line < 1 || slot < 1) return null;
  return { line, slot };
}

interface Marker {
  player: LineupPlayerDto;
  side: "home" | "away";
  xPct: number;
  yPct: number;
}

/**
 * Kenar payı — oyuncu ve ismi saha dışına taşmasın.
 * 8% seçildi: 5'li hatta yatay aralık (100-16)/4 = 21% ≈ 76px, işaretleyici 64px →
 * ~12px nefes payı (10%'da bu pay 4px'e düşüyordu, isimler bitişik görünüyordu).
 */
const X_MARGIN = 8;
/**
 * Takımın kendi yarısında kullandığı dikey bant.
 *
 * Merkezdeki 12% boşluk ÖLÇÜMLE seçildi: işaretleyici ~42px yüksekliğinde ve tipik
 * saha ~470px; iki takımın en ileri hatları 6% (≈28px) aralıkla konduğunda aynı x'e
 * düşen forvetler üst üste biniyordu (15497 ve 32550'de ölçüldü). 12% ≈ 56px > 42px.
 */
const HOME_BACK = 96;
const HOME_FRONT = 56;
const AWAY_BACK = 4;
const AWAY_FRONT = 44;

/**
 * Gerçek grid → ekran koordinatı.
 *  • Hat (grid'in 1. sayısı) → y: takımın kendi kalesinden merkeze doğru EŞİT dağıtılır.
 *    Hat sayısı dizilişten gelir (4-2-3-1 → GK + 4 hat), bu yüzden 3-4-1-2 ile 4-3-3
 *    farklı y dağılımı üretir. Sabit 4 satır YOKTUR.
 *  • Hat içi sıra (2. sayı) → x: ARTAN sırayla, kenar payı bırakılarak eşit aralıklı.
 *    Sağ/sol yönü sağlayıcı yanıtından doğrulanamadığı için TERS ÇEVRİLMEZ.
 */
function placeByGrid(players: LineupPlayerDto[], side: "home" | "away") {
  const withGrid = players
    .map((p) => ({ p, g: parseGrid(p.grid) }))
    .filter((x): x is { p: LineupPlayerDto; g: { line: number; slot: number } } => x.g !== null);

  const missing = players.filter((p) => parseGrid(p.grid) === null);

  const lines = [...new Set(withGrid.map((x) => x.g.line))].sort((a, b) => a - b);
  const lineCount = lines.length;

  const back = side === "home" ? HOME_BACK : AWAY_BACK;
  const front = side === "home" ? HOME_FRONT : AWAY_FRONT;

  const markers: Marker[] = lines.flatMap((line, lineIndex) => {
    const t = lineCount <= 1 ? 0 : lineIndex / (lineCount - 1);
    const yPct = back + (front - back) * t;

    const inLine = withGrid.filter((x) => x.g.line === line).sort((a, b) => a.g.slot - b.g.slot);
    const n = inLine.length;
    const usable = 100 - X_MARGIN * 2;

    return inLine.map((x, i) => ({
      player: x.p,
      side,
      // n=1 → tam orta; n>1 → kenar paylı eşit dağılım.
      xPct: n === 1 ? 50 : X_MARGIN + (i / (n - 1)) * usable,
      yPct,
    }));
  });

  return { markers, missing };
}

/**
 * ÇAKIŞMA KAYDIRMASI — yalnız görsel son çare.
 * İki işaretleyici hem yatayda hem dikeyde çok yakınsa birini birkaç puan kaydırır.
 * Hat DEĞİŞMEZ, oyuncu başka pozisyona TAŞINMAZ; kaydırma hat aralığından küçüktür.
 */
function separate(markers: Marker[]): Marker[] {
  const MIN_X = 13;
  const MIN_Y = 9;
  const out = markers.map((m) => ({ ...m }));

  for (let i = 0; i < out.length; i++) {
    for (let j = i + 1; j < out.length; j++) {
      const a = out[i];
      const b = out[j];
      // Çapraz takım çiftleri de kontrol edilir: merkez çizgide karşı takımın
      // forvetiyle çakışma ölçülen gerçek bir durumdu (aynı x, dy ≈ 28px).
      if (Math.abs(a.xPct - b.xPct) >= MIN_X) continue;
      if (Math.abs(a.yPct - b.yPct) >= MIN_Y) continue;
      // Yukarıdaki her zaman daha yukarı, aşağıdaki daha aşağı itilir (hat sırası korunur).
      const push = 2.5;
      if (a.yPct <= b.yPct) {
        a.yPct -= push;
        b.yPct += push;
      } else {
        a.yPct += push;
        b.yPct -= push;
      }
    }
  }
  return out;
}

function Pitch({ home, away }: { home: LineupPlayerDto[]; away: LineupPlayerDto[] }) {
  const homePlaced = placeByGrid(home, "home");
  const awayPlaced = placeByGrid(away, "away");
  const markers = separate([...homePlaced.markers, ...awayPlaced.markers]);
  const missing = [...homePlaced.missing, ...awayPlaced.missing];

  return (
    <div className="relative min-h-0 flex-1 overflow-hidden rounded-2xl border border-goalai-border bg-[linear-gradient(180deg,#16391f_0%,#123018_50%,#16391f_100%)]">
      {/* saha çizgileri */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute left-0 right-0 top-1/2 h-px -translate-y-1/2 bg-white/20" />
        <div className="absolute left-1/2 top-1/2 h-16 w-16 -translate-x-1/2 -translate-y-1/2 rounded-full border border-white/20" />
        <div className="absolute inset-2 rounded-xl border border-white/15" />
      </div>

      {markers.map((m, i) => (
        <PlayerMarker key={`${m.side}-${m.player.shirtNumber}-${i}`} {...m} index={i} />
      ))}

      {missing.length > 0 && (
        <p className="absolute inset-x-3 bottom-1.5 truncate text-center text-[9px] text-white/55">
          Saha konumu bildirilmeyen: {missing.map((p) => p.playerName).join(", ")}
        </p>
      )}
    </div>
  );
}

/** Uzun adı tek satırda okunur tut: "Pierre-Emerick Aubameyang" → "P. Aubameyang". */
function shortName(full: string): string {
  const parts = full.trim().split(/\s+/);
  if (parts.length < 2) return full;
  return `${parts[0][0]}. ${parts[parts.length - 1]}`;
}

function PlayerMarker({
  player,
  xPct,
  yPct,
  side,
  index,
}: Marker & { index: number }) {
  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.6 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: 0.25, delay: 0.02 * index, ease: "easeOut" }}
      className="absolute flex w-16 -translate-x-1/2 -translate-y-1/2 flex-col items-center gap-0.5"
      style={{ left: `${xPct}%`, top: `${yPct}%` }}
      title={`${player.shirtNumber} · ${player.playerName}`}
    >
      <span
        className={`flex h-[26px] w-[26px] items-center justify-center rounded-full text-[11px] font-bold shadow-[0_2px_6px_rgba(0,0,0,0.45)] ${
          side === "home"
            ? "bg-goalai-accent text-goalai-surface"
            : "bg-white text-goalai-surface"
        }`}
      >
        {player.shirtNumber}
      </span>
      {/* İsim büyütüldü (8.5px → 10px) ama işaretleyici YÜKSEKLİĞİ artmadı:
          daire 28→26px ve leading-none ile toplam ~40px'te kaldı, böylece hat
          aralığındaki nefes payı korunuyor. Uzun ad kısaltılır + ellipsis. */}
      <span className="w-full truncate text-center text-[10px] font-semibold leading-none text-white drop-shadow-[0_1px_2px_rgba(0,0,0,0.9)]">
        {shortName(player.playerName)}
      </span>
    </motion.div>
  );
}
