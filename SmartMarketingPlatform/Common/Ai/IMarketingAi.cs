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
}

public sealed record AiCallResult<T>(T Output, int InputTokens, int OutputTokens);

public sealed record RecommendationDraft(
    RecommendationAction ActionType,
    JsonObject Payload,
    DateTimeOffset? ProposedPublishAt,
    string Rationale,
    decimal Confidence);
