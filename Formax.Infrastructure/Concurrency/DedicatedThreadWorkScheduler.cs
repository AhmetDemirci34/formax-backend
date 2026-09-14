using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;

namespace Formax.Infrastructure.Concurrency
{
    /// <summary>
    /// Sabit sayıda ÖZEL iş parçacığı + FIFO kuyruk. İş, kuyruğa alındığı andaki ExecutionContext ile
    /// çalışır: AsyncLocal kapsamları (LLM sayacı, API-Football çağrı kapsamı, DB istek istatistiği)
    /// korunur. İstek başlamadan iptal edilirse iş hiç çalışmaz.
    /// </summary>
    public sealed class DedicatedThreadWorkScheduler : IBlockingWorkScheduler, IDisposable
    {
        private readonly BlockingCollection<Action> _queue = new();
        private readonly Thread[] _threads;
        private long _queued, _running, _completed, _maxQueueWaitTicks;

        public int WorkerCount => _threads.Length;
        public long Queued => Interlocked.Read(ref _queued);
        public long Running => Interlocked.Read(ref _running);
        public long Completed => Interlocked.Read(ref _completed);
        public double MaxQueueWaitMs => Interlocked.Read(ref _maxQueueWaitTicks) / (double)TimeSpan.TicksPerMillisecond;

        public DedicatedThreadWorkScheduler(int workers, string name = "formax-blocking")
        {
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
            Interlocked.Increment(ref _queued);

            _queue.Add(() =>
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
            });
            return tcs.Task;
        }

        private void Work()
        {
            foreach (var item in _queue.GetConsumingEnumerable()) item();
        }

        public void Dispose() => _queue.CompleteAdding();
    }
}
