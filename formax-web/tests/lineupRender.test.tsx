import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import type { LineupPlayerDto, LineupSectionDto, MatchDetailDto } from "@/types/api";
import { LineupPanel, positionLabel } from "@/components/match-center/lineup/LineupPanel";

/**
 * KADRO BİLGİSİ — 0 / eksik / tam kadro üç durumda da bozulmadan render edilmeli.
 *
 * Bu testler ağa çıkmaz ve yüzde ÜRETMEZ: panel yalnız backend'in verdiğini gösterir.
 */

function player(n: number, name: string, position: string | null, grid: string | null = null): LineupPlayerDto {
  return { shirtNumber: n, playerName: name, position: position ?? "", grid, isCaptain: false } as LineupPlayerDto;
}

function section(extra: Partial<LineupSectionDto> = {}): LineupSectionDto {
  return {
    lineupsAnnounced: false,
    homeFormation: null,
    awayFormation: null,
    homeStartingXI: [],
    homeBench: [],
    awayStartingXI: [],
    awayBench: [],
    ...extra,
  };
}

function match(lineup: LineupSectionDto): MatchDetailDto {
  return {
    homeTeam: { name: "Bologna" },
    awayTeam: { name: "Torino" },
    lineup,
  } as unknown as MatchDetailDto;
}

describe("Kadro Bilgisi paneli", () => {
  it("kadro YOKKEN bekleme metnini gösterir, uydurma oyuncu yazmaz", () => {
    const html = renderToStaticMarkup(
      <LineupPanel match={match(section({ status: "Waiting", lastCheckedUtc: null }))} />,
    );
    expect(html).toContain('data-lineup-state="waiting"');
    expect(html).not.toContain("İlk 11");
    expect(html).not.toContain("Son kontrol");
  });

  it("kadro yokken SON GERÇEK KONTROL damgası varsa gösterir", () => {
    const html = renderToStaticMarkup(
      <LineupPanel
        match={match(section({ status: "SourceDelayed", lastCheckedUtc: "2026-09-19T12:15:25Z" }))}
      />,
    );
    expect(html).toContain('data-lineup-state="waiting"');
    expect(html).toContain("Son kontrol");
  });

  it("EKSİK kadroda (tek taraf) doğrulanmış tarafı gösterir, boş tarafı uydurmaz", () => {
    const html = renderToStaticMarkup(
      <LineupPanel
        match={match(
          section({
            lineupsAnnounced: true,
            homeFormation: "3-4-3",
            awayFormation: null,
            homeStartingXI: [player(1, "Łukasz Skorupski", "G")],
            awayStartingXI: [],
          }),
        )}
      />,
    );
    expect(html).toContain('data-lineup-state="verified"');
    expect(html).toContain("Skorupski");
    expect(html).toContain("Diziliş bilgisi mevcut değil"); // deplasman dizilişi UYDURULMADI
    expect(html).toContain("İlk 11 açıklanmadı");
  });

  it("TAM kadroda ilk 11, forma numarası, mevki, kaynak ve son kontrol görünür", () => {
    const html = renderToStaticMarkup(
      <LineupPanel
        match={match(
          section({
            lineupsAnnounced: true,
            homeFormation: "3-4-3",
            awayFormation: "3-5-2",
            homeStartingXI: [player(1, "Łukasz Skorupski", "G"), player(9, "Santiago Castro", "F")],
            homeBench: [player(23, "Federico Ravaglia", "G")],
            awayStartingXI: [player(20, "Lucas Perri", "G"), player(18, "Giovanni Simeone", "F")],
            awayBench: [],
            source: "Lega Serie A",
            lastCheckedUtc: "2026-09-19T12:15:25Z",
          }),
        )}
      />,
    );
    expect(html).toContain('data-lineup-state="verified"');
    expect(html).toContain("3-4-3");
    expect(html).toContain("3-5-2");
    expect(html).toContain("Skorupski");
    expect(html).toContain("Simeone");
    expect(html).toContain(">18<"); // forma numarası
    expect(html).toContain("KL"); // mevki etiketi (kaleci)
    expect(html).toContain("FOR"); // mevki etiketi (forvet)
    expect(html).toContain("Lega Serie A");
    expect(html).toContain("Son kontrol");
    expect(html).toContain("Yedekler");
  });

  it("mevki etiketi YALNIZ kaynağın verdiği koddan üretilir", () => {
    expect(positionLabel("G")).toBe("KL");
    expect(positionLabel("d")).toBe("DEF");
    expect(positionLabel("M")).toBe("ORT");
    expect(positionLabel("F")).toBe("FOR");
    // Kaynak vermediyse ya da tanınmıyorsa etiket YOK — tahmin edilmez.
    expect(positionLabel("")).toBeNull();
    expect(positionLabel(null)).toBeNull();
    expect(positionLabel(undefined)).toBeNull();
    expect(positionLabel("Centrocampista")).toBeNull();
  });

  it("mevki gelmeyen oyuncuda satır bozulmaz, etiket çıkmaz", () => {
    const html = renderToStaticMarkup(
      <LineupPanel
        match={match(
          section({
            lineupsAnnounced: true,
            homeStartingXI: [player(7, "Adı Var Mevkisi Yok", null)],
            awayStartingXI: [player(7, "Karşı Oyuncu", null)],
          }),
        )}
      />,
    );
    expect(html).toContain("Adı Var Mevkisi Yok");
    expect(html).not.toContain("ORT");
    expect(html).not.toContain("FOR");
  });
});
