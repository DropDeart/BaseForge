using System.Security.Cryptography.X509Certificates;
using BaseForge.Identity.Authentication;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil.");

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddSingleton(authOptions);

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

// İnteraktif (authorization_code) akış: giriş yoksa login sayfasına yönlendir.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});

// Dış kimlik sağlayıcıları (config'te ClientId dolu olanlar).
ExternalProviders.Add(builder.Services.AddAuthentication(), authOptions.Providers);

builder.Services.AddOpenIddict()
    .AddCore(options => options
        .UseEntityFrameworkCore()
        .UseDbContext<IdentityServiceDbContext>())
    .AddServer(options =>
    {
        // Sabit issuer (config'ten) — downstream servisler tutarlı doğrulasın.
        if (!string.IsNullOrWhiteSpace(authOptions.Issuer))
        {
            options.SetIssuer(new Uri(authOptions.Issuer));
        }

        options.SetTokenEndpointUris("connect/token");
        options.SetAuthorizationEndpointUris("connect/authorize");

        options.AllowPasswordFlow()
               .AllowClientCredentialsFlow()
               .AllowRefreshTokenFlow()
               .AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange();

        // Scope'lar config'ten + standart OIDC scope'ları.
        var scopeNames = authOptions.Scopes
            .Select(s => s.Name)
            .Concat([Scopes.Profile, Scopes.Email, Scopes.Roles, Scopes.OfflineAccess])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        options.RegisterScopes(scopeNames);

        // Asimetrik imzalama (RSA) → JWKS. Sertifika verildiyse kalıcı, yoksa ephemeral (dev).
        options.AddEphemeralEncryptionKey();
        if (!string.IsNullOrWhiteSpace(authOptions.SigningCertificatePath) && File.Exists(authOptions.SigningCertificatePath))
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                authOptions.SigningCertificatePath,
                authOptions.SigningCertificatePassword);
            options.AddSigningCertificate(certificate);
        }
        else
        {
            options.AddEphemeralSigningKey();
        }

        // Access token JWS (şifresiz) olsun ki downstream public key ile offline doğrulayabilsin.
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
builder.Services.AddRazorPages();

var app = builder.Build();

// Şema oluştur + seed (dev kolaylığı; prod'da migration kullanılır).
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.SeedAsync(scope.ServiceProvider, authOptions);
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();

app.Run();
