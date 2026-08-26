namespace Formax.Domain.Entities
{
    /// <summary>
    /// FORMAX GDP — DOĞRULANMIŞ resmi sosyal medya hesabı kaydı (SocialDiscoveryJob'un takip
    /// ettiği tek kaynak). Fan/anonim/dedikodu hesapları BURAYA GİRMEZ — yalnız verified resmi
    /// kulüp/federasyon/lig/turnuva/milli takım hesapları. Registry DB-backed + admin-seedable.
    ///
    /// Platform-genişletilebilir: yeni platform = yeni Platform değeri + ISocialProvider impl.
    /// </summary>
    public class OfficialSocialAccount
    {
        public int Id { get; set; }

        /// <summary>Kapsam: "Team" | "Competition" | "Federation".</summary>
        public string ScopeType { get; set; } = "Team";

        /// <summary>Team kapsamı için provider EXTERNAL takım id'si (Team.ExternalTeamId ile eşlenir). Null = takım-dışı.</summary>
        public string? ExternalTeamId { get; set; }

        /// <summary>Competition kapsamı için external lig id (Match.LeagueId ile eşlenir). 0 = yok.</summary>
        public int LeagueId { get; set; }

        /// <summary>"YouTube" | "X" | "Instagram" | "Facebook" | "RSS".</summary>
        public string Platform { get; set; } = "YouTube";

        /// <summary>Hesap kullanıcı adı/handle (ör. "liverpoolfc").</summary>
        public string Handle { get; set; } = string.Empty;

        /// <summary>Doğrudan feed URL (RSS/Atom). Boşsa provider handle'dan türetir.</summary>
        public string FeedUrl { get; set; } = string.Empty;

        /// <summary>Doğrulanmış görünen ad (feed metadata ile teyit edilir).</summary>
        public string AccountName { get; set; } = string.Empty;

        /// <summary>Yalnızca doğrulanmış resmi hesap true olur.</summary>
        public bool Verified { get; set; }

        /// <summary>Pasifse SocialDiscoveryJob atlar.</summary>
        public bool Active { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
