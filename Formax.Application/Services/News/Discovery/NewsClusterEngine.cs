using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.News.Discovery
{
    /// <summary>
    /// FORMAX Data Engine v2 — News Clustering. Haber metnini (başlık+özet) anahtar
    /// kelime tablosuna göre ÇOK ETİKETLİ kümelere ayırır. Bir haber birden fazla
    /// kümeye girebilir. Deterministik; AI/LLM yok.
    /// </summary>
    public sealed class NewsClusterEngine
    {
        private static readonly (string Cluster, string[] Keywords)[] Rules =
        {
            ("Injury",          new[] { "injury", "injured", "knock", "strain", "fitness doubt", "sidelined", "sakat", "sakatlık" }),
            ("Disciplinary",    new[] { "suspended", "suspension", "ban", "red card", "cezalı", "kırmızı kart" }),
            ("Transfer",        new[] { "transfer", "signing", "sign", "bid", "deal", "move", "loan", "fee", "transfer", "imza", "bonservis" }),
            ("Lineup",          new[] { "lineup", "line-up", "starting xi", "predicted", "team news", "kadro", "ilk 11", "muhtemel 11" }),
            ("Coach",           new[] { "coach", "manager", "boss", "head coach", "sacked", "appoint", "teknik direktör", "hoca" }),
            ("Press Conference",new[] { "press conference", "presser", "said", "speaks", "interview", "basın toplantısı" }),
            ("Form",            new[] { "form", "winning run", "unbeaten", "streak", "slump", "crisis", "form" }),
            ("Tactical",        new[] { "tactic", "formation", "system", "press", "build-up", "taktik", "diziliş" }),
            ("Fan Reaction",    new[] { "fans", "supporters", "ultras", "tifo", "taraftar" }),
            ("Club Statement",  new[] { "statement", "official", "club confirm", "announce", "açıklama", "resmi" }),
            ("Referee",         new[] { "referee", "var", "official appointed", "whistle", "hakem" }),
            ("Weather",         new[] { "weather", "rain", "storm", "snow", "pitch", "hava" }),
            ("Travel",          new[] { "travel", "flight", "arrive", "journey", "seyahat" }),
            ("Schedule",        new[] { "fixture", "schedule", "kick-off", "postponed", "rescheduled", "date confirmed", "fikstür", "ertelendi" }),
        };

        public IReadOnlyList<string> Classify(string headline, string summary)
        {
            var text = $"{headline} {summary}".ToLowerInvariant();
            var hits = new List<string>();

            foreach (var (cluster, keywords) in Rules)
                if (keywords.Any(k => text.Contains(k, StringComparison.Ordinal)))
                    hits.Add(cluster);

            if (hits.Count == 0) hits.Add("General");
            return hits;
        }
    }
}
