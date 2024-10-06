using System.Security.Cryptography;
using System.Text;

namespace Audkenning.Utils
{
    public class HashUtil
    {
        public static string GenerateSHA512Hash(string input)
        {
            using (var sha512 = SHA512.Create())
            {
                byte[] hashBytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hashBytes);
            }
        }

        public static int CalculateVerificationCode(string base64Hash)
        {
            using (var sha256 = SHA256.Create())
            {
                // Compute the SHA256 hash
                byte[] sha256HashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(base64Hash));

                // Extract the last two bytes and convert them to an unsigned integer
                int lastTwoBytes = (sha256HashBytes[sha256HashBytes.Length - 2] << 8) + sha256HashBytes[sha256HashBytes.Length - 1];

                // Calculate verification code
                return lastTwoBytes % 10000;
            }
        }

        public static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            var randomString = new char[length];

            for (int i = 0; i < length; i++)
            {
                randomString[i] = chars[random.Next(chars.Length)];
            }

            return new string(randomString);
        }
    }
}
