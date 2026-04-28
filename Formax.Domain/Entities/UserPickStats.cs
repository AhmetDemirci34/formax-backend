using System;

namespace Formax.Domain.Entities;

public class UserPickStats
{
    public string UserId { get; set; } = "";

    public string PickLabel { get; set; } = "";

    public int Total { get; set; }

    public int Win { get; set; }

    public double Accuracy =>
        Total == 0 ? 0 : (double)Win / Total;
}