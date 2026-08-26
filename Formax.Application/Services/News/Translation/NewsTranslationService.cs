using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.LLM;
using Formax.Application.DTOs.Nabiz;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.News.Translation
{
    /// <summary>
    /// SON DAKİKA — haber başlığı/özetinin kullanıcının FORMAX dilindeki karşılığı.
    ///
    /// MİMARİ: çeviriyi BACKEND yapar (frontend'de çeviri yoktur) ve MEVCUT
    /// <see cref="ILLMClient"/> zinciri kullanılır — yeni üçüncü taraf çeviri servisi YOK.
    ///
    /// ÖNBELLEK: sonuç (ContentHash, Language) ile kalıcı yazılır. Aynı haber aynı dilde
    /// ikinci kez ASLA çevrilmez; yeniden başlatma da önbelleği düşürmez.
    ///
    /// DÜRÜSTLÜK KURALI: çeviri ya BÜTÜN olarak uygulanır ya da hiç uygulanmaz. Model
    /// susarsa, bozuk JSON dönerse, öge sayısı tutmazsa veya bir başlık boş gelirse o öge
    /// ORİJİNAL hâliyle bırakılır (<see cref="NabizFeedItemDto.IsTranslated"/> = false).
    /// Yarım çeviri, uydurma özet veya anlam değiştiren metin ÜRETİLMEZ.
    ///
    /// ÖZEL İSİMLER: takım/oyuncu/teknik direktör/kaynak adları çevrilmez — prompt bunu
    /// açıkça yasaklar; ayrıca kaynak adı, URL, tarih ve saat modele hiç gönderilmez.
    /// </summary>
    public sealed class NewsTranslationService
    {
        /// <summary>Tek LLM çağrısında çevrilecek en fazla haber (istek boyutu ve süre için).</summary>
        private const int BatchSize = 10;

        private static readonly IReadOnlyDictionary<string, string> LanguageNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["tr"] = "Turkish",
                ["en"] = "English",
                ["de"] = "German",
                ["es"] = "Spanish",
                ["fr"] = "French",
                ["it"] = "Italian",
                ["pt"] = "Portuguese",
                ["ar"] = "Arabic",
            };

        private readonly ILLMClient _llm;
        private readonly IMatchNewsTranslationRepository _store;
        private readonly ILogger<NewsTranslationService> _logger;

        public NewsTranslationService(
            ILLMClient llm,
            IMatchNewsTranslationRepository store,
            ILogger<NewsTranslationService> logger)
        {
            _llm = llm;
            _store = store;
            _logger = logger;
        }

        /// <summary>Desteklenen dil mi? (FORMAX dil listesiyle aynı küme.)</summary>
        public static bool IsSupported(string? language)
            => !string.IsNullOrWhiteSpace(language) && LanguageNames.ContainsKey(language.Trim());

        /// <summary>
        /// Listeyi hedef dile uyarlar. Ögeler YERİNDE güncellenir; sıra ve sayı DEĞİŞMEZ.
        /// </summary>
        /// <param name="scopeKey">Kilit kapsamı (maçın FORMAX_MATCH_ID'si) — bkz. eşzamanlılık notu.</param>
        public async Task ApplyAsync(
            List<NabizFeedItemDto> items, string language, string scopeKey, CancellationToken ct = default)
        {
            if (items.Count == 0) return;
            if (!IsSupported(language)) return;

            var lang = language.Trim().ToLowerInvariant();

            // Zaten hedef dilde olan haber çevrilmez ("EN kullanıcı → İngilizce haber aynen").
            var needed = items
                .Where(i => !string.IsNullOrWhiteSpace(i.Id))
                .Where(i => !string.Equals(i.Language, lang, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (needed.Count == 0) return;

            // 1) Kalıcı önbellek.
            var missing = await ApplyFromCacheAsync(needed, lang, ct);
            if (missing.Count == 0) return;

            //
            // 2) EŞZAMANLI İSTEK KİLİDİ.
            //
            // NEDEN (ölçüldü 18.08): aynı (maç, dil) için iki istek aynı anda gelince ikisi de
            // LLM'e gidiyordu; birinin çıktısı kullanılamayınca o istek ORİJİNAL (İngilizce)
            // metinle dönüyordu — kullanıcı Türkçe seçmişken İngilizce haber görüyordu.
            // Kilidi alan çevirir; bekleyen, kilit açılınca önbelleği YENİDEN okur (çift
            // kontrollü kilitleme) ve LLM'i ikinci kez yormaz.
            //
            // Kilit KAPSAMI = (maç, dil). Dil-bazlı tek kilit çok genişti: A maçının Almanca
            // çevirisi B maçının Almanca isteğini bekletiyor ve istemci zaman aşımına düşüyordu.
            var gate = Gates.GetOrAdd(scopeKey + "|" + lang, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            try
            {
                missing = await ApplyFromCacheAsync(missing, lang, ct);
                if (missing.Count == 0) return;

                // 3) Eksikler için LLM. Parçalar PARALEL gider: 25 haber sıralı çevrildiğinde
                // ~24 sn sürüyordu; kullanıcı o süre boyunca çevrilmemiş liste görüyordu.
                var targetName = LanguageNames[lang];
                var batches = Chunk(missing, BatchSize).ToList();
                var results = await Task.WhenAll(
                    batches.Select(b => TranslateBatchAsync(b, lang, targetName, ct)));

                var fresh = new List<MatchNewsTranslation>();
                foreach (var translated in results)
                {
                    if (translated == null) continue; // sustu/bozuk → o parça orijinal kalır

                    foreach (var (item, headline, summary) in translated)
                    {
                        Apply(item, headline, summary);
                        fresh.Add(new MatchNewsTranslation
                        {
                            ContentHash = item.Id,
                            Language = lang,
                            Headline = headline,
                            Summary = summary ?? string.Empty
                        });
                    }
                }

                if (fresh.Count > 0)
                {
                    try
                    {
                        await _store.UpsertAsync(fresh, ct);
                    }
                    catch (Exception ex)
                    {
                        // Önbelleğe yazamamak çeviriyi geçersiz kılmaz — kullanıcı doğru metni görür.
                        _logger.LogWarning(ex, "[NEWS-TR] Çeviri önbelleğe yazılamadı ({Count} öge)", fresh.Count);
                    }
                }

                var stillOriginal = missing.Count - fresh.Count;
                if (stillOriginal > 0)
                {
                    // Sessiz kalma: kaç haberin çevrilemediği loglanır (rapor edilebilir olsun).
                    _logger.LogWarning(
                        "[NEWS-TR] {Failed}/{Total} haber '{Lang}' diline çevrilemedi — orijinal metin gösteriliyor",
                        stillOriginal, missing.Count, lang);
                }
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>Kalıcı önbellekten uygulanabilenleri uygular; geriye kalanları döndürür.</summary>
        private async Task<List<NabizFeedItemDto>> ApplyFromCacheAsync(
            List<NabizFeedItemDto> candidates, string lang, CancellationToken ct)
        {
            var cached = await _store.GetAsync(candidates.Select(i => i.Id), lang, ct);
            if (cached.Count == 0) return candidates;

            var remaining = new List<NabizFeedItemDto>(candidates.Count);
            foreach (var item in candidates)
            {
                if (cached.TryGetValue(item.Id, out var hit)) Apply(item, hit.Headline, hit.Summary);
                else remaining.Add(item);
            }
            return remaining;
        }

        /// <summary>Dil başına tek eşzamanlı çeviri — aynı işin iki kez yapılmasını engeller.</summary>
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

        /// <summary>Çeviriyi ögeye uygular; orijinali kaybetmeden.</summary>
        private static void Apply(NabizFeedItemDto item, string headline, string? summary)
        {
            if (string.IsNullOrWhiteSpace(headline)) return; // boş çeviri = çeviri yok

            item.OriginalHeadline = item.Headline;
            item.OriginalSummary = item.Summary;
            item.Headline = headline;

            // Özet YALNIZ modelden anlamlı metin geldiyse değiştirilir; aksi hâlde orijinal
            // özet korunur (boş özet göstermek bilgi kaybıdır).
            if (!string.IsNullOrWhiteSpace(summary)) item.Summary = summary;

            item.IsTranslated = true;
        }

        /// <summary>
        /// Tek LLM çağrısı. Başarısızlıkta null → çağıran orijinali korur.
        /// </summary>
        private async Task<List<(NabizFeedItemDto Item, string Headline, string? Summary)>?>
            TranslateBatchAsync(
                List<NabizFeedItemDto> batch, string lang, string targetLanguageName, CancellationToken ct)
        {
            var system =
                $"You are a professional sports news translator. Translate football news into {targetLanguageName}.\n" +
                "STRICT RULES:\n" +
                "1. NEVER translate or alter proper nouns: club names, player names, coach names, " +
                "stadium names, publisher/source names. Keep them EXACTLY as written.\n" +
                "2. Translate ONLY the meaning of the text. Do not add facts, scores, opinions or context.\n" +
                "3. Do not summarize, do not shorten, do not expand.\n" +
                "4. If a text is already in the target language, return it unchanged.\n" +
                "5. Output MUST be a JSON array only. No markdown, no code fences, no commentary.\n" +
                "Each element: {\"i\": <index>, \"h\": \"<translated headline>\", \"s\": \"<translated summary>\"}\n" +
                "Return exactly one element per input item, same indexes.";

            var sb = new StringBuilder();
            sb.AppendLine($"Translate the following {batch.Count} football news item(s) into {targetLanguageName}.");
            sb.AppendLine("Input JSON:");
            var payload = batch.Select((b, idx) => new
            {
                i = idx,
                h = b.Headline ?? string.Empty,
                s = b.Summary ?? string.Empty
            }).ToList();
            sb.AppendLine(JsonSerializer.Serialize(payload));

            string raw;
            try
            {
                raw = await _llm.GenerateAsync(system, sb.ToString(), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NEWS-TR] LLM çağrısı başarısız — orijinal metin korunuyor");
                return null;
            }

            if (string.IsNullOrWhiteSpace(raw)) return null; // AI sustu → fallback

            var parsed = ParseArray(raw);
            if (parsed == null || parsed.Count == 0) return null;

            var result = new List<(NabizFeedItemDto, string, string?)>();
            foreach (var (index, headline, summary) in parsed)
            {
                if (index < 0 || index >= batch.Count) continue;
                if (string.IsNullOrWhiteSpace(headline)) continue;
                result.Add((batch[index], headline.Trim(), string.IsNullOrWhiteSpace(summary) ? null : summary.Trim()));
            }

            return result.Count == 0 ? null : result;
        }

        /// <summary>
        /// Modelin çıktısından JSON dizisini ayıklar. Model kod bloğu/ön söz eklerse
        /// ilk '[' ile son ']' arası alınır. Bozuk çıktı → null (orijinal korunur).
        /// </summary>
        private List<(int Index, string? Headline, string? Summary)>? ParseArray(string raw)
        {
            var start = raw.IndexOf('[');
            var end = raw.LastIndexOf(']');
            if (start < 0 || end <= start) return null;

            var json = raw.Substring(start, end - start + 1);

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

                var list = new List<(int, string?, string?)>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;
                    if (!el.TryGetProperty("i", out var iEl)) continue;

                    int index;
                    if (iEl.ValueKind == JsonValueKind.Number) index = iEl.GetInt32();
                    else if (iEl.ValueKind == JsonValueKind.String && int.TryParse(iEl.GetString(), out var pi)) index = pi;
                    else continue;

                    var h = el.TryGetProperty("h", out var hEl) && hEl.ValueKind == JsonValueKind.String
                        ? hEl.GetString() : null;
                    var s = el.TryGetProperty("s", out var sEl) && sEl.ValueKind == JsonValueKind.String
                        ? sEl.GetString() : null;

                    list.Add((index, h, s));
                }
                return list;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("[NEWS-TR] Model geçersiz JSON döndürdü — orijinal metin korunuyor: {Message}", ex.Message);
                return null;
            }
        }

        private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
        {
            for (var i = 0; i < source.Count; i += size)
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }
}
