using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.AdminApi.Authentication;
using Application.AdminApi.IdentityProvisioning;
using Application.IdentityAccess;
using Xunit;

namespace Application.AdminApi.Tests;

public sealed class ZitadelIdentityVerifierTests
{
    private const string Authority = "https://identity.example.test";
    private const string ApiToken = "test-zitadel-token";

    [Fact]
    public async Task CurrentV2HumanUserShapeIsVerifiedWithBearerAuthentication()
    {
        const string subject = "29847293487293487";
        using var client = new HttpClient(new DelegateHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal($"/v2/users/{subject}", request.RequestUri?.AbsolutePath);
            Assert.Equal(new AuthenticationHeaderValue("Bearer", ApiToken), request.Headers.Authorization);
            Assert.Contains(
                request.Headers.Accept,
                value => string.Equals(value.MediaType, "application/json", StringComparison.Ordinal));
            return Task.FromResult(JsonResponse(
                JsonSerializer.Serialize(new
                {
                    details = new { sequence = 1 },
                    user = new
                    {
                        userId = subject,
                        state = "USER_STATE_ACTIVE",
                        human = new
                        {
                            userId = subject,
                            profile = new { displayName = "Example" },
                        },
                    },
                })));
        }))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var verifier = CreateVerifier(client);

        var result = await verifier.VerifyAsync(
            ExternalIdentity.Create(Authority, subject),
            CancellationToken.None);

        Assert.Equal(ExternalIdentityVerification.Verified, result);
    }

    [Fact]
    public async Task MissingAndMachineUsersAreNotImportedAsInteractiveAccounts()
    {
        const string subject = "machine-or-missing";
        using var missingClient = new HttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        Assert.Equal(
            ExternalIdentityVerification.NotFound,
            await CreateVerifier(missingClient).VerifyAsync(
                ExternalIdentity.Create(Authority, subject),
                CancellationToken.None));

        using var machineClient = new HttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(JsonResponse(
                JsonSerializer.Serialize(new
                {
                    user = new
                    {
                        userId = subject,
                        machine = new { name = "automation" },
                    },
                })))))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        Assert.Equal(
            ExternalIdentityVerification.NotInteractiveUser,
            await CreateVerifier(machineClient).VerifyAsync(
                ExternalIdentity.Create(Authority, subject),
                CancellationToken.None));
    }

    [Fact]
    public async Task MismatchedProviderIdentityAndMalformedSuccessResponsesFailClosed()
    {
        const string subject = "expected-subject";
        using var mismatchedClient = new HttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(JsonResponse(
                """{"user":{"userId":"different-subject","human":{}}}"""))))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        await Assert.ThrowsAsync<AdminIdentityProviderUnavailableException>(() =>
            CreateVerifier(mismatchedClient).VerifyAsync(
                ExternalIdentity.Create(Authority, subject),
                CancellationToken.None));

        foreach (var body in new[]
        {
            """{"user":""", "[]", "null", "true", "42", "\"provider-sensitive-detail\"",
            "{}", """{"user":null}""", """{"user":[]}""", """{"user":"invalid"}""",
            """{"user":{"userId":42,"human":{}}}""",
        })
        {
            using var malformedClient = new HttpClient(new DelegateHandler((_, _) =>
                Task.FromResult(JsonResponse(body))))
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
            await Assert.ThrowsAsync<AdminIdentityProviderUnavailableException>(() =>
                CreateVerifier(malformedClient).VerifyAsync(
                    ExternalIdentity.Create(Authority, subject),
                    CancellationToken.None));
        }
    }

    [Fact]
    public async Task ProviderFailureTimeoutAndOversizedResponseFailClosed()
    {
        var identity = ExternalIdentity.Create(Authority, "failure-subject");
        using var failureClient = new HttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        await Assert.ThrowsAsync<AdminIdentityProviderUnavailableException>(() =>
            CreateVerifier(failureClient).VerifyAsync(identity, CancellationToken.None));

        using var timeoutClient = new HttpClient(new DelegateHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse("""{"user":{"userId":"never","human":{}}}""");
        }))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        await Assert.ThrowsAsync<AdminIdentityProviderUnavailableException>(() =>
            CreateVerifier(timeoutClient, TimeSpan.FromMilliseconds(25))
                .VerifyAsync(identity, CancellationToken.None));

        using var oversizedClient = new HttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(JsonResponse(new string('x', (64 * 1024) + 1)))))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        await Assert.ThrowsAsync<AdminIdentityProviderUnavailableException>(() =>
            CreateVerifier(oversizedClient).VerifyAsync(identity, CancellationToken.None));
    }

    [Fact]
    public async Task DifferentIssuerIsRejectedWithoutCallingProvider()
    {
        var calls = 0;
        using var client = new HttpClient(new DelegateHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(JsonResponse("""{"user":{"userId":"subject","human":{}}}"""));
        }))
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        var result = await CreateVerifier(client).VerifyAsync(
            ExternalIdentity.Create("https://other-issuer.example.test", "subject"),
            CancellationToken.None);

        Assert.Equal(ExternalIdentityVerification.IssuerMismatch, result);
        Assert.Equal(0, calls);
    }

    private static ZitadelIdentityVerifier CreateVerifier(
        HttpClient client,
        TimeSpan? requestTimeout = null) =>
        new(
            client,
            new ZitadelIdentityProvisioningConfiguration(
                new Uri($"{Authority}/", UriKind.Absolute),
                ApiToken,
                requestTimeout ?? TimeSpan.FromSeconds(1)),
            new AdminOidcAuthenticationConfiguration(
                Authority,
                "admin-api-tests",
                TimeSpan.FromSeconds(1),
                TimeSpan.Zero));

    private static HttpResponseMessage JsonResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
