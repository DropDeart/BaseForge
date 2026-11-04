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
        services.AddDbContext<IdentityServiceDbContext>(o => o.UseNpgsql(_container.GetConnectionString()));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityServiceDbContext>();
        services.AddSingleton(Options);
        services.AddTenancy(Options);
        services.AddAccounts();
        services.AddOtp(Options, new DevelopmentEnvironment());
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

    public Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
        => SendAsync(email, body, cancellationToken);

    /// <summary>Hedefe giden son mesajdaki kod.</summary>
    public string LastCodeFor(string to)
    {
        lock (_sent)
        {
            var text = _sent.Last(m => m.To == to).Text;
            return new string(text.SkipWhile(c => !char.IsAsciiDigit(c)).TakeWhile(char.IsAsciiDigit).ToArray());
        }
    }
}

/// <summary>Testte "Development" ortamı (Log sağlayıcısına izin).</summary>
internal sealed class DevelopmentEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "BaseForge.Identity.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
