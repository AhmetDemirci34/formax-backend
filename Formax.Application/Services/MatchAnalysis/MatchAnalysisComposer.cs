using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.MatchAnalysis
{
    /// <summary>
    /// ANALİZ BESTECİSİ — kanıtları kısa, maça özel Türkçe cümlelere çevirir. Sayı HESAPLAMAZ:
    /// her sayı <see cref="MatchEvidenceBuilder"/>'ın ürettiği değerdir. Kanıtı olmayan bölüm/cümle
    /// ÜRETİLMEZ; takım adları açıkça yazılır; kesinlik/garanti ve oyun tavsiyesi yoktur.
    ///
    /// ÖRNEKLEM KURALI (n = takımın uygun lig maçı sayısı, en fazla 5):
    ///  • 0   → genelleme yok; yalnız "form karşılaştırması yapılamıyor" notu.
    ///  • 1–2 → yalnız gerçek toplamlar (maç başına ortalama/eğilim YOK).
    ///  • 3–4 → ortalama yazılabilir, sınırlı örneklem Belirsizlik notunda açıkça söylenir.
    ///  • 5   → "son 5 lig maçı" üzerinden.
    ///
    /// <paramref name="variant"/> yalnız aynı kanıtın farklı dizilişidir (benzerlik engeli
    /// yeniden üretimde kullanır); içerik ve sayı değişmez, süs kelimesi eklenmez.
    /// </summary>
    public static class MatchAnalysisComposer
    {
        public static readonly IReadOnlyList<string> Markets = new[]
        {
            "Ev Sahibi Kazanır", "Beraberlik", "Deplasman Kazanır",
            "Çifte Şans (1X)", "Çifte Şans (X2)", "Çifte Şans (12)",
            "1.5 Üst", "1.5 Alt", "2.5 Üst", "2.5 Alt", "3.5 Üst", "3.5 Alt",
            "Karşılıklı Gol Var", "Karşılıklı Gol Yok"
        };

        private sealed record Side(string Name, string Code, IReadOnlyDictionary<string, double> Form, IReadOnlyDictionary<string, double> Venue);

        public static MatchAnalysisDocument Compose(AnalysisInput input, IReadOnlyList<EvidenceItem> evidence, int variant = 0)
        {
            var doc = new MatchAnalysisDocument();
            var h = new Side(input.HomeName, "home", Values(evidence, MatchEvidenceBuilder.FormKey("home")), Values(evidence, MatchEvidenceBuilder.VenueKey("home")));
            var a = new Side(input.AwayName, "away", Values(evidence, MatchEvidenceBuilder.FormKey("away")), Values(evidence, MatchEvidenceBuilder.VenueKey("away")));

            doc.WhyWatch = WhyWatch(h, a, evidence, variant);
            doc.KeyBattle = KeyBattle(h, a, variant);
            doc.LineupImpact = LineupImpact(h, a, evidence);
            doc.Uncertainty = Uncertainty(h, a, input.LeagueName, variant);
            doc.Scenarios = Scenarios(h, a);
            return doc;
        }

        // ── yardımcılar ───────────────────────────────────────────────────────────

        private static IReadOnlyDictionary<string, double> Values(IReadOnlyList<EvidenceItem> ev, string key)
            => MatchEvidenceBuilder.Find(ev, key)?.Values ?? new Dictionary<string, double>();

        private static int I(IReadOnlyDictionary<string, double> v, string k) => v.TryGetValue(k, out var x) ? (int)x : 0;
        private static string N(IReadOnlyDictionary<string, double> v, string k) => MatchEvidenceBuilder.Num(v.TryGetValue(k, out var x) ? x : 0);
        private static string FormKey(Side s) => MatchEvidenceBuilder.FormKey(s.Code);
        private static string VenueKey(Side s) => MatchEvidenceBuilder.VenueKey(s.Code);
        private static AnalysisSentence S(string text, params string[] keys) => new(text, keys);

        /// <summary>"2 galibiyet, 1 beraberlik ve 1 mağlubiyet" — sıfır olan kalem yazılmaz.</summary>
        public static string Record(IReadOnlyDictionary<string, double> v)
        {
            var parts = new List<string>();
            if (I(v, "w") > 0) parts.Add($"{I(v, "w")} galibiyet");
            if (I(v, "d") > 0) parts.Add($"{I(v, "d")} beraberlik");
            if (I(v, "l") > 0) parts.Add($"{I(v, "l")} mağlubiyet");
            return parts.Count <= 1 ? string.Join("", parts) : string.Join(", ", parts.Take(parts.Count - 1)) + " ve " + parts[^1];
        }

        private static string FormScope(IReadOnlyDictionary<string, double> f)
            => I(f, "n") == MatchEvidenceBuilder.MaxFormMatches ? "son 5 lig maçında" : $"bu sezon oynadığı {I(f, "n")} lig maçında";

        private static string VenueScope(Side s)
        {
            var n = I(s.Venue, "n");
            var place = s.Code == "home" ? "iç sahada" : "deplasmanda";
            return n == MatchEvidenceBuilder.MaxFormMatches ? $"{place} son 5 lig maçında" : $"{place} oynadığı {n} lig maçında";
        }

        // ── BU MAÇI NEDEN İZLEMELİ? ───────────────────────────────────────────────

        private static List<AnalysisSentence> WhyWatch(Side h, Side a, IReadOnlyList<EvidenceItem> ev, int variant)
        {
            var list = new List<AnalysisSentence>();

            var sh = MatchEvidenceBuilder.Find(ev, MatchEvidenceBuilder.StandingKey("home"))?.Values;
            var sa = MatchEvidenceBuilder.Find(ev, MatchEvidenceBuilder.StandingKey("away"))?.Values;
            if (sh != null && sa != null)
            {
                var ph = I(sh, "pos"); var pa = I(sa, "pos");
                if (ph <= 4 || pa <= 4 || Math.Abs(ph - pa) <= 2)
                    list.Add(S(variant == 0
                        ? $"{h.Name} {N(sh, "pts")} puanla {ph}. sırada, {a.Name} {N(sa, "pts")} puanla {pa}. sırada."
                        : $"Puan tablosunda {h.Name} {ph}. sırada ({N(sh, "pts")} puan), {a.Name} {pa}. sırada ({N(sa, "pts")} puan).",
                        MatchEvidenceBuilder.StandingKey("home"), MatchEvidenceBuilder.StandingKey("away")));
            }

            foreach (var s in new[] { h, a })
            {
                var n = I(s.Form, "n");
                if (n < 3) continue;
                if (I(s.Form, "l") == 0)
                    list.Add(S(n == MatchEvidenceBuilder.MaxFormMatches
                        ? $"{s.Name} ligde son 5 maçında yenilmedi: {Record(s.Form)}."
                        : $"{s.Name} bu sezon oynadığı {n} lig maçının hiçbirini kaybetmedi: {Record(s.Form)}.", FormKey(s)));
                else if (I(s.Form, "w") == 0)
                    list.Add(S($"{s.Name} bu sezon oynadığı {n} lig maçında henüz kazanamadı: {Record(s.Form)}.", FormKey(s)));
            }

            if (I(h.Form, "n") >= 3 && I(a.Form, "n") >= 3)
            {
                var th = h.Form["totalAvg"]; var ta = a.Form["totalAvg"];
                if (th >= 3.0 && ta >= 3.0)
                    list.Add(S(variant == 0
                        ? $"{h.Name} maçlarında ortalama {N(h.Form, "totalAvg")}, {a.Name} maçlarında ortalama {N(a.Form, "totalAvg")} gol çıktı."
                        : $"Gol ortalaması iki tarafta da yüksek: {h.Name} maçlarında {N(h.Form, "totalAvg")}, {a.Name} maçlarında {N(a.Form, "totalAvg")}.",
                        FormKey(h), FormKey(a)));
                else if (th <= 2.0 && ta <= 2.0)
                    list.Add(S($"{h.Name} maçlarında ortalama {N(h.Form, "totalAvg")}, {a.Name} maçlarında ortalama {N(a.Form, "totalAvg")} gol çıktı; iki tarafın maçları da düşük skorlu geçti.",
                        FormKey(h), FormKey(a)));
            }

            var rh = MatchEvidenceBuilder.Find(ev, MatchEvidenceBuilder.RestKey("home"))?.Values;
            var ra = MatchEvidenceBuilder.Find(ev, MatchEvidenceBuilder.RestKey("away"))?.Values;
            if (rh != null && ra != null && Math.Abs(I(rh, "days") - I(ra, "days")) >= 3)
                list.Add(S($"{h.Name} son maçından bu yana {N(rh, "days")} gün, {a.Name} ise {N(ra, "days")} gün dinlendi.",
                    MatchEvidenceBuilder.RestKey("home"), MatchEvidenceBuilder.RestKey("away")));

            foreach (var s in new[] { h, a })
                if (I(s.Form, "n") >= 3 && I(s.Form, "cs") >= 2)
                    list.Add(S($"{s.Name} {FormScope(s.Form)} {I(s.Form, "cs")} kez gol yemedi.", FormKey(s)));

            return list.Take(3).ToList();
        }

        // ── MAÇIN KİLİDİ ─────────────────────────────────────────────────────────

        private static List<AnalysisSentence> KeyBattle(Side h, Side a, int variant)
        {
            var list = new List<AnalysisSentence>();
            var hv = I(h.Venue, "n"); var av = I(a.Venue, "n");

            if (hv >= 1 && av >= 1)
            {
                list.Add(Battle(h, a, attacker: h, defender: a, variant));
                list.Add(Battle(h, a, attacker: a, defender: h, variant));
                return list;
            }

            var hn = I(h.Form, "n"); var an = I(a.Form, "n");
            if (hn >= 1 && an >= 1)
                list.Add(S($"{h.Name} {FormScope(h.Form)} {N(h.Form, "gf")} gol attı, {N(h.Form, "ga")} gol yedi; " +
                           $"{a.Name} {FormScope(a.Form)} {N(a.Form, "gf")} gol attı, {N(a.Form, "ga")} gol yedi.",
                    FormKey(h), FormKey(a)));
            return list;
        }

        private static AnalysisSentence Battle(Side h, Side a, Side attacker, Side defender, int variant)
        {
            var an = I(attacker.Venue, "n"); var dn = I(defender.Venue, "n");
            var useAvg = an >= 3 && dn >= 3;
            // Sıfır "0 gol attı" diye değil, olduğu gibi söylenir: "gol atamadı" / "gol yemedi".
            string attack = I(attacker.Venue, "gf") == 0 ? "gol atamadı"
                : useAvg ? $"maç başına {N(attacker.Venue, "gfAvg")} gol üretti" : $"{N(attacker.Venue, "gf")} gol attı";
            string defend = I(defender.Venue, "ga") == 0 ? "gol yemedi"
                : useAvg ? $"maç başına {N(defender.Venue, "gaAvg")} gol yedi" : $"{N(defender.Venue, "ga")} gol yedi";
            var text = variant == 0
                ? $"{attacker.Name} {VenueScope(attacker)} {attack}; {defender.Name} {VenueScope(defender)} {defend}."
                : $"{defender.Name} {VenueScope(defender)} {defend}, karşısındaki {attacker.Name} ise {VenueScope(attacker)} {attack}.";
            return S(text, VenueKey(attacker), VenueKey(defender));
        }

        // ── KADRO ETKİSİ ─────────────────────────────────────────────────────────

        private static List<AnalysisSentence> LineupImpact(Side h, Side a, IReadOnlyList<EvidenceItem> ev)
        {
            var lh = MatchEvidenceBuilder.Find(ev, MatchEvidenceBuilder.LineupKey("home"));
            var la = MatchEvidenceBuilder.Find(ev, MatchEvidenceBuilder.LineupKey("away"));
            string? fh = lh?.Texts?.GetValueOrDefault("formation");
            string? fa = la?.Texts?.GetValueOrDefault("formation");
            var list = new List<AnalysisSentence>();
            if (fh != null && fa != null)
                list.Add(S($"Resmî ilk 11'lerde {h.Name} {fh}, {a.Name} {fa} dizilişiyle başlıyor.",
                    MatchEvidenceBuilder.LineupKey("home"), MatchEvidenceBuilder.LineupKey("away")));
            else if (fh != null)
                list.Add(S($"{h.Name} resmî ilk 11'ini {fh} dizilişiyle açıkladı.", MatchEvidenceBuilder.LineupKey("home")));
            else if (fa != null)
                list.Add(S($"{a.Name} resmî ilk 11'ini {fa} dizilişiyle açıkladı.", MatchEvidenceBuilder.LineupKey("away")));
            else if (lh != null && la != null)
                list.Add(S($"{h.Name} ve {a.Name} resmî ilk 11'lerini açıkladı; kaynak diziliş bilgisi vermedi.",
                    MatchEvidenceBuilder.LineupKey("home"), MatchEvidenceBuilder.LineupKey("away")));
            return list;
        }

        // ── BELİRSİZLİK ──────────────────────────────────────────────────────────

        private static AnalysisSentence? Uncertainty(Side h, Side a, string league, int variant)
        {
            var hn = I(h.Form, "n"); var an = I(a.Form, "n");
            if (hn == 0 && an == 0)
                return S($"{h.Name} ve {a.Name} bu sezon henüz tamamlanmış lig maçı oynamadı; form karşılaştırması yapılamıyor.",
                    FormKey(h), FormKey(a));
            if (hn == 0 || an == 0)
            {
                var s = hn == 0 ? h : a;
                return S($"{s.Name} bu sezon henüz tamamlanmış lig maçı oynamadı; form karşılaştırması yapılamıyor.", FormKey(s));
            }
            if (hn < MatchEvidenceBuilder.MaxFormMatches || an < MatchEvidenceBuilder.MaxFormMatches)
                return S(variant == 0
                    ? $"Örneklem sınırlı: {h.Name} için {hn}, {a.Name} için {an} tamamlanmış lig maçı var."
                    : $"Değerlendirme sınırlı örnekleme dayanıyor ({h.Name} {hn}, {a.Name} {an} lig maçı).",
                    FormKey(h), FormKey(a));
            return null;
        }

        // ── OLASI SENARYOLARIN GEREKÇESİ ─────────────────────────────────────────

        private static List<ScenarioReason> Scenarios(Side h, Side a)
        {
            var list = new List<ScenarioReason>();
            if (I(h.Form, "n") == 0 || I(a.Form, "n") == 0) return list;

            // Sonuç marketlerinde iç saha/deplasman ayrımı; ayrım yoksa lig formu.
            Side V(Side s) => I(s.Venue, "n") > 0 ? s : s with { Venue = s.Form };
            var hv = V(h); var av = V(a);
            string HScope() => I(h.Venue, "n") > 0 ? VenueScope(h) : FormScope(h.Form);
            string AScope() => I(a.Venue, "n") > 0 ? VenueScope(a) : FormScope(a.Form);
            string HK() => I(h.Venue, "n") > 0 ? VenueKey(h) : FormKey(h);
            string AK() => I(a.Venue, "n") > 0 ? VenueKey(a) : FormKey(a);

            AnalysisSentence? Wins(Side s, IReadOnlyDictionary<string, double> v, string scope, string key)
                => I(v, "w") > 0 ? S($"{s.Name} {scope} {I(v, "w")} galibiyet aldı.", key) : null;
            AnalysisSentence? Points(Side s, IReadOnlyDictionary<string, double> v, string scope, string key)
                => I(v, "wd") > 0 ? S($"{s.Name} {scope} {I(v, "wd")} kez puanla ayrıldı.", key) : null;
            AnalysisSentence? Unbeaten(Side s, IReadOnlyDictionary<string, double> v, string scope, string key)
                => I(v, "wd") > 0 ? S($"{s.Name} {scope} {I(v, "wd")} kez yenilmedi.", key) : null;

            list.Add(new ScenarioReason("Ev Sahibi Kazanır", Wins(h, hv.Venue, HScope(), HK()), Points(a, av.Venue, AScope(), AK())));
            list.Add(new ScenarioReason("Deplasman Kazanır", Wins(a, av.Venue, AScope(), AK()), Points(h, hv.Venue, HScope(), HK())));
            list.Add(new ScenarioReason("Çifte Şans (1X)", Unbeaten(h, hv.Venue, HScope(), HK()), Wins(a, av.Venue, AScope(), AK())));
            list.Add(new ScenarioReason("Çifte Şans (X2)", Unbeaten(a, av.Venue, AScope(), AK()), Wins(h, hv.Venue, HScope(), HK())));

            var dh = I(h.Form, "d"); var da = I(a.Form, "d");
            list.Add(new ScenarioReason("Beraberlik",
                dh + da > 0 ? S($"{h.Name} {FormScope(h.Form)} {dh}, {a.Name} {FormScope(a.Form)} {da} beraberlik yaşadı.", FormKey(h), FormKey(a)) : null,
                S($"Aynı maçlarda {h.Name} {I(h.Form, "wl")}, {a.Name} {I(a.Form, "wl")} kez kazanan ya da kaybeden taraf oldu.", FormKey(h), FormKey(a))));
            list.Add(new ScenarioReason("Çifte Şans (12)",
                I(h.Form, "wl") + I(a.Form, "wl") > 0
                    ? S($"{h.Name} {FormScope(h.Form)} {I(h.Form, "wl")} maçı, {a.Name} {FormScope(a.Form)} {I(a.Form, "wl")} maçı galibiyet ya da mağlubiyetle bitirdi.", FormKey(h), FormKey(a))
                    : null,
                dh + da > 0 ? S($"{h.Name} ve {a.Name} maçlarında beraberlik sayısı sırasıyla {dh} ve {da}.", FormKey(h), FormKey(a)) : null));

            foreach (var (label, key, line) in new[] { ("1.5", "over15", 2), ("2.5", "over25", 3), ("3.5", "over35", 4) })
            {
                int oh = I(h.Form, key), oa = I(a.Form, key);
                var lineKey = LineKey(label);
                AnalysisSentence? Over() => oh + oa > 0
                    ? S($"{h.Name} {FormScope(h.Form)} {oh}, {a.Name} {FormScope(a.Form)} {oa} kez {line} veya daha fazla gol çıktı.", FormKey(h), FormKey(a), lineKey)
                    : null;
                int uh = I(h.Form, "under" + key[4..]), ua = I(a.Form, "under" + key[4..]);
                AnalysisSentence? Under() => uh + ua > 0
                    ? S($"{h.Name} maçlarının {uh}, {a.Name} maçlarının {ua} tanesi {label.Replace('.', ',')} gol sınırının altında bitti.", FormKey(h), FormKey(a), lineKey)
                    : null;
                list.Add(new ScenarioReason($"{label} Üst", Over(), Under()));
                list.Add(new ScenarioReason($"{label} Alt", Under(), Over()));
            }

            int bh = I(h.Form, "btts"), ba = I(a.Form, "btts");
            AnalysisSentence? Btts() => bh + ba > 0
                ? S($"{h.Name} {FormScope(h.Form)} {bh}, {a.Name} {FormScope(a.Form)} {ba} kez iki takım da gol buldu.", FormKey(h), FormKey(a))
                : null;
            int ftsH = I(h.Form, "fts"), csA = I(a.Form, "cs"), ftsA = I(a.Form, "fts"), csH = I(h.Form, "cs");
            AnalysisSentence? NoBtts() => ftsH + csA + ftsA + csH > 0
                ? S($"{h.Name} {ftsH} maçta gol atamadı ve {csH} maçta gol yemedi; {a.Name} için bu sayılar {ftsA} ve {csA}.", FormKey(h), FormKey(a))
                : null;
            list.Add(new ScenarioReason("Karşılıklı Gol Var", Btts(), NoBtts()));
            list.Add(new ScenarioReason("Karşılıklı Gol Yok", NoBtts(), Btts()));

            return list.Where(r => r.Support != null || r.Risk != null).ToList();
        }

        public static string LineKey(string label) => $"line:{label}";
    }
}
