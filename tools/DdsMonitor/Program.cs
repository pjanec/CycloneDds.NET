var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "DDS Monitor scaffolding");

app.Run();
