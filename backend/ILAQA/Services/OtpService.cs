using System.Security.Cryptography;
using System.Text;

namespace ILAQA.Services;

// Six-digit email codes. Only an HMAC of the code is stored, keyed with a
// server secret (Otp:SecretKey) and bound to one account or email.
public sealed class OtpService(IConfiguration configuration)
{
    private readonly byte[] _secretKey = LoadSecretKey(configuration);

    // A cryptographically secure code, 000000 to 999999.
    public string GenerateOtp() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    // ownerKey binds the code to one account (or email, or purpose).
    public string HashOtp(string ownerKey, string otp) =>
        Convert.ToHexString(HMACSHA256.HashData(_secretKey, Encoding.UTF8.GetBytes($"{ownerKey}:{otp}")));

    // Constant-time compare, so timing does not leak the stored hash.
    public bool VerifyOtp(string ownerKey, string submittedOtp, string storedHash)
    {
        if (submittedOtp.Length != 6 || !submittedOtp.All(char.IsAsciiDigit))
            return false;

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(HashOtp(ownerKey, submittedOtp)));
    }

    private static byte[] LoadSecretKey(IConfiguration configuration)
    {
        var secret = configuration["Otp:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("OTP secret key is missing.");

        var key = Convert.FromBase64String(secret);
        return key.Length >= 32 ? key : throw new InvalidOperationException("OTP secret key must be at least 32 bytes.");
    }
}
