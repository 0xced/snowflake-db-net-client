using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BeetleX.FastHttpApi;

namespace Snowflake.Client;

public sealed class BeetleXWebServer(BrowserAuthenticatorFavicon? favicon = null, string? appName = null) : TokenWebServer(favicon, appName)
{
    private HttpApiServer? _apiServer;
    private string? _token;

    public override async Task<ushort> StartAsync(Func<string, Task> tokenCallback, CancellationToken ct)
    {
        var httpOptions = new HttpOptions { UseIPv6 = false, Port = 0 };
        _apiServer = new HttpApiServer(httpOptions);

        _apiServer.Map("/", async context =>
        {
            const string prefix = "/?token=";
            _token = context.Request.Url.StartsWith(prefix) ? context.Request.Url[prefix.Length..] : "";
            if (Favicon == null)
            {
                await tokenCallback(_token).ConfigureAwait(false);
            }
            return new HtmlResult(PageContent);
        });

        if (Favicon != null)
        {
            _apiServer.Map(Favicon.Path, async _ =>
            {
                await tokenCallback(_token ?? "").ConfigureAwait(false);
                return new BinaryResult(Favicon.Content, Favicon.ContentType);
            });
        }

        await _apiServer.Open().ConfigureAwait(false);

        var localEndPoint = _apiServer.Server.Options.Listens.Single().Socket.LocalEndPoint as IPEndPoint;

        return Convert.ToUInt16(localEndPoint?.Port);
    }

    public override Task StopAsync(CancellationToken ct)
    {
        _apiServer?.Dispose();
        return Task.CompletedTask;
    }

    private class HtmlResult(string text) : TextResult(text, autoGzip: false)
    {
        public override IHeaderItem ContentType => new HeaderItem("Content-Type: text/html; charset=utf-8\r\n");
    }
}