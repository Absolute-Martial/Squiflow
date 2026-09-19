using Autofac;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SquiFlow.IdentityAccess;
using SquiFlow.IdentityAccess.Postgres;
using SquiFlow.Tenancy;
using SquiFlow.Tenancy.Postgres;
using Xunit;

namespace SquiFlow.CoreApi.Tests;

public sealed class AutofacHostCompositionTests : IClassFixture<WhiteLabelApiFactory>
{
    private readonly WhiteLabelApiFactory _factory;

    public AutofacHostCompositionTests(WhiteLabelApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void CoreApiUsesAutofacAndPreservesScopedLifetimeDisposal()
    {
        using var application = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<ScopedProbe>()));

        Assert.NotNull(application.Services.GetRequiredService<ILifetimeScope>());

        var firstScope = application.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<ScopedProbe>();
        var same = firstScope.ServiceProvider.GetRequiredService<ScopedProbe>();

        Assert.Same(first, same);

        using (var secondScope = application.Services.CreateScope())
        {
            var second = secondScope.ServiceProvider.GetRequiredService<ScopedProbe>();
            Assert.NotSame(first, second);
        }

        firstScope.Dispose();
        Assert.True(first.IsDisposed);
    }

    [Fact]
    public void ProductionDirectoriesAndDbContextsRemainScopedAndAreDisposed()
    {
        using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "Authentication:Authority",
                    WhiteLabelApiFactory.Authority);
                builder.UseSetting(
                    "Authentication:Audience",
                    WhiteLabelApiFactory.Audience);
                builder.UseSetting(
                    "ConnectionStrings:PrimaryDatabase",
                    "Host=unused.example.test;Database=application");
                builder.UseSetting("Branding:DisplayName", "Example Operations");
                builder.UseSetting("Branding:ShortName", "Example");
                builder.UseSetting("Branding:LegalName", "Example Company Ltd.");
                builder.UseSetting("Branding:ThemeKey", "example-brand");
                builder.UseSetting("Branding:LogoUrl", "/tenant-assets/logo.svg");
                builder.UseSetting("Branding:FaviconUrl", "/tenant-assets/favicon.svg");
                builder.UseSetting("Branding:SupportUrl", "https://support.example.test");
                builder.UseSetting("Branding:PrivacyUrl", "https://www.example.test/privacy");
                builder.UseSetting("Branding:TermsUrl", "https://www.example.test/terms");
            });

        var firstScope = application.Services.CreateScope();
        var firstIdentity = firstScope.ServiceProvider
            .GetRequiredService<IdentityAccessDbContext>();
        var firstTenancy = firstScope.ServiceProvider
            .GetRequiredService<TenancyDbContext>();

        Assert.Same(
            firstIdentity,
            firstScope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>());
        Assert.Same(
            firstTenancy,
            firstScope.ServiceProvider.GetRequiredService<TenancyDbContext>());
        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<IAccountBindingDirectory>(),
            firstScope.ServiceProvider.GetRequiredService<IAccountBindingDirectory>());
        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<ITenantMembershipDirectory>(),
            firstScope.ServiceProvider.GetRequiredService<ITenantMembershipDirectory>());

        using (var secondScope = application.Services.CreateScope())
        {
            Assert.NotSame(
                firstIdentity,
                secondScope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>());
            Assert.NotSame(
                firstTenancy,
                secondScope.ServiceProvider.GetRequiredService<TenancyDbContext>());
        }

        firstScope.Dispose();
        Assert.Throws<ObjectDisposedException>(() => firstIdentity.SaveChanges());
        Assert.Throws<ObjectDisposedException>(() => firstTenancy.SaveChanges());
    }

    private sealed class ScopedProbe : IDisposable
    {
        internal bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
