using Snowflake.Client.Model;

namespace Snowflake.Client;

public class OAuthInfo : AuthInfo
{
    private const string MicrosoftOnlineBaseUrl = "https://login.microsoftonline.com/";

    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    public string Authority { get; set; } = "";

    public string TenantId
    {
        get => Authority.StartsWith(MicrosoftOnlineBaseUrl) ? Authority.Substring(MicrosoftOnlineBaseUrl.Length).TrimEnd('/') : "";
        set => Authority = $"{MicrosoftOnlineBaseUrl}{value}/";
    }

    public string[] Scopes { get; set; } = [];
}