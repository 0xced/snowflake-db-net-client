using Snowflake.Client.Helpers;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Snowflake.Client.Json;

namespace Snowflake.Client.Model
{
    /// <summary>
    /// Configuration for SnowflakeClient
    /// </summary>
    public class SnowflakeClientSettings
    {
        /// <summary>
        /// Data used to authenticate in Snowflake: user, password, account and region
        /// </summary>
        public AuthInfo AuthInfo { get; }

        /// <summary>
        /// Snowflake URL: host, protocol and port
        /// </summary>
        public UrlInfo UrlInfo { get; }

        /// <summary>
        /// Snowflake session objects to set: role, schema, database and warehouse
        /// </summary>
        public SessionInfo SessionInfo { get; }

        /// <summary>
        /// Serializer options used to map data response to your model
        /// </summary>
        public JsonSerializerOptions JsonMapperOptions { get; }

        /// <summary>
        /// Options used in ChunksDownloader
        /// </summary>
        public ChunksDownloaderOptions ChunksDownloaderOptions { get; }

        /// <summary>
        /// Snowflake can return response data in a table form ("rowset") or in chunks or both.
        /// Set this parameter to true to fetch chunks, so the whole data set will be in a rowset. 
        /// Default value: False 
        /// </summary>
        public bool DownloadChunksForQueryRawResponses { get; set; }

        public SnowflakeClientSettings(AuthInfo authInfo, SessionInfo sessionInfo = null, UrlInfo urlInfo = null,
            JsonSerializerOptions jsonMapperOptions = null, ChunksDownloaderOptions chunksDownloaderOptions = null,
            bool downloadChunksForQueryRawResponses = false)
        {
            AuthInfo = authInfo ?? new AuthInfo();
            SessionInfo = sessionInfo ?? new SessionInfo();
            UrlInfo = urlInfo ?? new UrlInfo();
            JsonMapperOptions = jsonMapperOptions ?? new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            ChunksDownloaderOptions = chunksDownloaderOptions ?? new ChunksDownloaderOptions() { PrefetchThreadsCount = 4 };
            DownloadChunksForQueryRawResponses = downloadChunksForQueryRawResponses;

            UrlInfo.Host = string.IsNullOrEmpty(UrlInfo.Host)
                ? BuildHostName(AuthInfo.Account, AuthInfo.Region)
                : ReplaceUnderscores(UrlInfo.Host);
        }

        /// <summary>
        /// Creates the login request data used to log in into Snowflake.
        /// </summary>
        /// <returns>The <see cref="LoginRequestData"/> used to log in into Snowflake.</returns>
        /// <remarks>
        /// This method can be overriden to perform a login which is not based on the user and password.
        /// For example, it can be used to perform an SSO login by setting the <see cref="LoginRequestData.Token"/> property.
        /// </remarks>
        protected internal virtual Task<LoginRequestData> GetLoginRequestDataAsync(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(AuthInfo.User))
                throw new InvalidOperationException($"The user must be specified in the {nameof(Model.AuthInfo)}.");

            if (string.IsNullOrEmpty(AuthInfo.Password))
                throw new InvalidOperationException($"The password must be specified in the {nameof(Model.AuthInfo)}.");

            var loginRequestData = new LoginRequestData
            {
                LoginName = AuthInfo.User,
                Password = AuthInfo.Password,
                AccountName = AuthInfo.Account,
            };
            return Task.FromResult(loginRequestData);
        }

        private static string BuildHostName(string account, string region)
        {
            if (string.IsNullOrEmpty(account))
                throw new ArgumentException("Account name cannot be empty.");

            var hostname = $"{ReplaceUnderscores(account)}.";

            if (!string.IsNullOrEmpty(region) && region.ToLower() != "us-west-2")
                hostname += $"{region}.";

            var cloudTag = SnowflakeUtils.GetCloudTagByRegion(region);

            if (!string.IsNullOrEmpty(cloudTag))
                hostname += $"{cloudTag}.";

            hostname += "snowflakecomputing.com";

            return hostname.ToLower();
        }

        // Underscores in hostname will lead to SSL cert verification issue.
        // See https://github.com/snowflakedb/snowflake-connector-net/issues/160#issuecomment-692883663
        private static string ReplaceUnderscores(string account)
        {
            return account.Replace("_", "-");
        }
    }
}