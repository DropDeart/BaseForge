namespace BaseForge.Core.Exceptions;

/// <summary>
/// BaseForge tabanlı tüm uygulama (domain) istisnalarının temel sınıfı. API katmanındaki
/// exception middleware bu tipi tanıyıp uygun HTTP yanıtına dönüştürür.
/// </summary>
public abstract class BaseException : Exception
{
    /// <summary>İstisnayı makinece okunabilir bir hata koduyla başlatır.</summary>
    /// <param name="errorCode">Hata kodu (örn. <c>"user.not_found"</c>).</param>
    /// <param name="message">İnsan tarafından okunabilir açıklama.</param>
    protected BaseException(string errorCode, string message)
        : base(message) => ErrorCode = errorCode;

    /// <summary>İstisnayı bir hata kodu ve sarmalanan istisnayla başlatır.</summary>
    /// <param name="errorCode">Hata kodu.</param>
    /// <param name="message">İnsan tarafından okunabilir açıklama.</param>
    /// <param name="innerException">Sarmalanan asıl istisna.</param>
    protected BaseException(string errorCode, string message, Exception innerException)
        : base(message, innerException) => ErrorCode = errorCode;

    /// <summary>Makinece okunabilir hata kodu.</summary>
    public string ErrorCode { get; }
}
