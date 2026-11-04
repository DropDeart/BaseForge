using BaseForge.Identity.Otp;

namespace BaseForge.Identity.Tests;

/// <summary>Kod üretimi ve hash doğrulaması.</summary>
public class OtpCodeTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void Generate_HasFixedLengthDigits(int length)
    {
        for (var i = 0; i < 50; i++)
        {
            var code = OtpCode.Generate(length);
            Assert.Equal(length, code.Length);
            Assert.True(code.All(char.IsAsciiDigit));
        }
    }

    [Fact]
    public void Hash_IsSalted_AndMatchesOnlyTheCode()
    {
        var first = OtpCode.Hash("123456");
        var second = OtpCode.Hash("123456");

        Assert.NotEqual(first, second);   // tuz farklı
        Assert.True(OtpCode.Matches(first, "123456"));
        Assert.True(OtpCode.Matches(first, " 123456 "));
        Assert.False(OtpCode.Matches(first, "123457"));
        Assert.False(OtpCode.Matches("bozuk", "123456"));
    }
}
