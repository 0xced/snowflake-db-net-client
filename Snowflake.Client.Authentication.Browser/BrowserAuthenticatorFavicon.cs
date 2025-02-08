namespace Snowflake.Client;

public class BrowserAuthenticatorFavicon // : BrowserHttpResponse()
{
    public BrowserAuthenticatorFavicon(byte[] content, string contentType = "image/x-icon")
    {
        Content = content;
        (ContentType, Path) = contentType switch
        {
            // https://en.wikipedia.org/wiki/Favicon#Image_file_format_support
            "image/png" => ("image/png", "/favicon.png"),
            "image/gif" => ("image/gif", "/favicon.gif"),
            "image/jpeg" => ("image/jpeg", "/favicon.jpg"),
            "image/svg+xml" => ("image/svg+xml", "/favicon.svg"),
            _ => ("image/x-icon", "/favicon.ico"),
        };
    }

    public byte[] Content { get; }

    public string ContentType { get; }

    public string Path { get; }
}
/*
public class BrowserAuthenticatorFavicon
{
    public BrowserAuthenticatorFavicon(byte[] content, string contentType = MediaTypeNames.Image.Icon)
    {
        Content = content;
        (ContentType, Path) = contentType switch
        {
            // https://en.wikipedia.org/wiki/Favicon#Image_file_format_support
            MediaTypeNames.Image.Png => (MediaTypeNames.Image.Png, "/favicon.png"),
            MediaTypeNames.Image.Gif => (MediaTypeNames.Image.Gif, "/favicon.gif"),
            MediaTypeNames.Image.Jpeg => (MediaTypeNames.Image.Jpeg, "/favicon.jpg"),
            MediaTypeNames.Image.Svg => (MediaTypeNames.Image.Svg, "/favicon.svg"),
            _ => (MediaTypeNames.Image.Icon, "/favicon.ico"),
        };
    }

    public byte[] Content { get; }

    public string ContentType { get; }

    public string Path { get; }
}
*/