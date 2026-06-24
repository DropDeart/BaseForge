namespace BaseForge.Core.Exceptions;

/// <summary>
/// Bir veya daha fazla doğrulama (validation) kuralı ihlal edildiğinde fırlatılır.
/// API katmanında HTTP 400'e karşılık gelir. <see cref="Errors"/>, alan başına hata
/// mesajlarını taşır.
/// </summary>
public sealed class ValidationException : BaseException
{
    private const string DefaultErrorCode = "validation.failed";
    private const string DefaultMessage = "Bir veya daha fazla doğrulama hatası oluştu.";

    /// <summary>Alan bazlı hatalarla yeni bir <see cref="ValidationException"/> oluşturur.</summary>
    /// <param name="errors">Alan adından o alana ait hata mesajlarına eşleme.</param>
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(DefaultErrorCode, DefaultMessage) => Errors = errors;

    /// <summary>Tek bir alan ve mesajla yeni bir <see cref="ValidationException"/> oluşturur.</summary>
    /// <param name="field">Hatalı alanın adı.</param>
    /// <param name="error">Hata mesajı.</param>
    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] })
    {
    }

    /// <summary>Alan adından o alana ait hata mesajlarına eşleme.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
