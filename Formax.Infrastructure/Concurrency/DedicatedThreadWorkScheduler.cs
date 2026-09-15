using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;

namespace Formax.Infrastructure.Concurrency
{
    /// <summary>Kuyruk dolu ya da kapanıyor: iş KABUL EDİLMEDİ (sessizce düşürülmedi; çağıran 503 döner).</summary>
    public sealed class SchedulerSaturatedException : Exception
    {
        public SchedulerSaturatedException(string message) : base(message) { }
    }

    /// <summary>
    /// Sabit sayıda ÖZEL iş parçacığı + SINIRLI FIFO kuyruk. İş, kuyruğa alındığı andaki ExecutionContext ile
    /// çalışır: AsyncLocal kapsamları (LLM sayacı, API-Football çağrı kapsamı, DB istek istatistiği)
    /// korunur. İstek başlamadan iptal edilirse iş hiç çalışmaz.
    ///
    /// SINIRLI (15.09.2026): kuyruk kapasitesi doluysa iş reddedilir ve görev <see cref="SchedulerSaturatedException"/> ile
    /// biter — bekleyen istek sınırsız birikip belleği/gecikmeyi büyütmez, iş de SESSİZCE kaybolmaz (her iş ya çalışır ya
    /// iptal/ret sonucu döner). Kapanışta kuyrukta kalan işler iptal edilerek tamamlanır; iş parçacıkları beklenir.
    /// </summary>
    public sealed class DedicatedThreadWorkScheduler : IBlockingWorkScheduler, IDisposable
    {
        private sealed class WorkItem
        {
            public required Action Run { get; init; }
            public required Action Cancel { get; init; }
        }

        private readonly BlockingCollection<WorkItem> _queue;
        private readonly Thread[] _threads;
        private long _queued, _running, _completed, _maxQueueWaitTicks, _rejected, _peakQueued, _canceledOnShutdown;
        private int _disposed;

        public int WorkerCount => _threads.Length;
        public int Capacity { get; }
        public long Queued => Interlocked.Read(ref _queued);
        public long Running => Interlocked.Read(ref _running);
        public long Completed => Interlocked.Read(ref _completed);
        /// <summary>Kapasite dolu olduğu için reddedilen iş sayısı (doygunluk metriği).</summary>
        public long Rejected => Interlocked.Read(ref _rejected);
        public long PeakQueued => Interlocked.Read(ref _peakQueued);
        public long CanceledOnShutdown => Interlocked.Read(ref _canceledOnShutdown);
        /// <summary>0..1 — kuyruk doluluk oranı.</summary>
        public double Saturation => Capacity == 0 ? 0 : Math.Min(1.0, Queued / (double)Capacity);
        public double MaxQueueWaitMs => Interlocked.Read(ref _maxQueueWaitTicks) / (double)TimeSpan.TicksPerMillisecond;

        public DedicatedThreadWorkScheduler(int workers, string name = "formax-blocking", int capacity = 512)
        {
            Capacity = Math.Clamp(capacity, 1, 100_000);
            _queue = new BlockingCollection<WorkItem>(new ConcurrentQueue<WorkItem>(), Capacity);
            _threads = new Thread[Math.Clamp(workers, 1, 64)];
            for (var i = 0; i < _threads.Length; i++)
            {
                _threads[i] = new Thread(Work) { IsBackground = true, Name = $"{name}-{i}" };
                _threads[i].Start();
            }
        }

        public Task<T> RunAsync<T>(Func<T> work, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested) return Task.FromCanceled<T>(ct);
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var context = ExecutionContext.Capture();
            var enqueued = Stopwatch.GetTimestamp();

            var item = new WorkItem
            {
                Cancel = () => tcs.TrySetException(new SchedulerSaturatedException("zamanlayıcı kapandı; iş çalıştırılmadı")),
                Run = () =>
                {
                    Interlocked.Decrement(ref _queued);
                    var waitTicks = (Stopwatch.GetTimestamp() - enqueued) * TimeSpan.TicksPerSecond / Stopwatch.Frequency;
                    long cur;
                    while (waitTicks > (cur = Interlocked.Read(ref _maxQueueWaitTicks)))
                        if (Interlocked.CompareExchange(ref _maxQueueWaitTicks, waitTicks, cur) == cur) break;

                    if (ct.IsCancellationRequested) { tcs.TrySetCanceled(ct); return; }
                    Interlocked.Increment(ref _running);
                    try
                    {
                        void Invoke(object? _)
                        {
                            try { tcs.TrySetResult(work()); }
                            catch (OperationCanceledException oce) { tcs.TrySetCanceled(oce.CancellationToken); }
                            catch (Exception ex) { tcs.TrySetException(ex); }
                        }
                        if (context != null) ExecutionContext.Run(context, Invoke, null);
                        else Invoke(null);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _running);
                        Interlocked.Increment(ref _completed);
                    }
                }
            };

            var depth = Interlocked.Increment(ref _queued);
            bool added;
            try { added = _queue.TryAdd(item); }
            catch (InvalidOperationException) { added = false; }   // CompleteAdding sonrası (kapanış)
            if (!added)
            {
                Interlocked.Decrement(ref _queued);
                Interlocked.Increment(ref _rejected);
                return Task.FromException<T>(new SchedulerSaturatedException(
                    _queue.IsAddingCompleted ? "zamanlayıcı kapanıyor" : $"iş kuyruğu dolu (kapasite {Capacity})"));
            }
            long peak;
            while (depth > (peak = Interlocked.Read(ref _peakQueued)))
                if (Interlocked.CompareExchange(ref _peakQueued, depth, peak) == peak) break;
            return tcs.Task;
        }

        private void Work()
        {
            foreach (var item in _queue.GetConsumingEnumerable()) item.Run();
        }

        /// <summary>Düzgün kapanış: yeni iş alınmaz, kuyrukta kalanlar iptal sonucu ile biter, iş parçacıkları beklenir.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            _queue.CompleteAdding();
            while (_queue.TryTake(out var pending))
            {
                Interlocked.Decrement(ref _queued);
                Interlocked.Increment(ref _canceledOnShutdown);
                pending.Cancel();
            }
            foreach (var t in _threads) t.Join(TimeSpan.FromSeconds(5));
        }
    }
}
