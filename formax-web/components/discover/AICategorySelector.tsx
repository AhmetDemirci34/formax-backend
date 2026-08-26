"use client";

import { useState } from "react";
import { CategoryCard } from "@/components/discover/CategoryCard";
import {
  SparklesIcon,
  CalendarIcon,
  TargetIcon,
  TicketIcon,
  FlameIcon,
} from "@/components/discover/icons";

export type CategoryKey =
  | "ai"
  | "today"
  | "high"
  | "coupon"
  | "surprise";

interface AICategorySelectorProps {
  /** Aktif kategori değişince (kategori mantığı backend'e aittir; UI yalnız iletir). */
  onChange?: (key: CategoryKey) => void;
  defaultKey?: CategoryKey;
}

const CATEGORIES: { key: CategoryKey; title: string; icon: React.ReactNode }[] = [
  { key: "ai", title: "AI Önerilerim", icon: <SparklesIcon size={19} /> },
  { key: "today", title: "Bugünkü Maçlar", icon: <CalendarIcon size={19} /> },
  { key: "high", title: "Yüksek Olasılıklar", icon: <TargetIcon size={19} /> },
  { key: "coupon", title: "Kupon Önerileri", icon: <TicketIcon size={19} /> },
  { key: "surprise", title: "Sürpriz Maçlar", icon: <FlameIcon size={19} /> },
];

/**
 * FORMAX · AICategorySelector (02, Home)
 * Header'ın hemen altında yatay-scroll AI kategori seçici. Aktif kart neon glow,
 * pasif kart glass. Kategori mantığı backend'e aittir; UI yalnız seçimi iletir (onChange).
 */
export function AICategorySelector({ onChange, defaultKey = "ai" }: AICategorySelectorProps) {
  const [active, setActive] = useState<CategoryKey>(defaultKey);

  const select = (key: CategoryKey) => {
    setActive(key);
    // TODO(backend): AI kategori seçimi için backend sözleşmesi yok. Mevcut Interest
    // sistemi maç/takım/lig boyutludur; "kategori" boyutu YOKTUR ve yeni event tipi
    // üretilmez. Feed'i kategoriye göre filtreleyecek endpoint/parametre eklendiğinde
    // onChange gerçek isteğe bağlanacak. Şimdilik yalnız görsel seçim (sahte başarı yok).
    onChange?.(key);
  };

  return (
    <div className="flex items-stretch gap-1">
      {CATEGORIES.map((c) => (
        <CategoryCard
          key={c.key}
          icon={c.icon}
          title={c.title}
          active={active === c.key}
          onClick={() => select(c.key)}
        />
      ))}
    </div>
  );
}
