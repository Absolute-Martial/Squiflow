using System.Reflection;
using SquiFlow.Observability.Logging;
using SquiFlow.Web.Components;

var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
using var logger = StructuredLogging.Create("SquiFlow.Web", version);

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapRazorComponents<App>();
app.MapHealthChecks("/health/live");

app.Lifetime.ApplicationStarted.Register(() => logger.Information("Tenant Web started"));
app.Lifetime.ApplicationStopping.Register(() => logger.Information("Tenant Web stopping"));

app.Run();
