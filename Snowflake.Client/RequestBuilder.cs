using Snowflake.Client.Json;
using Snowflake.Client.Model;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Snowflake.Client
{
    internal class RequestBuilder
    {
        private readonly UrlInfo _urlInfo;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        private string _masterToken;
        private string _sessionToken;

        internal RequestBuilder(UrlInfo urlInfo)
        {
            _urlInfo = urlInfo;
            
            _jsonSerializerOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        internal void SetSessionTokens(string sessionToken, string masterToken)
        {
            _sessionToken = sessionToken;
            _masterToken = masterToken;
        }

        internal void ClearSessionTokens()
        {
            _sessionToken = null;
            _masterToken = null;
        }

        internal HttpRequestMessage BuildLoginRequest(LoginRequestData data, SessionInfo sessionInfo)
        {
            var requestUri = BuildLoginUrl(sessionInfo);
            var requestBody = new LoginRequest() { Data = data };
            var request = BuildJsonRequestMessage(requestUri, HttpMethod.Post, requestBody);

            return request;
        }

        internal HttpRequestMessage BuildCancelQueryRequest(string requestId)
        {
            var requestUri = BuildCancelQueryUrl();
            var requestBody = new CancelQueryRequest()
            {
                RequestId = requestId
            };

            var request = BuildJsonRequestMessage(requestUri, HttpMethod.Post, requestBody);

            return request;
        }

        internal HttpRequestMessage BuildRenewSessionRequest()
        {
            var requestUri = BuildRenewSessionUrl();
            var requestBody = new RenewSessionRequest()
            {
                OldSessionToken = _sessionToken,
                RequestType = "RENEW"
            };

            var request = BuildJsonRequestMessage(requestUri, HttpMethod.Post, requestBody, true);

            return request;
        }

        internal HttpRequestMessage BuildQueryRequest(string sql, object sqlParams, bool describeOnly)
        {
            var queryUri = BuildQueryUrl();

            var requestBody = new QueryRequest()
            {
                SqlText = sql,
                DescribeOnly = describeOnly,
                Bindings = ParameterBinder.BuildParameterBindings(sqlParams)
            };

            var request = BuildJsonRequestMessage(queryUri, HttpMethod.Post, requestBody);

            return request;
        }

        internal HttpRequestMessage BuildCloseSessionRequest()
        {
            var queryParams = new Dictionary<string, string>();
            queryParams[SnowflakeConst.SF_QUERY_SESSION_DELETE] = "true";
            queryParams[SnowflakeConst.SF_QUERY_REQUEST_ID] = Guid.NewGuid().ToString();

            var requestUri = BuildUri(SnowflakeConst.SF_SESSION_PATH, queryParams);
            var request = BuildJsonRequestMessage(requestUri, HttpMethod.Post);

            return request;
        }

        internal HttpRequestMessage BuildGetResultRequest(string getResultUrl)
        {
            var queryUri = BuildUri(getResultUrl);
            var request = BuildJsonRequestMessage(queryUri, HttpMethod.Get);

            return request;
        }

        internal Uri BuildLoginUrl(SessionInfo sessionInfo)
        {
            var queryParams = new Dictionary<string, string>
            {
                [SnowflakeConst.SF_QUERY_WAREHOUSE] = sessionInfo.Warehouse,
                [SnowflakeConst.SF_QUERY_DB] = sessionInfo.Database,
                [SnowflakeConst.SF_QUERY_SCHEMA] = sessionInfo.Schema,
                [SnowflakeConst.SF_QUERY_ROLE] = sessionInfo.Role,
                [SnowflakeConst.SF_QUERY_REQUEST_ID] = Guid.NewGuid().ToString() // extract to shared part ?
            };

            var loginUrl = BuildUri(SnowflakeConst.SF_LOGIN_PATH, queryParams);
            return loginUrl;
        }

        internal Uri BuildCancelQueryUrl()
        {
            var queryParams = new Dictionary<string, string>
            {
                [SnowflakeConst.SF_QUERY_REQUEST_ID] = Guid.NewGuid().ToString(),
                [SnowflakeConst.SF_QUERY_REQUEST_GUID] = Guid.NewGuid().ToString()
            };

            var url = BuildUri(SnowflakeConst.SF_QUERY_CANCEL_PATH, queryParams);
            return url;
        }

        internal Uri BuildRenewSessionUrl()
        {
            var queryParams = new Dictionary<string, string>
            {
                [SnowflakeConst.SF_QUERY_REQUEST_ID] = Guid.NewGuid().ToString(),
                [SnowflakeConst.SF_QUERY_REQUEST_GUID] = Guid.NewGuid().ToString()
            };

            var url = BuildUri(SnowflakeConst.SF_TOKEN_REQUEST_PATH, queryParams);
            return url;
        }

        private Uri BuildQueryUrl()
        {
            var queryParams = new Dictionary<string, string>
            {
                [SnowflakeConst.SF_QUERY_REQUEST_ID] = Guid.NewGuid().ToString()
            };

            var loginUrl = BuildUri(SnowflakeConst.SF_QUERY_PATH, queryParams);
            return loginUrl;
        }

        internal Uri BuildUri(string basePath, Dictionary<string, string> queryParams = null)
        {
            var uriBuilder = new UriBuilder
            {
                Scheme = _urlInfo.Protocol,
                Host = _urlInfo.Host,
                Port = _urlInfo.Port,
                Path = basePath
            };

            if (queryParams != null && queryParams.Count > 0)
            {
                var paramCollection = HttpUtility.ParseQueryString("");
                foreach (var kvp in queryParams)
                {
                    if (!string.IsNullOrEmpty(kvp.Value))
                        paramCollection.Add(kvp.Key, kvp.Value);
                }
                uriBuilder.Query = paramCollection.ToString() ?? "";
            }

            return uriBuilder.Uri;
        }

        private HttpRequestMessage BuildJsonRequestMessage(Uri uri, HttpMethod method, bool useMasterToken = false)
        {
            return BuildJsonRequestMessage<object>(uri, method, null, useMasterToken);
        }

        private HttpRequestMessage BuildJsonRequestMessage<T>(Uri uri, HttpMethod method, T requestBody = default, bool useMasterToken = false)
        {
            var request = new HttpRequestMessage();
            request.Method = method;
            request.RequestUri = uri;

            if (requestBody != null && method != HttpMethod.Get)
            {
                request.Content = JsonContent.Create(requestBody, options: _jsonSerializerOptions);
            }

            if (_sessionToken != null)
            {
                var authToken = useMasterToken ? _masterToken : _sessionToken;
                request.Headers.Add("Authorization", $"Snowflake Token=\"{authToken}\"");
            }

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/snowflake"));
            var clientInfo = ClientAppInfo.Instance;
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(clientInfo.DriverName, clientInfo.DriverVersion));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(clientInfo.Environment.OSVersion));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(clientInfo.Environment.NETRuntime, clientInfo.Environment.NETVersion));

            return request;
        }
    }
}