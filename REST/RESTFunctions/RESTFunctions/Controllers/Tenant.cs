using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;
using RESTFunctions.Services;

namespace RESTFunctions.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class Tenant : ControllerBase
    {
        public Tenant(TokenAccessor oauth)
        {
            _oauth = oauth;
        }
        TokenAccessor _oauth;

        // POST api/values
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] TenantDef tenant)
        {
            if ((string.IsNullOrEmpty(tenant.Name) || (string.IsNullOrEmpty(tenant.UserObjectId))))
                return BadRequest("Invalid parameters");

            var token = await _oauth.GetAccessTokenAsync(new string[] { "https://graph.microsoft.com/.default" });
            var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", token);
            // does the user exist?
            Guid guid;
            if (!Guid.TryParse(tenant.UserObjectId, out guid))
                return BadRequest("Invalid user id");
            try
            {
                await http.GetStringAsync($"https://graph.microsoft.com/users/{tenant.UserObjectId}");
            } catch(HttpRequestException)
            {
                return BadRequest("Unable to validate user id");
            }
            if ((tenant.Name.Length > 60) || !Regex.IsMatch(tenant.Name, "^[a-z_]\\w*$"))
                return BadRequest("Invalid tenant name");
            var resp = await http.GetAsync($"https://graph.microsoft.com/groups?$filter=(displayName eq '{tenant.Name}')");
            if (resp.IsSuccessStatusCode)
                return BadRequest("Tenant already exists");
            else if (resp.StatusCode != System.Net.HttpStatusCode.NotFound)
                return BadRequest("Unable to validate tenant existence");
            var group = new
            {
                description = tenant.Description,
                displayName = tenant.Name,
                groupTypes = new string[] { "unified" },
                mailEnabled = false,
                securityEnabled = true,
                owners = new string[] { tenant.UserObjectId }
            };
            //  https://docs.microsoft.com/en-us/graph/api/group-post-groups?view=graph-rest-1.0&tabs=http
            resp = await http.PostAsJsonAsync(
                "https://graph.microsoft.com/groups",
                group);
            if (!resp.IsSuccessStatusCode)
                return BadRequest("Tenant creation failed");
            var json = await resp.Content.ReadAsAsync<string>();
            var newGroup = JObject.Parse(json);
            var id = newGroup["id"].Value<string>();
            // add this group to the user's tenant collection
            return new OkObjectResult(new { Id = id });
        }
    }

    public class TenantDef
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string UserObjectId { get; set; }
    }
}
