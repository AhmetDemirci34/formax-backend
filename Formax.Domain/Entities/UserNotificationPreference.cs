namespace Formax.Domain.Entities
{
    /// <summary>
    /// KULLANICININ BİLDİRİM TERCİHİ — içerik bazlı aç/kapat (event tipi YOK).
    ///
    /// Anahtar biçimi Bildirim Tercihleri ekranıyla aynıdır: "match:123", "team:45",
    /// "league:7", "formax:system". Satır yoksa tercih AÇIK sayılır (takip edilen içerik
    /// bildirim gönderir). Bildirim üreten job'lar göndermeden önce buna bakar.
    /// </summary>
    public class UserNotificationPreference
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string PrefKey { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;

        public DateTime UpdatedAtUtc { get; set; }
    }
}
