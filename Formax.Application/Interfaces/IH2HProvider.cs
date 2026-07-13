using Formax.Application.DTOs.Matches;

namespace Formax.Application.Interfaces;

public interface IH2HProvider
{
    Task<H2HDto?> GetAsync(int homeAfId, int awayAfId);
}
