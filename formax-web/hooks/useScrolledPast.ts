"use client";

import { useEffect, useState, type RefObject } from "react";

/**
 * Verilen elemandan yukarı doğru ilk kaydırma konteynerini bulur.
 *
 * Yalnızca `overflow-y` hesaplanmış değerine bakılır; `scrollHeight > clientHeight`
 * KONTROLÜ YAPILMAZ — mount anında içerik henüz taşmamış olabilir ve konteyner
 * yanlışlıkla atlanıp `window`'a düşülür (scroll dinleyicisi hiç tetiklenmez).
 */
export function findScrollParent(el: HTMLElement | null): HTMLElement | Window {
  let node: HTMLElement | null = el?.parentElement ?? null;
  while (node) {
    const { overflowY } = getComputedStyle(node);
    if (/(auto|scroll|overlay)/.test(overflowY)) return node;
    node = node.parentElement;
  }
  return window;
}

/**
 * `ref`'in bulunduğu kaydırma konteynerinde scrollTop > threshold olduğunda `true` döner.
 *
 * FORMAX'ta sayfa `window` üzerinde değil, AppChrome içindeki `overflow-y-auto`
 * konteynerinde kayar; bu yüzden dinleyici sabit bir hedefe değil, en yakın
 * kaydırılabilir ataya bağlanır.
 */
export function useScrolledPast<T extends HTMLElement>(
  ref: RefObject<T | null>,
  threshold = 10
): boolean {
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    const target = findScrollParent(ref.current);
    const read = () =>
      target instanceof Window ? window.scrollY : (target as HTMLElement).scrollTop;

    const onScroll = () => setScrolled(read() > threshold);
    onScroll();

    target.addEventListener("scroll", onScroll, { passive: true });
    return () => target.removeEventListener("scroll", onScroll);
  }, [ref, threshold]);

  return scrolled;
}
