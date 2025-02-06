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

    public Context CreateContext(IFeatureCollection contextFeatures) => new(contextFeatures);

    public void DisposeContext(Context context, Exception? exception) {}

    public async Task ProcessRequestAsync(Context context)
    {
        var httpContext = new DefaultHttpContext(context.Features);

        if (httpContext.Request.Path == "/")
        {
            var token = httpContext.Request.Query["token"].ToString();
            await WriteResponseAsync(httpContext, favicon, appName).ConfigureAwait(false);
            await tokenCallback(token).ConfigureAwait(false);
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
            // Doesn't work with Safari, see https://bugs.webkit.org/show_bug.cgi?id=236616 and https://github.com/case/safari-favicons-base64
            var iconData = Convert.ToBase64String(favicon.Content);
            writer.Write(Encoding.UTF8.GetBytes($"    <link rel=\"icon\" type=\"{favicon.ContentType}\" href=\"data:{favicon.ContentType};base64,{iconData}\">\n"));
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