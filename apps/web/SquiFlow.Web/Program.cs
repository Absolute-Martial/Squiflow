using SquiFlow.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents();

var app = builder.Build();
app.UseAntiforgery();
app.MapRazorComponents<App>();
app.MapGet("/health", () => Results.Ok(new { status = "ok", host = "tenant-web" }));
app.Run();
