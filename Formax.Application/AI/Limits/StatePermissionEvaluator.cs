using Formax.Application.States;

namespace Formax.Application.AI.Limits
{
    public sealed class StatePermissionEvaluator
    {
        public bool IsStateAllowedToSpeak(UserState userState)
        {
            // 🔒 Varsayılan: konuşma yok
            // FAZ-7 kapsamında SADECE kayıtlı ve aktif kullanıcı
            return userState switch
            {
                UserState.Registered => true,

                // Anonymous & Dormant → sessizlik
                _ => false
            };
        }
    }
}
