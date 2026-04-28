using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Formax.Domain.Entities;

public class UserWeightProfile
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] // 🔥 KRİTİK
    public int UserId { get; set; }

    public double AffinityWeight { get; set; } = 0.3;
    public double BanditWeight { get; set; } = 0.8;
    public double ExplorationWeight { get; set; } = 0.5;
    public double InterestWeight { get; set; } = 1.2;

    public DateTime UpdatedAtUtc { get; set; }
}