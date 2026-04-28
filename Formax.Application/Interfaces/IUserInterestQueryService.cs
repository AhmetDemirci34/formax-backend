using System.Collections.Generic;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IUserInterestQueryService
    {
        Task<IReadOnlyList<UserInterestDto>> GetTopAsync(
            int userId,
            string layer,
            int take);
    }

    public sealed class UserInterestDto
    {
        public string Key { get; set; } = null!;
        public int Score { get; set; }
    }
}
