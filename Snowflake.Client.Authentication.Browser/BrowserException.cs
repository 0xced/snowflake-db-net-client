using System;
using Snowflake.Client.Model;

namespace Snowflake.Client;

/// <summary>
/// Thrown by <c>Snowflake.Client.Authentication.Browser</c> when authentication fails.
/// </summary>
public class BrowserException : SnowflakeException
{
    public BrowserException(string message) : base(message)
    {
    }

    public BrowserException(string message, int? code) : base(message, code)
    {
    }

    public BrowserException(string message, Exception innerException) : base(message, innerException)
    {
    }
}