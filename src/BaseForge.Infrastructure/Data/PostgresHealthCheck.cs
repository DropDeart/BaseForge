using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace BaseForge.Infrastructure.Data;

/// <summary>
/// PostgreSQL bağlantısını <c>SELECT 1</c> ile doğrulayan health check. Servisin zaten kullandığı
/// EF Core connection string'i üzerinden, ayrı bir bağımlılık (ör. AspNetCore.HealthChecks.NpgSql)
/// eklemeden çalışır.
/// </summary>
public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    /// <param name="connectionString">PostgreSQL bağlantı dizesi (<c>UsePostgreSQL</c>'e verilenle aynı).</param>
    public PostgresHealthCheck(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL'e bağlanılamadı.", ex);
        }
    }
}
