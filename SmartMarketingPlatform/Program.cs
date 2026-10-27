using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using SmartMarketingPlatform.Common.Businesses;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SmartMarketingPlatform.Common.Auth;
using SmartMarketingPlatform.Common.Errors;
using SmartMarketingPlatform.Common.Health;
using SmartMarketingPlatform.Common.Options;
using SmartMarketingPlatform.Common.Secrets;
using SmartMarketingPlatform.Common.Storage;
using SmartMarketingPlatform.Common.Statuses;
using SmartMarketingPlatform.Data;
using SmartMarketingPlatform.Entities;
using SmartMarketingPlatform.Enums;
using SmartMarketingPlatform.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("MarketingDb")
    ?? throw new InvalidOperationException("Connection string 'MarketingDb' was not found.");

// ---- Data -------------------------------------------------------------------
builder.Services.AddDbContext<MarketingDbContext>(options => options.UseSqlServer(connectionString));

// ---- Authentication (JWT) ---------------------------------------------------
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwt = jwtSection.Get<JwtOptions>() ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing.");
if (jwt.Key.Length < JwtOptions.MinKeyLength)
    throw new InvalidOperationException(
        $"Jwt:Key must be at least {JwtOptions.MinKeyLength} characters. Set it with the Jwt__Key environment variable.");
builder.Services.Configure<JwtOptions>(jwtSection);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep "sub", "role", "tid" as they are
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AppClaims.Name,
            RoleClaimType = AppClaims.Role
        };
    });

// Secure by default: every endpoint needs a valid token unless it says [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.TenantOwner, p => p.RequireRole(nameof(MembershipRole.Owner)))
    .AddPolicy(Policies.TenantAdmin, p => p.RequireRole(nameof(MembershipRole.Owner), nameof(MembershipRole.Admin)))
    .AddPolicy(Policies.CanEdit, p => p.RequireRole(
        nameof(MembershipRole.Owner), nameof(MembershipRole.Admin), nameof(MembershipRole.Editor)))
    .AddPolicy(Policies.ActiveSubscription, p => p.RequireAuthenticatedUser().AddRequirements(new SubscriptionRequirement()));

builder.Services.AddScoped<IAuthorizationHandler, SubscriptionRequirementHandler>();              // reads the DB per request
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationResultHandler>();   // 402

// ---- API: controllers, JSON, errors, docs -----------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));   // enums as "Owner", not 0

// Every error (validation, 401/403/404, exceptions) is returned as RFC 7807 ProblemDetails.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance ??= $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // "Authorize" button in Swagger UI: paste the accessToken from /api/auth/login.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the accessToken returned by /api/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

// ---- Application services ---------------------------------------------------
builder.Services.Configure<SubscriptionPricingOptions>(builder.Configuration.GetSection(SubscriptionPricingOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TranslationService>();
builder.Services.AddSingleton<StatusCatalog>();
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddScoped<PlatformDetector>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<ITenantService, TenantService>();

builder.Services.AddScoped<IBusinessAccess, BusinessAccess>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<IBrandService, BrandService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<IAudienceSegmentService, AudienceSegmentService>();

// Files and secrets: local implementations in development, cloud services later (same interfaces).
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddDataProtection();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<ISecretStore, LocalSecretStore>();

builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IKnowledgeSourceService, KnowledgeSourceService>();
builder.Services.AddScoped<ISocialAccountService, SocialAccountService>();
builder.Services.AddScoped<IAutomationPolicyService, AutomationPolicyService>();

var app = builder.Build();

// ---- Pipeline (order matters) -----------------------------------------------
app.UseExceptionHandler();      // first: catches exceptions from everything below
app.UseStatusCodePages();       // empty 401/403/404/405 responses also get a ProblemDetails body

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();        // who are you? (reads the Bearer token)
app.UseAuthorization();         // are you allowed? (policies, roles)

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync })
   .AllowAnonymous();

app.Run();

// Lets the integration test project start the API (WebApplicationFactory<Program>).
public partial class Program;
