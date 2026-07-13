using System.Security.Cryptography.X509Certificates;
using BaseForge.Identity.Authentication;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

// CreateBuilder'dan ÖNCE yüklenmeli — aksi halde environment-variable configuration
// provider'ı (WebApplication.CreateBuilder içinde eklenir) .env'deki değerleri göremez.
// .env yoksa (örn. üretimde secret'lar zaten gerçek ortam değişkenleriyle veriliyorsa) sessizce atlanır.
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil.");

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddSingleton(authOptions);

// SPA'lardan (farklı origin) /connect/token ve /api/* çağrılabilmesi için — izinli origin'ler
// appsettings/env'den gelir, kod değişikliği/regen gerekmez (bkz. appsettings.json "Cors:AllowedOrigins").
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(cors =>
{
    cors.AddPolicy("ConfiguredOrigins", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddDbContext<IdentityServiceDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.UseOpenIddict();
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        // NOT: Bu 5 alan BaseForge.CodeGen/Spec/AuthSpecValidator.cs'de birebir aynı kurallarla
        // (uzunluk + büyük/küçük harf + rakam + özel karakter) yeniden doğrulanıyor — biri değişirse diğeri de güncellenmeli.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
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
// '/api/*' istekleri SPA'nın fetch() çağrıları — 302 yerine düz 401/403 dönmeli, aksi halde
// fetch index.html'e yönlendirilip 200 gibi görünür ve JSON parse hatası verir.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
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

// Merkez kullanıcı entity'sine (ApplicationUser) diğer servislerin gRPC ile salt-okunur erişimi.
builder.Services.AddGrpc();

var app = builder.Build();

// Reverse proxy (nginx vb.) arkasında çalışırken gerçek şema/host'u (https, gerçek domain) Kestrel'e
// bildirir — aksi halde OpenIddict discovery/authorization/token URL'leri yanlış (http, proxy'nin iç
// adresi) görünür. Docker port publish NAT'i yüzünden istek değişken bir gateway IP'sinden geldiği için
// KnownNetworks/KnownProxies temizlenir; güvenlik, container portunun yalnızca 127.0.0.1'e publish
// edilmesinden gelir (dışarıdan doğrudan erişilemez, yalnızca aynı host'taki nginx erişebilir).
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Şema oluştur + seed (dev kolaylığı; prod'da migration kullanılır).
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.SeedAsync(scope.ServiceProvider, authOptions);
}

app.UseStaticFiles();
app.UseCors("ConfiguredOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGrpcService<BaseForge.Identity.Grpc.UserGrpcService>();

// Ortak Giriş SPA'sı (Login/Register/Admin) wwwroot'a gömülü — client-side route'lar (örn. /Account/Login)
// için index.html fallback'i. /api, /connect ve gRPC uçları yukarıda zaten eşlendiği için önceliklidir.
app.MapFallbackToFile("index.html");

app.Run();
