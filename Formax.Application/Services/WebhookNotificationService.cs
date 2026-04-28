using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace Formax.Infrastructure.Services
{
    public class WebhookNotificationService : INotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly NotificationOptions _options;

        public WebhookNotificationService(
            HttpClient httpClient,
            IOptions<NotificationOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task NotifyAsync(int matchId, string title, string message)
        {
            var payload = new
            {
                matchId,
                title,
                message
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            await _httpClient.PostAsync(
                _options.Webhook.Url,
                content);
        }
    }
}
