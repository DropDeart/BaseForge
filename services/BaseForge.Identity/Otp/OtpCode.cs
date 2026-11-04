using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BaseForge.Identity.Otp;

/// <summary>Kod üretimi ve tuzlu hash doğrulaması.</summary>
public static class OtpCode
{
    /// <summary>Kriptografik rastgele, baştaki sıfırları korunmuş kod.</summary>
    public static string Generate(int length)
        => RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, length)).ToString(CultureInfo.InvariantCulture).PadLeft(length, '0');

    /// <summary>"tuz:hash" biçiminde saklanacak değer.</summary>
    public static string Hash(string code)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(Digest(salt, code));
    }

    /// <summary>Sabit zamanlı karşılaştırma.</summary>
    public static bool Matches(string stored, string code)
    {
        var parts = stored.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[0]);
        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(parts[1]), Digest(salt, code.Trim()));
    }

    private static byte[] Digest(byte[] salt, string code) => HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(code));
}
