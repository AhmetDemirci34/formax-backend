using System;
using System.Linq;

namespace Formax.Application.Services.News.Intelligence
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — Source Quality. Her kaynağa (yayıncı/domain) güvenilirlik
    /// puanı verir; Confidence hesabında kullanılır. Resmi kulüp/federasyon en yüksek,
    /// blog en düşük. Bilinmeyen → makul orta.
    /// </summary>
    public sealed class SourceQualityResolver
    {
        private static readonly string[] Federation = { "fifa", "uefa", "thefa", "federation", "federasyon", "tff" };
        private static readonly string[] OfficialClub = { "official", "club statement", "arsenal.com", "liverpoolfc", "realmadrid", "fcbarcelona", "mancity", "manutd" };
        private static readonly string[] MajorMedia = { "bbc", "espn", "sky sports", "skysports", "the guardian", "guardian", "goal.com", "goal ", "reuters", "the athletic", "athletic", "ap news", "afp" };
        private static readonly string[] VerifiedSports = { "marca", "as.com", "fabrizio", "gazzetta", "kicker", "lequipe", "football italia", "evening standard", "mirror", "telegraph", "fotomac", "sabah spor" };
        private static readonly string[] Local = { "local", "regional", "haberler", "gazete" };

        public int Quality(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return 50;
            var s = source.ToLowerInvariant();

            if (Federation.Any(s.Contains)) return 95;
            if (OfficialClub.Any(s.Contains)) return 95;
            if (MajorMedia.Any(s.Contains)) return 90;
            if (VerifiedSports.Any(s.Contains)) return 85;
            if (Local.Any(s.Contains)) return 70;

            // Bilinmeyen yayıncı: blog/orta seviye.
            return 60;
        }

        /// <summary>Bir haberin tüm kaynaklarından en yüksek kaliteyi alır.</summary>
        public int BestQuality(System.Collections.Generic.IEnumerable<string> sources)
        {
            var best = 50;
            foreach (var src in sources)
                best = Math.Max(best, Quality(src));
            return best;
        }
    }
}
