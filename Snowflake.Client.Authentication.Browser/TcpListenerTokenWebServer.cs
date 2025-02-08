using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Snowflake.Client;

public partial class TcpListenerTokenWebServer(BrowserAuthenticatorFavicon? favicon, string? appName = null) : TokenWebServer(favicon, appName)
{
    [GeneratedRegex(@"GET /\?token=([^ ]+) HTTP/1\.1")]
    private static partial Regex TokenRegex();

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private Task? _listenTask;

    public override Task<ushort> StartAsync(Func<string, Task> tokenCallback, CancellationToken ct)
    {
        _listener.Start();

        _listenTask = Task.Run(async () =>
        {
            // https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/sockets/tcp-classes#create-a-tcplistener
            using var tcpClient = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            var stream = tcpClient.GetStream();
            await using (stream.ConfigureAwait(false))
            {
                using var reader = new StreamReader(stream, leaveOpen: true);
                while (true)
                {
                    var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    var match = TokenRegex().Match(line ?? "");
                    if (match.Success)
                    {
                        var token = match.Groups[1].Value;
                        await tokenCallback(token).ConfigureAwait(false);
                    }
                    if (line?.Length == 0)
                    {
                        var writer = new StreamWriter(stream, leaveOpen: true) { NewLine = "\r\n" };
                        await using (writer.ConfigureAwait(false))
                        {
                            await writer.WriteLineAsync("HTTP/1.1 200 OK").ConfigureAwait(false);
                            await writer.WriteLineAsync("Content-Type: text/html; charset=utf-8").ConfigureAwait(false);
                            await writer.WriteLineAsync().ConfigureAwait(false);
                            await writer.WriteAsync(PageContent).ConfigureAwait(false);
                        }
                        break;
                    }
                }
            }
        }, ct);

        var localEndPoint = _listener.Server.LocalEndPoint as IPEndPoint;
        return Task.FromResult(Convert.ToUInt16(localEndPoint?.Port));
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        if (_listenTask != null)
        {
            await _listenTask.ConfigureAwait(false);
        }
        _listener.Stop();
    }
}