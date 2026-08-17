using LabWatch.Core;
using LabWatch.Infrastructure;
using LabWatch.Web.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<BrightDataOptions>(builder.Configuration.GetSection(BrightDataOptions.SectionName));
builder.Services.Configure<FixtureOptions>(builder.Configuration.GetSection(FixtureOptions.SectionName));
builder.Services.AddDbContextFactory<LabWatchDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("LabWatch") ?? "Data Source=labwatch.db"));
builder.Services.AddSingleton<SnapshotValidator>();
builder.Services.AddSingleton<ImpactMatcher>();
builder.Services.AddSingleton<AdvisoryJsonParser>();
builder.Services.AddHttpClient<IBrightDataClient, BrightDataClient>(client =>
    client.BaseAddress = new Uri("https://api.brightdata.com/"));
builder.Services.AddHttpClient<FixtureControlService>();
builder.Services.AddScoped<IngestionService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<DemoWorkflowService>();
builder.Services.AddScoped<DatabaseInitializer>();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
