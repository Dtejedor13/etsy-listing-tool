using System.Security.Cryptography;
using System.Text;

namespace EtsyBacklogListingGenerator.Auth
{
    public static class PkceGenerator
    {
        public static string GenerateCodeVerifier()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);

            return Base64UrlEncode(bytes);
        }

        public static string GenerateCodeChallenge(string verifier)
        {
            var hash = SHA256.HashData(
                Encoding.ASCII.GetBytes(verifier));

            return Base64UrlEncode(hash);
        }

        public static string GenerateState()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
