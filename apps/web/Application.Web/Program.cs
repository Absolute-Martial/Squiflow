using Application.ApplicationEntry;
using Application.Web.Components;
using Application.Web.Authentication;

var builder = WebApplication.CreateBuilder(args);
if (!ServiceAddress.TryParse(builder.Configuration["ApplicationEntry:CoreApiBaseUrl"], out var serviceAddress))
{
    throw new InvalidOperationException("Configure ApplicationEntry:CoreApiBaseUrl with an HTTPS service origin, or HTTP on localhost.");
}

builder.Services.AddSingleton(serviceAddress!);
builder.Services.AddSingleton(_ => ApplicationEntryClient.CreateHttpClient());
builder.Services.AddSingleton<ApplicationEntryClient>();
builder.Services.AddSingleton<TenantEntryClient>();
var login = WebLoginConfiguration.Read(builder.Configuration);
WebLogin.Add(builder, login);
builder.Services.AddRazorComponents().AddInteractiveServerComponents(options =>
{
    options.DetailedErrors = false;
    options.DisconnectedCircuitMaxRetained = 16;
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromSeconds(30);
});

var app = builder.Build();
var developmentImageOrigin = serviceAddress!.Scheme == Uri.UriSchemeHttp
    ? $" {serviceAddress.GetLeftPart(UriPartial.Authority)}"
    : string.Empty;
var identityFormOrigin = login is null ? string.Empty : " " + login.Authority.GetLeftPart(UriPartial.Authority);
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    CreateScopeForErrors = true,
    ExceptionHandler = async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await new Microsoft.AspNetCore.Http.HttpResults.RazorComponentResult<Application.Web.Components.Pages.Unavailable>()
            .ExecuteAsync(context);
    }
});
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.ContentSecurityPolicy = $"default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' https:{developmentImageOrigin}; connect-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'{identityFormOrigin}";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    if (login is not null && (context.Request.Path.StartsWithSegments("/auth") ||
        context.Request.Path.StartsWithSegments("/signin-oidc") || context.Request.Path.StartsWithSegments("/workspace")) &&
        (!context.Request.IsHttps || !string.Equals(context.Request.Host.Value, login.PublicOrigin.Authority, StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
WebLogin.Map(app, login);
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode(options =>
    options.ContentSecurityFrameAncestorsPolicy = "'none'");
app.MapGet("/health/live", () => Results.Ok());
app.Run();

public partial class Program;
