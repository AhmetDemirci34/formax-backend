"use client";

// FORMAX · TAHMİNLERİM — kullanıcının seçtiği olası sonuçlar.
//
// KAYNAK BACKEND'DİR (/api/picks/me). Eski ekran localStorage'daki
// `formax_predictions` kaydını okuyordu: seçim yalnız o tarayıcıda yaşıyor,
// başka cihazda yok oluyordu. Artık seçim hesaba bağlıdır.
//
// SETTLEMENT DÜRÜSTLÜĞÜ: doğru/yanlış YALNIZ hesaplanabilen marketlerde gösterilir.
// Hesaplanamayan seçim "yanlış" SAYILMAZ — ekran bunu açıkça söyler.

import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/context/AuthContext";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { getMyPredictions, type UserPickDto, type UserPredictionCardDto } from "@/lib/api/picks";
import { MY_PREDICTIONS_KEY } from "@/hooks/useMatchPicks";

export function MyPicksList() {
  const { isLoggedIn, isHydrated } = useAuth();

  const { data, isLoading, isError } = useQuery<UserPredictionCardDto[]>({
    queryKey: MY_PREDICTIONS_KEY,
    queryFn: getMyPredictions,
    enabled: isLoggedIn,
    staleTime: 30_000,
  });

  if (!isHydrated || (isLoggedIn && isLoading)) return <Skeleton />;

  if (!isLoggedIn) {
    return (
      <Message
        title="Seçimlerini görmek için giriş yapmalısın."
        subtitle="Seçimlerin hesabına kaydedilir; tüm cihazlarında aynı görünür."
      />
    );
  }

  if (isError) {
    return (
      <Message
        title="Seçimler şu an yüklenemedi."
        subtitle="Bağlantı kurulduğunda listen burada görünecek."
      />
    );
  }

  const cards = data ?? [];
  if (cards.length === 0) {
    return (
      <Message
        title="Henüz bir seçim yapmadın."
        subtitle="Maç detayındaki Olası Sonuçlar listesinden seçim yapabilirsin."
      />
    );
  }

  return (
    <div className="flex flex-col gap-2.5 px-4 pb-[calc(var(--bottom-nav-height)+24px)]">
      {cards.map((c) => (
        <PredictionCard key={c.matchId} card={c} />
      ))}
    </div>
  );
}

function PredictionCard({ card }: { card: UserPredictionCardDto }) {
  const router = useRouter();
  const isFinished = card.status?.toLowerCase() === "finished";

  return (
    <button
      type="button"
      onClick={() => router.push(`/match/${card.matchId}`)}
      className="flex w-full flex-col gap-2.5 rounded-[14px] bg-bg-glass p-3 text-left"
    >
      <div className="flex items-center justify-between gap-2">
        <span className="truncate text-[10.5px] font-semibold uppercase tracking-wide text-text-muted">
          {card.league}
        </span>
        <span className="shrink-0 text-[10.5px] tabular-nums text-text-muted">
          {formatDate(card.matchDateUtc)}
        </span>
      </div>

      <div className="flex flex-col gap-1">
        <TeamRow
          name={card.homeTeam}
          logo={card.homeTeamLogoUrl}
          score={isFinished ? card.homeScore : null}
        />
        <TeamRow
          name={card.awayTeam}
          logo={card.awayTeamLogoUrl}
          score={isFinished ? card.awayScore : null}
        />
      </div>

      {/* İY / 2Y / MS — YALNIZ bitmiş maçta ve YALNIZ gerçekten varsa. 0-0 uydurulmaz;
          2Y backend'de hesaplanır (frontend çıkarma yapmaz). */}
      {isFinished && <p className="text-[10.5px] tabular-nums text-text-muted">{scoreLine(card)}</p>}

      <div className="flex flex-col gap-1.5 border-t border-white/[0.06] pt-2">
        <span className="text-[10px] font-bold uppercase tracking-[0.14em] text-text-muted">
          Senin seçimin
        </span>
        {card.selections.map((s) => (
          <SelectionRow key={s.id} selection={s} />
        ))}
      </div>

      <StatusBadge cardStatus={card.cardStatus} />
    </button>
  );
}

export function SelectionRow({ selection }: { selection: UserPickDto }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <span className="min-w-0 flex-1 truncate text-[13px] text-text-primary">
        {selection.label}
      </span>
      <span className="flex shrink-0 items-center gap-2">
        {/* SEÇİM ANINDAKİ olasılık — model sonradan değişse de bu sayı sabittir. */}
        <span className="text-[11.5px] tabular-nums text-text-muted">
          %{selection.probabilityPercent}
        </span>
        <SelectionOutcome selection={selection} />
      </span>
    </div>
  );
}

/**
 * SEÇİMİN SONUCU — uydurma YOK.
 *
 * isCorrect null ise sonuç HESAPLANAMAMIŞTIR (ör. ilk yarı skoru depoda yok,
 * "ilk golü kim attı" skordan türetilemez). Böyle bir seçim sessizce "yanlış"
 * sayılmaz; ekran bunu açıkça söyler.
 */
export function SelectionOutcome({ selection }: { selection: UserPickDto }) {
  const text = selectionOutcomeLabel(selection);
  if (!text) return null;
  const tone =
    selection.isCorrect === true
      ? "font-bold uppercase text-formax-green"
      : selection.isCorrect === false
        ? "font-bold uppercase text-formax-amber"
        : "font-semibold text-text-muted";
  return <span className={`text-[10.5px] ${tone}`}>{text}</span>;
}

/**
 * SEÇİMİN SONUÇ ETİKETİ — saf fonksiyon (test edilir).
 * Hesaplanamayan seçim "Yanlış" SAYILMAZ; sonuçlanmamış seçimde etiket yoktur.
 */
export function selectionOutcomeLabel(selection: Pick<UserPickDto, "selectionStatus" | "isCorrect">): string | null {
  if (selection.selectionStatus === "Unsettleable") return "Sonuç hesaplanamadı";
  if (selection.isCorrect === true) return "Doğru";
  if (selection.isCorrect === false) return "Yanlış";
  return null;
}

/** Kart durumu etiketi — "Tamamlandı" yalnız backend Settled derse. */
export function cardStatusLabel(cardStatus: string): string {
  return cardStatus === "Settled" ? "Tamamlandı" : cardStatus === "Pending" ? "Bekleyen" : "Aktif";
}

/** "İY 1-0 · 2Y 1-1 · MS 2-1" — yalnız gerçekten var olan parçalar. */
export function scoreLine(
  card: Pick<
    UserPredictionCardDto,
    | "homeScore"
    | "awayScore"
    | "halfTimeHomeScore"
    | "halfTimeAwayScore"
    | "secondHalfHomeScore"
    | "secondHalfAwayScore"
  >
): string {
  const parts: string[] = [];
  if (card.halfTimeHomeScore != null && card.halfTimeAwayScore != null)
    parts.push(`İY ${card.halfTimeHomeScore}-${card.halfTimeAwayScore}`);
  if (card.secondHalfHomeScore != null && card.secondHalfAwayScore != null)
    parts.push(`2Y ${card.secondHalfHomeScore}-${card.secondHalfAwayScore}`);
  if (card.homeScore != null && card.awayScore != null) parts.push(`MS ${card.homeScore}-${card.awayScore}`);
  return parts.join(" · ");
}

export function StatusBadge({ cardStatus }: { cardStatus: string }) {
  const label = cardStatusLabel(cardStatus);
  const tone =
    cardStatus === "Settled"
      ? "text-text-muted"
      : cardStatus === "Pending"
        ? "text-formax-amber"
        : "text-goalai-accent";
  return (
    <span className={`text-[10px] font-bold uppercase tracking-[0.14em] ${tone}`}>{label}</span>
  );
}

function TeamRow({
  name,
  logo,
  score,
}: {
  name: string;
  logo?: string | null;
  score?: number | null;
}) {
  return (
    <div className="flex items-center gap-2.5">
      <TeamCrest name={name} logoUrl={logo ?? undefined} size={20} />
      <span className="min-w-0 flex-1 truncate text-[13.5px] font-medium text-text-primary">
        {name}
      </span>
      {score != null && (
        <span className="shrink-0 text-[13.5px] font-bold tabular-nums text-text-primary">
          {score}
        </span>
      )}
    </div>
  );
}

function Message({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <div className="flex flex-col items-center gap-2 px-8 pt-24 text-center">
      <p className="text-[14px] font-semibold text-text-primary">{title}</p>
      <p className="text-[12.5px] leading-relaxed text-text-muted">{subtitle}</p>
    </div>
  );
}

function Skeleton() {
  return (
    <div className="flex flex-col gap-2.5 px-4 pt-2">
      {[0, 1, 2].map((i) => (
        <div key={i} className="h-[150px] animate-pulse rounded-[14px] bg-white/[0.04]" />
      ))}
    </div>
  );
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleString("tr-TR", {
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  });
}
