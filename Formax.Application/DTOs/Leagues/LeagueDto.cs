namespace Formax.Application.DTOs.Leagues
{
    public class LeagueDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        // NOT: Projede lig logosu kaynağı yok → null (uydurulmaz).
        public string? LogoUrl { get; set; }
    }
}
