using Formax.Domain.Entities;

public interface IUserPickRepository
{
    Task<List<UserPick>> GetByMatchId(int matchId);
    Task Update(UserPick pick);
    Task Add(UserPick pick);
    Task SaveChanges();

    // ── SEÇİLEBİLİR OLASI SONUÇLAR (06.09.2026 · additive) ────────────────────
    //
    // PARALEL BİR TAHMİN SİSTEMİ YOK: aynı UserPicks tablosu, aynı repository.
    // Yalnız kullanıcı bazlı okuma ve silme eklendi.

    /// <summary>Bir kullanıcının bir maçtaki tüm seçimleri.</summary>
    Task<List<UserPick>> GetByUserAndMatchAsync(string userId, int matchId, CancellationToken ct = default);

    /// <summary>Bir kullanıcının tüm seçimleri (Tahminlerim ekranı).</summary>
    Task<List<UserPick>> GetByUserAsync(string userId, CancellationToken ct = default);

    /// <summary>Tek bir seçimi kaldırır (kullanıcı aynı satıra tekrar bastığında).</summary>
    Task RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>Verilen seçimleri toplu kaldırır (aynı gruptaki çelişkili seçim).</summary>
    Task RemoveRangeAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
