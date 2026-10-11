using Microsoft.AspNetCore.Diagnostics;
using Npgsql;

namespace Application.CoreApi;

internal sealed class CoreApiExceptionHandler(ILogger<CoreApiExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, int, string, string, Exception?> LogFailure =
        LoggerMessage.Define<string, int, string, string>(LogLevel.Error, new EventId(1001, "ApiFailure"),
            "API failure {Code} with status {StatusCode}; type {ExceptionType}; trace {TraceId}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // A disconnected caller cannot receive a problem response. This is not a dependency outage.
        if (context.RequestAborted.IsCancellationRequested)
            return false;

        var (status, code, title) = exception switch
        {
            Application.Orders.OrderProfileUnavailableException =>
                (503, "order_profile_unavailable", "A compatible authoritative Order profile is unavailable."),
            NpgsqlException { IsTransient: true } =>
                (503, "database_unavailable", "The database operation could not be completed."),
            BadHttpRequestException request =>
                (request.StatusCode, "invalid_request", "The request could not be processed."),
            _ => (500, "internal_error", "The request could not be completed."),
        };
        var traceId = context.TraceIdentifier;

        // Provider messages, SQL, payloads and exception objects may contain sensitive material.
        LogFailure(logger, code, status, exception.GetType().Name, traceId, null);
        await Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = traceId })
            .ExecuteAsync(context);
        return true;
    }
}
