using BaseForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BaseForge.IntegrationTests.Support;

/// <summary>
/// Postgres tabanlı entegrasyon testlerinin ortak <see cref="BaseForgeDbContext"/> türevi.
/// Yalnızca outbox atomiklik testlerinin ihtiyaç duyduğu <see cref="Widgets"/> dışında ek bir şey
/// içermez — Outbox/Inbox/DLQ testleri bu DbSet'i hiç kullanmaz, sorun değil.
/// </summary>
internal sealed class TestDbContext(DbContextOptions options) : BaseForgeDbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();
}

/// <summary>Outbox atomiklik testlerinde kullanılan basit bir business entity'si.</summary>
internal sealed class Widget
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
}

/// <summary>Her Postgres entegrasyon test dosyasının ihtiyaç duyduğu, tek satırlık ortak yardımcılar.</summary>
internal static class TestDbContextFactory
{
    /// <summary>Paylaşılan container'daki ana bağlantı dizesinden, benzersiz (izole) bir DB adıyla yeni bir bağlantı dizesi üretir.</summary>
    /// <param name="baseConnectionString">Fixture'ın (örn. <see cref="Persistence.PostgresFixture"/>) paylaşılan container bağlantı dizesi.</param>
    /// <param name="prefix">Üretilen DB adının öneki (test dosyasını ayırt etmek için, örn. <c>"outbox"</c>, <c>"inbox"</c>).</param>
    public static string UniqueConnectionString(string baseConnectionString, string prefix)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = $"{prefix}_{Guid.NewGuid():N}",
        };
        return builder.ConnectionString;
    }

    /// <summary>Verilen bağlantı dizesiyle yeni bir <see cref="TestDbContext"/> açar ve şemayı (<c>EnsureCreated</c>) oluşturur.</summary>
    public static TestDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(connectionString).Options;
        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}

/// <summary>Entegrasyon testlerinde tekrar eden "koşul gerçekleşene kadar bekle" polling deseni.</summary>
internal static class PollingHelpers
{
    /// <summary>
    /// <paramref name="condition"/> <see langword="true"/> dönene kadar (veya zaman aşımına uğrayana
    /// kadar) 50ms aralıklarla bekler; zaman aşımında <c>Assert.True</c> ile başarısız olur.
    /// </summary>
    public static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (!await condition().ConfigureAwait(false) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        Assert.True(await condition().ConfigureAwait(false), "Beklenen koşul zaman aşımına uğradı.");
    }

    /// <summary>Senkron koşullar için — bkz. <see cref="WaitUntilAsync(Func{Task{bool}}, TimeSpan?)"/>.</summary>
    public static Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
        => WaitUntilAsync(() => Task.FromResult(condition()), timeout);
}
