using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class HighRiskActionAdmissionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MissingDeploymentContractFailsClosed()
    {
        var admission = Create(new Dictionary<string, string?>());

        var result = admission.Require(Principal("tenant-web", "urn:application:assurance:high-risk", Now),
            HighRiskAction.TransferInitialOwner);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Status(result));
    }

    [Theory]
    [InlineData("other-client", "urn:application:assurance:high-risk", 0, StatusCodes.Status403Forbidden)]
    [InlineData("tenant-web", "weaker", 0, StatusCodes.Status403Forbidden)]
    [InlineData("tenant-web", "urn:application:assurance:high-risk", -301, StatusCodes.Status403Forbidden)]
    [InlineData("tenant-web", "urn:application:assurance:high-risk", 61, StatusCodes.Status403Forbidden)]
    public void WrongClientStrengthOrAuthenticationAgeFailsClosed(
        string clientId,
        string acr,
        int secondsFromNow,
        int expectedStatus)
    {
        var admission = Create(Config());

        var result = admission.Require(Principal(clientId, acr, Now.AddSeconds(secondsFromNow)),
            HighRiskAction.TransferInitialOwner);

        Assert.Equal(expectedStatus, Status(result));
    }

    [Fact]
    public void MissingAuthenticationTimeCannotBeReplacedByArbitraryClaims()
    {
        var admission = Create(Config());
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "owner"),
                new Claim("azp", "tenant-web"),
                new Claim("acr", "urn:application:assurance:high-risk"),
                new Claim("x-high-risk", "true"),
            ],
            "test");

        var result = admission.Require(new ClaimsPrincipal(identity), HighRiskAction.TransferInitialOwner);

        Assert.Equal(StatusCodes.Status403Forbidden, Status(result));
    }

    [Fact]
    public void ExactConfiguredProviderEvidenceWithinAgeWindowIsAccepted()
    {
        var admission = Create(Config());

        var result = admission.Require(
            Principal("tenant-web", "urn:application:assurance:high-risk", Now.AddMinutes(-2)),
            HighRiskAction.TransferInitialOwner);

        Assert.Null(result);
    }

    private static HighRiskActionAdmission Create(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new HighRiskActionAdmission(
            HighRiskActionAdmissionConfiguration.From(configuration),
            new FixedTimeProvider(Now));
    }

    private static Dictionary<string, string?> Config() => new()
    {
        ["HighRiskActions:TenantWebClientId"] = "tenant-web",
        ["HighRiskActions:RequiredAcr"] = "urn:application:assurance:high-risk",
        ["HighRiskActions:MaximumAuthenticationAgeSeconds"] = "300",
        ["HighRiskActions:FutureClockSkewSeconds"] = "60",
    };

    private static ClaimsPrincipal Principal(string clientId, string acr, DateTimeOffset authenticationTime) =>
        new(new ClaimsIdentity(
            [
                new Claim("sub", "owner"),
                new Claim("azp", clientId),
                new Claim("acr", acr),
                new Claim("auth_time", authenticationTime.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ],
            "test"));

    private static int? Status(IResult? result) => (result as IStatusCodeHttpResult)?.StatusCode;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
