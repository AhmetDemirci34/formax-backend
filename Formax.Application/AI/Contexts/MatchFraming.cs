namespace Formax.Application.AI.Contexts
{
    /// <summary>
    /// GDP fikstür verisinden TÜRETİLEN maç çerçevesi (framing).
    /// Ham veri değil, mevcut alanlardan (lig adı + tur etiketi) hesaplanan sinyaldir.
    /// Kaynak alan yoksa graceful default döner (League / Low / false).
    /// IsDerby burada YOKtur: GDP'nin mevcut kanonik verisinde takım-şehri/rakiplik
    /// bulunmadığından türetilemez (bkz. kapanış raporu, bilinçli kapsam dışı).
    /// </summary>
    public sealed record MatchFraming
    {
        public CompetitionType CompetitionType { get; init; } = CompetitionType.League;
        public ImportanceLevel ImportanceLevel { get; init; } = ImportanceLevel.Low;
        public bool IsElimination { get; init; }
        public bool IsFinal { get; init; }
    }
}
