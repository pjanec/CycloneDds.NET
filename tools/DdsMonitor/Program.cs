using DdsMonitor.Components;
using DdsMonitor.Engine.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

builder.Services.AddDdsMonitorServices(builder.Configuration);

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

app.Run();
