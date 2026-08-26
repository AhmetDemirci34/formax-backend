using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Son Dakika haber çevirilerinin kalıcı önbelleği. Anahtar (ContentHash, Language) —
    /// haber deposunun zaten kullandığı tekilleştirme anahtarı; yeni kimlik uydurulmaz.
    /// Aynı haber ikinci kez ASLA çevrilmez (LLM çağrısı ücretli ve yavaştır).
    /// </summary>
    public interface IMatchNewsTranslationRepository
    {
        /// <summary>Verilen haberlerin hedef dildeki mevcut çevirileri (ContentHash → çeviri).</summary>
        Task<Dictionary<string, MatchNewsTranslation>> GetAsync(
            IEnumerable<string> contentHashes, string language, CancellationToken ct = default);

        /// <summary>Yeni çevirileri yazar; (ContentHash, Language) zaten varsa atlar.</summary>
        Task<int> UpsertAsync(IEnumerable<MatchNewsTranslation> translations, CancellationToken ct = default);
    }
}
