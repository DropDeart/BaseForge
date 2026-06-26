using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil.");

builder.Services.AddDbContext<IdentityServiceDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.UseOpenIddict();
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<IdentityServiceDbContext>()
    .AddDefaultTokenProviders();

// Identity claim tiplerini OpenIddict'in beklediği isimlere hizala.
builder.Services.Configure<IdentityOptions>(options =>
{
    options.ClaimsIdentity.UserNameClaimType = Claims.Name;
    options.ClaimsIdentity.UserIdClaimType = Claims.Subject;
    options.ClaimsIdentity.RoleClaimType = Claims.Role;
});

builder.Services.AddOpenIddict()
    .AddCore(options => options
        .UseEntityFrameworkCore()
        .UseDbContext<IdentityServiceDbContext>())
    .AddServer(options =>
    {
        // Sabit issuer (downstream servisler tutarlı doğrulasın). Yoksa istek host'una göre değişir.
        var issuer = builder.Configuration["OpenIddict:Issuer"];
        if (!string.IsNullOrWhiteSpace(issuer))
        {
            options.SetIssuer(new Uri(issuer));
        }

        options.SetTokenEndpointUris("connect/token");
        options.SetAuthorizationEndpointUris("connect/authorize");

        options.AllowPasswordFlow()
               .AllowClientCredentialsFlow()
               .AllowRefreshTokenFlow()
               .AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange();

        options.RegisterScopes(SeedData.ApiScope, Scopes.Profile, Scopes.Email, Scopes.Roles, Scopes.OfflineAccess);

        // Asimetrik imzalama (RSA) → JWKS ile yayınlanır; access token JWS (şifresiz) olsun ki
        // downstream servisler public key ile offline doğrulayabilsin.
        options.AddEphemeralEncryptionKey();
        options.AddEphemeralSigningKey();
        options.DisableAccessTokenEncryption();

        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough()
               .EnableAuthorizationEndpointPassthrough()
               .DisableTransportSecurityRequirement(); // container içi HTTP (TLS reverse-proxy'de sonlanır)
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddControllers();

var app = builder.Build();

// Şema oluştur + seed (dev kolaylığı; prod'da migration kullanılır).
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.SeedAsync(scope.ServiceProvider);
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
