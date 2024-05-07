using Autodesk.Forge.Core;
using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.DesignAutomation.Model;
using Autodesk.Oss.Model;
using DARunner.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace DARunner.Services
{
    public class DebugArgument : IArgument
    {
        [DataMember(Name = "uploadJobFolder", EmitDefaultValue = false)]
        public bool UploadJobFolder { get; set; }       
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
    public class WorkItemService
    {
        private readonly HttpClient _client;
        private APS _aps;
        private DesignAutomationClient _daClient;
        public WorkItemService(APS aps, DesignAutomationClient client)
        {
            _client = new HttpClient
            {
                BaseAddress = new Uri("https://developer.api.autodesk.com/da/us-east/")
            };
            _aps = aps;
            _daClient = client;
        }
        public async Task<(List<WorkItemStatus>, WorkItemStatus)> CreateWorkItemsCombineAsync(
        List<WorkItem> partsWorkItems,
        WorkItem combinatorWorkItem,
        IDictionary<string, string> headers)
        {
            try
            {
                var payload = new JObject
                {
                    ["parts"] = JArray.FromObject(partsWorkItems),
                    ["combinator"] = JObject.FromObject(combinatorWorkItem)
                };
                
                var request = new HttpRequestMessage(HttpMethod.Post,
                                 requestUri: Marshalling.BuildRequestUri("/v3/workitems/combine",
                                 routeParameters: new Dictionary<string, object>(),
                                 queryParameters: new Dictionary<string, object>()))
                {
                    Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json")
                };

                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                

                using (var response = await _client.SendAsync(request))
                {
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonObj = JObject.Parse(content);
                    var partsStatus = jsonObj["parts"]?.ToObject<List<WorkItemStatus>>() ?? new List<WorkItemStatus>();
                    var combinatorStatus = jsonObj["combinator"]?.ToObject<WorkItemStatus>() ?? new WorkItemStatus();
                    return (partsStatus, combinatorStatus);
                }
            }
            catch (HttpRequestException ex)
            {
                // Handle HTTP request exceptions
                Console.WriteLine($"HTTP Error: {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                // Handle JSON parsing exceptions
                Console.WriteLine($"JSON Error: {ex.Message}");
                throw;
            }
        }
        public async Task<(WorkItemStatus, List<Task<WorkItemStatus>>)> RunWorkItemCombinatorAsync(List<ObjectDetails> objectDetails)
        {
            try
            {

                
                var combineWorkItem = await GetCombinatorWorkItem(objectDetails[0].ObjectId, objectDetails[1].ObjectId, objectDetails[2].ObjectId);

                var partWorkItems = new List<WorkItem>();
                foreach (var part in new string[] { "first.dwg", "second.dwg" })
                {
                    var partDetails = await _aps.GetObjectId(part, Path.Combine(Directory.GetCurrentDirectory(), part), false);
                    var inputKey = part.Replace(".dwg", ".pdf");
                    var partWorkItem = await GetPartWorkItem(partDetails.ObjectId, inputKey);
                    partWorkItems.Add(partWorkItem);
                }

                var oauth = await _aps.GetInternalToken();
                var headers = new Dictionary<string, string>()
                {
                    { "Content-Type", "application/json" },
                    { "Authorization", $"Bearer {oauth.AccessToken}" }
                };

                var status = await CreateWorkItemsCombineAsync(partWorkItems, combineWorkItem, headers);

                var partsStatuses = status.Item1.ToArray();
                var partsStatusTaskList = new List<Task<WorkItemStatus>>();
                foreach (var partStatus in partsStatuses)
                {
                    partsStatusTaskList.Add(RunWorkItemStatusQueryAsync(partStatus));
                }
                var partsStatusTask = Task.WhenAll(partsStatusTaskList);

                var combinatorStatusTask = RunWorkItemStatusQueryAsync(status.Item2);
                await Task.WhenAll(partsStatusTask, combinatorStatusTask);

                // get combinator report
                var combinatorStatus = combinatorStatusTask.Result;
                var prefix = combinatorStatus.Status == Status.Success ? "ok" : "err";
                var combinatorReport = await _aps.DownloadToDocsAsync(combinatorStatus.ReportUrl, $"{prefix}_combinator_{combinatorStatus.Id}.log");
                Console.WriteLine(
                     $"Combinator workitem: {combinatorStatusTask.Result.Id}, " +
                     $"Report: {combinatorStatusTask.Result.ReportUrl}\n"
                     );
                                
                return (combinatorStatusTask.Result, partsStatusTaskList);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        public async Task<List<ObjectDetails>> SetupObjectIdsAysnc()
        {
            var firstDetails = await _aps.GetObjectId("first.pdf", Directory.GetCurrentDirectory(), true);
            var secondDetails = await _aps.GetObjectId("second.pdf", Directory.GetCurrentDirectory(), true);
            var finalDetails = await _aps.GetObjectId("final.pdf", Directory.GetCurrentDirectory(), true);
            return [firstDetails, secondDetails, finalDetails];
        }
        public async Task DownloadFilesAsync(List<ObjectDetails> objectDetails, List<Task<WorkItemStatus>> partsStatusTaskList)
        {
            partsStatusTaskList.ForEach(part =>
            {
                Console.WriteLine(
                    $"Part workitem: {part.Result.Id}, " +
                    $"Report: {part.Result.ReportUrl}\n"
                    );
            });


            foreach (var objectDetail in objectDetails)
            {
                Console.WriteLine($"Downloading {objectDetail.ObjectKey}");
                var link = await _aps.GetSignedS3DownloadLink(objectDetail.ObjectKey);
                await _aps.DownloadToDocsAsync(link, objectDetail.ObjectKey);
            }
        }



        //  Create a part work item, essentially a work item that will process a single part of the final assembly
        public async Task<WorkItem> GetPartWorkItem(string objectId, string inputKey)
        {
            var token = await _aps.GetInternalToken();
            var bearerToken = "Bearer " + token.AccessToken;
            var workItem = new WorkItem()
            {
                ActivityId = "AutoCAD.PlotToPDF+prod",
                Arguments = new Dictionary<string, IArgument>()
                {
                   {
                       "HostDwg", new XrefTreeArgument()
                       {
                           Url = objectId,
                           Verb = Verb.Get,
                           Headers = new Dictionary<string, string>()
                           {
                               { "Authorization", bearerToken}
                           }
                       }
                   },
                   {
                       "Result", new XrefTreeArgument()
                       {
                           Verb = Verb.Put,
                           Url = $"das://intermediate/{inputKey}"

                       }
                   }
               }

            };
            Console.WriteLine($"\nCreated Part WI {JsonConvert.SerializeObject(workItem, Formatting.Indented)}");
            return workItem;
        }

        //  Create a combinator work item, essentially a work item that will combine the parts into the final assembly
        public async Task<WorkItem> GetCombinatorWorkItem(string first, string second,string final)
        {
            var token = await _aps.GetInternalToken();
            var bearerToken = "Bearer " + token.AccessToken;
            var workitem = new WorkItem()
            {
                ActivityId = "xrefgetapp.mergepdf+prod",
                Arguments = new Dictionary<string, IArgument>()
                {
                    {  "first", new XrefTreeArgument()
                                 {
                                     Verb = Verb.Put,
                                     Url = first,
                                     LocalName = "first.pdf",
                                     Headers = new Dictionary<string, string>()
                                     {
                                            { "Authorization", bearerToken}
                                     }
                                 }
                    },
                    {
                        "second", new XrefTreeArgument()
                                 {
                                     Verb = Verb.Put,
                                     Url = second,
                                     LocalName = "second.pdf",
                                     Headers = new Dictionary<string, string>()
                                     {
                                            { "Authorization", bearerToken}
                                     }
                                 }
                    },
                    { "final", new XrefTreeArgument()
                                {
                                    Verb = Verb.Put,
                                    Url = final,
                                    Headers = new Dictionary<string, string>()
                                    {
                                        { "Authorization", bearerToken}
                                    },
                                    LocalName = "final.pdf"
                                }
                    },
                    {
                        "adskDebug", new DebugArgument()
                        {
                            UploadJobFolder = true
                        }
                    },
                    {
                        "adskMask", new StringArgument()
                        {
                            Value = "true"
                        }
                    }
                },
                LimitProcessingTimeSec = 900
            };
            Console.WriteLine($"\nCreated Combine WI {JsonConvert.SerializeObject(workitem,Formatting.Indented)}");
            return workitem;
        }

        public async Task<WorkItemStatus> RunWorkItemStatusQueryAsync(WorkItemStatus status)
        {
            Console.WriteLine($"Created WI {status.Id}");
            while (!status.Status.IsDone())
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                status = await _daClient.GetWorkitemStatusAsync(status.Id);
                Console.WriteLine(status.Status.ToString());
            }
            var processingTime = (status.Stats?.TimeUploadEnded - status.Stats?.TimeDownloadStarted)?.TotalSeconds;
            var queueTime = (status.Stats?.TimeDownloadStarted - status.Stats?.TimeQueued)?.TotalSeconds;
            Console.WriteLine($"WorkIetm {status.Id} completed," +
                            $" result = {status.Status}," +
                            $" queue time = {queueTime}s, " +
                            $"processing time = {processingTime}s");
            if (status.ReportUrl != null)
            {
                var prefix = status.Status == Status.Success ? "ok" : "err";
                var report = await _aps.DownloadToDocsAsync(status.ReportUrl, $"{prefix}_{status.Id}.log");
                Console.WriteLine(report);
            }
            return status;
        }
    }
}
