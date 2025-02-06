using System.ComponentModel;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Snowflake.Client;
using Snowflake.Client.Model;
using Spectre.Console;
using Spectre.Console.Cli;

namespace snowflake;

[Description("Test Snowflake authentication. If client id, client secret and tenant id are provided then OAuth is used. Else browser authentication is used.")]
public class DefaultCommand(IAnsiConsole console, CancellationToken cancellationToken) : AsyncCommand<DefaultCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<account>")]
        [Description("The Snowflake account name")]
        public string Account { get; init; } = "";

        [CommandArgument(1, "<region>")]
        [Description("The account region, e.g. [b]west-europe[/] or [b]ca-central-1[/] or [b]europe-west4[/]. The cloud (azure/aws/gcp) is determined automatically from the region.")]
        public string Region { get; init; } = "";

        [CommandOption("-f|--favicon")]
        [Description("An optional file to use as the favicon for the authentication response page. See https://en.wikipedia.org/wiki/Favicon#Image_file_format_support for supported formats.")]
        public FileInfo? FaviconFile { get; init; }

        [CommandOption("-i|--client-id")]
        [Description("The OAuth client id.")]
        public string? ClientId { get; init; }

        [CommandOption("-p|--client-secret")]
        [Description("The OAuth client secret.")]
        public string? ClientSecret { get; init; }

        [CommandOption("-t|--tenant-id")]
        [Description("The OAuth tenant id.")]
        public string? TenantId { get; init; }

        [CommandOption("-s|--scope")]
        [Description("The OAuth scopes. Can be repeated multiple times for multiple scopes.")]
        public string[] Scopes { get; init; } = [];

        internal BrowserAuthenticatorFavicon? Favicon { get; private set; }

        public override ValidationResult Validate()
        {
            if (FaviconFile is not null && FaviconFile.Exists)
            {
                var contentType = FaviconFile.Extension switch
                {
                    ".png" => MediaTypeNames.Image.Png,
                    ".gif" => MediaTypeNames.Image.Gif,
                    ".jpg" => MediaTypeNames.Image.Jpeg,
                    ".jpeg" => MediaTypeNames.Image.Jpeg,
                    ".svg" => MediaTypeNames.Image.Svg,
                    _ => MediaTypeNames.Image.Icon,
                };
                Favicon = new BrowserAuthenticatorFavicon(File.ReadAllBytes(FaviconFile.FullName), contentType);
            }

            return base.Validate();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var clientSettings = CreateClientSettings(settings);
        var client = new SnowflakeClient(clientSettings);

        console.WriteLine($"{(await client.InitNewSessionAsync(cancellationToken) ? "✅" : "❌")}  InitNewSessionAsync");
        console.WriteLine($"{(await client.RenewSessionAsync(cancellationToken) ? "✅" : "❌")}  RenewSessionAsync");

        return 0;
    }

    private static SnowflakeClientSettings CreateClientSettings(Settings settings)
    {
        if (settings.TenantId is not null && settings.ClientId is not null && settings.ClientSecret is not null)
        {
            var oauthInfo = new OAuthInfo
            {
                Account = settings.Account,
                Region = settings.Region,
                ClientId = settings.ClientId,
                ClientSecret = settings.ClientSecret,
                TenantId = settings.TenantId,
                Scopes = settings.Scopes,
            };

            return new OAuthAuthenticatorSettings(oauthInfo);
        }

        var authInfo = new AuthInfo
        {
            Account = settings.Account,
            Region = settings.Region,
        };

        return new BrowserAuthenticatorSettings(authInfo, settings.Favicon, appName: "the snowflake CLI");
    }
}
