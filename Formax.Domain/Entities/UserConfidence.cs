using System;

namespace Formax.Domain.Entities;

public class UserConfidence
{
    public string UserId { get; set; } = "";

    public int Value { get; set; } = 50;

    public DateTime UpdatedAt { get; set; }
}