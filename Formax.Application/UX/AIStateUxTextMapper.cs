using Formax.Application.States;
using Formax.Domain.States;

namespace Formax.Application.UX
{
    public static class AIStateUxTextMapper
    {
        public static string GetMainMessage(AIStateResult result)
        {
            return result.State switch
            {
                AIUxState.Extended =>
                    AIUxTextCatalog.StateMessages.Extended,

                AIUxState.Short =>
                    AIUxTextCatalog.StateMessages.Short,

                AIUxState.Silent =>
                    "Şu anda bu maç için ek bir AI yorumu üretilemiyor.",

                AIUxState.SelfRetracted =>
                    AIUxTextCatalog.StateMessages.SelfRetracted,

                _ =>
                    "Şu an bu maç için yorum üretemiyorum."
            };
        }
    }
}
