// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Docs.Build
{
    internal static class RuledHttpClient
    {
        private const int RetryCount = 3;
        private const int RetryInterval = 1000;
        private static readonly HttpClient s_httpClient = new HttpClient();

        public static async Task<HttpResponseMessage> GetAsync(string requestUri, HttpSecretConfig[] rules)
        {
            HttpResponseMessage response;
            for (var i = 0; i < RetryCount; i++)
            {
                try
                {
                    // Create new instance of HttpRequestMessage to avoid System.InvalidOperationException:
                    // "The request message was already sent. Cannot send the same request message multiple times."
                    var message = CreateHttpRequestMessage(requestUri, rules);
                    message.Method = HttpMethod.Get;
                    response = await s_httpClient.SendAsync(message);
                }
                catch (HttpRequestException) when (i < RetryCount - 1)
                {
                    await Task.Delay(RetryInterval);
                    continue;
                }
                return response;
            }

            Debug.Fail("should never reach here");
            return null;
        }

        public static async Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content, HttpSecretConfig[] rules)
        {
            var message = CreateHttpRequestMessage(requestUri, rules);
            message.Method = HttpMethod.Put;
            message.Content = content;
            return await s_httpClient.SendAsync(message);
        }

        private static HttpRequestMessage CreateHttpRequestMessage(string requestUri, HttpSecretConfig[] rules)
        {
            var message = new HttpRequestMessage();

            foreach (var rule in rules)
            {
                if (rule.Match(requestUri))
                {
                    // TODO: merge query if requestUri also contains query
                    message.RequestUri = new Uri(requestUri + rule.Query);
                    foreach (var header in rule.Headers)
                    {
                        message.Headers.Add(header.Key, header.Value);
                    }
                    return message;
                }
            }

            message.RequestUri = new Uri(requestUri);
            return message;
        }

        private static bool Match(this HttpSecretConfig rule, string uri)
        {
            if (!string.IsNullOrEmpty(rule.BaseUrl) && uri.StartsWith(rule.BaseUrl))
                return true;

            return false;
        }
    }
}
