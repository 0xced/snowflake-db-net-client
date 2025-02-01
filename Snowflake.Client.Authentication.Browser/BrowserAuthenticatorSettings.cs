using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Snowflake.Client.Json;
using Snowflake.Client.Model;

namespace Snowflake.Client;

/// <summary>
/// Snowflake client settings for authenticating through a web browser.
/// Neither a user nor a password are required in the <see cref="AuthInfo"/>.
/// </summary>
public class BrowserAuthenticatorSettings : SnowflakeClientSettings
{
    private readonly BrowserAuthenticator _authenticator;

    public BrowserAuthenticatorSettings(AuthInfo authInfo, SessionInfo? sessionInfo = null, UrlInfo? urlInfo = null,
        JsonSerializerOptions? jsonMapperOptions = null, ChunksDownloaderOptions? chunksDownloaderOptions = null,
        bool downloadChunksForQueryRawResponses = false)
        : base(authInfo, sessionInfo, urlInfo, jsonMapperOptions, chunksDownloaderOptions, downloadChunksForQueryRawResponses)
    {
        _authenticator = new BrowserAuthenticator(UrlInfo);
    }

    /// <summary>
    /// Time to wait for authentication in the default browser before failing with <see cref="BrowserException"/>.
    /// Default value: 1 minute.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(1);

    protected override async Task<LoginRequestData> GetLoginRequestDataAsync(CancellationToken ct)
    {
        string token;
        string proofKey;

        try
        {
            (token, proofKey) = await _authenticator.AuthenticateAsync(Timeout, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not (BrowserException or OperationCanceledException))
        {
            throw new BrowserException("Browser authentication failed.", exception);
        }

        var loginRequestData = new LoginRequestData
        {
            Authenticator = "externalbrowser",
            Token = token,
            ProofKey = proofKey,
        };
        return loginRequestData;
    }
}