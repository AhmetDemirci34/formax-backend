using System;

namespace Formax.Domain.States
{
    public enum AIContextKey
    {
        None = 0,

        // =========================
        // PRE-MATCH
        // =========================
        PreMatch = 1,              // ÜST BAĞLAM
        PreMatchSummary = 10,
        PreMatchExtended = 11,

        // =========================
        // LIVE MATCH
        // =========================
        LiveMatch = 2,             // ÜST BAĞLAM
        LiveMatchSummary = 20,
        LiveMatchExtended = 21,

        // =========================
        // POST-MATCH
        // =========================
        PostMatch = 3              // (ileride genişletilecek)
    }
}
