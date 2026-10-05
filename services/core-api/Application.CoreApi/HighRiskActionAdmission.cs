using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Application.CoreApi;

internal enum HighRiskAction
{
    TransferInitialOwner = 1,
}

internal sealed class HighRiskActionAdmissionConfiguration
{
    internal const string SectionName = "HighRiskActions";

    private HighRiskActionAdmissionConfiguration(
        string? tenantWebClientId,
        string? requiredAcr,
        TimeSpan maximumAuthenticationAge,
        TimeSpan futureClockSkew)
    {
        TenantWebClientId = tenantWebClientId;
        RequiredAcr = requiredAcr;
        MaximumAuthenticationAge = maximumAuthenticationAge;
        FutureClockSkew = futureClockSkew;
    }

    internal string? TenantWebClientId { get; }
    internal string? RequiredAcr { get; }
    internal TimeSpan MaximumAuthenticationAge { get; }
    internal TimeSpan FutureClockSkew { get; }
    internal bool IsConfigured => !string.IsNullOrWhiteSpace(TenantWebClientId) && !string.IsNullOrWhiteSpace(RequiredAcr);

    internal static HighRiskActionAdmissionConfiguration From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        var tenantWebClientId = Normalize(section["TenantWebClientId"]);
        var requiredAcr = Normalize(section["RequiredAcr"]);
        var maximumAgeSeconds = ReadBoundedInt(section["MaximumAuthenticationAgeSeconds"], 300, 60, 900, "MaximumAuthenticationAgeSeconds");
        var futureClockSkewSeconds = ReadBoundedInt(section["FutureClockSkewSeconds"], 60, 0, 120, "FutureClockSkewSeconds");
        return new HighRiskActionAdmissionConfiguration(
            tenantWebClientId,
            requiredAcr,
            TimeSpan.FromSeconds(maximumAgeSeconds),
            TimeSpan.FromSeconds(futureClockSkewSeconds));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int ReadBoundedInt(string? raw, int defaultValue, int minimum, int maximum, string name)
    {
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < minimum || parsed > maximum)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{name} must be an integer from {minimum} through {maximum}.");
        }
        return parsed;
    }
}

internal sealed class HighRiskActionAdmission(
    HighRiskActionAdmissionConfiguration configuration,
    TimeProvider timeProvider)
{
    internal IResult? Require(
        ClaimsPrincipal principal,
        HighRiskAction action)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));

        if (!configuration.IsConfigured)
        {
            return Problem(
                StatusCodes.Status503ServiceUnavailable,
                "high_risk_admission_unavailable",
                "The high-risk authentication contract is not configured for this deployment.");
        }

        var clientId = principal.FindFirstValue("azp");
        if (!string.Equals(clientId, configuration.TenantWebClientId, StringComparison.Ordinal))
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "high_risk_client_required",
                "This operation requires the trusted Tenant Web authentication context.");
        }

        var acr = principal.FindFirstValue("acr");
        if (!string.Equals(acr, configuration.RequiredAcr, StringComparison.Ordinal))
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "high_risk_authentication_strength_required",
                "This operation requires the configured strong authentication context.");
        }

        var rawAuthenticationTime = principal.FindFirstValue("auth_time");
        if (!long.TryParse(rawAuthenticationTime, NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "high_risk_authentication_time_required",
                "This operation requires a provider-authenticated authentication time.");
        }

        DateTimeOffset authenticationTime;
        try
        {
            authenticationTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "high_risk_authentication_time_invalid",
                "The authentication time is not valid.");
        }

        var now = timeProvider.GetUtcNow();
        if (authenticationTime > now + configuration.FutureClockSkew ||
            now - authenticationTime > configuration.MaximumAuthenticationAge)
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "high_risk_authentication_stale",
                "This operation requires recent strong authentication.");
        }

        return null;
    }

    private static ProblemHttpResult Problem(int status, string code, string detail) =>
        TypedResults.Problem(
            statusCode: status,
            title: "High-risk action admission denied.",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
