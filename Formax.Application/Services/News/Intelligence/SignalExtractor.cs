using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.News.Intelligence
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — Signal Extraction. Ham haberden değil, SİNYAL üretir.
    /// Çok-etiketli; bir içerik birden çok sinyal taşıyabilir. Deterministik.
    ///
    /// DÜZELTİLDİ: kelimeler TÜRKÇE-DUYARSIZ eşleşiyordu ("sakatlık" içindeki 'ı',
    /// "cezalı"nın 'ı'sı) ve dağarcık dardı → gerçek Türkçe futbol haberi "General"
    /// etiketleniyor, pack'in tip kapısında düşüyordu. Artık metin ve anahtar kelimeler
    /// aynı normalizasyondan (<see cref="NewsTextNormalizer.Fold"/>) geçer.
    /// </summary>
    public sealed class SignalExtractor
    {
        // Sinyal → anahtar kelimeler. TÜMÜ ASCII-fold edilmiş yazılır (fold sonrası eşleşir).
        private static readonly (string Signal, string[] Keywords)[] Rules =
        {
            ("Injury",          new[] { "injury", "injured", "injuries", "knock", "strain", "fitness",
                                        "sidelined", "doubt", "ruled out", "recovery", "surgery",
                                        "sakat", "sakatlik", "sakatlandi", "tedavi", "ameliyat",
                                        "forma giyemeyecek", "kadroda yok" }),
            ("Suspension",      new[] { "suspended", "suspension", "ban", "banned", "red card",
                                        "disciplinary", "cezali", "ceza aldi", "kirmizi kart",
                                        "men cezasi", "pfdk" }),
            ("Transfer",        new[] { "transfer", "signing", "signs", "signed", "bid", "deal",
                                        "loan", "fee", "medical", "contract", "release clause",
                                        "imza", "bonservis", "kiralik", "sozlesme", "anlasti",
                                        "kadrosuna katti", "ayriligi" }),
            ("Lineup",          new[] { "lineup", "line-up", "starting xi", "predicted", "team news",
                                        "squad list", "kadro", "ilk 11", "muhtemel 11", "aciklanan kadro",
                                        "kamp kadrosu" }),
            ("Coach",           new[] { "coach", "manager", "boss", "sacked", "appoint", "head coach",
                                        "teknik direktor", "hoca", "gorevden alindi", "gorevi birakti",
                                        "yeni teknik" }),
            ("Club Statement",  new[] { "statement", "official", "announce", "announced", "confirm",
                                        "confirmed", "club confirm", "aciklama", "resmi", "duyurdu",
                                        "kulup aciklamasi", "kap" }),
            ("Referee",         new[] { "referee", "official appointed", "hakem", "var hakemi",
                                        "hakem atamasi", "duduk" }),
            ("Schedule",        new[] { "postponed", "rescheduled", "date confirmed", "ertelendi",
                                        "tarihi degisti", "saat degisikligi" }),
            ("Weather",         new[] { "weather", "heavy rain", "storm", "snow", "heatwave",
                                        "hava kosullari", "saganak", "kar yagisi", "firtina" }),
            ("Travel",          new[] { "travel", "flight", "arrived", "journey", "seyahat",
                                        "kafile", "ucus" }),
            ("Form",            new[] { "form", "winning run", "unbeaten", "streak", "slump",
                                        "galibiyet serisi", "yenilmezlik", "form grafigi" }),
            ("Pressure",        new[] { "must win", "must-win", "pressure", "crisis", "decisive",
                                        "baski", "kritik", "kriz", "zorunlu galibiyet" }),
            ("Derby",           new[] { "derby", "rivalry", "clasico", "derbi", "ezeli rakip" }),
            ("Fan Interest",    new[] { "sold out", "tickets", "supporters", "ultras", "attendance",
                                        "taraftar", "bilet", "tribun", "seyirci" }),
            ("Press Conference",new[] { "press conference", "presser", "speaks", "interview",
                                        "basin toplantisi", "konustu", "aciklamalarda bulundu",
                                        "roportaj" }),
        };

        public IReadOnlyList<string> Extract(string headline, string summary)
        {
            var text = NewsTextNormalizer.Fold($"{headline} {summary}");
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
