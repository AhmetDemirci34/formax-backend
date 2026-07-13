using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.News.Intelligence
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — Signal Extraction. Ham haberden değil, SİNYAL üretir.
    /// v2 cluster setini genişletir (Pressure / Derby / Fan Interest ek sinyalleri).
    /// Çok-etiketli; bir içerik birden çok sinyal taşıyabilir. Deterministik.
    /// </summary>
    public sealed class SignalExtractor
    {
        // Sinyal → anahtar kelimeler (EN+TR). v2 cluster'larıyla uyumlu + ek sinyaller.
        private static readonly (string Signal, string[] Keywords)[] Rules =
        {
            ("Injury",          new[] { "injury", "injured", "knock", "strain", "fitness", "sidelined", "doubt", "sakat" }),
            ("Suspension",      new[] { "suspended", "suspension", "ban", "red card", "cezalı", "kırmızı kart" }),
            ("Transfer",        new[] { "transfer", "signing", "sign", "bid", "deal", "loan", "fee", "imza", "bonservis" }),
            ("Lineup",          new[] { "lineup", "line-up", "starting xi", "predicted", "team news", "kadro", "ilk 11" }),
            ("Coach",           new[] { "coach", "manager", "boss", "sacked", "appoint", "teknik direktör", "hoca" }),
            ("Club Statement",  new[] { "statement", "official", "announce", "confirm", "açıklama", "resmi" }),
            ("Referee",         new[] { "referee", "var", "official appointed", "hakem" }),
            ("Schedule",        new[] { "fixture", "schedule", "kick-off", "postponed", "rescheduled", "fikstür", "ertelendi" }),
            ("Weather",         new[] { "weather", "rain", "storm", "snow", "pitch", "hava" }),
            ("Travel",          new[] { "travel", "flight", "arrive", "journey", "seyahat" }),
            ("Form",            new[] { "form", "winning run", "unbeaten", "streak", "slump", "crisis" }),
            ("Pressure",        new[] { "must win", "must-win", "pressure", "crisis", "decisive", "baskı", "kritik" }),
            ("Derby",           new[] { "derby", "rivalry", "clasico", "clásico", "derbi", "rakip" }),
            ("Fan Interest",    new[] { "fans", "sold out", "tickets", "supporters", "ultras", "taraftar", "bilet" }),
            ("Press Conference",new[] { "press conference", "presser", "speaks", "interview", "basın toplantısı" }),
        };

        public IReadOnlyList<string> Extract(string headline, string summary)
        {
            var text = $"{headline} {summary}".ToLowerInvariant();
            var hits = new List<string>();
            foreach (var (signal, keywords) in Rules)
                if (keywords.Any(k => text.Contains(k, StringComparison.Ordinal)))
                    hits.Add(signal);

            if (hits.Count == 0) hits.Add("General");
            return hits;
        }

        /// <summary>Birincil (en belirleyici) sinyal — Evidence.Type olarak kullanılır.</summary>
        public string Primary(IReadOnlyList<string> signals) =>
            signals.FirstOrDefault(s => s != "General") ?? "General";
    }
}
