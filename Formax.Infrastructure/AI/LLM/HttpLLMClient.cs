using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.AI.LLM
{
    /// <summary>
    /// FORMAX Radar v2 — gerçek LLM istemcisi (sağlayıcı-bağımsız).
    ///
    /// Audit'te ölü bulunan ILLMClient zincirini canlandırır. Yapılandırmadan okur:
    ///   Llm:Provider   = "Ollama" | "OpenAI" | "Mock"   (varsayılan: Mock → güvenli)
    ///   Llm:Endpoint   = Ollama taban URL (ör. http://localhost:11434)
    ///   Llm:Model      = model adı (ör. "llama3.2:3b", "gpt-4o-mini")
    ///   Llm:ApiKey     = OpenAI anahtarı (yoksa OpenAI:ApiKey'e düşer)
    ///   Llm:TimeoutSeconds = istek zaman aşımı (varsayılan 30)
    ///
    /// TASARIM: Bu istemci ASLA exception fırlatmaz. Sağlayıcı yapılandırılmamışsa
    /// veya çağrı başarısızsa boş string döner. Çağıran katman (pipeline / use case)
    /// boş çıktıyı "AI sustu" olarak yorumlar ve deterministik fallback'e döner.
    /// Böylece LLM kapalıyken mevcut sistem bozulmaz (geri-uyum garantisi).
    /// </summary>
    public sealed class HttpLLMClient : ILLMClient
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<HttpLLMClient> _logger;

        public HttpLLMClient(HttpClient http, IConfiguration config, ILogger<HttpLLMClient> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;

            var timeout = ReadInt("Llm:TimeoutSeconds", 30);
            _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeout, 5, 120));
        }

        public async Task<string> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
        {
            var provider = (_config["Llm:Provider"] ?? "Mock").Trim();

            try
            {
                return provider.ToLowerInvariant() switch
                {
                    "ollama" => await CallOllamaAsync(systemPrompt, userPrompt, cancellationToken),
                    "openai" => await CallOpenAiAsync(systemPrompt, userPrompt, cancellationToken),
                    _ => string.Empty // Mock / yapılandırılmamış → fallback tetikler
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[LLM] '{Provider}' çağrısı başarısız — fallback'e dönülüyor", provider);
                return string.Empty;
            }
        }

        // ── Ollama (local, STEP-3: llama3.2:3b) ────────────────────────────────
        private async Task<string> CallOllamaAsync(string system, string user, CancellationToken ct)
        {
            var endpoint = (_config["Llm:Endpoint"] ?? "http://localhost:11434").TrimEnd('/');
            var model = _config["Llm:Model"] ?? "llama3.2:3b";

            // format="json": Ollama'nın yapılandırılmış çıktı kipi. Çağıran katman (Radar
            // pipeline) KATI JSON bekliyor; bu kip olmadan küçük modeller şemayı yok sayıp
            // düz metin döndürüyor → ExtractJson başarısız → her istek fallback'e düşüyor.
            // Ölçüldü (llama3.2:3b, gerçek RadarPromptComposer prompt'u): kipsiz düz metin,
            // kiple geçerli JSON. Sağlayıcı bu alanı bilmiyorsa yok sayar (geri-uyumlu).
            var body = new
            {
                model,
                stream = false,
                format = "json",
                options = new { temperature = 0.4 },
                messages = new[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/api/chat")
            {
                Content = JsonContent(body)
            };

            // Ollama Cloud (https://ollama.com/api/chat) "Authorization: Bearer <key>" ister;
            // yerel sunucu (localhost:11434) anahtar istemez ve fazladan header'ı yok sayar.
            // Bu yüzden başlık YALNIZ yapılandırmada anahtar varsa eklenir → tek istemci hem
            // yerel hem bulut Ollama'ya gider, yeni bir sağlayıcı/abstraction gerekmez.
            // Anahtar koda yazılmaz; Llm:ApiKey (ör. Llm__ApiKey ortam değişkeni) üzerinden gelir.
            var apiKey = _config["Llm:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            using var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[LLM] Ollama HTTP {Status}", resp.StatusCode);
                return string.Empty;
            }

            using var doc = JsonDocument.Parse(json);
            // Ollama /api/chat → { message: { content: "..." } }
            if (doc.RootElement.TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content))
            {
                return content.GetString()?.Trim() ?? string.Empty;
            }
            return string.Empty;
        }

        // ── OpenAI (cloud fallback) ────────────────────────────────────────────
        private async Task<string> CallOpenAiAsync(string system, string user, CancellationToken ct)
        {
            var apiKey = _config["Llm:ApiKey"] ?? _config["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("[LLM] OpenAI anahtarı yok — fallback");
                return string.Empty;
            }

            var endpoint = (_config["Llm:Endpoint"] ?? "https://api.openai.com").TrimEnd('/');
            var model = _config["Llm:Model"] ?? "gpt-4o-mini";

            var body = new
            {
                model,
                temperature = 0.4,
                messages = new[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/v1/chat/completions")
            {
                Content = JsonContent(body)
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[LLM] OpenAI HTTP {Status}", resp.StatusCode);
                return string.Empty;
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString()?.Trim() ?? string.Empty;
        }

        private static StringContent JsonContent(object body) =>
            new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        private int ReadInt(string key, int fallback) =>
            int.TryParse(_config[key], out var v) ? v : fallback;
    }
}
