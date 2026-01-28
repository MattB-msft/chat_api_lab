using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Identity.Client;
using AgentOrchestrator.Models;

namespace AgentOrchestrator.Auth;

public interface ITokenService
{
    Task<string> GetAccessTokenAsync(string sessionId);
    Task<AuthenticationResult> AcquireTokenByAuthorizationCodeAsync(string code, string sessionId);
    void ClearTokenCache(string sessionId);
    Task<string> BuildAuthorizationUrlAsync(string state);
    Task<string> ExchangeTeamsSsoTokenAsync(string ssoToken);
}

/// <summary>
/// Token management service using Microsoft Authentication Library (MSAL).
///
/// MSAL CONCEPTS:
/// - ConfidentialClientApplication: Server-side apps with secure secret storage
/// - AcquireTokenByAuthorizationCode: Exchange OAuth code for tokens
/// - AcquireTokenSilent: Get cached token or refresh automatically
///
/// SECURITY FEATURES:
/// - Tokens encrypted at rest using AES-256
/// - Automatic session cleanup via TTL
/// - Thread-safe token refresh with locking
///
/// See: https://learn.microsoft.com/azure/active-directory/develop/msal-overview
/// </summary>
public class TokenService : ITokenService, IDisposable
{
    private readonly IConfidentialClientApplication _msalClient;
    private readonly AzureAdSettings _settings;
    private readonly ILogger<TokenService> _logger;
    private readonly Timer _cleanupTimer;
    private readonly byte[] _encryptionKey;
    private bool _disposed;

    // Token cache entry with expiration tracking
    private sealed class CacheEntry
    {
        public required byte[] EncryptedToken { get; init; }
        public required byte[] IV { get; init; }
        public required DateTimeOffset ExpiresOn { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public string? AccountIdentifier { get; init; }
    }

    // Thread-safe token cache with encrypted entries
    private readonly ConcurrentDictionary<string, CacheEntry> _tokenCache = new();
    
    // Semaphore for preventing concurrent token refresh for the same session
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new();

    // Session TTL: remove entries older than this even if token hasn't expired
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(8);
    
    // Cleanup interval
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(15);

    public TokenService(AzureAdSettings settings, ILogger<TokenService> logger)
    {
        _settings = settings;
        _logger = logger;

        // Generate encryption key from client secret (in production, use Azure Key Vault)
        // This ensures tokens are encrypted at rest in memory
        _encryptionKey = DeriveEncryptionKey(settings.ClientSecret);

        _msalClient = ConfidentialClientApplicationBuilder
            .Create(settings.ClientId)
            .WithClientSecret(settings.ClientSecret)
            .WithAuthority($"{settings.Instance}{settings.TenantId}")
            .WithRedirectUri(settings.RedirectUri)
            .Build();

        // Start background cleanup timer
        _cleanupTimer = new Timer(CleanupExpiredSessions, null, CleanupInterval, CleanupInterval);
    }

    /// <summary>
    /// Derive a 256-bit encryption key from the client secret using PBKDF2.
    /// </summary>
    private static byte[] DeriveEncryptionKey(string secret)
    {
        // Use a fixed salt (in production, store this securely)
        var salt = "AgentOrchestratorTokenEncryption"u8.ToArray();
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(secret),
            salt,
            iterations: 100000,
            HashAlgorithmName.SHA256,
            outputLength: 32);
    }

    /// <summary>
    /// Encrypt a token using AES-256-CBC.
    /// </summary>
    private (byte[] encrypted, byte[] iv) EncryptToken(string token)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor();
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var encrypted = encryptor.TransformFinalBlock(tokenBytes, 0, tokenBytes.Length);
        
        return (encrypted, aes.IV);
    }

    /// <summary>
    /// Decrypt a token using AES-256-CBC.
    /// </summary>
    private string DecryptToken(byte[] encrypted, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = iv;
        
        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        
        return Encoding.UTF8.GetString(decrypted);
    }

    /// <summary>
    /// Background task to cleanup expired sessions.
    /// </summary>
    private void CleanupExpiredSessions(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredKeys = new List<string>();

        foreach (var kvp in _tokenCache)
        {
            // Remove if token expired or session TTL exceeded
            if (kvp.Value.ExpiresOn < now || kvp.Value.CreatedAt.Add(SessionTtl) < now)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            _tokenCache.TryRemove(key, out _);
            if (_refreshLocks.TryRemove(key, out var semaphore))
            {
                semaphore.Dispose();
            }
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired token cache entries", expiredKeys.Count);
        }
    }

    public async Task<string> BuildAuthorizationUrlAsync(string state)
    {
        var scopes = _settings.Scopes;

        var authUrl = await _msalClient
            .GetAuthorizationRequestUrl(scopes)
            .WithExtraQueryParameters($"state={Uri.EscapeDataString(state)}")
            .ExecuteAsync();

        return authUrl.ToString();
    }

    public async Task<AuthenticationResult> AcquireTokenByAuthorizationCodeAsync(string code, string sessionId)
    {
        var scopes = _settings.Scopes;

        var result = await _msalClient
            .AcquireTokenByAuthorizationCode(scopes, code)
            .ExecuteAsync();

        // Encrypt and store token
        var (encrypted, iv) = EncryptToken(result.AccessToken);
        _tokenCache[sessionId] = new CacheEntry
        {
            EncryptedToken = encrypted,
            IV = iv,
            ExpiresOn = result.ExpiresOn,
            CreatedAt = DateTimeOffset.UtcNow,
            AccountIdentifier = result.Account?.HomeAccountId?.Identifier
        };

        _logger.LogInformation("Token acquired for session {SessionId}", sessionId);
        return result;
    }

    public async Task<string> GetAccessTokenAsync(string sessionId)
    {
        if (!_tokenCache.TryGetValue(sessionId, out var cacheEntry))
        {
            throw new InvalidOperationException("No token found for session. Please login first.");
        }

        // Check if token is expired or about to expire (within 5 minutes)
        if (cacheEntry.ExpiresOn < DateTimeOffset.UtcNow.AddMinutes(5))
        {
            _logger.LogInformation("Token expired or expiring soon, attempting refresh for session {SessionId}", sessionId);

            // Get or create a semaphore for this session to prevent concurrent refresh
            var refreshLock = _refreshLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
            
            await refreshLock.WaitAsync();
            try
            {
                // Double-check after acquiring lock (another thread may have refreshed)
                if (_tokenCache.TryGetValue(sessionId, out var updatedEntry) && 
                    updatedEntry.ExpiresOn >= DateTimeOffset.UtcNow.AddMinutes(5))
                {
                    return DecryptToken(updatedEntry.EncryptedToken, updatedEntry.IV);
                }

                // Use stored account identifier instead of deprecated GetAccountsAsync()
                if (!string.IsNullOrEmpty(cacheEntry.AccountIdentifier))
                {
                    var account = await _msalClient.GetAccountAsync(cacheEntry.AccountIdentifier);

                    if (account != null)
                    {
                        var result = await _msalClient
                            .AcquireTokenSilent(_settings.Scopes, account)
                            .ExecuteAsync();

                        // Encrypt and store refreshed token
                        var (encrypted, iv) = EncryptToken(result.AccessToken);
                        _tokenCache[sessionId] = new CacheEntry
                        {
                            EncryptedToken = encrypted,
                            IV = iv,
                            ExpiresOn = result.ExpiresOn,
                            CreatedAt = cacheEntry.CreatedAt, // Keep original creation time
                            AccountIdentifier = result.Account?.HomeAccountId?.Identifier
                        };
                        
                        return result.AccessToken;
                    }
                }

                _logger.LogWarning("No account identifier found for session {SessionId}", sessionId);
            }
            catch (MsalUiRequiredException)
            {
                _logger.LogWarning("Silent token acquisition failed, user needs to re-authenticate");
                ClearTokenCache(sessionId);
                throw new InvalidOperationException("Session expired. Please login again.");
            }
            finally
            {
                refreshLock.Release();
            }
        }

        return DecryptToken(cacheEntry.EncryptedToken, cacheEntry.IV);
    }

    public void ClearTokenCache(string sessionId)
    {
        _tokenCache.TryRemove(sessionId, out _);
        if (_refreshLocks.TryRemove(sessionId, out var semaphore))
        {
            semaphore.Dispose();
        }

        _logger.LogInformation("Token cache cleared for session {SessionId}", sessionId);
    }

    /// <summary>
    /// Exchange a Teams SSO token for a Microsoft Graph access token using On-Behalf-Of (OBO) flow.
    ///
    /// TEAMS SSO FLOW:
    /// 1. Teams client gets an SSO token (id_token) for the bot's app
    /// 2. Bot exchanges this token for an access token with Graph scopes using OBO
    /// 3. Bot uses the access token to call Microsoft Graph on behalf of the user
    ///
    /// See: https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/authentication/auth-aad-sso-bots
    /// </summary>
    public async Task<string> ExchangeTeamsSsoTokenAsync(string ssoToken)
    {
        try
        {
            // Use On-Behalf-Of flow to exchange the Teams SSO token for a Graph access token
            var result = await _msalClient
                .AcquireTokenOnBehalfOf(_settings.Scopes, new UserAssertion(ssoToken))
                .ExecuteAsync();

            _logger.LogInformation("Teams SSO token exchanged successfully for user {User}", result.Account?.Username);
            return result.AccessToken;
        }
        catch (MsalUiRequiredException ex)
        {
            _logger.LogWarning(ex, "Teams SSO token exchange requires consent");
            throw new UnauthorizedAccessException(
                "Additional consent required. Please grant permissions in the Teams app settings.", ex);
        }
        catch (MsalServiceException ex) when (ex.ErrorCode == "invalid_grant")
        {
            _logger.LogWarning(ex, "Teams SSO token is invalid or expired");
            throw new UnauthorizedAccessException("Teams authentication failed. Please try again.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _cleanupTimer.Dispose();
        foreach (var semaphore in _refreshLocks.Values)
        {
            semaphore.Dispose();
        }
        _refreshLocks.Clear();
        _tokenCache.Clear();
        
        // Clear encryption key from memory
        Array.Clear(_encryptionKey, 0, _encryptionKey.Length);
        
        _disposed = true;
    }
}
