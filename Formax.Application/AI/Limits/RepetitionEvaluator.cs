namespace Formax.Application.AI.Limits
{
    public sealed class RepetitionEvaluator
    {
        public bool IsRepetitionBlocked(
            string? lastExtendedContextKey,
            string? nextExtendedContextKey)
        {
            // İlk kez gösterim → tekrar yok
            if (string.IsNullOrEmpty(lastExtendedContextKey))
                return false;

            // Yeni bir bağlam yoksa → tekrar yok
            if (string.IsNullOrEmpty(nextExtendedContextKey))
                return false;

            // Aynı bağlam arka arkaya → engel
            return lastExtendedContextKey == nextExtendedContextKey;
        }
    }
}
