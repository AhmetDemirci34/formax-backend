using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.News.Discovery
{
    /// <summary>
    /// FORMAX Data Engine v2 — Search Query Builder. Maç metadata'sından (ev/deplasman/
    /// lig) DİNAMİK arama sorguları üretir: eşleşme, sakatlık, kadro, teknik direktör,
    /// transfer, basın toplantısı, form. Sorgular öncelik sırasıyla döner.
    /// </summary>
    public sealed class MatchNewsSearchQueryBuilder
    {
        // Takım başına üretilen açı ekleri (EN — global haber kapsamı için).
        private static readonly string[] Angles =
            { "injury", "team news", "lineup", "coach", "transfer", "press conference", "form" };

        // Maç bağlamlı (iki takımı da anan) haber açıları — sorgu bütçesinin başında kullanılır.
        private static readonly string[] MatchAngles =
            { "team news", "injury", "press conference" };

        // TÜRKÇE açılar — Türk takımlarında gerçek haber bu kelimelerle çıkar.
        private static readonly string[] TurkishAngles =
            { "sakatlık", "kadro", "teknik direktör", "transfer", "açıklama" };

        private static readonly string[] TurkishAngles2 = { "muhtemel 11", "basın toplantısı" };

        public NewsQuery Build(string formaxMatchId, string home, string away,
            string league, string country, DateTime kickoffUtc)
        {
            var q = new List<string>();
            var turkish = IsTurkishContext(league, country, home, away);

            // ÖNCELİK NEDEN BÖYLE: sağlayıcılar sorgu listesinin yalnız İLK BİRKAÇINI kullanır.
            // Çıplak eşleşme sorguları ("A vs B") ağırlıkla fikstür, TV yayın ve oran sayfası
            // döndürüyor; bunlar factual evidence olamıyor. Bu yüzden bütçenin başına HABER
            // NİYETLİ sorgular konur.
            if (!string.IsNullOrWhiteSpace(home) && !string.IsNullOrWhiteSpace(away))
            {
                if (turkish)
                {
                    // Türkçe bağlamda önce takım-açı: "Fenerbahçe sakatlık" gerçek gelişme verir,
                    // "Fenerbahçe vs Galatasaray" ise fikstür/yayın sayfası getirir.
                    foreach (var angle in TurkishAngles.Take(3))
                        foreach (var team in new[] { home, away })
                            q.Add($"{team} {angle}");
                }

                foreach (var angle in MatchAngles)
                    q.Add($"{home} {away} {angle}");

                q.Add($"{home} vs {away}");
                q.Add($"{home} {away}");
                if (!string.IsNullOrWhiteSpace(league))
                    q.Add($"{league} {home} {away}");
            }

            // Takım-açı sorguları (kalan bütçe).
            foreach (var team in new[] { home, away }.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                foreach (var angle in Angles)
                    q.Add($"{team} {angle}");

                if (turkish)
                    foreach (var angle in TurkishAngles.Concat(TurkishAngles2))
                        q.Add($"{team} {angle}");
            }

            // Tekille + sırayı koru.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = q.Where(x => seen.Add(x)).ToList();

            return new NewsQuery
            {
                FormaxMatchId = formaxMatchId,
                HomeTeam = home,
                AwayTeam = away,
                League = league,
                Country = country,
                KickoffUtc = kickoffUtc,
                Locale = turkish ? "tr" : "en",
                Queries = ordered
            };
        }

        /// <summary>
        /// Maç Türkiye bağlamında mı? Süper Lig / Türkiye Kupası ya da bilinen Türk kulüpleri.
        /// Avrupa kupasında oynayan bir Türk takımının haberi de Türkçe kaynaklarda çıkar,
        /// bu yüzden lig adına ek olarak takım adına da bakılır.
        /// </summary>
        private static bool IsTurkishContext(string? league, string? country, string? home, string? away)
        {
            var ctx = Intelligence.NewsTextNormalizer.Fold($"{league} {country}");
            if (ctx.Contains("super lig", StringComparison.Ordinal)
                || ctx.Contains("superlig", StringComparison.Ordinal)
                || ctx.Contains("turkiye", StringComparison.Ordinal)
                || ctx.Contains("turkey", StringComparison.Ordinal)
                || ctx.Contains("trendyol", StringComparison.Ordinal)
                || ctx.Contains("1. lig", StringComparison.Ordinal))
                return true;

            var teams = Intelligence.NewsTextNormalizer.Fold($"{home} {away}");
            return TurkishClubs.Any(c => teams.Contains(c, StringComparison.Ordinal));
        }

        private static readonly string[] TurkishClubs =
        {
            "galatasaray", "fenerbahce", "besiktas", "trabzonspor", "basaksehir", "adana demirspor",
            "antalyaspor", "alanyaspor", "konyaspor", "sivasspor", "kayserispor", "gaziantep",
            "goztepe", "rizespor", "samsunspor", "eyupspor", "kasimpasa", "hatayspor",
            "bodrum", "genclerbirligi", "karagumruk", "pendikspor", "istanbulspor", "umraniyespor",
            "altay", "bursaspor", "ankaragucu", "denizlispor", "erzurumspor", "manisa"
        };
    }
}
