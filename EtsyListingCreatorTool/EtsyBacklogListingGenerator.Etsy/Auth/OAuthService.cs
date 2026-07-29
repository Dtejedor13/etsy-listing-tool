using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace EtsyBacklogListingGenerator.Auth;

internal sealed class OAuthService
{
    private const string AuthorizationEndpoint = "https://www.etsy.com/oauth/connect";
    private const string TokenEndpoint = "https://api.etsy.com/v3/public/oauth/token";
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(1);

    private readonly HttpClient _http;
    private readonly TokenStore _tokenStore = new();
    private readonly string _clientId;
    private readonly Uri _redirectUri;
    private readonly string _scopes;

    public OAuthService(HttpClient http)
    {
        _http = http;
        _clientId = RequireEnvironmentVariable("ETSY_CLIENT_ID");
        _redirectUri = CreateRedirectUri(RequireEnvironmentVariable("ETSY_REDIRECT_URI"));
        _scopes = NormalizeScopes(
            Environment.GetEnvironmentVariable("ETSY_SCOPES") ?? "listings_r listings_w shops_r shops_w");
    }

    public async Task<OAuthToken> LoginAsync()
    {
        var storedToken = await _tokenStore.LoadAsync();
        if (storedToken is not null)
        {
            if (storedToken.ExpiresAt > DateTimeOffset.UtcNow.Add(ExpiryBuffer))
                return storedToken;

            if (!string.IsNullOrWhiteSpace(storedToken.RefreshToken))
                return await RefreshTokenAsync(storedToken);
        }

        return await AuthorizeAsync();
    }

    public Task<OAuthToken> GetTokenAsync() => LoginAsync();

    private async Task<OAuthToken> AuthorizeAsync()
    {
        var verifier = PkceGenerator.GenerateCodeVerifier();
        var state = PkceGenerator.GenerateState();
        var authorizationUri = BuildAuthorizationUri(state, verifier);
        var callbackReceived = new TaskCompletionSource<OAuthCallback>(TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options =>
            options.ListenLocalhost(_redirectUri.Port, listenOptions => listenOptions.UseHttps()));

        await using var callbackServer = builder.Build();
        callbackServer.MapGet(_redirectUri.AbsolutePath, async context =>
        {
            var query = context.Request.Query;
            callbackReceived.TrySetResult(new OAuthCallback(
                query["state"].ToString(),
                query["code"].ToString(),
                query["error"].ToString(),
                query["error_description"].ToString()));

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync("<html><body><p>Authorization received. You can return to the application.</p></body></html>");
        });

        await callbackServer.StartAsync();
        try
        {
            Console.WriteLine("Opening Etsy authorization in your browser...");
            Console.WriteLine($"If it does not open, visit: {authorizationUri}");
            Process.Start(new ProcessStartInfo(authorizationUri.AbsoluteUri) { UseShellExecute = true });

            var callback = await callbackReceived.Task;
            if (!string.Equals(callback.State, state, StringComparison.Ordinal))
                throw new InvalidOperationException("Etsy OAuth state did not match the authorization request.");

            if (!string.IsNullOrWhiteSpace(callback.Error))
                throw new InvalidOperationException($"Etsy OAuth error: {callback.Error}{(string.IsNullOrWhiteSpace(callback.ErrorDescription) ? string.Empty : $" - {callback.ErrorDescription}")}");

            if (string.IsNullOrWhiteSpace(callback.Code))
                throw new InvalidOperationException("Etsy OAuth callback did not include an authorization code.");

            var token = await ExchangeCodeForTokenAsync(callback.Code, verifier);
            await _tokenStore.SaveAsync(token);
            return token;
        }
        finally
        {
            await callbackServer.StopAsync();
        }
    }

    private async Task<OAuthToken> RefreshTokenAsync(OAuthToken token)
    {
        var refreshedToken = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _clientId,
            ["refresh_token"] = token.RefreshToken
        });

        await _tokenStore.SaveAsync(refreshedToken);
        return refreshedToken;
    }

    private Task<OAuthToken> ExchangeCodeForTokenAsync(string code, string verifier) =>
        RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _clientId,
            ["redirect_uri"] = _redirectUri.AbsoluteUri,
            ["code"] = code,
            ["code_verifier"] = verifier
        });

    private async Task<OAuthToken> RequestTokenAsync(Dictionary<string, string> formValues)
    {
        using var response = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(formValues));
        var responseContent = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Etsy token request failed ({(int)response.StatusCode}): {responseContent}");

        var payload = JsonSerializer.Deserialize<TokenResponse>(responseContent)
            ?? throw new InvalidOperationException("Etsy returned an empty token response.");

        if (string.IsNullOrWhiteSpace(payload.AccessToken) || string.IsNullOrWhiteSpace(payload.RefreshToken))
            throw new InvalidOperationException("Etsy returned a token response without an access or refresh token.");

        return new OAuthToken
        {
            AccessToken = payload.AccessToken,
            RefreshToken = payload.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn)
        };
    }

    private Uri BuildAuthorizationUri(string state, string verifier)
    {
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _clientId,
            ["redirect_uri"] = _redirectUri.AbsoluteUri,
            ["scope"] = _scopes,
            ["state"] = state,
            ["code_challenge"] = PkceGenerator.GenerateCodeChallenge(verifier),
            ["code_challenge_method"] = "S256"
        };

        var query = string.Join("&", parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new Uri($"{AuthorizationEndpoint}?{query}");
    }

    private static Uri CreateRedirectUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var redirectUri) ||
            !redirectUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !redirectUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            redirectUri.IsDefaultPort)
        {
            throw new InvalidOperationException("ETSY_REDIRECT_URI must be an HTTPS localhost URL with an explicit port, for example https://localhost:8080/oauth/callback/.");
        }

        return redirectUri;
    }

    private static string RequireEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set the {name} environment variable before authenticating with Etsy.");

    private static string NormalizeScopes(string scopes) =>
        string.Join(
            ' ',
            scopes.Replace(',', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal));

    private sealed record OAuthCallback(string State, string Code, string Error, string ErrorDescription);

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
