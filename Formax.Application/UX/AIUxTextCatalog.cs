using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.UX
{
    public static class AIUxTextCatalog
    {
        public static class StateMessages
        {
            public const string Extended =
                "Bu maç için şu an konuşabileceğim anlamlı bir bağlam var.";

            public const string Short =
                "Bu maçla ilgili önemli noktaları zaten paylaştım.";

            public const string Silent =
                "Şu an eklenebilecek yeni ve güvenilir bir bağlam yok.";

            public const string SelfRetracted =
                "Mevcut veriler çelişkili olduğu için yorum yapmamayı tercih ediyorum.";
        }
    }
}

