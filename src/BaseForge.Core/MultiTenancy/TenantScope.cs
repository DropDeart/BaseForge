namespace BaseForge.Core.MultiTenancy;

/// <summary>
/// HTTP isteği dışındaki kod yolları (RabbitMQ tüketicisi, arka plan işleri) için ortam (ambient) kiracı bağlamı.
/// <see cref="System.Threading.AsyncLocal{T}"/> tabanlıdır; <c>CurrentTenant</c> token'da <c>tenant_id</c> yoksa
/// buraya bakar. Bkz. docs/ARCH.md §5.5.
/// </summary>
/// <example>
/// <code>
/// using (TenantScope.Enter(envelope.TenantId))
/// {
///     await mediator.Publish(evt, ct); // handler'lar bu kiracının verisini görür/yazar
/// }
/// </code>
/// </example>
public static class TenantScope
{
    private static readonly AsyncLocal<Guid?> Ambient = new();

    /// <summary>O anki ortam kiracısı; kapsam açılmamışsa <see langword="null"/>.</summary>
    public static Guid? Current => Ambient.Value;

    /// <summary>Kapsam boyunca ortam kiracısını <paramref name="tenantId"/> yapar; dispose edilince öncekine döner.</summary>
    public static IDisposable Enter(Guid? tenantId)
    {
        var previous = Ambient.Value;
        Ambient.Value = tenantId;
        return new Restore(previous);
    }

    private sealed class Restore(Guid? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Ambient.Value = previous;
            _disposed = true;
        }
    }
}
