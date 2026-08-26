"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import type { MatchDetailDto, NabizFeedItemDto } from "@/types/api";
import { useChrome } from "@/context/ChromeContext";
import { useMatchNews } from "@/hooks/useMatchNews";
import { ViewShell } from "./ViewShell";

/**
 * NewsView (activeView === 'news') — SON DAKİKA.
 *
 * VERİ: `/api/matches/{id}/news?lang=<seçili FORMAX dili>`. Liste backend'de üretilir
 * (maç kapsam kapıları + PublishedUtc DESC + tekilleştirme) ve seçili dile backend'in
 * mevcut LLM zinciriyle uyarlanır. Bu dosya SIRALAMAZ, FİLTRELEMEZ, ÇEVİRMEZ, metin
 * ÜRETMEZ.
 *
 * FALLBACK: dil ucu yüklenirken/başarısızken `/detail` ile zaten gelmiş GERÇEK haber
 * listesi (orijinal dilinde) gösterilir — kullanıcı boş ekran görmez, uydurma da yok.
 *
 * AKIŞ: kart → FORMAX İÇİNDE haber detayı (aynı görünüm içinde panel; yeni rota YOK).
 * "Kaynağa Git" ikincil aksiyondur; ana aksiyon dış siteye GÖTÜRMEZ.
 */
export function NewsView({ match, onClose }: { match: MatchDetailDto; onClose: () => void }) {
  const { language } = useChrome();
  const [selected, setSelected] = useState<NabizFeedItemDto | null>(null);

  const { data, isLoading, isError } = useMatchNews(match.matchId, language);

  //
  // FALLBACK KURALI — `/detail` haberleri ÇEVRİLMEMİŞ gelir (çeviri yalnız `/news` ucunda).
  //
  // NEDEN ÖNEMLİ (ölçüldü 18.08): fallback koşulsuz gösterilince, dil ucu çeviriyi
  // üretirken (soğuk önbellekte saniyeler) kullanıcı Türkçe seçmiş olmasına rağmen
  // İngilizce başlıklar görüyordu. "Bazı haberler hâlâ İngilizce" şikâyetinin kaynağı buydu.
  //
  // Bu yüzden fallback YALNIZ zaten kullanıcının dilinde olan haberler için kullanılır.
  // Aksi hâlde çeviri gelene kadar yükleniyor durumu gösterilir — yanlış dilde içerik
  // göstermektense beklemek doğrudur.
  //
  const rawFallback = match.nabizFeed?.items ?? [];
  const fallback = rawFallback.every(
    (i) => (i.language ?? "").toLowerCase() === language.toLowerCase()
  )
    ? rawFallback
    : [];

  const items = data?.items ?? fallback;

  const showError = isError && fallback.length === 0;
  const showLoading = isLoading && fallback.length === 0;

  return (
    <ViewShell
      title={selected ? "Haber" : "Son Dakika"}
      onClose={selected ? () => setSelected(null) : onClose}
    >
      {/*
        Liste ↔ detay geçişinde AnimatePresence KULLANILMAZ: `mode="wait"` çıkış
        animasyonu bitene kadar detayı mount etmiyor, bu da dokunuşla açılış arasına
        gereksiz gecikme koyuyordu. Detayın kendi giriş animasyonu yeterli.
      */}
      {selected ? (
        <NewsDetail item={selected} match={match} onBack={() => setSelected(null)} />
      ) : showLoading ? (
        <Notice tone="muted">Haberler yükleniyor…</Notice>
      ) : showError ? (
        <Notice tone="warn">Haberler şu an yüklenemedi. Lütfen tekrar dene.</Notice>
      ) : items.length === 0 ? (
        <Notice tone="muted">Bu maç için güncel haber bulunmuyor.</Notice>
      ) : (
        <div className="space-y-3 pb-4">
          {items.map((n, i) => (
            <NewsCard key={newsKey(n, i)} item={n} index={i} onOpen={() => setSelected(n)} />
          ))}
        </div>
      )}
    </ViewShell>
  );
}

function Notice({ tone, children }: { tone: "muted" | "warn"; children: React.ReactNode }) {
  return (
    <div
      className={`rounded-xl border px-4 py-3 text-[13px] ${
        tone === "warn"
          ? "border-formax-amber/30 bg-formax-amber/[0.06] text-formax-amber"
          : "border-goalai-border bg-goalai-surface-bright text-white/55"
      }`}
    >
      {children}
    </div>
  );
}

/** Kimlik: backend haber kimliği → kanonik URL → başlık. */
function newsKey(n: NabizFeedItemDto, i: number): string {
  return n.id?.trim() || n.sourceUrl?.trim() || `${n.headline}-${i}`;
}

// ── Liste kartı ──────────────────────────────────────────────────────────────

function NewsCard({
  item,
  index,
  onOpen,
}: {
  item: NabizFeedItemDto;
  index: number;
  onOpen: () => void;
}) {
  const stamp = formatStamp(item.publishedAt);

  return (
    <motion.article
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.28, delay: Math.min(index, 6) * 0.05, ease: "easeOut" }}
    >
      <button
        type="button"
        onClick={onOpen}
        className="block w-full overflow-hidden rounded-2xl border border-goalai-border bg-goalai-surface-bright text-left transition-colors hover:bg-white/[0.04] active:scale-[0.995]"
      >
        {item.imageUrl && <NewsImage src={item.imageUrl} className="h-36" />}
        <div className="p-3.5">
          {stamp && (
            <p className="text-[10.5px] font-semibold uppercase tracking-wide tabular-nums text-goalai-accent">
              {stamp}
            </p>
          )}
          <p className="mt-1 line-clamp-3 break-words text-[14.5px] font-semibold leading-snug text-white">
            {item.headline}
          </p>
          {item.summary && (
            <p className="mt-1.5 line-clamp-2 break-words text-[12.5px] leading-relaxed text-white/60">
              {item.summary}
            </p>
          )}
          <div className="mt-2 flex items-center justify-between gap-2 text-[11px]">
            {/* Kaynak adı ASLA çevrilmez. */}
            <span className="min-w-0 truncate text-white/45">{item.source || "—"}</span>
            <span className="shrink-0 font-semibold text-white/55">Haberi Gör →</span>
          </div>
        </div>
      </button>
    </motion.article>
  );
}

// ── FORMAX içi haber detayı ──────────────────────────────────────────────────

function NewsDetail({
  item,
  match,
  onBack,
}: {
  item: NabizFeedItemDto;
  match: MatchDetailDto;
  onBack: () => void;
}) {
  const stamp = formatStamp(item.publishedAt);
  const href = item.sourceUrl?.trim() || null;

  return (
    <motion.div
      initial={{ opacity: 0, x: 24 }}
      animate={{ opacity: 1, x: 0 }}
      exit={{ opacity: 0, x: 24 }}
      transition={{ duration: 0.22, ease: "easeOut" }}
      className="pb-4"
    >
      {/* Geri + haberin hangi maçtan açıldığı */}
      <div className="mb-3 flex items-center gap-2">
        <button
          type="button"
          onClick={onBack}
          aria-label="Son Dakika listesine dön"
          className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full border border-goalai-border text-white/70 transition-colors hover:bg-white/10 hover:text-white active:scale-95"
        >
          <span aria-hidden className="text-[14px] leading-none">←</span>
        </button>
        <p className="min-w-0 truncate text-[10.5px] font-semibold uppercase tracking-wide text-white/40">
          {match.homeTeam.name} — {match.awayTeam.name}
        </p>
      </div>

      <article className="overflow-hidden rounded-2xl border border-goalai-border bg-goalai-surface-bright">
        {/* Görsel yalnız gerçekten varsa; yoksa boş alan bırakılmaz. */}
        {item.imageUrl && <NewsImage src={item.imageUrl} className="h-44" />}

        <div className="p-4">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-[10.5px] font-semibold uppercase tracking-wide">
            {stamp && <span className="tabular-nums text-goalai-accent">{stamp}</span>}
            {item.source && (
              <>
                <span className="text-white/25">·</span>
                {/* Kaynak adı çevrilmez, olduğu gibi gösterilir. */}
                <span className="text-white/50">{item.source}</span>
              </>
            )}
          </div>

          <h3 className="mt-2 break-words text-[18px] font-bold leading-tight text-white">
            {item.headline}
          </h3>

          {item.summary && (
            <p className="mt-3 whitespace-pre-line break-words text-[13.5px] leading-relaxed text-white/70">
              {item.summary}
            </p>
          )}

          {/*
            Çeviri uygulandıysa sağlayıcının orijinal başlığı da gösterilir — kullanıcı
            kaynağın ne dediğini görebilir. Metin ÜRETİLMEZ, backend'in taşıdığı orijinal.
          */}
          {item.isTranslated && item.originalHeadline && (
            <div className="mt-4 rounded-xl border border-goalai-border/60 bg-white/[0.02] p-3">
              <p className="text-[9.5px] font-semibold uppercase tracking-wide text-white/35">
                Kaynak metni
              </p>
              <p className="mt-1 break-words text-[12px] leading-relaxed text-white/45">
                {item.originalHeadline}
              </p>
            </div>
          )}

          {/* İkincil aksiyon — ana akış FORMAX içindedir. URL yoksa hiç gösterilmez. */}
          {href && (
            <a
              href={href}
              target="_blank"
              rel="noopener noreferrer"
              className="mt-4 flex items-center justify-center rounded-xl border border-goalai-border px-4 py-2.5 text-[12.5px] font-semibold text-white/70 transition-colors hover:bg-white/[0.05] hover:text-white active:scale-[0.99]"
            >
              Kaynağa Git ↗
            </a>
          )}
        </div>
      </article>
    </motion.div>
  );
}

/** Bozuk/erişilemeyen görsel metni bozmaz: yüklenemezse alan tamamen kaldırılır. */
function NewsImage({ src, className }: { src: string; className: string }) {
  const [failed, setFailed] = useState(false);
  if (failed) return null;
  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={src}
      alt=""
      loading="lazy"
      onError={() => setFailed(true)}
      className={`w-full object-cover ${className}`}
    />
  );
}

/**
 * "18.08.2026 • 19:42" — backend'in UTC damgası kullanıcının yerel saatine çevrilir.
 * Manuel saat kaydırması yapılmaz; dönüşümü tarayıcı yapar. Tarih/saat ÇEVRİLMEZ.
 */
function formatStamp(iso?: string): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const date = d.toLocaleDateString("tr-TR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
  const time = d.toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });
  return `${date} • ${time}`;
}
