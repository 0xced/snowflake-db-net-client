namespace Snowflake.Client;

public class BrowserHttpResponse(byte[] data, string contentType, string path)
{
    public byte[] Data { get; } = data;

    public string ContentType { get; } = contentType;

    public string Path { get; } = path;
}