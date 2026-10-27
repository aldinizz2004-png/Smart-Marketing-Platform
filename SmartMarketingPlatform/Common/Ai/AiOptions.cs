namespace SmartMarketingPlatform.Common.Ai;

// "Ai" section. Prices are per 1M tokens and are used to estimate the cost of every call,
// whatever the provider. The values in appsettings are placeholders until a provider is chosen.
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; init; } = "Fake";
    public string Model { get; init; } = "fake-marketing-v1";
    public decimal InputCostPer1MTokensUsd { get; init; }
    public decimal OutputCostPer1MTokensUsd { get; init; }
}
