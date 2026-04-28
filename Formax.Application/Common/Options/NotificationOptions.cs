namespace Formax.Application.Common.Options
{
    public class NotificationOptions
    {
        public WebhookOptions Webhook { get; set; } = new();
    }

    public class WebhookOptions
    {
        public string Url { get; set; } = string.Empty;
    }
}
