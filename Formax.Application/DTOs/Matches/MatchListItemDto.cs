using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Matches
{
    public sealed class MatchListItemDto
    {
        public int MatchId { get; init; }
        public string HomeTeam { get; init; } = string.Empty;
        public string AwayTeam { get; init; } = string.Empty;
        public string League { get; init; } = string.Empty;
        public DateTime StartTime { get; init; }

        public ScoreDto? Score { get; init; }
        public int? Minute { get; init; }
        public string Status { get; init; } = string.Empty;

        // --------------------------------------------------
        // FORMAX ANA REFERANS v1.1 — Sapma Motoru çıktıları
        // Not: UI oran/yüzde göstermez. Backend 0–100 hesaplar; UI metinle anlatır.
        // --------------------------------------------------
        public int? OynanmaSkoru { get; set; }          // 0–100
        public int? GucSkoru { get; set; }              // 0–100
        public int? Sapma { get; set; }                 // 0–100

        public string? OynanmaYonu { get; set; }        // Home / Away / Denge
        public string? GercekGucYonu { get; set; }      // Home / Away / Denge

        // Freshness (tazelik)
        public string? OynanmaFreshness { get; set; }   // Live / LastKnown / Stale
        public int? OynanmaAgeSeconds { get; set; }
        public bool? AnalysisMuted { get; set; }

        public string? SapmaBolgesi { get; set; }       // Denge Bölgesi / Yanılma Riski / Yüksek Sapma
        public bool? SessizMi { get; set; }             // Sapma < 60
        public string? SapmaMetni { get; set; }         // Dil kimliği metni

        [JsonIgnore]
        public int RankScore { get; set; }

        
    }
}
