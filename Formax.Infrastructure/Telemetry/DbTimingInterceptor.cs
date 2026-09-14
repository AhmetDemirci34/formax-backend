using System;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Telemetry
{
    /// <summary>
    /// Bir HTTP isteğinin DB kullanımı — <see cref="DbRequestScope"/> ile başlatılır, EF interceptor'ı doldurur.
    /// AsyncLocal üzerinden taşınır; senkron ve asenkron EF çağrılarının ikisini de görür.
    /// </summary>
    public sealed class DbRequestStats
    {
        private long _commands, _commandTicks, _maxCommandTicks, _connectionOpens, _connectionWaitTicks, _maxConnectionWaitTicks;
        private string? _slowestSql;

        public string CorrelationId { get; }
        public DbRequestStats(string correlationId) => CorrelationId = correlationId;

        public long Commands => Interlocked.Read(ref _commands);
        public double CommandMs => Interlocked.Read(ref _commandTicks) / (double)TimeSpan.TicksPerMillisecond;
        public double MaxCommandMs => Interlocked.Read(ref _maxCommandTicks) / (double)TimeSpan.TicksPerMillisecond;
        public long ConnectionOpens => Interlocked.Read(ref _connectionOpens);
        public double ConnectionWaitMs => Interlocked.Read(ref _connectionWaitTicks) / (double)TimeSpan.TicksPerMillisecond;
        public double MaxConnectionWaitMs => Interlocked.Read(ref _maxConnectionWaitTicks) / (double)TimeSpan.TicksPerMillisecond;
        public string? SlowestSql => _slowestSql;

        internal void AddCommand(TimeSpan duration, string sql)
        {
            Interlocked.Increment(ref _commands);
            Interlocked.Add(ref _commandTicks, duration.Ticks);
            long current;
            while (duration.Ticks > (current = Interlocked.Read(ref _maxCommandTicks)))
            {
                if (Interlocked.CompareExchange(ref _maxCommandTicks, duration.Ticks, current) == current)
                {
                    _slowestSql = sql.Length <= 160 ? sql : sql[..160];
                    break;
                }
            }
        }

        internal void AddConnectionOpen(TimeSpan wait)
        {
            Interlocked.Increment(ref _connectionOpens);
            Interlocked.Add(ref _connectionWaitTicks, wait.Ticks);
            long current;
            while (wait.Ticks > (current = Interlocked.Read(ref _maxConnectionWaitTicks)))
                if (Interlocked.CompareExchange(ref _maxConnectionWaitTicks, wait.Ticks, current) == current) break;
        }
    }

    /// <summary>Geçerli isteğin DB istatistiği — isteğin dışında null.</summary>
    public static class DbRequestScope
    {
        private static readonly AsyncLocal<DbRequestStats?> Current = new();
        public static DbRequestStats? Stats => Current.Value;
        public static DbRequestStats Begin(string correlationId) => Current.Value = new DbRequestStats(correlationId);
        public static void End() => Current.Value = null;
    }

    /// <summary>
    /// EF CORE ZAMANLAYICISI — bağlantı açma beklemesi, komut süresi ve açık bağlantı/okuyucu sayısı.
    ///
    /// NEDEN: 14.09.2026'da /detail 10–30 sn takıldı; SQL Server aynı sorguları ≤423 ms'de bitirdiğini
    /// söylerken EF 12–25 sn ölçüyordu. Gecikmenin bağlantı havuzunda mı, komutta mı yoksa süreç
    /// içinde mi olduğunu ayırmak için her aşama ayrı ölçülür. Tam SQL metni artık her komutta
    /// loglanmaz; yalnız eşik üstü komut Warning olarak yazılır.
    /// </summary>
    public sealed class DbTimingInterceptor : DbCommandInterceptor, IDbConnectionInterceptor
    {
        public static readonly TimeSpan SlowCommandThreshold = TimeSpan.FromMilliseconds(1000);

        private static long _openConnections, _openReaders, _commandsTotal, _slowCommands;
        public static long OpenConnections => Interlocked.Read(ref _openConnections);
        public static long OpenReaders => Interlocked.Read(ref _openReaders);
        public static long CommandsTotal => Interlocked.Read(ref _commandsTotal);
        public static long SlowCommands => Interlocked.Read(ref _slowCommands);

        private readonly ILogger<DbTimingInterceptor> _log;
        public DbTimingInterceptor(ILogger<DbTimingInterceptor> log) => _log = log;

        // ── Bağlantı ────────────────────────────────────────────────────────────
        private static readonly ConditionalWeakTableStopwatch OpenTimers = new();

        public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        { OpenTimers.Start(connection); return result; }

        public ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        { OpenTimers.Start(connection); return ValueTask.FromResult(result); }

        public void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Opened(connection);

        public Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        { Opened(connection); return Task.CompletedTask; }

        public void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData) => Interlocked.Decrement(ref _openConnections);

        public Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
        { Interlocked.Decrement(ref _openConnections); return Task.CompletedTask; }

        public void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData) => OpenTimers.Stop(connection);

        public Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken = default)
        { OpenTimers.Stop(connection); return Task.CompletedTask; }

        private void Opened(DbConnection connection)
        {
            Interlocked.Increment(ref _openConnections);
            var wait = OpenTimers.Stop(connection);
            DbRequestScope.Stats?.AddConnectionOpen(wait);
            if (wait > SlowCommandThreshold)
                _log.LogWarning("[DB-SLOW] cid={Cid} baglanti acma {Ms} ms (acik baglanti={Open})",
                    DbRequestScope.Stats?.CorrelationId ?? "-", (int)wait.TotalMilliseconds, OpenConnections);
        }

        // ── Komut ───────────────────────────────────────────────────────────────
        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        { Executed(command, eventData.Duration); Interlocked.Increment(ref _openReaders); return result; }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        { Executed(command, eventData.Duration); Interlocked.Increment(ref _openReaders); return ValueTask.FromResult(result); }

        public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
        { Executed(command, eventData.Duration); return result; }

        public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
        { Executed(command, eventData.Duration); return ValueTask.FromResult(result); }

        public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        { Executed(command, eventData.Duration); return result; }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        { Executed(command, eventData.Duration); return ValueTask.FromResult(result); }

        public override InterceptionResult DataReaderClosing(DbCommand command, DataReaderClosingEventData eventData, InterceptionResult result)
        { Interlocked.Decrement(ref _openReaders); return result; }

        public override ValueTask<InterceptionResult> DataReaderClosingAsync(DbCommand command, DataReaderClosingEventData eventData, InterceptionResult result)
        { Interlocked.Decrement(ref _openReaders); return ValueTask.FromResult(result); }

        private void Executed(DbCommand command, TimeSpan duration)
        {
            Interlocked.Increment(ref _commandsTotal);
            var sql = command.CommandText ?? string.Empty;
            DbRequestScope.Stats?.AddCommand(duration, sql);
            if (duration <= SlowCommandThreshold) return;
            Interlocked.Increment(ref _slowCommands);
            _log.LogWarning("[DB-SLOW] cid={Cid} komut {Ms} ms: {Sql}",
                DbRequestScope.Stats?.CorrelationId ?? "-", (int)duration.TotalMilliseconds,
                sql.Length <= 240 ? sql.Replace('\n', ' ') : sql[..240].Replace('\n', ' '));
        }

        /// <summary>Bağlantı başına açma kronometresi (bağlantı nesnesine iliştirilir, sızıntı yapmaz).</summary>
        private sealed class ConditionalWeakTableStopwatch
        {
            private readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbConnection, Stopwatch> _table = new();
            public void Start(DbConnection c) { _table.Remove(c); _table.Add(c, Stopwatch.StartNew()); }
            public TimeSpan Stop(DbConnection c)
            {
                if (!_table.TryGetValue(c, out var sw)) return TimeSpan.Zero;
                _table.Remove(c);
                return sw.Elapsed;
            }
        }
    }
}
