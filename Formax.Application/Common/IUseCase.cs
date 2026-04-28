using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Common
{
    public interface IUseCase<in TRequest, out TResponse>
    {
        TResponse Execute(TRequest request);
    }
}

