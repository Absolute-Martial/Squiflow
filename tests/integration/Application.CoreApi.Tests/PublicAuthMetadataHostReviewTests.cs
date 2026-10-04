using System.Net;
using System.Net.Http.Headers;
using Application.CoreApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed class PublicAuthMetadataHostReviewTests
{
    [Theory]
    [InlineData(true, "missing", HttpStatusCode.Unauthorized)]
    [InlineData(true, "invalid", HttpStatusCode.Unauthorized)]
    [InlineData(true, "valid", HttpStatusCode.OK)]
    [InlineData(false, "missing", HttpStatusCode.Unauthorized)]
    [InlineData(false, "invalid", HttpStatusCode.Unauthorized)]
    [InlineData(false, "valid", HttpStatusCode.OK)]
    public async Task AuthorizationRequiredRouteCannotSkipAuthentication(
        bool classifiedPublic, string identity, HttpStatusCode expected)
    {
        var registration = new MetadataTrapRouteRegistration(classifiedPublic);
        using var baseline = new WhiteLabelApiFactory();
        using var factory = baseline.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter>(registration)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, MetadataTrapRouteRegistration.Path);
        request.Headers.Add("X-Platform-Admin", "true");
        request.Headers.Add("X-Admin-Device", "registered");
        if (identity != "missing")
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
                identity == "valid" ? baseline.CreateToken() : "malformed-metadata-trap-bearer");
        }

        using var response = await client.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(identity == "valid", registration.HandlerEntered);
        if (identity == "valid")
        {
            Assert.Equal("authenticated-review-endpoint", body);
        }
        else
        {
            Assert.DoesNotContain("authenticated-review-endpoint", body, StringComparison.Ordinal);
        }
    }

    private sealed class MetadataTrapRouteRegistration(bool classifiedPublic) : IStartupFilter
    {
        internal const string Path = "/__review/authorization-metadata";
        internal bool HandlerEntered { get; private set; }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(routes =>
            {
                var route = routes.MapGet(Path, async context =>
                {
                    Assert.True(context.User.Identity?.IsAuthenticated);
                    HandlerEntered = true;
                    await context.Response.WriteAsync("authenticated-review-endpoint", context.RequestAborted);
                }).RequireAuthorization();
                if (classifiedPublic)
                {
                    route.WithMetadata(new EndpointAccessMetadata(EndpointAccess.PublicLiveness));
                }
            });
        };
    }
}

