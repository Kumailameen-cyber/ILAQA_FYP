
using System.Security.Cryptography;
using System.Text;

namespace ILAQA.Services
{
    public class OtpService
    {
        private readonly byte[] _secretKey;

        public OtpService(IConfiguration configuration)
        {
            var secret = configuration["Otp:SecretKey"];

            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new InvalidOperationException(
                    "OTP secret key is missing.");
            }

            _secretKey = Convert.FromBase64String(secret);

            if (_secretKey.Length < 32)
            {
                throw new InvalidOperationException(
                    "OTP secret key must be at least 32 bytes.");
            }
        }

        // Generate a cryptographically secure six-digit OTP.
        public string GenerateOtp()
        {
            int number = RandomNumberGenerator.GetInt32(
                0, 1_000_000);

            return number.ToString("D6");
        }

        // Protect the OTP using a server-side secret.
        // Including the user ID binds the code to one account.
        public string HashOtp(string userId, string otp)
        {
            var message = Encoding.UTF8.GetBytes(
                $"{userId}:{otp}");

            var hash = HMACSHA256.HashData(
                _secretKey, message);

            return Convert.ToHexString(hash);
        }

        // Compare the submitted OTP securely.
        public bool VerifyOtp(
            string userId,
            string submittedOtp,
            string storedHash)
        {
            if (submittedOtp.Length != 6 ||
                !submittedOtp.All(char.IsAsciiDigit))
            {
                return false;
            }

            var submittedHash = HashOtp(
                userId, submittedOtp);

            byte[] expectedBytes;

            try
            {
                expectedBytes =
                    Convert.FromHexString(storedHash);
            }
            catch (FormatException)
            {
                return false;
            }

            var actualBytes =
                Convert.FromHexString(submittedHash);

            return CryptographicOperations.FixedTimeEquals(
                expectedBytes, actualBytes);
        }
    }
}
