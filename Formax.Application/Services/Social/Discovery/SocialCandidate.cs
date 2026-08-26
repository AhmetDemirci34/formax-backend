using System;

namespace Formax.Application.Services.Social.Discovery
{
    /// <summary>Phase 7 — bir resmi hesaptan çekilen ham sosyal paylaşım adayı (sağlayıcı-nötr).</summary>
    public sealed class SocialCandidate
    {
        public string Platform { get; set; } = "";
        public string AccountHandle { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime PublishedUtc { get; set; }
    }
}
