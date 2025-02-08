using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Snowflake.Client;

public sealed class KestrelWebServer(BrowserAuthenticatorFavicon? favicon, string appName) : TokenWebServer(favicon, appName)
{
    private KestrelServer? _server;

    public override async Task<ushort> StartAsync(Func<string, Task> tokenCallback, CancellationToken ct)
    {
        var serverOptions = new KestrelServerOptions();
        serverOptions.Listen(IPAddress.Loopback, 0);
        var transportFactory = new SocketTransportFactory(Options.Create(new SocketTransportOptions()), NullLoggerFactory.Instance);
        _server = new KestrelServer(Options.Create(serverOptions), transportFactory, NullLoggerFactory.Instance);

        var app = new TokenApplication(tokenCallback, Favicon, PageContent);
        await _server.StartAsync(app, ct).ConfigureAwait(false);
        var address = _server.Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First();
        var port = address[(address.LastIndexOf(':') + 1)..];

        return ushort.Parse(port, NumberStyles.None);
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        if (_server != null)
        {
            await _server.StopAsync(ct).ConfigureAwait(false);
        }
    }
}