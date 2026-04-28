using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Discovery;

public class DiscoverySessionMemoryService
{
    // thread-safe
    private readonly ConcurrentDictionary<int, List<int>> _userRecentMatches = new();

    private const int MaxSize = 10;

    // 🔥 TRACK
    public void TrackInteraction(int userId, int matchId)
    {
        var list = _userRecentMatches.GetOrAdd(userId, _ => new List<int>());

        lock (list)
        {
            list.Remove(matchId);
            list.Insert(0, matchId);

            if (list.Count > MaxSize)
                list.RemoveAt(list.Count - 1);
        }
    }

    // 🔁 BACKWARD
    public void MarkViewed(int userId, int matchId)
    {
        TrackInteraction(userId, matchId);
    }

    // 🔁 BACKWARD
    public bool HasSeen(int userId, int matchId)
    {
        if (!_userRecentMatches.TryGetValue(userId, out var list))
            return false;

        lock (list)
        {
            return list.Contains(matchId);
        }
    }

    // 🎯 SCORE (normalize edildi)
    public double GetScore(int userId, int matchId)
    {
        if (!_userRecentMatches.TryGetValue(userId, out var list))
            return 0;

        lock (list)
        {
            var index = list.IndexOf(matchId);

            if (index == -1)
                return 0;

            // 0–1 arası normalize (en yeni = 1.0)
            return 1.0 - (index / (double)MaxSize);
        }
    }

    // 🧹 CLEAR
    public void ClearSession(int userId)
    {
        _userRecentMatches.TryRemove(userId, out _);
    }
}