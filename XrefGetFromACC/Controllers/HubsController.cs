﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using XrefGetFromACC.Models;

namespace XrefGetFromACC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HubsController : ControllerBase
    {
        private readonly ILogger<HubsController> _logger;
        private readonly APS _aps;

        public HubsController(ILogger<HubsController> logger, APS aps)
        {
            _logger = logger;
            _aps = aps;
        }

        [HttpGet()]
        public async Task<ActionResult<string>> ListHubs()
        {
            var tokens = await AuthController.PrepareTokens(Request, Response, _aps);
            if (tokens == null)
            {
                return Unauthorized();
            }
            var hubs = await _aps.GetHubs(tokens);
            return JsonConvert.SerializeObject(hubs);
        }

        [HttpGet("{hub}/projects")]
        public async Task<ActionResult<string>> ListProjects(string hub)
        {
            var tokens = await AuthController.PrepareTokens(Request, Response, _aps);
            if (tokens == null)
            {
                return Unauthorized();
            }
            var projects = await _aps.GetProjects(hub, tokens);
            return JsonConvert.SerializeObject(projects);
        }

        [HttpGet("{hub}/projects/{project}/contents")]
        public async Task<ActionResult<string>> ListItems(string hub, string project, [FromQuery] string? folder_id)
        {
            var tokens = await AuthController.PrepareTokens(Request, Response, _aps);
            if (tokens == null)
            {
                return Unauthorized();
            }
            if (string.IsNullOrEmpty(folder_id))
            {
                var folders = await _aps.GetTopFolders(hub, project, tokens);
                return JsonConvert.SerializeObject(folders);
            }
            else
            {
                var contents = await _aps.GetFolderContents(project, folder_id, tokens);
                return JsonConvert.SerializeObject(contents);
            }
        }

        [HttpGet("{hub}/projects/{project}/contents/{item}/versions")]
        public async Task<ActionResult<string>> ListVersions(string hub, string project, string item)
        {
            var tokens = await AuthController.PrepareTokens(Request, Response, _aps);
            if (tokens == null)
            {
                return Unauthorized();
            }
            var versions = await _aps.GetVersions(project, item, tokens);
          
            return JsonConvert.SerializeObject(versions);
        }
      
    }
}
