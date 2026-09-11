import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import {
  SelectionRow,
  StatusBadge,
  cardStatusLabel,
  scoreLine,
  selectionOutcomeLabel,
} from "@/components/predictions/MyPicksList";
import type { UserPickDto } from "@/lib/api/picks";

// Settlement integration testinin (gerçek SQL Server) ürettiği DTO biçimi: 2-1 biten
// maçta MS 1 (doğru), 2,5 Alt (yanlış), "ilk golü ev sahibi atar" (desteklenmeyen).
function pick(p: Partial<UserPickDto>): UserPickDto {
  return {
    id: "p",
    matchId: 1,
    marketKey: "MS_1",
    label: "Ev sahibi kazanır",
    probabilityPercent: 61,
    selectionStatus: "Settled",
    createdAtUtc: "2026-09-10T10:00:00Z",
    isCorrect: null,
    ...p,
  } as UserPickDto;
}

describe("Tahminlerim settlement etiketleri", () => {
  it("doğru / yanlış / hesaplanamadı ayrı etiketlenir; uydurma sonuç yok", () => {
    expect(selectionOutcomeLabel(pick({ isCorrect: true }))).toBe("Doğru");
    expect(selectionOutcomeLabel(pick({ isCorrect: false }))).toBe("Yanlış");
    expect(selectionOutcomeLabel(pick({ selectionStatus: "Unsettleable", isCorrect: null }))).toBe("Sonuç hesaplanamadı");
    // Henüz sonuçlanmamış (Active/Pending) seçimde etiket YOK.
    expect(selectionOutcomeLabel(pick({ selectionStatus: "Active", isCorrect: null }))).toBeNull();
  });

  it("kart durumu: Settled → Tamamlandı, Pending → Bekleyen, diğer → Aktif", () => {
    expect(cardStatusLabel("Settled")).toBe("Tamamlandı");
    expect(cardStatusLabel("Pending")).toBe("Bekleyen");
    expect(cardStatusLabel("Active")).toBe("Aktif");
    expect(renderToStaticMarkup(<StatusBadge cardStatus="Settled" />)).toContain("Tamamlandı");
  });

  it("seçim satırı seçim anındaki olasılığı ve sonucu gösterir", () => {
    const html = renderToStaticMarkup(<SelectionRow selection={pick({ isCorrect: true, probabilityPercent: 61 })} />);
    expect(html).toContain("Ev sahibi kazanır");
    expect(html).toContain("%61");
    expect(html).toContain("Doğru");
  });

  it("İY / 2Y / MS yalnız gerçekten varsa gösterilir", () => {
    expect(
      scoreLine({ homeScore: 2, awayScore: 1, halfTimeHomeScore: 1, halfTimeAwayScore: 0, secondHalfHomeScore: 1, secondHalfAwayScore: 1 })
    ).toBe("İY 1-0 · 2Y 1-1 · MS 2-1");
    expect(scoreLine({ homeScore: 2, awayScore: 1, halfTimeHomeScore: null, halfTimeAwayScore: null })).toBe("MS 2-1");
  });
});
