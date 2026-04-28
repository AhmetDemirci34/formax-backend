using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface ICouponItemWriteRepository
    {
        void Add(CouponItem item);
        void Update(CouponItem item);
    }
}

