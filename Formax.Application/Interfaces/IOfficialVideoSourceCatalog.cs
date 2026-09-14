using System.Collections.Generic;
using Formax.Application.Services.PostMatch;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// ÇALIŞMA ZAMANI RESMÎ KAYNAK LİSTESİ — yayın hakkı tohumları + DB kataloğundaki DOĞRULANMIŞ kaynaklar.
    /// Keşif, kimlik doğrulayıcı ve kayıt kapısı bu listeyi kullanır; sayfa açılışı kullanmaz.
    /// </summary>
    public interface IOfficialVideoSourceCatalog
    {
        IReadOnlyList<OfficialVideoSource> Current();
    }
}
