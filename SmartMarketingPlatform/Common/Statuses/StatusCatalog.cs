using Microsoft.EntityFrameworkCore;
using SmartMarketingPlatform.Common.Localization;
using SmartMarketingPlatform.Data;
using SmartMarketingPlatform.DTOs.Common;
using SmartMarketingPlatform.Entities;
using SmartMarketingPlatform.Services;

namespace SmartMarketingPlatform.Common.Statuses;

// Statuses are seed data that only change with a deployment, so they are read once
// and kept in memory. Responses get a translated StatusInfo without joining the Statuses table.
public sealed class StatusCatalog(
    IServiceScopeFactory scopeFactory,
    TranslationService translations,
    IHttpContextAccessor httpContextAccessor)
{
    // PublicationOnly: if the database is down on the first call, the failure is not cached.
    private readonly Lazy<IReadOnlyDictionary<int, Status>> _statuses =
        new(() => Load(scopeFactory), LazyThreadSafetyMode.PublicationOnly);

    public StatusInfo Describe(int statusId)
    {
        if (!_statuses.Value.TryGetValue(statusId, out var status))
            throw new InvalidOperationException($"Unknown status id {statusId}.");

        var language = RequestLanguage.Current(httpContextAccessor.HttpContext);
        return new StatusInfo(status.Id, status.Code, translations.Translate(status.TranslationKey, language));
    }

    private static IReadOnlyDictionary<int, Status> Load(IServiceScopeFactory scopeFactory)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketingDbContext>();
        return db.Statuses.AsNoTracking().ToDictionary(s => s.Id);
    }
}
