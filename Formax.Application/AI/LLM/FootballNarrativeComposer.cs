using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Formax.Application.AI.Decision;

namespace Formax.Application.AI.LLM
{
    public enum FootballNarrativeSurface { DiscoverCard, MatchDetail, AiIncele }

    /// <summary>
    /// FORMAX AI Brain — LLM NARRATIVE. YALNIZ <see cref="MatchReading"/>'in 11 adlı Story bloğunu OKUR
    /// (Match/Team/Player/Squad/Tactical/Timeline/Competition/News/Psychology/Hidden Story + FORMAX Opinion);
    /// hesap/olasılık/tahmin/veri ÜRETMEZ. Görevi bu yapıyı doğal Türkçeye çevirmek — eski bir futbol
    /// yorumcusu gibi. Deterministik (Mock): aynı okuma → aynı metin.
    ///
    /// Üç yüzey, üç ayrı üslup (asla aynı metin):
    ///  • Discover   — tek-iki cümle, en çarpıcı gerçek çıkarım (merak uyandırır).
    ///  • MatchDetail— tek akıcı paragraf (maçı anlatır).
    ///  • AI İncele  — çok paragraflı derin anlatı (televizyon yorumcusu; her paragrafın amacı ayrı).
    ///
    /// Tekrar-önleyici: aynı cümle birden çok bölümde yinelenmez. Yasaklı ifade + kaynak dili süzülür.
    /// Canlıda (Reading.Live dolu) anlatım GERÇEK canlı veriden yürür (tahmini canlı veri ASLA).
    /// </summary>
    public sealed class FootballNarrativeComposer
    {
        private static readonly string[] Banned =
        {
            "kazanacak", "kesin", "banko", "iddia", "oyna", "olası sonuç", "gol olur", "gol olmaz",
            "%80", "% 80", "garanti", "kaybedecek", "net favori"
        };

        private static readonly string[] ForbiddenSource =
        {
            "kulüp açıkladı", "habere göre", "haberlere göre", "kaynaklara göre", "basına göre",
            "gazete", "basın toplantısı", "açıklama yaptı", "duyurdu", "bbc", "sky sports"
        };

        public string Compose(AiDecisionPackage pkg, FootballNarrativeSurface surface)
        {
            var r = pkg?.Reading;
            if (r == null || !r.HasData)
                return "Bu maç için henüz doğal dil yorumu üretecek yeterli gerçek veri yok.";

            var live = r.Live is { Count: > 0 };
            return surface switch
            {
                FootballNarrativeSurface.DiscoverCard => Sanitize(ComposeDiscover(pkg, r, live)),
                FootballNarrativeSurface.MatchDetail  => Sanitize(ComposeMatchDetail(pkg, r, live)),
                _                                     => Sanitize(ComposeAiIncele(pkg, r, live))
            };
        }

        // ═══════════ Discover — tek-iki cümle, en çarpıcı gerçek çıkarım (merak) ═══════════
        private static string ComposeDiscover(AiDecisionPackage pkg, MatchReading r, bool live)
        {
            if (live)
            {
                var l1 = r.Live.FirstOrDefault() ?? "";
                var l2 = r.Live.Skip(1).FirstOrDefault(x => !ForbiddenLine(x)) ?? "";
                return string.IsNullOrWhiteSpace(l2) ? l1 : $"{l1} {l2}";
            }
            var lead = FirstNonEmpty(
                r.MatchStoryLines.FirstOrDefault(),
                r.HiddenStory.FirstOrDefault(),
                r.PlayerStory.FirstOrDefault(),
                r.CompetitionStory.FirstOrDefault());
            var second = FirstNonEmpty(
                r.NewsStory.FirstOrDefault(),
                r.TimelineStory.FirstOrDefault(),
                r.MatchStoryLines.Skip(1).FirstOrDefault());
            if (string.IsNullOrWhiteSpace(lead)) lead = $"{pkg.HomeName} ile {pkg.AwayName} karşı karşıya.";
            return string.IsNullOrWhiteSpace(second) || second == lead ? lead : $"{lead} {second}";
        }

        // ═══════════ MatchDetail — tek akıcı paragraf (maçı anlatır) ═══════════
        private static string ComposeMatchDetail(AiDecisionPackage pkg, MatchReading r, bool live)
        {
            var used = new HashSet<string>();
            var sb = new StringBuilder();
            if (live)
            {
                sb.Append(Join(Take(r.Live.Where(x => !ForbiddenLine(x)), 3, used)) + " ");
                Add(sb, First(r.MatchStoryLines, used));
                return sb.ToString().Trim();
            }
            var comp = First(r.CompetitionStory, used);
            sb.Append(string.IsNullOrWhiteSpace(comp)
                ? $"{pkg.HomeName} - {pkg.AwayName}. "
                : $"{pkg.HomeName} - {pkg.AwayName}: {comp.ToLowerFirst()} ");
            Add(sb, First(r.MatchStoryLines, used));
            Add(sb, First(r.PlayerStory, used));
            Add(sb, First(r.HiddenStory, used));
            Add(sb, First(r.TimelineStory, used));
            Add(sb, First(r.FormaxOpinion, used));
            return sb.ToString().Trim();
        }

        // ═══════════ AI İncele — çok paragraflı derin anlatı (yorumcu; her paragraf ayrı amaç) ═══════════
        private static string ComposeAiIncele(AiDecisionPackage pkg, MatchReading r, bool live)
        {
            var used = new HashSet<string>();
            var paras = new List<string>();

            // 1) Bağlam + maçın hikâyesi
            Para(paras, used, "", r.CompetitionStory, r.MatchStoryLines);
            // 2) Takımlar (form/karakter + geçmiş)
            Para(paras, used, "", r.TeamStory);
            // 3) Oyuncular (etki, istatistik değil)
            Para(paras, used, "Bireysel eksende ", r.PlayerStory);
            // 4) Kadro
            Para(paras, used, "", r.SquadStory);
            // 5) Takvim/fikstür
            Para(paras, used, "", r.TimelineStory);
            // 6) Haberin etkisi (yorum)
            Para(paras, used, "Gündemin maça yansıması: ", r.NewsStory);
            // 7) Taktik (maç öncesi yalnız DNA/güç/form)
            Para(paras, used, "", r.TacticalStory);
            // 8) Psikoloji
            Para(paras, used, "", r.PsychologyStory);
            // 9) Gizli / çapraz sentez
            Para(paras, used, "İşin ötesine bakınca: ", r.HiddenStory);
            // 10) Canlı — gerçek canlı veriden
            if (live) Para(paras, used, "Sahadaki durum: ", r.Live.Where(x => !ForbiddenLine(x)).ToList());
            // 11) FORMAX görüşü
            Para(paras, used, "FORMAX'ın görüşü: ", r.FormaxOpinion);

            return string.Join("\n\n", paras.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        // ── yardımcılar (tekrar-önleyici) ──

        // Bir paragrafı, verilen blok(lar)ın DAHA ÖNCE KULLANILMAMIŞ cümlelerinden kurar.
        private static void Para(List<string> paras, HashSet<string> used, string prefix, params IReadOnlyList<string>[] blocks)
        {
            var picks = new List<string>();
            foreach (var b in blocks)
                if (b != null)
                    foreach (var s in b)
                        if (!string.IsNullOrWhiteSpace(s) && used.Add(s.Trim()))
                            picks.Add(s.Trim());
            if (picks.Count == 0) return;
            paras.Add((prefix + string.Join(" ", picks)).Trim());
        }

        private static string First(IReadOnlyList<string> block, HashSet<string> used)
        {
            if (block == null) return "";
            foreach (var s in block)
                if (!string.IsNullOrWhiteSpace(s) && used.Add(s.Trim())) return s.Trim();
            return "";
        }

        private static IEnumerable<string> Take(IEnumerable<string> src, int n, HashSet<string> used)
        {
            int c = 0;
            foreach (var s in src)
            {
                if (c >= n) break;
                if (!string.IsNullOrWhiteSpace(s) && used.Add(s.Trim())) { yield return s.Trim(); c++; }
            }
        }

        private static void Add(StringBuilder sb, string s)
        {
            if (!string.IsNullOrWhiteSpace(s)) sb.Append(s.Trim() + " ");
        }

        private static string Join(IEnumerable<string> xs) => string.Join(" ", xs);

        private static string FirstNonEmpty(params string?[] xs)
            => xs.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "";

        private static bool ForbiddenLine(string s)
            => !string.IsNullOrEmpty(s) && ForbiddenSource.Any(f => s.ToLowerInvariant().Contains(f));

        // Yasaklı ifade / kaynak dili taşıyan cümleleri düşür (güvenlik ağı).
        private static string Sanitize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var parts = text.Split(new[] { ". " }, StringSplitOptions.None);
            var kept = parts.Where(sn =>
                !Banned.Any(b => sn.ToLowerInvariant().Contains(b)) &&
                !ForbiddenSource.Any(f => sn.ToLowerInvariant().Contains(f))).ToArray();
            var result = string.Join(". ", kept).Trim();
            return string.IsNullOrWhiteSpace(result) ? text : result;
        }
    }

    internal static class StringCaseExtensions
    {
        /// <summary>İlk harfi küçültür (cümle ortasına gömerken). Türkçe I/İ korunur.</summary>
        public static string ToLowerFirst(this string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToLower(s[0]) + s.Substring(1);
        }
    }
}
