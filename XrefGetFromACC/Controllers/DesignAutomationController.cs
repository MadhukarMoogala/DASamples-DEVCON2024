using Autodesk.Forge.Core;
using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.DesignAutomation.Model;
using Autodesk.Oss.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;
using XrefGetFromACC.Models;
using static System.Net.WebRequestMethods;

namespace XrefGetFromACC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DesignAutomationController : ControllerBase
    {
        public class StartWorkitemInput
        {
          public string? Data { get; set; }
        }
        private IHubContext<DesignAutomationHub> _hubContext;
        // Design Automation v3 API
        DesignAutomationClient _designAutomation;
        private readonly APS _aps;
        // Used to access the application folder (temp location for files & bundles)
        private IWebHostEnvironment _env;


        // Constructor, where env and hubContext are specified
        public DesignAutomationController(IHubContext<DesignAutomationHub> hubContext, DesignAutomationClient api, APS aps,IWebHostEnvironment env)
        {
            _designAutomation = api;
            _hubContext = hubContext;
            _aps = aps;
            _env = env;
        }
        [HttpPost("workitems")]
        public async Task<IActionResult> StartWorkitem([FromForm] StartWorkitemInput input)
        {
            string json = input.Data ?? "{}";
            JObject workItemData = JObject.Parse(json);      
            JToken? itemId = workItemData["itemUrl"] ?? null;
            JToken? browserConnectionId = workItemData["browserConnectionId"] ?? null;
            if(browserConnectionId == null  )
            {
                return BadRequest("Invalid data");
            }
            string connectionId = browserConnectionId.ToString();

            ObjectDetails objectDetails = await _aps.GetObjectId("result.zip", _env.ContentRootPath);
            if(objectDetails is null)
            {
                return BadRequest("Failed to create object id in Bucket");
            }
            var tokens = await AuthController.PrepareTokens(Request, Response, _aps); 
            
            //This token will have `userid` access to ACC data
            var bearerToken1 = $"Bearer {tokens?.InternalToken}";
            Console.WriteLine($"Bearer Token 1: {bearerToken1}");
            //This token is to upload result to OSS bucket
            var acmToken = await _aps.GetInternalToken();
            var bearerToken2 = $"Bearer {acmToken.AccessToken}";
            Console.WriteLine($"Bearer Token 2: {bearerToken2}");         

            var workitem = new WorkItem
            {
                ActivityId = "xrefgetapp.fetchxrefs+prod",
                Arguments = new Dictionary<string, IArgument>()
                {
                    {
                        "inputFile", new XrefTreeArgument()
                        {
                            Url = itemId?.ToString(),
                            Verb = Verb.RefGet,
                            Headers = new Dictionary<string, string>()
                            {
                                { "Authorization", bearerToken1 }
                            }
                        }
                    },
                    {
                        "etransmit", new XrefTreeArgument()
                        {
                            Verb = Verb.Put,
                            Url = objectDetails.ObjectId,                           
                            Headers = new Dictionary<string, string>()
                            {
                                { "Authorization", bearerToken2 }
                            }
                        }
                    }
                }
            };

            WorkItemStatus workItemStatus = await _designAutomation.CreateWorkItemAsync(workitem);
            MonitorWorkitem(connectionId, workItemStatus, objectDetails);
            return Ok(new { WorkItemId = workItemStatus.Id });
        }

        private async Task MonitorWorkitem(string browserConnectionId, WorkItemStatus workItemStatus, ObjectDetails obj)
        {
            try
            {

                while (!workItemStatus.Status.IsDone())
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    workItemStatus = await _designAutomation.GetWorkitemStatusAsync(workItemStatus.Id);
                    await _hubContext.Clients.Client(browserConnectionId).SendAsync("onComplete", workItemStatus.ToString());
                }
                using (var httpClient = new HttpClient())
                {
                    byte[] bs = await httpClient.GetByteArrayAsync(workItemStatus.ReportUrl);
                    string report = System.Text.Encoding.Default.GetString(bs);
                    await _hubContext.Clients.Client(browserConnectionId).SendAsync("onComplete", report);
                }

                if (workItemStatus.Status == Status.Success)
                {
                    var dlink = await _aps.GetSignedS3DownloadLink(obj.ObjectKey);
                    await _hubContext.Clients.Client(browserConnectionId).SendAsync("downloadResult", dlink);
                    Console.WriteLine("Congrats!");
                }

            }
            catch (Exception ex)
            {
                await _hubContext.Clients.Client(browserConnectionId).SendAsync("onComplete", ex.Message);
                Console.WriteLine(ex.Message);
            }
        }
    }

    
    public class DesignAutomationHub : Hub
    {
        public string GetConnectionId() { 
            return Context.ConnectionId; 
        }
    }

  

}
