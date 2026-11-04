using BaseForge.Identity.Accounts;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using BaseForge.Identity.Messaging;
using BaseForge.Identity.Otp;
using BaseForge.Identity.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace BaseForge.Identity.Tests;

/// <summary>RUN_DB_TESTS=1 iken gerçek Postgres; Identity + kiracı servisleri DI ile kurulur.</summary>
public sealed class TenancyFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private ServiceProvider? _provider;

    /// <summary>Test ayarları (fixture başına; testler değiştirebilir).</summary>
    public AuthOptions Options { get; } = new()
    {
        Roles = ["SuperAdmin", "TenantAdmin", "Operator", "Driver"],
        Registration = new RegistrationOptions { Enabled = true, DefaultRole = "User", SelfAssignableRoles = ["Driver"] },
        Otp = new OtpOptions { Enabled = true, ResendCooldownSeconds = 60, MaxPerHour = 3, MaxAttempts = 3 },
        PasswordReset = new PasswordResetOptions { Enabled = true, ResetUrl = "https://app.test/sifre-sifirla", CooldownSeconds = 60 },
        MultiTenancy = new MultiTenancyOptions
        {
            Enabled = true,
            AdminRole = "TenantAdmin",
            MemberRoles = ["Operator"],
            RequireApproval = true,
            ApproverRoles = ["SuperAdmin"],
        },
    };

    /// <summary>Gönderilen SMS/e-postalar (kod buradan okunur).</summary>
    public CapturingSender Messages { get; } = new();

    /// <summary>Servis sağlayıcı (testler kendi scope'unu açar).</summary>
    public IServiceProvider Services => _provider ?? throw new InvalidOperationException("RUN_DB_TESTS=1 değil.");

    public async Task InitializeAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_DB_TESTS"), "1", StringComparison.Ordinal))
        {
            return;
        }

        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();   // şifre sıfırlama token'ları (uygulamada AddIdentity ekler)
        services.AddDbContext<IdentityServiceDbContext>(o => o.UseNpgsql(_container.GetConnectionString()).UseOpenIddict());
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityServiceDbContext>()
            .AddDefaultTokenProviders();
        services.AddOpenIddict().AddCore(o => o.UseEntityFrameworkCore().UseDbContext<IdentityServiceDbContext>());
        services.AddSingleton(Options);
        services.AddTenancy(Options);
        services.AddAccounts(Options);
        services.AddMessaging(Options, new DevelopmentEnvironment());
        services.AddOtp(Options);
        services.AddSingleton<ISmsSender>(Messages);
        services.AddSingleton<IEmailSender>(Messages);
        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
        await db.Database.EnsureCreatedAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Options.Roles.Append("User"))
        {
            await roles.CreateAsync(new IdentityRole<Guid>(role));
        }
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>Benzersiz test e-postası.</summary>
    public static string Email() => $"u{Guid.NewGuid():N}@test.local";

    /// <summary>Rastgele E.164 test telefonu.</summary>
    public static string Phone() => $"+90555{Random.Shared.Next(1_000_000, 9_999_999)}";

    /// <summary>Şirket açar ve onaylar; şirket, yöneticisi ve kodu döner.</summary>
    public static async Task<(Guid TenantId, Guid AdminId, string Slug)> ActiveTenantAsync(IServiceProvider sp, string name)
    {
        var admin = await CreateUserAsync(sp, Email());
        var onboarding = sp.GetRequiredService<ITenantOnboardingService>();
        var created = await onboarding.CreateAsync(admin.Id, new CreateTenantRequest(name, null), default);
        await onboarding.ReviewAsync(created.Tenant.Id, TenantStatus.Active, Guid.NewGuid(), null, default);
        return (created.Tenant.Id, admin.Id, created.Tenant.Slug);
    }

    /// <summary>Kodun bekleme süresini geçmiş gibi gösterir (yeniden gönderim testleri).</summary>
    public static Task AgeChallengesAsync(IServiceProvider sp, string target)
        => sp.GetRequiredService<IdentityServiceDbContext>().Set<OtpChallenge>().Where(c => c.Target == target)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.CreatedAt, c => c.CreatedAt.AddMinutes(-2)));

    /// <summary>Kullanıcı oluşturur.</summary>
    public static async Task<ApplicationUser> CreateUserAsync(IServiceProvider sp, string email, bool emailConfirmed = true, string? phone = null)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = emailConfirmed,
            PhoneNumber = phone,
            PhoneNumberConfirmed = phone is not null,
        };
        var result = await sp.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        return user;
    }
}

/// <summary>RUN_DB_TESTS=1 değilse atlanır.</summary>
public sealed class DbFactAttribute : FactAttribute
{
    public DbFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_DB_TESTS"), "1", StringComparison.Ordinal))
        {
            Skip = "RUN_DB_TESTS=1 değil (gerçek Postgres gerekir).";
        }
    }
}

/// <summary>Gönderilen mesajları saklar; son kodu verir.</summary>
public sealed class CapturingSender : ISmsSender, IEmailSender
{
    private readonly List<(string To, string Text)> _sent = [];

    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        lock (_sent) { _sent.Add((phoneNumber, message)); }
        return Task.CompletedTask;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        => SendAsync(message.To, message.Text, cancellationToken);

    /// <summary>Hedefe giden son mesaj (yoksa null).</summary>
    public string? LastTo(string to)
    {
        lock (_sent)
        {
            return _sent.LastOrDefault(m => m.To == to).Text;
        }
    }

    /// <summary>Hedefe giden son mesajdaki kod (4-8 haneli son sayı).</summary>
    public string LastCodeFor(string to)
        => System.Text.RegularExpressions.Regex.Matches(LastTo(to) ?? string.Empty, @"\b\d{4,8}\b")[^1].Value;
}

/// <summary>Testte "Development" ortamı (Log sağlayıcısına izin).</summary>
internal sealed class DevelopmentEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "BaseForge.Identity.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
