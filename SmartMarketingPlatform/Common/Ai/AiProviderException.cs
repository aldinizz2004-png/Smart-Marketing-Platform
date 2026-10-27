namespace SmartMarketingPlatform.Common.Ai;

// Thrown by an IMarketingAi implementation when the provider fails (timeout, quota, bad answer ...).
public sealed class AiProviderException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
