using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class CoreApiRequestBudgets
{
    internal const string TimeoutKey = "RequestBudget:ProtectedRequestTimeoutSeconds";

    internal static IServiceCollection AddCoreApiRequestBudgets(this IServiceCollection services, IConfiguration configuration)
    {
        var seconds = configuration.GetValue<int?>(TimeoutKey);
        if (seconds is null or < 1 or > 120)
            throw new InvalidOperationException($"{TimeoutKey} must be between 1 and 120.");

        // The native timeout logger includes the caught exception. Provider/payload details
        // may be inside it; the safe response below is the only timeout diagnostic contract.
        services.AddLogging(logging => logging.AddFilter(
            "Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutsMiddleware", LogLevel.None));
        services.AddRequestTimeouts(options => options.DefaultPolicy = new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(seconds.Value),
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            WriteTimeoutResponse = context => context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status504GatewayTimeout,
                Title = "The request exceeded its processing budget.",
                Extensions = new Dictionary<string, object?>
                {
                    ["code"] = "request_timeout",
                    ["traceId"] = context.TraceIdentifier,
                },
            }, options: null, contentType: "application/problem+json", cancellationToken: CancellationToken.None),
        });
        return services;
    }

    internal static IApplicationBuilder UseCoreApiRequestBudgets(this IApplicationBuilder app) =>
        app.UseWhen(context => context.GetEndpoint()?.Metadata.GetMetadata<EndpointAccessMetadata>() is { IsProtected: true },
            branch => branch.UseRequestTimeouts());
}
