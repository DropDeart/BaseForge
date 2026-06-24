namespace BaseForge.Core.Interfaces;

/// <summary>
/// O anki istek bağlamındaki kullanıcıyı temsil eder. Implementasyonu API katmanında
/// (örn. <c>HttpContext</c> / JWT claim'lerinden) sağlanır; audit alanlarını doldurmak
/// ve yetkilendirme için kullanılır.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Kullanıcının kimliği; kimlik doğrulanmamışsa <see langword="null"/>.</summary>
    string? UserId { get; }

    /// <summary>İstek kimliği doğrulanmış bir kullanıcıya aitse <see langword="true"/>.</summary>
    bool IsAuthenticated { get; }
}
