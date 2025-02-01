using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snowflake.Client.Model;

namespace Snowflake.Client;

internal class BrowserAuthenticator(UrlInfo urlInfo)
{
    private static readonly HttpClient HtpClient = new();

    // lang=html
    private const string AuthenticationResponseHtml =
        """
        <!DOCTYPE html>
        <html>
          <head>
            <title>Authentication Response from Snowflake</title>
          </head>
          <body>
          Your identity was confirmed and propagated to the Snowflake .NET client. You can close this window now and go back where you started from.
          </body>
        </html>
        """;

    public async Task<(string Token, string ProofKey)> AuthenticateAsync(TimeSpan timeout, CancellationToken ct)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();

        var channel = Channel.CreateBounded<string>(1);

        app.MapGet("/", async (string token) =>
        {
            await channel.Writer.WriteAsync(token, ct).ConfigureAwait(false);
            return TypedResults.Text(AuthenticationResponseHtml, contentType: MediaTypeNames.Text.Html, contentEncoding: Encoding.UTF8);
        });

        // See https://andrewlock.net/how-to-automatically-choose-a-free-port-in-asp-net-core/
        int? port = null;
        app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() =>
        {
            var address = ((IApplicationBuilder)app).ServerFeatures.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
            port = address == null ? null : new Uri(address).Port;
        });

        await app.StartAsync(ct).ConfigureAwait(false);

        if (!port.HasValue)
        {
            throw new BrowserException("Failed to start local web server");
        }

        // See https://github.com/snowflakedb/snowflake-connector-net/blob/v4.3.0/Snowflake.Data/Core/Session/SFSessionProperty.cs#L296-L299
        var accountName = urlInfo.Host.Split('.')[0];
        var requestData = new AuthenticatorRequestData(accountName, port.Value.ToString());
        var requestUri = $"{urlInfo}/session/authenticator-request";
        var response = await HtpClient.PostAsJsonAsync(requestUri, new AuthenticatorRequest(requestData), ct).ConfigureAwait(false);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthenticatorResponse>(ct).ConfigureAwait(false);
        var ssoUrl = GetSsoUrl(authResponse, requestUri);
        var proofKey = GetProofKey(authResponse, requestUri);

        try
        {
            Process.Start(new ProcessStartInfo(ssoUrl) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            throw new BrowserException($"Failed to open browser to {ssoUrl}", exception);
        }

        var token = await GetTokenAsync(channel.Reader, timeout, ct).ConfigureAwait(false);

        await app.StopAsync(ct).ConfigureAwait(false);

        return (token, proofKey);
    }

    private static string GetSsoUrl(AuthenticatorResponse? response, string requestUri)
        => response?.Data?.SsoUrl ?? throw new BrowserException($"Failed to retrieve the SSO URL from {requestUri}", response?.Code);

    private static string GetProofKey(AuthenticatorResponse? response, string requestUri)
        => response?.Data?.ProofKey ?? throw new BrowserException($"Failed to retrieve the proof key from {requestUri}", response?.Code);

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