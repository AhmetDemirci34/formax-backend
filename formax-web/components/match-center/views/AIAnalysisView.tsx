"use client";

import { motion } from "framer-motion";
import type { MatchDetailDto } from "@/types/api";
import { useMatchDecision } from "@/hooks/useMatchDecision";
import { useMatchPicks } from "@/hooks/useMatchPicks";
import { NarrativeBlocks, hasNarrativeContent } from "../narrative/NarrativeBlocks";
import { ViewShell } from "./ViewShell";

/**
 * AIAnalysisView (activeView === 'analysis') — FORMAX'ın merkez ekranı.
 *
 * ANLATININ TEK KAYNAĞI: Match Intelligence (Gemma) → match.aiNarrative.
 * Sayfa zaten /detail'i yüklediği için EK İSTEK ATILMAZ; Keşfet ve AI İncele
 * de aynı react-query cache'ini (["match", matchId]) okur → ikinci AI üretimi olmaz.
 *
 * KİLİTLİ KARAR (16.08): Eski Decision/MatchReadingEngine ANLATISI kullanıcıya
 * GÖSTERİLMEZ. Ölçüldü: o katman maçta yer almayan takımdan söz ediyor
 * ("Galatasaray ile aradaki fark" — Kasımpaşa–Trabzonspor maçında), aynı cümleyi
 * tekrarlıyor ve bozuk ek üretiyordu. Endpoint ve backend kodu YERİNDE DURUR;
 * bu ekran /decision'dan yalnız SAYISAL karar verisini okur:
 *   • probabilities → Olası Sonuçlar (market adı/yüzdesi/oranı backend'indir)
 *
 * KULLANICIYA GÖSTERİLMEYENLER (ürün kararı, backend'de silinmedi):
 *   • decision.confidence → AI Güven endeksi bloğu (iç mekanizma)
 *   • "AI İncele" ikinci ekranına bağlantı (aynı veriyi tekrar ediyordu)
 *
 * Boş blok tamamen gizlenir — placeholder yok, uydurma metin yok, frontend
 * hesaplaması yok.
 */
export function AIAnalysisView({ match, onClose }: { match: MatchDetailDto; onClose: () => void }) {
  const { data: decision, isLoading, isError } = useMatchDecision(match.matchId);

  // Decision'dan YALNIZ sayısal karar verisi okunur (anlatı okunmaz).
  //
  // SUNUM FİLTRESİ (hesap DEĞİL): backend 16 market üretir; ekranda %30 altı sonuçlar
  // gürültüdür. Değerler backend'in; burada yalnız hangi satırların gösterileceği ve
  // sırası belirlenir — probability/odd ÜRETİLMEZ, DÖNÜŞTÜRÜLMEZ.
  //   filtre: probability >= 30 · sıra: probability DESC · en fazla: 5
  const probabilities = [...(decision?.probabilities ?? [])]
    .filter((p) => p.probability >= 30)
    .sort((a, b) => b.probability - a.probability)
    .slice(0, 5);

  // Gemma anlatısı — /detail yanıtından birebir; burada hiçbir alan türetilmez.
  const narrative = match.aiNarrative ?? null;
  const hasNarrative = hasNarrativeContent(narrative);

  return (
    <ViewShell title="AI Maç Analizi" onClose={onClose}>
      <div className="space-y-3 pb-4">
        {/* Bu sezon form — backend özeti (LLM değil); anlatıdan ÖNCE gelir ki
            kullanıcı önce dayanağı, sonra yorumu görsün. */}
        <SeasonFormBlock match={match} />

        {/* Match Intelligence anlatısı — /detail ile birlikte geldi, beklemez. */}
        {hasNarrative && narrative && <NarrativeBlocks narrative={narrative} />}

        {isLoading && (
          <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 py-3 text-[13px] text-white/55">
            AI analizi yükleniyor…
          </div>
        )}

        {isError && (
          <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 py-3 text-[13px] text-white/55">
            AI analizi şu an yüklenemedi.
          </div>
        )}

        {!isLoading && !isError && (
          <>
            {/* AI Güven Endeksi bloğu KULLANICIYA GÖSTERİLMEZ (ürün kararı):
                skor/sinyal sayısı/veri kalitesi ürünün iç mekanizmasıdır ve kullanıcıyı
                "AI'ya ne kadar güvenmeliyim?" sorusuna itiyordu. Backend confidence
                hesabı, endpoint ve DTO YERİNDE DURUR — yalnız bu render kaldırıldı.
                (decision.confidence başka yüzeylerde kullanılmaya devam ediyor.) */}

            {/* Olası Sonuçlar — senaryo motoru çıktısı (market adı/sırası backend'in),
                her satırda "Senin seçimin" kontrolü. */}
            {probabilities.length > 0 && (
              <PossibleResultsBlock match={match} probabilities={probabilities} />
            )}

            {/* "AI İncele — analizi rahat oku" bağlantısı KALDIRILDI (ürün kararı):
                ikinci ekran aynı Match Intelligence/Gemma verisini başka yerleşimde
                tekrar ediyordu, kullanıcıya yeni bilgi vermiyordu. Analiz tek ekranda.
                /match/[id]/ai ROTASI ARTIK KALDIRILDI: Maçlar satırı, AI İncele ve
                Takip sayfası asıl Maç Detay ekranına (/match/[id]) gider. Eski adres
                yalnız kalıcı yönlendirme bırakır. Analiz TEK ekranda — burada. */}

            {!hasNarrative && probabilities.length === 0 && (
              <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 py-3 text-[13px] text-white/55">
                Bu maç için AI analizi henüz üretilmedi.
              </div>
            )}
          </>
        )}
      </div>
    </ViewShell>
  );
}


/**
 * OLASI SONUÇLAR — model olasılıkları + "SENİN SEÇİMİN" kontrolü.
 *
 * KURALLAR:
 *  • Frontend OLASILIK ÜRETMEZ. Yüzde ve oran backend'in; burada yalnız hangi
 *    satırların gösterileceği ve seçim durumu yönetilir.
 *  • ÇAKIŞMA KURALI BACKEND'İNDİR. Aynı market grubundan ikinci bir seçim
 *    yapıldığında eskisini backend siler ve GÜNCEL listeyi döner; arayüz o listeyi
 *    olduğu gibi gösterir. Kural iki yerde ayrı ayrı yazılsaydı er geç ayrışırdı.
 *  • MAÇ BAŞLADIYSA yeni seçim yapılamaz — kontrol gizlenmez, DEVRE DIŞI kalır ve
 *    sebebi yazılır.
 *  • Kullanıcının seçimi modelin tahmini gibi ETİKETLENMEZ: rozet açıkça
 *    "Senin seçimin" der.
 */
function PossibleResultsBlock({
  match,
  probabilities,
}: {
  match: MatchDetailDto;
  probabilities: Array<{ market: string; probability: number; odd?: number | null }>;
}) {
  const { selections, isLoggedIn, toggle, rejection } = useMatchPicks(match.matchId);

  const selectedByLabel = new Map(selections.map((s) => [s.label, s]));

  // Maç başladı mı? Kaynak tek gerçektir: backend durumu + kickoff saati.
  const kickoff = match.matchDate ? new Date(match.matchDate).getTime() : NaN;
  const started =
    match.status?.toLowerCase() === "finished" ||
    match.status?.toLowerCase() === "live" ||
    (!Number.isNaN(kickoff) && Date.now() >= kickoff);

  return (
    <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright p-4">
      <div className="mb-3 flex items-baseline justify-between gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-[0.16em] text-goalai-accent">
          Olası Sonuçlar
        </h3>
        {selections.length > 0 && (
          <span className="text-[10.5px] font-semibold uppercase tracking-wide text-white/45">
            {selections.length} seçim
          </span>
        )}
      </div>

      <div className="space-y-2">
        {probabilities.map((p, i) => {
          const selected = selectedByLabel.get(p.market);
          const busy = toggle.isPending && toggle.variables?.marketLabel === p.market;
          const disabled = !isLoggedIn || (started && !selected) || busy;

          return (
            <motion.div
              key={`${p.market}-${i}`}
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.25, delay: Math.min(i, 8) * 0.04, ease: "easeOut" }}
              className={`rounded-xl border px-3.5 py-2.5 transition-colors ${
                selected
                  ? "border-formax-green/50 bg-formax-green/[0.08]"
                  : "border-goalai-border/70 bg-black/20"
              }`}
            >
              {/* Market · olasılık · oran. Gerekçe BU BÖLÜMDE tekrarlanmaz —
                  o "Olası Sonuçların Gerekçesi" bölümünün işidir (tek görev kuralı). */}
              <div className="flex items-start justify-between gap-3">
                <span className="min-w-0 text-sm leading-snug text-white/90">{p.market}</span>

                <span className="flex shrink-0 items-center gap-3">
                  <span className="flex flex-col items-end gap-0.5 leading-none">
                    <span className="text-base font-bold tabular-nums text-goalai-accent">
                      %{p.probability}
                    </span>
                    {/* ORAN — olasılığın ALTINDA. GERÇEK market oranı (backend
                        MatchMarketOdds → sağlayıcı). Karşılığı yoksa satır hiç çıkmaz;
                        frontend oran ÜRETMEZ, 1/probability YAPMAZ. */}
                    {p.odd != null && (
                      <span className="text-[15px] font-extrabold tabular-nums text-white">
                        {p.odd.toFixed(2)}
                      </span>
                    )}
                  </span>

                  <button
                    type="button"
                    disabled={disabled}
                    aria-pressed={!!selected}
                    aria-label={selected ? `${p.market} seçimini kaldır` : `${p.market} seç`}
                    onClick={() =>
                      toggle.mutate({
                        matchId: match.matchId,
                        marketLabel: p.market,
                        probabilityPercent: p.probability,
                        odd: p.odd ?? null,
                      })
                    }
                    className={`flex h-8 min-w-[32px] items-center justify-center rounded-lg px-2 text-[12px] font-bold transition-colors disabled:opacity-40 ${
                      selected
                        ? "bg-formax-green/20 text-formax-green"
                        : "bg-white/[0.06] text-white/70 hover:bg-white/[0.12]"
                    }`}
                  >
                    {busy ? "…" : selected ? "✓" : "+"}
                  </button>
                </span>
              </div>

              {/* KULLANICININ SEÇİMİ MODEL TAHMİNİ GİBİ GÖSTERİLMEZ. */}
              {selected && (
                <p className="mt-1.5 text-[10.5px] font-semibold uppercase tracking-wide text-formax-green/85">
                  Senin seçimin · seçim anındaki olasılık %{selected.probabilityPercent}
                </p>
              )}
            </motion.div>
          );
        })}
      </div>

      {/* DURUM AÇIKÇA SÖYLENİR — sessiz devre dışı bırakma yok. */}
      {!isLoggedIn && (
        <p className="mt-2.5 text-[11.5px] leading-snug text-white/45">
          Sonuç seçebilmek için giriş yapmalısın. Seçimlerin hesabına kaydedilir.
        </p>
      )}
      {isLoggedIn && started && (
        <p className="mt-2.5 text-[11.5px] leading-snug text-white/45">
          Maç başladığı için yeni seçim yapılamaz.
        </p>
      )}
      {rejection === "MATCH_ALREADY_STARTED" && (
        <p className="mt-2.5 text-[11.5px] leading-snug text-formax-amber/85">
          Maç başladığı için bu seçim kaydedilmedi.
        </p>
      )}
      {rejection === "UNKNOWN_MARKET" && (
        <p className="mt-2.5 text-[11.5px] leading-snug text-formax-amber/85">
          Bu market için seçim kaydı desteklenmiyor.
        </p>
      )}
      {toggle.isError && (
        <p className="mt-2.5 text-[11.5px] leading-snug text-formax-amber/85">
          Seçim kaydedilemedi, tekrar dene.
        </p>
      )}
    </div>
  );
}

/**
 * BU SEZON FORM — backend'in doğrulanmış sezon özeti (LLM DEĞİL).
 *
 * KÖK NEDEN (30.08.2026): AI metni "son beş maç" derken beşin dördü GEÇEN SEZONDANDI.
 * Anlatı katmanı artık sezon kapsamlı veriyle besleniyor; bu blok ise kullanıcıya
 * dayanağı DOĞRUDAN gösterir: hangi sezon, hangi lig, kaç tamamlanmış maç, G/B/M.
 *
 * Cümle backend'in deterministik çıktısıdır; burada metin üretilmez, sayı hesaplanmaz.
 * Örneklem 3 maçtan azsa sınırlılık açıkça yazılır — üstünlük iddiası kurulmaz.
 */
function SeasonFormBlock({ match }: { match: MatchDetailDto }) {
  const sides = [match.homeSeasonForm, match.awaySeasonForm].filter(
    (s): s is NonNullable<typeof s> => !!s && s.sentence.trim().length > 0
  );
  if (sides.length === 0) return null;

  // SINIRLAMA NOTU BACKEND'İNDİR (06.09.2026).
  //
  // Eskiden burada LİG düzeyindeki veri tamlığı okunuyor ve teknik bir uyarı
  // basılıyordu: "Sezon verileri henüz tamamlanmadı (1 lig maçı bekliyor)."
  // O sayı ligin BAŞKA bir maçına aitti ve ekrandaki iki takımla ilgisizdi;
  // yine de ikisinin de form değerlendirmesini kapatıyordu. Artık:
  //  • kapı takımın kendi örneklemidir (backend: TeamFormSampleQuality),
  //  • not yalnız takımın KENDİ eksik sonucu varsa gelir (limitationNote),
  //  • teknik alanlar (isSeasonDataComplete / seasonMissingFixtures) TEŞHİS'tir
  //    ve bu yüzeyde OKUNMAZ — CSS ile gizlenmez, hiç üretilmez.
  //
  // Aynı not iki bölümde tekrar etmesin diye maç başına TEK kez gösterilir.
  const limitation = sides.map((s) => s.limitationNote?.trim()).find((n) => !!n) ?? "";
  const scope = sides[0];

  return (
    <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright p-4">
      <h3 className="mb-2 text-xs font-semibold uppercase tracking-[0.16em] text-goalai-accent">
        Bu Sezon Form
      </h3>

      <p className="mb-2.5 text-[11px] text-white/40">
        {scope.seasonLabel} · {scope.leagueName || "lig"} · yalnız tamamlanmış lig maçları
      </p>

      <div className="space-y-2">
        {sides.map((s) => (
          <div key={s.teamId}>
            {/* Takım adı: iki taraf da aynı cümleyi alabildiği için (veri eksikken
                metin takım adı taşımaz) hangi takıma ait olduğu burada belirtilir. */}
            <p className="mb-0.5 text-[11px] font-semibold uppercase tracking-wide text-white/45">
              {s.teamName}
            </p>
            <p className="text-[13.5px] leading-[1.6] text-white/85">{s.sentence}</p>
            {s.played > 0 && (
              <p className="mt-0.5 text-[10.5px] tabular-nums text-white/40">
                O {s.played} · G {s.won} · B {s.drawn} · M {s.lost} · AG {s.goalsFor} · YG{" "}
                {s.goalsAgainst} · AV{" "}
                {s.goalDifference > 0 ? `+${s.goalDifference}` : s.goalDifference}
              </p>
            )}
          </div>
        ))}
      </div>

      {limitation ? (
        <p className="mt-2.5 text-[11.5px] leading-snug text-formax-amber/80">{limitation}</p>
      ) : null}
    </div>
  );
}
