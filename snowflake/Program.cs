using System;
using System.Reflection;
using System.Threading;
using snowflake;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<DefaultCommand>();
var cancellationTokenSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    // Try to cancel gracefully the first time, then abort the process the second time Ctrl+C is pressed
    eventArgs.Cancel = !cancellationTokenSource.IsCancellationRequested;
    cancellationTokenSource.Cancel();
};

app.Configure(config =>
{
    var assembly = typeof(Program).Assembly;
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "N/A";
    config.SetApplicationVersion(version);
    config.Settings.Registrar.RegisterInstance(cancellationTokenSource.Token);
    config.SetExceptionHandler((exception, _) =>
    {
        switch (exception)
        {
            case OperationCanceledException when cancellationTokenSource.IsCancellationRequested:
                AnsiConsole.WriteLine("The command was cancelled");
                return 0;
            case CommandAppException { Pretty: not null } commandAppException:
                AnsiConsole.Write(commandAppException.Pretty);
                break;
            case CommandAppException commandAppException:
                AnsiConsole.WriteLine(commandAppException.Message, Color.Red);
                break;
            default:
                AnsiConsole.WriteException(exception, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes);
                break;
        }

        if (exception is CommandAppException)
        {
            app.Run(["--help"]);
        }

        return 1;
    });
});

return await app.RunAsync(args);