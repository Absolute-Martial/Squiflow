using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Application.AdminApi.Composition;

internal static class AdminApiAdmission
{
    internal const string PolicyName = "platform-admin";

    internal static IServiceCollection AddAdminApiAdmission(
        this IServiceCollection services,
        int maximumConcurrentRequests)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable;
            options.AddConcurrencyLimiter(PolicyName, limiter =>
            {
                limiter.PermitLimit = maximumConcurrentRequests;
                limiter.QueueLimit = 0;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                await JsonSerializer.SerializeAsync(
                    context.HttpContext.Response.Body,
                    new
                    {
                        type = "about:blank",
                        title = "Admin API capacity is temporarily exhausted.",
                        status = StatusCodes.Status503ServiceUnavailable,
                        code = "admin_api_capacity_exceeded",
                    },
                    cancellationToken: cancellationToken);
            };
        });
        return services;
    }
}
