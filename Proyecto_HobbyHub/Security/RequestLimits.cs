using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Proyecto_HobbyHub.Security;

public static class RequestLimits
{
    public static string Partition(HttpContext context, bool ipOnly = false) =>
        !ipOnly && context.User.Identity?.IsAuthenticated == true &&
        int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
            ? $"user:{id}" : $"ip:{context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown"}";

    public static void Configure(RateLimiterOptions options, IConfiguration configuration)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        Add("SocialWrites", 60, 60);
        Add("AccountRecovery", 5, 900);
        Add(SecurityPolicies.LoginAttempts, 10, 60, true);
        Add("Registration", 5, 900, true);
        Add("Queries", 120, 60);
        Add("Searches", 30, 60);

        void Add(string name, int permits, int seconds, bool ipOnly = false)
        {
            permits = configuration.GetValue<int?>($"Security:RateLimits:{name}:PermitLimit") ?? permits;
            seconds = configuration.GetValue<int?>($"Security:RateLimits:{name}:WindowSeconds") ?? seconds;
            if (permits is < 1 or > 10000 || seconds is < 1 or > 86400)
                throw new InvalidOperationException($"Configuración inválida del límite {name}.");
            options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
                Partition(context, ipOnly), _ => new FixedWindowRateLimiterOptions
                { PermitLimit = permits, Window = TimeSpan.FromSeconds(seconds), QueueLimit = 0, AutoReplenishment = true }));
        }

        options.OnRejected = async (rejected, cancellationToken) =>
        {
            var context = rejected.HttpContext;
            if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                context.Response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retry.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.RequestServices.GetRequiredService<RequestSecurityLog>().Rejected(context, "RateLimitRejected");
            await context.Response.WriteAsync("Demasiadas solicitudes. Espera el tiempo indicado y vuelve a intentarlo.", cancellationToken);
        };
    }
}
