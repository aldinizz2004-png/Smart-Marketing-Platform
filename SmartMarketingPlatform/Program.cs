using Microsoft.EntityFrameworkCore;
using SmartMarketingPlatform.Data;
using SmartMarketingPlatform.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("MarketingDb")
    ?? throw new InvalidOperationException("Connection string 'MarketingDb' was not found.");

// Register the custom DbContext with SQL Server.
builder.Services.AddDbContext<MarketingDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddSingleton<TranslationService>();
builder.Services.AddScoped<PlatformDetector>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Is the database reachable and up to date?
app.MapGet("/health/db", async (MarketingDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    var pending = canConnect ? (await db.Database.GetPendingMigrationsAsync()).ToList() : new List<string>();
    return Results.Ok(new { canConnect, pendingMigrations = pending });
});

// Statuses of one entity, translated: /api/statuses/Campaign?lang=ar
app.MapGet("/api/statuses/{category}", async (string category, string? lang, MarketingDbContext db, TranslationService t) =>
{
    var statuses = await db.Statuses.AsNoTracking()
        .Where(s => s.Category == category && s.IsActive)
        .OrderBy(s => s.SortOrder)
        .ToListAsync();

    return Results.Ok(statuses.Select(s => new { s.Id, s.Code, s.TranslationKey, Text = t.Translate(s.TranslationKey, lang) }));
});

// Platforms with translated names: /api/platforms?lang=ar
app.MapGet("/api/platforms", async (string? lang, MarketingDbContext db, TranslationService t) =>
{
    var platforms = await db.Platforms.AsNoTracking().Where(p => p.IsActive).ToListAsync();

    return Results.Ok(platforms.Select(p => new
    {
        p.Code,
        p.DisplayNameKey,
        DisplayName = t.Translate(p.DisplayNameKey, lang),
        p.SupportsPublishing,
        p.SupportsCompetitorTracking
    }));
});

// Paste any competitor URL and see how it will be tracked:
// /api/platforms/detect?url=https://www.instagram.com/rival_cafe/
app.MapGet("/api/platforms/detect", async (string url, PlatformDetector detector) =>
{
    try
    {
        return Results.Ok(await detector.DetectAsync(url));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();
