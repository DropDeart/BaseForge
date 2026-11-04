using BaseForge.Identity.Contacts;
using BaseForge.Identity.ErrorHandling;
using BaseForge.Identity.Tenancy;

namespace BaseForge.Identity.Tests;

/// <summary>Şirket ve üyelik durum geçişleri, metin normalizasyonu (veritabanısız).</summary>
public class TenancyDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TenantStatus.Pending, TenantStatus.Active)]
    [InlineData(TenantStatus.Pending, TenantStatus.Rejected)]
    [InlineData(TenantStatus.Active, TenantStatus.Suspended)]
    [InlineData(TenantStatus.Suspended, TenantStatus.Active)]
    public void Tenant_AllowedReview_Changes(TenantStatus from, TenantStatus to)
    {
        var tenant = new Tenant { Status = from };
        tenant.Review(to, Guid.NewGuid(), "not", Now);

        Assert.Equal(to, tenant.Status);
        Assert.Equal(Now, tenant.ReviewedAt);
    }

    [Theory]
    [InlineData(TenantStatus.Pending, TenantStatus.Suspended)]
    [InlineData(TenantStatus.Active, TenantStatus.Rejected)]
    [InlineData(TenantStatus.Active, TenantStatus.Active)]
    public void Tenant_InvalidReview_Throws(TenantStatus from, TenantStatus to)
        => Assert.Throws<IdentityException>(() => new Tenant { Status = from }.Review(to, Guid.NewGuid(), null, Now));

    [Fact]
    public void Membership_InvitationFlow()
    {
        var invited = new TenantMembership { Status = MembershipStatus.Invited };
        invited.Accept(Now);
        Assert.Equal(MembershipStatus.Active, invited.Status);
        Assert.Throws<IdentityException>(() => invited.Accept(Now));   // ikinci kez kabul edilemez

        var declined = new TenantMembership { Status = MembershipStatus.Invited };
        declined.Decline(Now);
        Assert.Equal(MembershipStatus.Declined, declined.Status);
    }

    [Fact]
    public void Membership_JoinRequestFlow()
    {
        var request = new TenantMembership { Status = MembershipStatus.Requested };
        request.Approve(["Operator"], Now);
        Assert.Equal(MembershipStatus.Active, request.Status);
        Assert.Equal(["Operator"], request.Roles);

        Assert.Throws<IdentityException>(() => new TenantMembership { Status = MembershipStatus.Invited }.Approve(["Operator"], Now));
    }

    [Fact]
    public void Membership_Remove_ClearsDefault_AndOnlyOnce()
    {
        var membership = new TenantMembership { Status = MembershipStatus.Active, IsDefault = true };
        membership.Remove(Now);

        Assert.Equal(MembershipStatus.Removed, membership.Status);
        Assert.False(membership.IsDefault);
        Assert.Throws<IdentityException>(() => membership.Remove(Now));
    }

    [Theory]
    [InlineData("Öz Kantar A.Ş.", "oz-kantar-a-s")]
    [InlineData("  IŞIK  Nakliyat & Hafriyat ", "isik-nakliyat-hafriyat")]
    [InlineData("Çağrı Gümüş", "cagri-gumus")]
    public void Slug_IsAsciiAndHyphenated(string name, string expected)
        => Assert.Equal(expected, TenancyText.Slug(name));

    [Theory]
    [InlineData("+90 (555) 111 22 33", "+905551112233")]
    [InlineData("0555 111 22 33", "+905551112233")]
    [InlineData("555 111 22 33", "+905551112233")]
    [InlineData("0049 151 2345 6789", "+4915123456789")]
    [InlineData("12", null)]
    [InlineData("  ", null)]
    public void Phone_IsE164(string input, string? expected)
        => Assert.Equal(expected, ContactNormalizer.Phone(input));
}
