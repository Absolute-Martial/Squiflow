using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace Application.AdminApi.Composition;

internal static class AdminApiRequestBudgets
{
    private static readonly Action<ILogger, string, Exception?> DeadlineAfterResponseStarted =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1002, nameof(DeadlineAfterResponseStarted)),
            "Admin API request deadline expired after response headers started; trace {TraceId}");

    internal static IServiceCollection AddAdminApiRequestBudgets(
        this IServiceCollection services,
        int protectedRequestTimeoutSeconds)
    {
        // The native timeout logger includes the caught exception. Provider, database, or
        // request details can be nested in it; this host exposes only fixed timeout
        // responses and the safe started-response warning below.
        services.AddLogging(logging => logging.AddFilter(
            "Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutsMiddleware",
            LogLevel.None));
        services.AddRequestTimeouts(options => options.DefaultPolicy = new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(protectedRequestTimeoutSeconds),
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

    internal static IApplicationBuilder UseAdminApiRequestBudgets(this IApplicationBuilder app) =>
        app.UseWhen(
            context => context.GetEndpoint()?.Metadata.GetMetadata<AdminEndpointAccessMetadata>() is { IsProtected: true },
            branch =>
            {
                branch.UseRequestTimeouts();
                branch.Use(async (context, next) =>
                {
                    try
                    {
                        await next(context);
                    }
                    catch (Exception exception) when (
                        exception is OperationCanceledException or IOException &&
                        context.Response.HasStarted &&
                        context.RequestAborted.IsCancellationRequested &&
                        context.Features.Get<IHttpRequestTimeoutFeature>()?.RequestTimeoutToken.IsCancellationRequested is true)
                    {
                        // Native timeout middleware rethrows once headers start. Terminate here,
                        // before outer exception/server diagnostics can expose the exception.
                        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Application.AdminApi.RequestBudgets");
                        DeadlineAfterResponseStarted(logger, context.TraceIdentifier, null);
                        context.Abort();
                    }
                });
            });
}
