namespace Formax.Application.DTOs.Common;

public class TeamMediaDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? LogoUrl { get; set; }
    public string? ColorPrimary { get; set; }
    public string? ColorSecondary { get; set; }
    public PlayerDto? StarPlayer { get; set; }
}