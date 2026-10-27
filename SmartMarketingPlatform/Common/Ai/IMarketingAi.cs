using System.Text.Json.Nodes;
using SmartMarketingPlatform.Enums;

namespace SmartMarketingPlatform.Common.Ai;

// The only door to the AI. Today: FakeMarketingAi. Later: a real provider (Gemini, OpenAI ...)
// behind the same interface, chosen in configuration; no service changes.
public interface IMarketingAi
{
    string ModelName { get; }
    string PromptVersion { get; }

    Task<AiCallResult<JsonObject>> PlanCampaignAsync(MarketingContext context, DateTimeOffset now, CancellationToken cancellationToken);

    Task<AiCallResult<IReadOnlyList<RecommendationDraft>>> RecommendAsync(
        MarketingContext context, DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken);

    Task<AiCallResult<ContentDraft>> WriteContentAsync(MarketingContext context, ContentBrief brief, CancellationToken cancellationToken);
}

public sealed record AiCallResult<T>(T Output, int InputTokens, int OutputTokens);

public sealed record RecommendationDraft(
    RecommendationAction ActionType,
    JsonObject Payload,
    DateTimeOffset? ProposedPublishAt,
    string Rationale,
    decimal Confidence);

// What to write: where, what kind, about which product/offer, with which hook, and any extra instruction
// (e.g. "shorter"). PreviousBody is set when the AI rewrites an existing revision.
public sealed record ContentBrief(
    string PlatformCode,
    ContentType ContentType,
    ContentFormat? Format,
    HookType? Hook,
    ProductInfo? Product,
    OfferInfo? Offer,
    string? Instructions,
    string? PreviousBody);

public sealed record ContentDraft(
    string Body,
    ContentFormat Format,
    HookType? HookType,
    CallToAction? CallToAction,
    bool IncludesOffer,
    JsonObject CreativeDetails);
