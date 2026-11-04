using OpenIddict.Abstractions;

namespace BaseForge.Identity.Accounts;

/// <summary>Kullanıcının açık oturumlarını (refresh token'larını) iptal eder; yeni token için tekrar giriş gerekir.</summary>
public interface ISessionRevoker
{
    /// <summary>Kullanıcıların tüm token'larını iptal eder. Kısa ömürlü access token süresi dolana kadar geçerli kalır.</summary>
    Task RevokeAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
}

/// <inheritdoc cref="ISessionRevoker" />
internal sealed class SessionRevoker(IOpenIddictTokenManager tokens) : ISessionRevoker
{
    public async Task RevokeAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        foreach (var userId in userIds.Distinct())
        {
            await tokens.RevokeBySubjectAsync(userId.ToString(), cancellationToken);
        }
    }
}
