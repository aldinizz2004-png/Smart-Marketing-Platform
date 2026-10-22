namespace SmartMarketingPlatform.Entities;

// A login identity from the authentication provider.
public class AppUser
{
    public Guid Id { get; set; }
    public string AuthSubject { get; set; } = string.Empty;   // unique id from the auth provider
    public string Email { get; set; } = string.Empty;         // unique
    public string? FullName { get; set; }
    public string PreferredLanguage { get; set; } = "ar";     // "ar" or "en", picks the translation file
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<TenantMembership> Memberships { get; set; } = new List<TenantMembership>();
}