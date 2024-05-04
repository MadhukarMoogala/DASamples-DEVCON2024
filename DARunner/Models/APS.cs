using Autodesk.Forge.Core;
using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.DesignAutomation.Model;
using Autodesk.SDKManager;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Activity = Autodesk.Forge.DesignAutomation.Model.Activity;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using DARunner.Services;
using Spectre.Console;

namespace DARunner.Models
{
    public partial class APS
    {
        private readonly SDKManager _sdkManager;
        private readonly string _clientId;
        private readonly string _clientSecret;  
        private readonly string _bucket;    
        private DesignAutomationClient _daClient;
        private ForgeConfiguration _config;
        private readonly string Owner = "xrefgetapp";
        private readonly string PackageName = "mergepdfexe";
        private readonly string Label = "prod";
        private readonly string TargetEngine = "Autodesk.AutoCAD+24_3";
        private readonly string ActivityName = "mergepdf";

        public APS(DesignAutomationClient daApi, IOptions<ForgeConfiguration> config)
        {
            _sdkManager = SdkManagerBuilder.Create().Build();
            _config = config.Value;
            _clientId = _config.ClientId;
            _clientSecret = _config.ClientSecret;           
            _daClient = daApi;
            _bucket = $"darunner-{DateTimeOffset.Now.ToUnixTimeSeconds()}";
        }

            
        public async Task RunAsync()
        {
            
            WorkItemService ws = new WorkItemService(this, _daClient);
            await Task.Run(() => {
                AnsiConsole.Progress().AutoRefresh(true).Columns(
               [
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
               ]).Start(ctx =>
               {
                   var task = ctx.AddTask("[green]Setting up appbundle[/]", new ProgressTaskSettings { MaxValue = 100 });
                   var myApp = SetupAppBundleAsync().Result;
                   task.Increment(100);
                   task.Description = "[green]Setting up activity[/]";
                   task.MaxValue = 100;
                   var myActivity = CreateVarArgActivityAsync(myApp).Result;
                   task.Increment(100);
                   task.Description = "[green]Setting up Objects[/]";
                   task.MaxValue = 100;                  
                   var objectDetails = ws.SetupObjectIdsAysnc().Result;
                   task.Increment(100);
                   task.Description = "[green]Running workitem[/]";
                   task.MaxValue = 100;
                   var wiTask = ws.RunWorkItemCombinatorAsync(objectDetails).Result;
                   task.Increment(100);
                   task.Description = "[green]Completed! Downloading the assets[/]";
                   task.MaxValue = 100;
                   ws.DownloadFilesAsync(objectDetails,wiTask.Item2).Wait();
                   task.Increment(100);
               });
            });
        }
        private async Task<string> SetupAppBundleAsync()
        {
            Console.WriteLine("Setting up appbundle...");
            var myApp = $"{Owner}.{PackageName}+{Label}";
            var appResponse = await _daClient.AppBundlesApi.GetAppBundleAsync(myApp, throwOnError: false);
            var app = new AppBundle()
            {
                Engine = TargetEngine,
                Id = PackageName
            };
            var package = CreateZip();
            if (appResponse.HttpResponse.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"\tCreating appbundle {myApp}...");
                await _daClient.CreateAppBundleAsync(app, Label, package);
                return myApp;
            }
            await appResponse.HttpResponse.EnsureSuccessStatusCodeAsync();
            Console.WriteLine("\tFound existing appbundle...");
            if (!await EqualsAsync(package, appResponse.Content.Package))
            {
                Console.WriteLine($"\tUpdating appbundle {myApp}...");
                await _daClient.UpdateAppBundleAsync(app, Label, package);
            }
            return myApp;

            async Task<bool> EqualsAsync(string a, string b)
            {
                Console.Write("\tComparing bundles...");
                using var aStream = File.OpenRead(a);
                var bLocal = await DownloadToDocsAsync(b, "das-appbundle.zip");
                using var bStream = File.OpenRead(bLocal);
                using var hasher = SHA256.Create();
                var res = hasher.ComputeHash(aStream).SequenceEqual(hasher.ComputeHash(bStream));
                Console.WriteLine(res ? "Same." : "Different");
                return res;
            }
        }

        // Create a vararg activity with the required parameters "...", this is must to be able to use the combinator workitem
        private async Task<string> CreateVarArgActivityAsync(string myApp)
        {
            Console.WriteLine("Setting up activity...");
            var myActivity = $"{Owner}.{ActivityName}+{Label}";
            var actResponse = await _daClient.ActivitiesApi.GetActivityAsync(myActivity, throwOnError: false);
            var activity = new Activity()
            {
                Appbundles =
                    [
                        myApp
                    ],
                CommandLine =
                    [
                        $"\"$(appbundles[{PackageName}].path)\\MergePDF.bundle\\Contents\\MergePDF.exe\""
                    ],
                Engine = TargetEngine,                
                Parameters = new Dictionary<string, Parameter>()
                    {
                        { "...", new Parameter()},
                        { "final", new Parameter() { Verb = Verb.Put, LocalName = "final.pdf"} }
                    },
                Id = ActivityName
            };
            if (actResponse.HttpResponse.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Creating activity {myActivity}...");
                await _daClient.CreateActivityAsync(activity, Label);
                return myActivity;
            }
            await actResponse.HttpResponse.EnsureSuccessStatusCodeAsync();
            Console.WriteLine("\tFound existing activity...");
            if (!Equals(activity, actResponse.Content))
            {
                Console.WriteLine($"\tUpdating activity {myActivity}...");
                await _daClient.UpdateActivityAsync(activity, Label);
            }
            return myActivity;

            bool Equals(Activity a, Activity b)
            {
                Console.Write("\tComparing activities...");
                //ignore id and version
                b.Id = a.Id;
                b.Version = a.Version;
                var res = a.ToString() == b.ToString();
                Console.WriteLine(res ? "Same." : "Different");
                return res;
            }
        }

        static string CreateZip()
        {
            Console.WriteLine("\tGenerating autoloader zip...");
            string zip = Path.Combine(Directory.GetCurrentDirectory(), "package.zip");
            if (!File.Exists(zip))
            {
                zip = @"D:\Work\Projects\Devcon2024\Samples\Files\package.zip";
            }
            return zip;
        }
        public async Task<string> DownloadToDocsAsync(string url, string localFile, bool isOauthRequired = false)
        {
            var report = Directory.GetCurrentDirectory();
            var fname = Path.Combine(report, localFile);
            if (File.Exists(fname))
                File.Delete(fname);
            using var client = new HttpClient();
            if (isOauthRequired)
            {
                Token oAuth = await GetInternalToken();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oAuth.AccessToken);

            }
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            using (var fs = new FileStream(fname, FileMode.CreateNew))
            {
                await response.Content.CopyToAsync(fs);
            }

            return fname;
        }

    }
}
