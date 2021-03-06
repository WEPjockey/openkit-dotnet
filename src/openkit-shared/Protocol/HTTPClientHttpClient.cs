﻿//
// Copyright 2018-2019 Dynatrace LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using Dynatrace.OpenKit.API;
using Dynatrace.OpenKit.Core.Configuration;
using System.Linq;

namespace Dynatrace.OpenKit.Protocol
{
#if (!NET40 && !NET35)

    public class HTTPClientHttpClient : HTTPClient
    {
        private readonly HTTPClientConfiguration configuration;

        public HTTPClientHttpClient(ILogger logger, HTTPClientConfiguration configuration) : base(logger, configuration)
        {
            this.configuration = configuration;
        }

        protected override HTTPResponse GetRequest(string url, string clientIPAddress)
        {
            using (System.Net.Http.HttpClient httpClient = CreateHTTPClient(clientIPAddress))
            {
                var responseTask = httpClient.GetAsync(url);
                responseTask.Wait();

                return CreateHTTPResponse(responseTask.Result);
            }
        }

        protected override HTTPResponse PostRequest(string url, string clientIPAddress, byte[] gzippedPayload)
        {
            using (System.Net.Http.HttpClient httpClient = CreateHTTPClient(clientIPAddress))
            {
                var content = CreatePostContent(gzippedPayload);
                var responseTask = httpClient.PostAsync(url, content);
                responseTask.Wait();

                return CreateHTTPResponse(responseTask.Result);
            }
        }

        private System.Net.Http.HttpClient CreateHTTPClient(string clientIPAddress)
        {
            // The implementation of CreateHTTPClient varies based on the .NET technology
            var httpClient = CreateHTTPClient();

            if (clientIPAddress != null)
            {
                httpClient.DefaultRequestHeaders.Add("X-Client-IP", clientIPAddress);
            }

            return httpClient;
        }

        private static System.Net.Http.ByteArrayContent CreatePostContent(byte[] gzippedPayload)
        {
            if (gzippedPayload == null || gzippedPayload.Length == 0)
                return new System.Net.Http.ByteArrayContent(new byte[] { });

            var content = new System.Net.Http.ByteArrayContent(gzippedPayload);
            content.Headers.Add("Content-Encoding", "gzip");
            content.Headers.Add("Content-Length", gzippedPayload.Length.ToString());

            return content;
        }

        private static HTTPResponse CreateHTTPResponse(System.Net.Http.HttpResponseMessage result)
        {
            System.Threading.Tasks.Task<string> httpResponseContentTask = result.Content.ReadAsStringAsync();
            httpResponseContentTask.Wait();
            var response = httpResponseContentTask.Result;
            var responseCode = result.StatusCode;
            var headers = result.Headers.ToDictionary(pair => pair.Key.ToLowerInvariant(), pair => pair.Value.ToList());

            return new HTTPResponse
            {
                Response = response,
                ResponseCode = (int)responseCode,
                Headers = headers
            };
        }

        #region CreateHTTPClient implementations

#if WINDOWS_UWP || NETPCL4_5
        
        // handling for all frameworks that do not support certificate validation
        private System.Net.Http.HttpClient CreateHTTPClient()
        {
            return new System.Net.Http.HttpClient();
        }

#elif NETSTANDARD2_0

        // .NET standard uses the ServicePointManager's global callback
        private static bool remoteCertificateValidationCallbackInitialized = false;

        private System.Net.Http.HttpClient CreateHTTPClient()
        {
            if (!remoteCertificateValidationCallbackInitialized)
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback += configuration.SSLTrustManager.ServerCertificateValidationCallback;
                remoteCertificateValidationCallbackInitialized = true;
            }
            return new System.Net.Http.HttpClient();
        }

#elif NET45 || NET46 || NET47

        private System.Net.Http.HttpClient CreateHTTPClient()
        {
            var webRequestHandler = new System.Net.Http.WebRequestHandler();
            webRequestHandler.ServerCertificateValidationCallback += configuration.SSLTrustManager.ServerCertificateValidationCallback;
            return new System.Net.Http.HttpClient(webRequestHandler, true);
        }

#else

        private System.Net.Http.HttpClient CreateHTTPClient()
        {
            var httpClientHandler = new System.Net.Http.HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = configuration.SSLTrustManager.ServerCertificateValidationCallback.Invoke
            };
            return new System.Net.Http.HttpClient(httpClientHandler, true);
        }

#endif

        #endregion

    }

#endif
}
