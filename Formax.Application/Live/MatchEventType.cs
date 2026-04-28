namespace Formax.Application.Live
{
    public enum MatchEventType
    {
        // Genel maç eventleri
        MatchStarted,
        HalfTime,
        MatchEnded,

        // Oyun içi kritik eventler
        Goal,

        // Penaltı süreci
        PenaltyAwarded,
        PenaltyVarReviewStarted,
        PenaltyConfirmed,
        PenaltyCancelled,

        // Kırmızı kart süreci
        RedCardAwarded,
        RedCardVarReviewStarted,
        RedCardConfirmed,
        RedCardCancelled
    }
}
