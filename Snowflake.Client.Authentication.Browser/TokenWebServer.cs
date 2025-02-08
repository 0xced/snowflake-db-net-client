using System;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace Snowflake.Client;

/// <summary>
/// A server that must run on 127.0.0.1, listen for a GET request on <c>/</c> and pass the <c>token</c> found in the query.
/// </summary>
public abstract class TokenWebServer(BrowserAuthenticatorFavicon? favicon, string? appName = null)
{
    private readonly string _appName = appName ?? "the Snowflake .NET client";

    protected BrowserAuthenticatorFavicon? Favicon { get; } = favicon;

    protected string PageContent
    {
        get
        {
            var faviconLink = Favicon == null ? "" : $"\n    <link rel=\"icon\" type=\"{Favicon.ContentType}\" href=\"{Favicon.Path}\">\n";
            // lang=html
            var html = $"""
                        <!DOCTYPE html>
                        <html lang="en">
                          <head>
                            <title>Authentication Response from Snowflake</title>{faviconLink}
                          </head>
                          <body>
                            <div style="text-align: center; font-family: sans-serif;">
                              <p style="font-size: 4em; margin: 0;">🪪</p>
                              <p>Your identity was confirmed and propagated to {HtmlEncoder.Default.Encode(_appName)}.</p>
                              <p>You can close this window now and go back where you started from.</p>
                            </div>
                          </body>
                        </html>
                        """;
            return html;
        }
    }

    public abstract Task<ushort> StartAsync(Func<string, Task> tokenCallback, CancellationToken ct);

    public abstract Task StopAsync(CancellationToken ct);
}