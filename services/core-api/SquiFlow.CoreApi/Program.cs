using System.Reflection;
using SquiFlow.ApplicationKernel.Hosting;
using SquiFlow.ApplicationKernel.Modules;
using SquiFlow.Customers;
using SquiFlow.Observability.Logging;

var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
using var logger = StructuredLogging.Create("SquiFlow.CoreApi", version);

var moduleGraph = ModuleGraph.Build([CustomersModule.Descriptor]);
var coreApiModules = moduleGraph.ForHost(HostKind.CoreApi);
if (coreApiModules.Count == 0)
{
    throw new InvalidOperationException("CoreApi composition contains no supported capability modules.");
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapGet("/", () => Results.Ok(new
{
    service = "SquiFlow.CoreApi",
    version,
    phase = "0",
    authority = "No business persistence endpoints are implemented in Phase 0"
}));

app.Lifetime.ApplicationStarted.Register(() =>
    logger.Information("CoreApi started with {ModuleCount} composed module(s)", coreApiModules.Count));
app.Lifetime.ApplicationStopping.Register(() => logger.Information("CoreApi stopping"));

app.Run();
