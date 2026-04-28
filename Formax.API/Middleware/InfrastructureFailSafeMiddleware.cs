using System;
using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Formax.API.Middleware
{
    public sealed class InfrastructureFailSafeMiddleware
    {
        private readonly RequestDelegate _next;

        public InfrastructureFailSafeMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex) when (
                ex is SqlException ||
                ex is DbUpdateException ||
                ex.InnerException is SqlException
            )
            {
                if (!ShouldHandle(context))
                    throw;

                // 🔒 Kullanıcıya sızabilecek her şeyi burada kesiyoruz
                context.Response.Clear();
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";

                var safeResponse = new
                {
                    warning = "Şu anda geçici bir teknik sorun nedeniyle analiz üretilemiyor."
                };

                var json = JsonSerializer.Serialize(safeResponse);
                await context.Response.WriteAsync(json);
            }
        }

        private static bool ShouldHandle(HttpContext context)
        {
            var path = context?.Request?.Path.Value ?? string.Empty;

            // Only AI/LLM narrative style endpoints are masked.
            // Normal CRUD and listing endpoints must surface errors during development.
            return path.StartsWith("/api/ai", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/fai", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/aianalysis", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/ai-analysis", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/worldexpectation", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/home/ai-vitrine", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/home/narrative", StringComparison.OrdinalIgnoreCase);
        }
    }
}
