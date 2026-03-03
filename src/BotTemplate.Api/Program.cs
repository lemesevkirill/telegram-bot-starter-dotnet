var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health/live", () => Results.Json(new { status = "ok" }));
app.MapGet("/health/ready", () => Results.Json(new { status = "ok" }));

app.Run();
