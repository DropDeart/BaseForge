using BaseForge.Identity.Configuration;
using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using BaseForge.Identity.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace BaseForge.Identity.Tests;

/// <summary>RUN_DB_TESTS=1 iken gerçek Postgres; Identity + kiracı servisleri DI ile kurulur.</summary>
public sealed class TenancyFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private ServiceProvider? _provider;

    /// <summary>Test için kiracı ayarları.</summary>
    public static AuthOptions Options { get; } = new()
    {
        Roles = ["SuperAdmin", "TenantAdmin", "Operator", "Driver"],
        MultiTenancy = new MultiTenancyOptions
        {
            Enabled = true,
            AdminRole = "TenantAdmin",
            MemberRoles = ["Operator"],
            RequireApproval = true,
            ApproverRoles = ["SuperAdmin"],
        },
    };

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
        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
        await db.Database.EnsureCreatedAsync();
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
