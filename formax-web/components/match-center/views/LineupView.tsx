"use client";

import { motion } from "framer-motion";
import type { MatchDetailDto, LineupPlayerDto } from "@/types/api";
import { ViewShell } from "./ViewShell";

/**
 * LineupView (activeView === 'lineup') — AÇIKLANAN dizilişin görselleştirmesi.
 *
 * VERİ: /api/matches/{id}/detail → lineup.{homeFormation, awayFormation} ve her
 * oyuncuda `grid` ("hat:sıra"). İkisi de api-football'ın açıkladığı gerçek değerler;
 * DB'de saklanır (MatchLineups.Home/AwayFormation, MatchLineupPlayers.Grid) ve bu
 * ekran ek API çağrısı YAPMAZ.
 *
 * KURALLAR
 *  • Diziliş TAHMİN EDİLMEZ; hat sayısı grid'in ilk sayısından, hat içi sıra ikinci
 *    sayısından gelir. "Bilinmeyen pozisyon → M" zorlaması YOKTUR.
 *  • Grid DEĞİŞTİRİLMEZ. Aşağıdaki tek şey ekran koordinatının normalize edilmesidir:
 *    kenar payı, hat aralığı ve son çare olarak çok küçük çakışma kaydırması.
 *  • Grid'i olmayan oyuncu sahaya KONMAZ (uydurulmaz), ayrıca listelenir.
 */
export function LineupView({ match, onClose }: { match: MatchDetailDto; onClose: () => void }) {
  const l = match.lineup;
  const announced = l?.lineupsAnnounced === true;
  const hasPlayers = (l?.homeStartingXI?.length ?? 0) > 0 || (l?.awayStartingXI?.length ?? 0) > 0;

  return (
    <ViewShell title="Kadro Bilgisi" onClose={onClose} scroll={false}>
      {announced && hasPlayers ? (
        <div className="flex h-full flex-col gap-2">
          {/* Diziliş künyesi — takım adı + GERÇEK formation (yoksa "mevcut değil") */}
          <div className="shrink-0 space-y-1 rounded-xl border border-goalai-border bg-goalai-surface-bright px-3 py-2">
            <FormationRow name={match.awayTeam?.name} formation={l.awayFormation} away />
            <FormationRow name={match.homeTeam?.name} formation={l.homeFormation} />
          </div>

          <Pitch
            home={l.homeStartingXI}
            away={l.awayStartingXI}
          />
        </div>
      ) : (
        <LineupWaitingState lineup={l} />
      )}
    </ViewShell>
  );
}

/**
 * KADRO BEKLEME DURUMU — VAAT DEĞİL, DURUM.
 *
 * KALDIRILAN METİN (06.09.2026): "Kadrolar henüz açıklanmadı. Genellikle maçtan
 * ~1 saat önce netleşir." Bu sabit cümle YANLIŞTI: kadro her zaman tam bir saat
 * önce yayımlanmaz ve sistem o sırada henüz hiç sormamış bile olabiliyordu.
 * Ölçüldü: maça 45 dakika kalmışken kullanıcıya hâlâ "1 saat önce açıklanacak"
 * yazıyordu. Artık ekran üç GERÇEK duruma göre konuşur ve saat SÖZÜ VERMEZ:
 *
 *   • pencere henüz açılmadı (kickoff'a 90 dk'dan fazla var)
 *   • pencere açık, sağlayıcıda henüz veri yok
 *   • kickoff geçti, doğrulanmış kadro verisi hiç gelmedi
 *
 * Üç alanın da kaynağı backend'dir (lineup.pollingWindowOpen / kickoffPassed /
 * lastCheckedUtc). Bu ekran sağlayıcıya İSTEK ATMAZ — yalnız DB'den geleni okur.
 */
function LineupWaitingState({ lineup }: { lineup?: MatchDetailDto["lineup"] }) {
  const windowOpen = lineup?.pollingWindowOpen === true;
  const kickoffPassed = lineup?.kickoffPassed === true;
  const lastChecked = lineup?.lastCheckedUtc ?? null;

  const message = kickoffPassed
    ? "Bu maç için doğrulanmış kadro verisi bulunamadı."
    : windowOpen
      ? "Resmî kadrolar henüz veri sağlayıcısında yayımlanmadı. Yayımlandığında burada gösterilecek."
      : "Resmî kadrolar maç saatine yaklaşıldığında burada gösterilecek.";

  return (
    <div className="flex h-full flex-col items-center justify-center gap-2 px-6 text-center">
      <p className="max-w-[280px] text-sm leading-relaxed text-white/55">{message}</p>
      {/* Son kontrol yalnız GERÇEKTEN sorulmuşsa gösterilir; yoksa satır hiç çıkmaz. */}
      {lastChecked && !kickoffPassed && (
        <p className="text-[11px] tabular-nums text-white/30">
          Son kontrol: {formatCheckTime(lastChecked)}
        </p>
      )}
    </div>
  );
}

/** UTC damgasını kullanıcının yerel saatine çevirir; bozuksa satır gösterilmez. */
function formatCheckTime(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });
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
          value ? "text-goalai-accent" : "text-white/45"
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
