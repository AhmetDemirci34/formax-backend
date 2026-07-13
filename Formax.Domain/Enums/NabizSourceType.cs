namespace Formax.Domain.Enums;

/// <summary>
/// Classifies the origin and editorial intent of a NABIZ feed item.
/// Used by the frontend to render the correct chip / icon per item type.
/// </summary>
public enum NabizSourceType
{
    Flash    = 0,   // Son dakika / breaking news
    Yorum    = 1,   // Commentary / analyst opinion
    Roportaj = 2,   // Press conference / interview
    Official = 3,   // Club or federation official statement
    Trend    = 4,   // Trending topic / viral signal
    News     = 5    // General sports journalism
}
