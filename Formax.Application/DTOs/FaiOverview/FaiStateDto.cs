using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.FaiOverview;

public class FaiStateDto
{
    public string State { get; set; } = "SILENT"; // SILENT | ACTIVE | LOCKED
}