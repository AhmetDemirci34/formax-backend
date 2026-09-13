using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Enums;

namespace Formax.Application.Interfaces
{
    /// <summary>Maç bildirimi isteği — tür, metin ve kullanıcı başına tekillik anahtarı.</summary>
    public sealed record MatchNotificationRequest(
        int MatchId,
        string NotificationType,
        string Title,
        string Message,
        NotificationEventType EventType,
        Func<int, string> IdempotencyKeyForUser,
        DateTime NowUtc,
        int? LeagueId = null);

    public sealed record MatchNotificationDispatchResult(
        int ActiveFollowers,
        int Created,
        int SkippedDuplicate,
        int SkippedByPreference);

    /// <summary>
    /// MAÇ BİLDİRİMİ DAĞITICI — mevcut <c>UserNotification</c> tablosuna ve mevcut
    /// <c>INotificationService</c> teslimine yazar; paralel bildirim sistemi DEĞİLDİR.
    ///
    /// Yalnız maçı HÂLÂ aktif takip eden kullanıcılara; kullanıcının "match:{id}" tercihi
    /// kapalıysa yazılmaz. Tekillik DB seviyesindedir (filtreli UNIQUE indeks): restart ya da
    /// iki job örneği aynı bildirimi ikinci kez yazamaz. Çağıran bunu ancak kendi veri
    /// işlemi (commit) BAŞARIYLA bittikten sonra çağırır.
    /// </summary>
    public interface IMatchNotificationDispatcher
    {
        Task<MatchNotificationDispatchResult> DispatchAsync(MatchNotificationRequest request, CancellationToken ct = default);
    }
}
