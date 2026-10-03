using Application.AdminApi.Authorization;
using Application.AdminApi.IdentityProvisioning;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;

namespace Application.AdminApi;

internal sealed class AdminApiExceptionHandler(ILogger<AdminApiExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, int, string, string, Exception?> LogFailure =
        LoggerMessage.Define<string, int, string, string>(
            LogLevel.Error,
            new EventId(1001, "AdminApiFailure"),
            "Admin API failure {Code} with status {StatusCode}; type {ExceptionType}; trace {TraceId}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (context.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var (status, code, title) = exception switch
        {
            AdminAuthorizationProviderUnavailableException =>
                (StatusCodes.Status503ServiceUnavailable, "authorization_unavailable",
                    "Authorization service unavailable."),
            AdminIdentityProviderUnavailableException =>
                (StatusCodes.Status503ServiceUnavailable, "identity_provider_unavailable",
                    "Identity provider unavailable."),
            NpgsqlException { IsTransient: true } =>
                (StatusCodes.Status503ServiceUnavailable, "database_unavailable",
                    "Database unavailable."),
            BadHttpRequestException request =>
                (request.StatusCode, "invalid_request",
                    "The request could not be processed."),
            _ =>
                (StatusCodes.Status500InternalServerError, "internal_error",
                    "An internal error occurred."),
        };

        var traceId = context.TraceIdentifier;
        LogFailure(logger, code, status, exception.GetType().Name, traceId, null);
        context.Response.Headers.CacheControl = "no-store";
        await Results.Problem(
                statusCode: status,
                title: title,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = code,
                    ["traceId"] = traceId,
                })
            .ExecuteAsync(context);
        return true;
    }
}
