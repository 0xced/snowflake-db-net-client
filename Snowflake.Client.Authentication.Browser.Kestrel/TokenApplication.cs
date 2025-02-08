using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Snowflake.Client;

internal class TokenApplication(Func<string, Task> tokenCallback, BrowserAuthenticatorFavicon? favicon, string pageContent) : IHttpApplication<TokenApplication.Context>
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
            var pageData = Encoding.UTF8.GetBytes(pageContent);
            await WriteAsync(httpContext.Response, pageData, "text/html; charset=utf-8");
            if (favicon == null)
            {
                await tokenCallback(_token).ConfigureAwait(false);
            }
        }
        else if (httpContext.Request.Path == favicon?.Path)
        {
            await WriteAsync(httpContext.Response, favicon.Content, favicon.ContentType);
            await tokenCallback(_token ?? "").ConfigureAwait(false);
        }
        else
        {
            httpContext.Response.StatusCode = Status404NotFound;
        }
    }

    private static async Task WriteAsync(HttpResponse response, byte[] data, string contentType)
    {
        response.ContentType = contentType;
        response.ContentLength = data.Length;
        await response.BodyWriter.WriteAsync(data).ConfigureAwait(false);
    }
}
