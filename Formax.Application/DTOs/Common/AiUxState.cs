using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Common
{
    public enum AiUxState
    {
        Silent,             // AI konuşmuyor
        BasicContext,       // Temel bağlam
        ExtendedContext,    // Genişletilmiş bağlam
        Retracted           // AI kendini geri çekti
    }
}

