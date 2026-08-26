using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Logging
{
    /// <summary>
    /// Günlük döngülü DOSYA log sağlayıcısı — sıfır NuGet bağımlılığı.
    ///
    /// NEDEN VAR: konsol logu süreçle birlikte ölür. Sürekli çalışan bir sunucuda (Windows
    /// Service / systemd) konsol yoktur; "gece 03:00'teki cycle ne yaptı?" sorusunun cevabı
    /// ancak diskte kalıcı bir dosyada bulunur.
    ///
    /// NE YAPMAZ: hiçbir şeyi filtrelemez, zenginleştirmez, uzak bir yere göndermez. Yalnız
    /// ILogger boru hattının ürettiği satırı diske yazar. Gizli bilgi taşımaması, kaynaktaki
    /// log çağrılarının gizli bilgi içermemesiyle sağlanır (bkz. ShadowHealthState.Sanitize).
    /// </summary>
    public sealed class FileLoggerOptions
    {
        public bool Enabled { get; set; }

        /// <summary>Log klasörü. Göreli verilirse ContentRoot'a göre çözülür.</summary>
        public string Directory { get; set; } = "Logs";

        public string FilePrefix { get; set; } = "formax";

        /// <summary>Bu gün sayısından eski dosyalar açılışta silinir. 0 = hiç silme.</summary>
        public int RetainedDays { get; set; } = 30;
    }

    [ProviderAlias("File")]
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly FileLoggerOptions _options;
        private readonly string _directory;
        private readonly BlockingCollection<string> _queue = new(10_000);
        private readonly Thread _writer;
        private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
        private volatile bool _disposed;

        public FileLoggerProvider(FileLoggerOptions options, string contentRoot)
        {
            _options = options;
            _directory = Path.IsPathRooted(options.Directory)
                ? options.Directory
                : Path.Combine(contentRoot, options.Directory);

            System.IO.Directory.CreateDirectory(_directory);
            PurgeOldFiles();

            // Tek yazıcı iş parçacığı: istek yolundaki hiçbir çağrı disk I/O beklemez.
            _writer = new Thread(WriteLoop)
            {
                IsBackground = true,
                Name = "formax-file-logger"
            };
            _writer.Start();
        }

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

        internal void Enqueue(string line)
        {
            if (_disposed) return;
            // Kuyruk dolduysa log satırı DÜŞÜRÜLÜR. Uygulama loglama yüzünden yavaşlamaz.
            _queue.TryAdd(line);
        }

        private void WriteLoop()
        {
            var buffer = new StringBuilder();
            foreach (var line in _queue.GetConsumingEnumerable())
            {
                buffer.Clear();
                buffer.AppendLine(line);

                // Kuyrukta bekleyen varsa aynı açılışta yaz (tek dosya açma, çok satır).
                while (_queue.TryTake(out var more)) buffer.AppendLine(more);

                try
                {
                    File.AppendAllText(CurrentFilePath(), buffer.ToString(), Encoding.UTF8);
                }
                catch
                {
                    // Log yazılamaması uygulamayı düşürmez. Konsol sağlayıcısı hâlâ ayakta.
                }
            }
        }

        private string CurrentFilePath() =>
            Path.Combine(_directory,
                $"{_options.FilePrefix}-{DateTime.UtcNow:yyyyMMdd}.log");

        private void PurgeOldFiles()
        {
            if (_options.RetainedDays <= 0) return;
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-_options.RetainedDays);
                foreach (var file in System.IO.Directory.GetFiles(_directory, _options.FilePrefix + "-*.log"))
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
                }
            }
            catch
            {
                // Temizlik başarısızlığı loglamayı durdurmaz.
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _queue.CompleteAdding();
            // Kapanışta bekleyen satırların diske inmesi için kısa bir pencere.
            _writer.Join(TimeSpan.FromSeconds(5));
            _queue.Dispose();
        }

        private sealed class FileLogger : ILogger
        {
            private readonly string _category;
            private readonly FileLoggerProvider _provider;

            public FileLogger(string category, FileLoggerProvider provider)
            {
                _category = category;
                _provider = provider;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;

                var message = formatter(state, exception);
                if (string.IsNullOrEmpty(message) && exception is null) return;

                var line = string.Create(CultureInfo.InvariantCulture,
                    $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}Z [{Short(logLevel)}] {_category} — {message}");

                if (exception is not null)
                    line += Environment.NewLine + exception;

                _provider.Enqueue(line);
            }

            private static string Short(LogLevel level) => level switch
            {
                LogLevel.Trace => "TRC",
                LogLevel.Debug => "DBG",
                LogLevel.Information => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "CRT",
                _ => "???"
            };
        }
    }
}
