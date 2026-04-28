using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Services.AI
{
    public interface IAIService
    {
        Task<string> GenerateComment(string prompt);
    }
}
