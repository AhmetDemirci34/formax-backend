using Formax.Application.Services.AI;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class OpenAIService : IAIService
{
    private readonly HttpClient _http;

    public OpenAIService(HttpClient http, IConfiguration config)
    {
        _http = http;

        _http.BaseAddress = new Uri("https://api.openai.com/");

        var apiKey = config["OpenAI:ApiKey"];

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> GenerateComment(string prompt)
    {
        var body = new
        {
            model = "gpt-3.5-turbo", // 🔥 garanti model
            messages = new[]
            {
                new { role = "system", content = "Futbolu çok basit anlat. Kısa yaz." },
                new { role = "user", content = prompt }
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _http.PostAsync("v1/chat/completions", content);

        var json = await response.Content.ReadAsStringAsync();

        // 🔥 DEBUG
        Console.WriteLine("STATUS: " + response.StatusCode);
        Console.WriteLine("OPENAI BODY:");
        Console.WriteLine(json);

        if (!response.IsSuccessStatusCode)
            return "AI çalışmıyor";

        try
        {
            using var doc = JsonDocument.Parse(json);

            var result = doc.RootElement
                            .GetProperty("choices")[0]
                            .GetProperty("message")
                            .GetProperty("content")
                            .GetString();

            if (string.IsNullOrWhiteSpace(result))
                return "AI boş";

            return result.Trim();
        }
        catch
        {
            return "AI parse hatası";
        }
    }
}