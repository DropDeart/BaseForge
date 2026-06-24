namespace BaseForge.Core.Exceptions;

/// <summary>
/// İstenen bir kayıt bulunamadığında fırlatılır. API katmanında HTTP 404'e karşılık gelir.
/// </summary>
public sealed class NotFoundException : BaseException
{
    private const string DefaultErrorCode = "resource.not_found";

    /// <summary>Serbest bir mesajla yeni bir <see cref="NotFoundException"/> oluşturur.</summary>
    /// <param name="message">Açıklama.</param>
    public NotFoundException(string message)
        : base(DefaultErrorCode, message)
    {
    }

    /// <summary>Entity adı ve anahtarından okunabilir bir mesaj üretir.</summary>
    /// <param name="entityName">Bulunamayan entity'nin adı (örn. <c>"User"</c>).</param>
    /// <param name="key">Aranan anahtar değeri.</param>
    public NotFoundException(string entityName, object key)
        : base(DefaultErrorCode, $"'{entityName}' ({key}) kaydı bulunamadı.")
    {
    }
}
