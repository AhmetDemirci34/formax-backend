using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Formax.Application.AI.LLM
{
    /// <summary>
    /// LLM ÇAĞRI SAYACI — her gerçek LLM çağrısı çağıranın kapsamıyla sayılır.
    ///
    /// Kapsam AsyncLocal'dır: HTTP isteği ara katmanı "request:{yol}", arka plan job'ları kendi
    /// adını açar. Kapsamı olmayan çağrı "unscoped" sayılır. Böylece "kullanıcı sayfası LLM
    /// çağırdı mı?" sorusu tahminle değil sayaçla cevaplanır. Süreç ömrü boyunca tutulur.
    /// </summary>
    public static class LlmCallMeter
    {
        private static readonly AsyncLocal<string?> Current = new();
        private static readonly ConcurrentDictionary<string, int> Counts = new(StringComparer.Ordinal);

        public static DateTime StartedAtUtc { get; } = DateTime.UtcNow;

        public static IDisposable Begin(string scope)
        {
            var previous = Current.Value;
            Current.Value = scope;
            return new Restore(previous);
        }

        public static string Scope => Current.Value ?? "unscoped";

        public static void Record() => Counts.AddOrUpdate(Scope, 1, (_, c) => c + 1);

        public static IReadOnlyDictionary<string, int> Snapshot()
            => Counts.OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Value);

        /// <summary>Kullanıcı isteği kapsamında yapılmış çağrıların toplamı.</summary>
        public static int RequestCalls => Counts.Where(k => k.Key.StartsWith("request:", StringComparison.Ordinal)).Sum(k => k.Value);

        private sealed class Restore : IDisposable
        {
            private readonly string? _previous;
            public Restore(string? previous) => _previous = previous;
            public void Dispose() => Current.Value = _previous;
        }
    }
}
