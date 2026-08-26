using System;

namespace Formax.Application.DTOs.Social
{
    /// <summary>
    /// Phase 7 — Match Detail (Flash Gelişmeler / Resmi Paylaşımlar) için Canonical Social
    /// paylaşımının kullanıcıya gösterilen şekli. Tek Canonical Social; AI ile aynı kaynak.
    /// </summary>
    public sealed class MatchSocialPostDto
    {
        public string Headline { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Source { get; set; } = "";     // resmi hesap adı
        public string Platform { get; set; } = "";
        public DateTime PublishedAt { get; set; }
        public string Url { get; set; } = "";
        public string SignalType { get; set; } = "";
        public bool IsOfficial { get; set; }
    }
}
