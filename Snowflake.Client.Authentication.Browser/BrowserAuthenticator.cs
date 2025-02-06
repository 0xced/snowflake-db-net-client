using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Snowflake.Client.Model;

namespace Snowflake.Client;

internal class BrowserAuthenticator(UrlInfo urlInfo, BrowserAuthenticatorFavicon? favicon, string appName)
{
    private static readonly HttpClient HtpClient = new();

    public async Task<(string Token, string ProofKey)> AuthenticateAsync(TimeSpan timeout, CancellationToken ct)
    {
        var channel = Channel.CreateBounded<string>(1);

        using var server = CreateServer();

        var app = new TokenApplication(token => channel.Writer.WriteAsync(token, ct).AsTask(), favicon, appName);
        await server.StartAsync(app, ct).ConfigureAwait(false);
        var address = server.Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First();
        var port = address[(address.LastIndexOf(':') + 1)..];

        // See https://github.com/snowflakedb/snowflake-connector-net/blob/v4.3.0/Snowflake.Data/Core/Session/SFSessionProperty.cs#L296-L299
        var accountName = urlInfo.Host.Split('.')[0];
        var (ssoUrl, proofKey) = await AuthenticateAsync(accountName, port, ct).ConfigureAwait(false);

        OpenUrl(ssoUrl);

        var token = await GetTokenAsync(channel.Reader, timeout, ct).ConfigureAwait(false);

        await server.StopAsync(ct).ConfigureAwait(false);

        return (token, proofKey);
    }

    private static KestrelServer CreateServer()
    {
        var serverOptions = new KestrelServerOptions();
        serverOptions.Listen(IPAddress.Loopback, 0);
        var transportFactory = new SocketTransportFactory(Options.Create(new SocketTransportOptions()), NullLoggerFactory.Instance);
        return new KestrelServer(Options.Create(serverOptions), transportFactory, NullLoggerFactory.Instance);
    }

    private async Task<(string SsoUrl, string ProofKey)> AuthenticateAsync(string accountName, string port, CancellationToken ct)
    {
        var requestUri = $"{urlInfo}/session/authenticator-request";

        AuthenticatorResponse? authResponse;
        try
        {
            var requestData = new AuthenticatorRequestData(accountName, port);
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