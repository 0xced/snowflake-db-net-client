using System;
using Snowflake.Client.Model;

namespace Snowflake.Client;

/// <summary>
/// Thrown by <c>Snowflake.Client.Authentication.OAuth</c> when authentication fails.
/// </summary>
public class OAuthException : SnowflakeException
{
    public OAuthException(string message) : base(message)
    {
    }

    public OAuthException(string message, Exception innerException) : base(message, innerException)
    {
    }
}