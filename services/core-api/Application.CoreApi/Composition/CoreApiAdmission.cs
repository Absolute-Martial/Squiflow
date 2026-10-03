using System.Threading.RateLimiting;

namespace Application.CoreApi.Composition;

internal static class CoreApiAdmission
{
    internal const string TenantPermitLimitKey = "Admission:MaximumConcurrentRequestsPerTenant";

    internal const string PermitLimitKey = "Admission:MaximumConcurrentProtectedRequests";

    internal static IServiceCollection AddCoreApiAdmission(this IServiceCollection services, IConfiguration configuration)
    {
        var permits = configuration.GetValue<int?>(PermitLimitKey);
        if (permits is null or < 1 or > 256)
            throw new InvalidOperationException($"{PermitLimitKey} must be between 1 and 256.");

        var tenantPermits = configuration.GetValue<int?>(TenantPermitLimitKey);
        if (tenantPermits is null or < 1 or > 256)
            throw new InvalidOperationException($"{TenantPermitLimitKey} must be between 1 and 256.");
        services.AddSingleton(_ => new TenantAdmissionPartitions(tenantPermits.Value, permits.Value));
        services.AddScoped<TenantRequestAdmission>();

        services.AddRateLimiter(options =>
        {
            // One partition per process, never one unbounded partition per client-controlled identifier.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.GetEndpoint()?.Metadata.GetMetadata<EndpointAccessMetadata>() is { IsProtected: true }
                    ? RateLimitPartition.GetConcurrencyLimiter("protected", _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = permits.Value,
                        QueueLimit = 0,
                    })
                    : RateLimitPartition.GetNoLimiter("public"));
            options.OnRejected = async (rejection, cancellationToken) =>
            {
                var context = rejection.HttpContext;
                await Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "The application is at its concurrent request limit.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "api_capacity_exceeded",
                        ["traceId"] = context.TraceIdentifier,
                    }).ExecuteAsync(context);
            };
        });
        return services;
    }
}
