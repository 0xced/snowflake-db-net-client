using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Snowflake.Client.Json;
using Snowflake.Client.Model;

namespace Snowflake.Client;

/// <summary>
/// Snowflake client settings for authenticating through OAuth.
/// </summary>
public class OAuthAuthenticatorSettings : SnowflakeClientSettings
{
    private readonly OAuthInfo _authInfo;

    public OAuthAuthenticatorSettings(OAuthInfo authInfo, SessionInfo? sessionInfo = null, UrlInfo? urlInfo = null,
        JsonSerializerOptions? jsonMapperOptions = null, ChunksDownloaderOptions? chunksDownloaderOptions = null,
        bool downloadChunksForQueryRawResponses = false)
        : base(authInfo, sessionInfo, urlInfo, jsonMapperOptions, chunksDownloaderOptions, downloadChunksForQueryRawResponses)
    {
        _authInfo = authInfo;
    }

    protected override async Task<LoginRequestData> GetLoginRequestDataAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_authInfo.ClientId))
            throw new OAuthException($"The client id must be specified in the {nameof(Model.AuthInfo)}.");

        if (string.IsNullOrEmpty(_authInfo.ClientSecret))
            throw new OAuthException($"The client secret must be specified in the {nameof(Model.AuthInfo)}.");

        if (string.IsNullOrEmpty(_authInfo.Authority))
            throw new OAuthException($"The authority must be specified in the {nameof(Model.AuthInfo)}.");

        string token;

        try
        {
            var app = ConfidentialClientApplicationBuilder.Create(_authInfo.ClientId)
                .WithClientSecret(_authInfo.ClientSecret)
                .WithAuthority(_authInfo.Authority)
                .Build();

            var result = await app.AcquireTokenForClient(_authInfo.Scopes).ExecuteAsync(ct).ConfigureAwait(false);
            token = result.AccessToken;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new OAuthException("OAuth authentication failed.", exception);
        }

        var loginRequestData = new LoginRequestData
        {
            Authenticator = "oauth",
            Token = token,
        };
        return loginRequestData;
    }
}