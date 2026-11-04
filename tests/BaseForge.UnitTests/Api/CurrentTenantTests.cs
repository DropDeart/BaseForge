using System.Security.Claims;
using BaseForge.API.Authentication;
using BaseForge.Core.MultiTenancy;
using Microsoft.AspNetCore.Http;

namespace BaseForge.UnitTests.Api;

/// <summary>Kiracı çözümleme sırası ve x-tenant-id başlığının yalnızca servis token'ında kabulü (ARCH §5.5).</summary>
public class CurrentTenantTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static CurrentTenant Create(IEnumerable<Claim> claims, string? tenantHeader = null)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        if (tenantHeader is not null)
        {
            context.Request.Headers[BaseForgeClaims.TenantIdHeader] = tenantHeader;
        }

        return new CurrentTenant(new HttpContextAccessor { HttpContext = context });
    }

    [Fact]
    public void UserClaim_Wins()
        => Assert.Equal(TenantA, Create([new Claim(BaseForgeClaims.TenantId, TenantA.ToString())], TenantB.ToString()).TenantId);

    [Fact]
    public void Header_WithUserToken_IsIgnored()
        => Assert.Null(Create([new Claim("sub", "user-1")], TenantB.ToString()).TenantId);

    [Fact]
    public void Header_WithServiceToken_IsAccepted()
        => Assert.Equal(TenantB, Create([new Claim(BaseForgeClaims.ServiceToken, "1")], TenantB.ToString()).TenantId);

    [Fact]
    public void AmbientScope_IsUsedWhenNoClaim()
    {
        using (TenantScope.Enter(TenantA))
        {
            Assert.Equal(TenantA, Create([]).TenantId);
        }

        Assert.Null(TenantScope.Current);
    }
}
