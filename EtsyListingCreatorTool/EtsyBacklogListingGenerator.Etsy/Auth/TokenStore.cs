using System.Text.Json;

namespace EtsyBacklogListingGenerator.Auth
{
    public class TokenStore
    {
        private static readonly string TokenFile = Path.Combine(AppContext.BaseDirectory, "Data", "token.json");

        public async Task SaveAsync(OAuthToken token)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TokenFile)!);

            var json = JsonSerializer.Serialize(token,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            await File.WriteAllTextAsync(TokenFile, json);
        }

        public async Task<OAuthToken?> LoadAsync()
        {
            if (!File.Exists(TokenFile))
                return null;

            var json = await File.ReadAllTextAsync(TokenFile);

            return JsonSerializer.Deserialize<OAuthToken>(json);
        }
    }
}
