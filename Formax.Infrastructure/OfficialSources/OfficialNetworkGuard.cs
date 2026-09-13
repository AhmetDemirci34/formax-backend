using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.OfficialSources
{
    /// <summary>Host adını IP adreslerine çözer (testte değiştirilebilir).</summary>
    public interface IOfficialAddressResolver
    {
        Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct);
    }

    public sealed class DnsOfficialAddressResolver : IOfficialAddressResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
            => Dns.GetHostAddressesAsync(host, ct);
    }

    /// <summary>
    /// AĞ KORUMASI — resmî kaynak isteği yalnız herkese açık internet adresine gidebilir.
    /// Yerel/özel/bağlantı-yerel/CGNAT/çok-noktaya-yayın adresleri reddedilir (SSRF ve DNS
    /// yeniden bağlama koruması). Üretimde bağlantı kurulurken de aynı kontrol yapılır
    /// (<see cref="ConnectGuardedAsync"/>): DNS kontrolü ile bağlantı arasında adres değişemez.
    /// </summary>
    public static class OfficialNetworkGuard
    {
        public static bool IsPublic(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

            if (IPAddress.IsLoopback(address)) return false;

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();
                if (b[0] == 0) return false;                                   // 0.0.0.0/8
                if (b[0] == 10) return false;                                  // 10/8
                if (b[0] == 127) return false;                                 // 127/8
                if (b[0] == 169 && b[1] == 254) return false;                  // link-local
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false;     // 172.16/12
                if (b[0] == 192 && b[1] == 168) return false;                  // 192.168/16
                if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return false;    // CGNAT 100.64/10
                if (b[0] == 192 && b[1] == 0 && b[2] == 0) return false;       // 192.0.0/24
                if (b[0] >= 224) return false;                                 // multicast + ayrılmış
                return true;
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Any)) return false;
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return false;
                var b = address.GetAddressBytes();
                if ((b[0] & 0xFE) == 0xFC) return false;                       // fc00::/7 ULA
                return true;
            }

            return false;
        }

        /// <summary>
        /// Üretim bağlantı geri çağrısı: adresleri çözer, özel olanları eler, ilk herkese açık
        /// adrese bağlanır. Hiç herkese açık adres yoksa bağlantı KURULMAZ.
        /// </summary>
        public static async ValueTask<System.IO.Stream> ConnectGuardedAsync(
            System.Net.Http.SocketsHttpConnectionContext context, CancellationToken ct)
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct).ConfigureAwait(false);
            var allowed = addresses.Where(IsPublic).ToArray();
            if (allowed.Length == 0 || allowed.Length != addresses.Length)
                throw new OfficialPrivateAddressException(context.DnsEndPoint.Host);

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, ct).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }

    public sealed class OfficialPrivateAddressException : Exception
    {
        public OfficialPrivateAddressException(string host)
            : base($"'{host}' özel/yerel bir adrese çözülüyor; resmî kaynak isteği reddedildi.") { }
    }

    /// <summary>
    /// HOST BAŞINA HIZ SINIRI — aynı host'a iki istek arasında en az <c>minInterval</c>;
    /// aynı host'a eşzamanlı tek istek. Süreç içinde tekildir (singleton). Restart sonrası
    /// ilk istekte son ağ isteği anı defterden okunur.
    /// </summary>
    public sealed class OfficialHostRateLimiter
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _last = new(StringComparer.OrdinalIgnoreCase);

        public TimeSpan MinInterval { get; }

        public OfficialHostRateLimiter(TimeSpan? minInterval = null)
            => MinInterval = minInterval ?? TimeSpan.FromMilliseconds(1500);

        public async Task<IDisposable> AcquireAsync(
            string host, Func<Task<DateTime?>> persistedLast, Func<DateTime> utcNow, CancellationToken ct)
        {
            var gate = _gates.GetOrAdd(host, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!_last.TryGetValue(host, out var last))
                {
                    var p = await persistedLast().ConfigureAwait(false);
                    last = p ?? DateTime.MinValue;
                }
                var wait = last + MinInterval - utcNow();
                if (wait > TimeSpan.Zero && wait <= MinInterval)
                    await Task.Delay(wait, ct).ConfigureAwait(false);
                _last[host] = utcNow();
                return new Release(gate);
            }
            catch
            {
                gate.Release();
                throw;
            }
        }

        private sealed class Release : IDisposable
        {
            private SemaphoreSlim? _gate;
            public Release(SemaphoreSlim gate) => _gate = gate;
            public void Dispose() { Interlocked.Exchange(ref _gate, null)?.Release(); }
        }
    }
}
