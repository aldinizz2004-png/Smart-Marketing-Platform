namespace SmartMarketingPlatform.Common.Auth;

// Authorization policies, built from the role claim of the current tenant.
public static class Policies
{
    public const string TenantOwner = "TenantOwner";   // Owner only (payments, subscriptions)
    public const string TenantAdmin = "TenantAdmin";   // Owner or Admin (settings, members)
}
