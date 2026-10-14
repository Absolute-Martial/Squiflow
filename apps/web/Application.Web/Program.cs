using Application.ApplicationEntry;
using Application.Web.Components;

var builder = WebApplication.CreateBuilder(args);
if (!ServiceAddress.TryParse(builder.Configuration["ApplicationEntry:CoreApiBaseUrl"], out var serviceAddress))
{
    throw new InvalidOperationException("Configure ApplicationEntry:CoreApiBaseUrl with an HTTPS service origin, or HTTP on localhost.");
}

builder.Services.AddSingleton(serviceAddress!);
builder.Services.AddSingleton(_ => ApplicationEntryClient.CreateHttpClient());
builder.Services.AddSingleton<ApplicationEntryClient>();
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
app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsync("This page is unavailable. Reload to try again.");
}));
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.ContentSecurityPolicy = $"default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' https:{developmentImageOrigin}; connect-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next(context);
});
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode(options =>
    options.ContentSecurityFrameAncestorsPolicy = "'none'");
app.MapGet("/health/live", () => Results.Ok());
app.Run();

public partial class Program;
