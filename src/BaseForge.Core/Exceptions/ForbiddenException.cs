namespace BaseForge.Core.Exceptions;

/// <summary>
/// Çağıran kimliği doğrulanmış olsa bile istenen işlemi yapmaya yetkili değilse fırlatılır
/// (örn. başkasına ait bir kaydı güncellemeye çalışmak). API katmanında HTTP 403'e karşılık gelir.
/// </summary>
public sealed class ForbiddenException : BaseException
{
    private const string DefaultErrorCode = "access.forbidden";

    /// <summary>Serbest bir mesajla yeni bir <see cref="ForbiddenException"/> oluşturur.</summary>
    /// <param name="message">Açıklama.</param>
    public ForbiddenException(string message)
        : base(DefaultErrorCode, message)
    {
    }

    /// <summary>Entity adı ve anahtarından okunabilir bir mesaj üretir.</summary>
    /// <param name="entityName">Erişilmeye çalışılan entity'nin adı (örn. <c>"Order"</c>).</param>
    /// <param name="key">Kaydın anahtar değeri.</param>
    public ForbiddenException(string entityName, object key)
        : base(DefaultErrorCode, $"'{entityName}' ({key}) kaydı üzerinde bu işlem için yetkiniz yok.")
    {
    }
}
