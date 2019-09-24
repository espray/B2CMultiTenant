using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RESTFunctions.Services
{
    public class TokenAccessor
    {
        public TokenAccessor(IOptions<ConfidentialClientApplicationOptions> opts)
        {
            _app = ConfidentialClientApplicationBuilder
                .CreateWithApplicationOptions(opts.Value)
                .Build();
        }
        IConfidentialClientApplication _app;
        public async Task<string> GetAccessTokenAsync(string[] scopes)
        {

            var tokens = await _app.AcquireTokenForClient(
                scopes)
                .ExecuteAsync();
            return tokens.AccessToken;
        }
    }
}
