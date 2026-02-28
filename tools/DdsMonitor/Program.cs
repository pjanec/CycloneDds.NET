using DdsMonitor.Components;
using DdsMonitor.Engine.Hosting;
using DdsMonitor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

builder.Services.AddDdsMonitorServices(builder.Configuration);
builder.Services.AddSingleton<TooltipService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

app.Run();
