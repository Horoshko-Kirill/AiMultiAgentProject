using AiMultiAgent.Stats.Service.Data;
using AiMultiAgent.Stats.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<StatsDbContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("StatsDb") ??
        Environment.GetEnvironmentVariable("ConnectionStrings__StatsDb") ??
        "Data Source=stats.db";

    options.UseSqlite(connectionString);
});

builder.Services.AddHttpClient(nameof(UpstreamPmClient), http =>
{
    http.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddScoped<UpstreamPmClient>();
builder.Services.AddScoped<PmReportProjector>();
builder.Services.AddScoped<RunCaptureService>();
builder.Services.AddScoped<DashboardQueryService>();

builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StatsDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapOpenApi();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
