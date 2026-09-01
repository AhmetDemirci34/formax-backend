using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Http
{
    /// <summary>
    /// L2 — KALICI api-football yanıt deposu (restart-safe).
    ///
    /// NEDEN AYRI VE HAM SQL: bu bir ÖNBELLEKTİR, alan modeli değildir. FormaxDbContext'e entity
    /// olarak eklenseydi EF model snapshot'ı migration geçmişinden ayrılırdı (bu depoda daha önce
    /// yaşanmış bir tuzak). Tablo açılışta idempotent CREATE ile garanti edilir → MIGRATION YOK,
    /// snapshot'a dokunulmaz, alan tabloları etkilenmez.
    ///
    /// Yalnız BAŞARILI ve gövde-hatası OLMAYAN GET yanıtları yazılır (kota/plan hatası asla
    /// cache'lenmez). Anahtar: endpointFamily + normalize edilmiş sorgu parametreleri.
    /// </summary>
    public sealed class ApiFootballHttpCacheStore
    {
        public const string TableName = "ApiFootballHttpCache";
        public const string UsageTableName = "ApiFootballDailyUsage";

        private readonly string _connectionString;
        private readonly ILogger<ApiFootballHttpCacheStore> _logger;
        private int _ensured;

        public ApiFootballHttpCacheStore(IConfiguration config, ILogger<ApiFootballHttpCacheStore> logger)
            : this(config?["ConnectionStrings:FormaxDB"] ?? string.Empty, logger) { }

        public ApiFootballHttpCacheStore(string connectionString, ILogger<ApiFootballHttpCacheStore> logger)
        {
            _connectionString = connectionString ?? string.Empty;
            _logger = logger;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

        private async Task<SqlConnection?> OpenAsync(CancellationToken ct)
        {
            if (!IsConfigured) return null;
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);
            if (Interlocked.CompareExchange(ref _ensured, 1, 0) == 0)
            {
                try { await EnsureSchemaAsync(conn, ct).ConfigureAwait(false); }
                catch (Exception ex) { Interlocked.Exchange(ref _ensured, 0); _logger.LogWarning(ex, "[AF-CACHE] tablo hazırlanamadı"); }
            }
            return conn;
        }

        private static async Task EnsureSchemaAsync(SqlConnection conn, CancellationToken ct)
        {
            const string ddl = @"
IF OBJECT_ID('dbo.ApiFootballHttpCache','U') IS NULL
BEGIN
    CREATE TABLE dbo.ApiFootballHttpCache (
        CacheKey            nvarchar(400)  NOT NULL PRIMARY KEY,
        EndpointFamily      nvarchar(64)   NOT NULL,
        NormalizedKey       nvarchar(400)  NOT NULL,
        Payload             nvarchar(max)  NOT NULL,
        CreatedUtc          datetime2      NOT NULL,
        ExpiresUtc          datetime2      NOT NULL,
        ProviderResultCount int            NOT NULL
    );
    CREATE INDEX IX_ApiFootballHttpCache_Expires ON dbo.ApiFootballHttpCache(ExpiresUtc);
END;
IF OBJECT_ID('dbo.ApiFootballDailyUsage','U') IS NULL
BEGIN
    CREATE TABLE dbo.ApiFootballDailyUsage (
        DayUtc       date NOT NULL PRIMARY KEY,
        RealRequests int  NOT NULL
    );
END;";
            using var cmd = new SqlCommand(ddl, conn) { CommandType = CommandType.Text };
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        /// <summary>Taze kayıt varsa gövdeyi döndürür; yoksa null. Süresi geçmiş kayıt yok sayılır.</summary>
        public async Task<string?> TryGetAsync(string cacheKey, CancellationToken ct)
        {
            try
            {
                await using var conn = await OpenAsync(ct).ConfigureAwait(false);
                if (conn == null) return null;
                using var cmd = new SqlCommand(
                    $"SELECT Payload FROM dbo.{TableName} WHERE CacheKey=@k AND ExpiresUtc > SYSUTCDATETIME();", conn);
                cmd.Parameters.AddWithValue("@k", cacheKey);
                var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                return result as string;
            }
            catch (Exception ex)
            {
                // Cache ARIZASI çağrıyı engellemez — yalnız kazanç kaybedilir.
                _logger.LogWarning(ex, "[AF-CACHE] okuma başarısız ({Key})", cacheKey);
                return null;
            }
        }

        public async Task SetAsync(
            string cacheKey, string endpointFamily, string normalizedKey,
            string payload, DateTime expiresUtc, int providerResultCount, CancellationToken ct)
        {
            try
            {
                await using var conn = await OpenAsync(ct).ConfigureAwait(false);
                if (conn == null) return;
                using var cmd = new SqlCommand($@"
MERGE dbo.{TableName} AS t
USING (SELECT @k AS CacheKey) AS s ON t.CacheKey = s.CacheKey
WHEN MATCHED THEN UPDATE SET Payload=@p, CreatedUtc=SYSUTCDATETIME(), ExpiresUtc=@e,
                             ProviderResultCount=@c, EndpointFamily=@f, NormalizedKey=@n
WHEN NOT MATCHED THEN INSERT (CacheKey, EndpointFamily, NormalizedKey, Payload, CreatedUtc, ExpiresUtc, ProviderResultCount)
                      VALUES (@k, @f, @n, @p, SYSUTCDATETIME(), @e, @c);", conn);
                cmd.Parameters.AddWithValue("@k", cacheKey);
                cmd.Parameters.AddWithValue("@f", endpointFamily);
                cmd.Parameters.AddWithValue("@n", normalizedKey);
                cmd.Parameters.AddWithValue("@p", payload);
                cmd.Parameters.AddWithValue("@e", expiresUtc);
                cmd.Parameters.AddWithValue("@c", providerResultCount);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AF-CACHE] yazma başarısız ({Key})", cacheKey);
            }
        }

        /// <summary>Bugünkü gerçek istek sayacını 1 artırır ve YENİ değeri döndürür (-1 = ölçülemedi).</summary>
        public async Task<int> IncrementDailyUsageAsync(CancellationToken ct)
        {
            try
            {
                await using var conn = await OpenAsync(ct).ConfigureAwait(false);
                if (conn == null) return -1;
                using var cmd = new SqlCommand($@"
MERGE dbo.{UsageTableName} AS t
USING (SELECT CAST(SYSUTCDATETIME() AS date) AS DayUtc) AS s ON t.DayUtc = s.DayUtc
WHEN MATCHED THEN UPDATE SET RealRequests = t.RealRequests + 1
WHEN NOT MATCHED THEN INSERT (DayUtc, RealRequests) VALUES (s.DayUtc, 1);
SELECT RealRequests FROM dbo.{UsageTableName} WHERE DayUtc = CAST(SYSUTCDATETIME() AS date);", conn);
                var v = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                return v is int i ? i : Convert.ToInt32(v ?? -1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AF-CACHE] günlük sayaç artırılamadı");
                return -1;
            }
        }

        /// <summary>Bugünkü gerçek istek sayısı (yazmadan okur). -1 = ölçülemedi.</summary>
        public async Task<int> GetDailyUsageAsync(CancellationToken ct)
        {
            try
            {
                await using var conn = await OpenAsync(ct).ConfigureAwait(false);
                if (conn == null) return -1;
                using var cmd = new SqlCommand(
                    $"SELECT RealRequests FROM dbo.{UsageTableName} WHERE DayUtc = CAST(SYSUTCDATETIME() AS date);", conn);
                var v = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AF-CACHE] günlük sayaç okunamadı");
                return -1;
            }
        }
    }
}
