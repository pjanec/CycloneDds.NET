using DdsMonitor.Components;
using DdsMonitor.Engine.Hosting;
using DdsMonitor.Engine.Ui;
using DdsMonitor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

builder.Services.AddDdsMonitorServices(builder.Configuration);
builder.Services.AddSingleton<ITypeDrawerRegistry, TypeDrawerRegistry>();
builder.Services.AddSingleton<TooltipService>();
builder.Services.AddSingleton<ContextMenuService>();
builder.Services.AddScoped<WorkspacePersistenceService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

app.Run();
