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
        public Tenant(Graph graph)
        {
            _graph = graph;
        }
        Graph _graph;
        [HttpGet]
        public async Task<IActionResult> Get(string id)
        {
            Guid guid;
            if (!Guid.TryParse(id, out guid))
                return BadRequest("Invalid user id");
            var http = await _graph.GetClientAsync();
            try
            {
                var json = await http.GetStringAsync($"{Graph.BaseUrl}groups/{id}");
                var result = JObject.Parse(json);
                return new JsonResult(new TenantDef()
                {
                    Name = result["displayName"].Value<string>(),
                    Description = result["description"].Value<string>()
                });
            } catch (HttpRequestException)
            {
                return NotFound();
            }
        }

        // POST api/values
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] TenantDef tenant)
        {
            if ((string.IsNullOrEmpty(tenant.Name) || (string.IsNullOrEmpty(tenant.UserObjectId))))
                return BadRequest("Invalid parameters");

            var http = await _graph.GetClientAsync();
            // does the user exist?
            Guid guid;
            if (!Guid.TryParse(tenant.UserObjectId, out guid))
                return BadRequest("Invalid user id");
            try
            {
                await http.GetStringAsync($"{Graph.BaseUrl}users/{tenant.UserObjectId}");
            } catch (HttpRequestException ex)
            {
                return BadRequest("Unable to validate user id");
            }
            if ((tenant.Name.Length > 60) || !Regex.IsMatch(tenant.Name, "^[A-Za-z]\\w*$"))
                return BadRequest("Invalid tenant name");
            var resp = await http.GetAsync($"{Graph.BaseUrl}groups?$filter=(displayName eq '{tenant.Name}')");
            if (!resp.IsSuccessStatusCode)
                return BadRequest("Unable to validate tenant existence");
            var values = JObject.Parse(await resp.Content.ReadAsStringAsync())["value"].Value<JArray>();
            if (values.Count != 0)
                return BadRequest("Tenant already exists");
            var group = new
            {
                description = tenant.Description,
                mailNickname = tenant.Name,
                displayName = tenant.Name,
                groupTypes = new string[] { },
                mailEnabled = false,
                securityEnabled = true,
            };
            // add user who created this group as both owner and member
            var jGroup = JObject.FromObject(group);
            var owners = new string[] { $"{Graph.BaseUrl}users/{tenant.UserObjectId}" };
            jGroup.Add("owners@odata.bind", JArray.FromObject(owners));
            jGroup.Add("members@odata.bind", JArray.FromObject(owners));
            //  https://docs.microsoft.com/en-us/graph/api/group-post-groups?view=graph-rest-1.0&tabs=http
            resp = await http.PostAsync(
                $"{Graph.BaseUrl}groups",
                new StringContent(jGroup.ToString(), System.Text.Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
                return BadRequest("Tenant creation failed");
            var json = await resp.Content.ReadAsStringAsync();
            var newGroup = JObject.Parse(json);
            var id = newGroup["id"].Value<string>();
            // add this group to the user's tenant collection
            return new OkObjectResult(new { Id = id });
        }

        [HttpGet("forUser")]
        public async Task<IActionResult> GetForUser(string userId)
        {
            if ((string.IsNullOrEmpty(userId) || (string.IsNullOrEmpty(userId))))
                return BadRequest("Invalid parameters");

            var http = await _graph.GetClientAsync();
            // does the user exist?
            Guid guid;
            if (!Guid.TryParse(userId, out guid))
                return BadRequest("Invalid user id");
            try
            {
                var json = await http.GetStringAsync($"{Graph.BaseUrl}users/{userId}/memberOf");
                var groups = JObject.Parse(json)["value"].Value<JArray>();
                var membership = new List<Member>();
                foreach (var group in groups)
                {
                    var id = group["id"].Value<string>();
                    json = await http.GetStringAsync($"{Graph.BaseUrl}groups/{id}/owners");
                    var values = JObject.Parse(json)["value"].Value<JArray>();
                    var admin = values.FirstOrDefault(u => u["id"].Value<string>() == userId);
                    membership.Add(new Member
                    {
                        GroupId = group["id"].Value<string>(),
                        IsAdmin = admin != null
                    });
                }
                return new JsonResult(membership);
            }
            catch (HttpRequestException ex)
            {
                return BadRequest("Unable to validate user id");
            }
        }

        [HttpPut("{tenantId}")]
        public async Task<IActionResult> AddMember(Guid tenantId, Guid userId)
        {
            var http = await _graph.GetClientAsync();
            var resp = await http.PostAsync(
                $"{Graph.BaseUrl}groups/{tenantId}/members/$ref",
                new StringContent(
                    $"{{\"@odata.id\": \"https://graph.microsoft.com/v1.0/directoryObjects/{userId}\"}}}",
                    System.Text.Encoding.UTF8,
                    "application/json"));
            if (!resp.IsSuccessStatusCode)
                return BadRequest("Add member failed");
            return new OkResult();
        }
    }

    public class TenantDef
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string UserObjectId { get; set; }
    }
    public class Member
    {
        public string GroupId;
        public bool IsAdmin;
    }
}
