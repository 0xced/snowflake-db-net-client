using System;
using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Snowflake.Client;

internal class TokenApplication(Func<string, Task> tokenCallback, BrowserAuthenticatorFavicon? favicon, string appName) : IHttpApplication<TokenApplication.Context>
{
    internal record Context(IFeatureCollection Features);

    private string? _token;

    public Context CreateContext(IFeatureCollection contextFeatures) => new(contextFeatures);

    public void DisposeContext(Context context, Exception? exception) {}

    public async Task ProcessRequestAsync(Context context)
    {
        var httpContext = new DefaultHttpContext(context.Features);

        if (httpContext.Request.Path == "/")
        {
            _token = httpContext.Request.Query["token"].ToString();
            await WriteResponseAsync(httpContext, favicon, appName).ConfigureAwait(false);
            if (favicon == null)
            {
                await tokenCallback(_token).ConfigureAwait(false);
            }
        }
        else if (httpContext.Request.Path == favicon?.Path)
        {
            httpContext.Response.ContentType = favicon.ContentType;
            httpContext.Response.ContentLength = favicon.Content.Length;
            await httpContext.Response.BodyWriter.WriteAsync(favicon.Content).ConfigureAwait(false);
            await tokenCallback(_token ?? "").ConfigureAwait(false);
        }
        else
        {
            httpContext.Response.StatusCode = Status404NotFound;
        }
    }

    private static async Task WriteResponseAsync(HttpContext httpContext, BrowserAuthenticatorFavicon? favicon, string appName)
    {
        httpContext.Response.ContentType = "text/html; charset=utf-8";
        var writer = httpContext.Response.BodyWriter;
        writer.Write("<!DOCTYPE html>\n"u8);
        writer.Write("<html>\n"u8);
        writer.Write("  <head>\n"u8);
        writer.Write("    <title>Authentication Response from Snowflake</title>\n"u8);
        if (favicon != null)
        {
            writer.Write(Encoding.UTF8.GetBytes($"    <link rel=\"icon\" type=\"{favicon.ContentType}\" href=\"{favicon.Path}\">\n"));
        }
        writer.Write("  </head>\n"u8);
        writer.Write("  <body>\n"u8);
        writer.Write("    <div style=\"text-align: center; font-family: sans-serif;\">\n"u8);
        writer.Write("      <p style=\"font-size: 4em; margin: 0;\">🪪</p>\n"u8);
        writer.Write(Encoding.UTF8.GetBytes($"      <p>Your identity was confirmed and propagated to {HtmlEncoder.Default.Encode(appName)}.</p>\n"));
        writer.Write("      <p>You can close this window now and go back where you started from.</p>\n"u8);
        writer.Write("    </div>\n"u8);
        writer.Write("  </body>\n"u8);
        writer.Write("</html>"u8);
        await writer.FlushAsync().ConfigureAwait(false);
    }
}