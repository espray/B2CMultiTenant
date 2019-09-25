using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RESTFunctions.Services
{
    public class Graph
    {
        public const string BaseUrl = "https://graph.microsoft.com/v1.0/";
        public Graph(IOptions<ConfidentialClientApplicationOptions> opts)
        {
            _app = ConfidentialClientApplicationBuilder
                .CreateWithApplicationOptions(opts.Value)
                .Build();
        }
        IConfidentialClientApplication _app;

        public async Task<HttpClient> GetClientAsync()
        {
            var tokens = await _app.AcquireTokenForClient(
                new string[] { "https://graph.microsoft.com/.default" })
                .ExecuteAsync();
            var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", tokens.AccessToken);
            return http;
        }
    }
}
