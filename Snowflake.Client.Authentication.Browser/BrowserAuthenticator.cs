using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Snowflake.Client.Model;

namespace Snowflake.Client;

internal class BrowserAuthenticator(TokenWebServer webServer, UrlInfo urlInfo)
{
    private static readonly HttpClient HtpClient = new();

    public async Task<(string Token, string ProofKey)> AuthenticateAsync(TimeSpan timeout, CancellationToken ct)
    {
        var channel = Channel.CreateBounded<string>(1);

        var port = await webServer.StartAsync(token => channel.Writer.WriteAsync(token, ct).AsTask(), ct).ConfigureAwait(false);

        // See https://github.com/snowflakedb/snowflake-connector-net/blob/v4.3.0/Snowflake.Data/Core/Session/SFSessionProperty.cs#L296-L299
        var accountName = urlInfo.Host.Split('.')[0];
        var (ssoUrl, proofKey) = await AuthenticateAsync(accountName, port, ct).ConfigureAwait(false);

        OpenUrl(ssoUrl);

        var token = await GetTokenAsync(channel.Reader, timeout, ct).ConfigureAwait(false);

        await webServer.StopAsync(ct).ConfigureAwait(false);

        return (token, proofKey);
    }

    private async Task<(string SsoUrl, string ProofKey)> AuthenticateAsync(string accountName, int port, CancellationToken ct)
    {
        var requestUri = $"{urlInfo}/session/authenticator-request";

        AuthenticatorResponse? authResponse;
        try
        {
            var requestData = new AuthenticatorRequestData(accountName, port.ToString());
            var response = await HtpClient.PostAsJsonAsync(requestUri, new AuthenticatorRequest(requestData), ct).ConfigureAwait(false);
            authResponse = await response.Content.ReadFromJsonAsync<AuthenticatorResponse>(ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new BrowserException($"Failed to authenticate at {requestUri}", exception);
        }

        var ssoUrl = GetSsoUrl(authResponse, requestUri);
        var proofKey = GetProofKey(authResponse, requestUri);
        return (ssoUrl, proofKey);
    }

    private static string GetSsoUrl(AuthenticatorResponse? response, string requestUri)
        => response?.Data?.SsoUrl ?? throw new BrowserException($"Failed to retrieve the SSO URL from {requestUri}", response?.Code);

    private static string GetProofKey(AuthenticatorResponse? response, string requestUri)
        => response?.Data?.ProofKey ?? throw new BrowserException($"Failed to retrieve the proof key from {requestUri}", response?.Code);

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            throw new BrowserException($"Failed to open browser to {url}", exception);
        }
    }

    private static async Task<string> GetTokenAsync(ChannelReader<string> reader, TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutSource.CancelAfter(timeout);

        try
        {
            return await reader.ReadAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new BrowserException($"Failed to authenticate after waiting for {timeout.TotalSeconds:N0} seconds. " +
                                       $"Was Snowflake log in successful in the default browser?");
        }
    }

    private record AuthenticatorRequest(
        [property: JsonPropertyName("data")] AuthenticatorRequestData Data
    );

    private record AuthenticatorRequestData(
        [property: JsonPropertyName("ACCOUNT_NAME")] string AccountName,
        [property: JsonPropertyName("BROWSER_MODE_REDIRECT_PORT")] string BrowserModeRedirectPort,
        [property: JsonPropertyName("AUTHENTICATOR")] string Authenticator = "externalbrowser",
        [property: JsonPropertyName("CLIENT_APP_ID")] string? DriverName = null,
        [property: JsonPropertyName("CLIENT_APP_VERSION")] string? DriverVersion = null
    );

    private record AuthenticatorResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("code")] int? Code,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("data")] AuthenticatorResponseData? Data
    );

    private record AuthenticatorResponseData(
        [property: JsonPropertyName("tokenUrl")] string? TokenUrl,
        [property: JsonPropertyName("ssoUrl")] string? SsoUrl,
        [property: JsonPropertyName("proofKey")] string? ProofKey
    );
}