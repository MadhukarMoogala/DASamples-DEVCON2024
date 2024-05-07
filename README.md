# APS Design Automation Gets Boost

### Consolidated APS Design Automation Samples

This project showcases various new features of APS Design Automation through a collection of samples. These samples demonstrate:

- **Reference Downloading**
- **WorkItem Combine API**
- **DAS Intermediate Storage**
- **Activity Variable Arguments**

### **Reference Downloading**

Design Automation can now automatically follow and download references embedded within design files hosted on Autodesk Construction Cloud (ACC). Client applications can trigger this functionality by using the `refget` verb instead of `get` within an argument inside a workitem.*

##### Sample Activity

```csharp
 var activity = new Activity()
 {
     CommandLine =
     [
         "$(engine.path)\\accoreconsole.exe /i \"$(args[inputFile].path)\" /s $(settings[script].path)"
     ],
     Engine = "Autodesk.AutoCAD+24_3",
     Id = "fetchxrefs",
     Parameters = new Dictionary<string, Parameter>()
     {
         {
             "inputFile", new Parameter()
             {
                 Verb = Verb.RefGet,
                 Required = true
             }
         },
         {
             "etransmit", new Parameter()
             {
                 Verb = Verb.Put,
                 Required = true,
                 LocalName = "adskFiles",
                 Zip = true
             }
         }
     },
     Description = "Fetch all xref from host drawing"
 };
```

##### Sample WorkItem

```csharp
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
```

Note: 

- `bearerToken1` :
  
  - This is an OAuth token acquired through a 3-legged workflow. 
  
  - It contains a special claim named `userid` that identifies the user associated with the token.

- `bearerToken2`:
  
  - This is an OAuth token acquired through a 2-legged workflow. 
  
  - This token is useful in server-to-server workflows, such as creating Object Storage Service (OSS) buckets or running Design Automation jobs.

##### Sample Report Log

```json
{
  "Roots": [
    {
      "DownloadRoot": "T:\\Aces\\Jobs\\64f6331ba8b7480e844707caa0a29054\\adskFiles",
      "ReferencedSupportFilesMap": {},
      "OriginalReference": "https://developer.api.autodesk.com/data/v1/projects/b.adeb4f3b-1ee0-4fca-bb80-30934ae15668/items/urn:adsk.wipprod:dm.lineage:Q7lW1p_IR4K36AFeW-5law",
      "TypeId": "root",
      "LocalPath": "T:\\Aces\\Jobs\\64f6331ba8b7480e844707caa0a29054\\adskFiles/b.adeb4f3b-1ee0-4fca-bb80-30934ae15668/Project Files/Manufacturing/Cover Sheet.dwg",
      "DownloadSizeInBytes": 63626,
      "TimeStarted": "2024-05-04T05:05:06.6371778Z",
      "TimeXrefDiscoveryStarted": "2024-05-04T05:05:07.512109Z",
      "TimeXrefDiscoveryEnded": "2024-05-04T05:05:08.8528443Z",
      "TimeFinished": "2024-05-04T05:05:10.4396577Z",
      "Steps": [
        {
          "Reference": "https://developer.api.autodesk.com/data/v1/projects/b.adeb4f3b-1ee0-4fca-bb80-30934ae15668/items/urn:adsk.wipprod:dm.lineage:Q7lW1p_IR4K36AFeW-5law",
          "TimeDownloadStarted": "2024-05-04T05:05:06.6377665Z",
          "TimeDownloadEnded": "2024-05-04T05:05:07.4683883Z",
          "HttpCalls": [
            {
              "Method": "GET",
              "Uri": "https://developer.api.autodesk.com/data/v1/projects/b.adeb4f3b-1ee0-4fca-bb80-30934ae15668/items/urn:adsk.wipprod:dm.lineage:Q7lW1p_IR4K36AFeW-5law?includePathInProject=true",
              "StatusCode": 200,
              "Start": "2024-05-04T05:05:06.6762594Z",
              "End": "2024-05-04T05:05:07.1810062Z"
            },
            {
              "Method": "GET",
              "Uri": "https://developer.api.autodesk.com/oss/v2/buckets/wip.dm.prod/objects/28baf714-498c-470e-bc0a-ece83f9b0764.dwg/signeds3download",
              "StatusCode": 200,
              "Start": "2024-05-04T05:05:07.1958838Z",
              "End": "2024-05-04T05:05:07.317837Z"
            },
            {
              "Method": "GET",
              "Uri": "https://s3.amazonaws.com/com.autodesk.oss-persisten",
              "StatusCode": 200,
              "Start": "2024-05-04T05:05:07.3208233Z",
              "End": "2024-05-04T05:05:07.4050276Z"
            },
            {
              "Method": "STREAM",
              "Uri": "https://s3.amazonaws.com/com.autodesk.oss-persistent/",
              "StatusCode": 200,
              "Start": "2024-05-04T05:05:07.4052317Z",
              "End": "2024-05-04T05:05:07.4094669Z"
            },
            {
              "Method": "GET",
              "Uri": "https://api.userprofile.autodesk.com/userinfo",
              "StatusCode": 200,
              "Start": "2024-05-04T05:05:07.4116481Z",
              "End": "2024-05-04T05:05:07.4448657Z"
            }
          ]
        }
      ]      
```

- For more information visit [Reference Downloading | Design Automation API | Autodesk Platform Services](https://aps.autodesk.com/en/docs/design-automation/v3/developers_guide/reference-downloading/)

## **WorkItem Combine API**

The `workitems/combine` endpoint allows users to create a simple fan-in workflow. In this workflow, one to many workitems (parts) must complete before a final workitem (combinator) is processed.

**Requirements:**

- The activity referenced in the combinator should be a variable argument activity.

**Sample WorkItem Payload**

- Create a Part Workitem

- Compose an Array of Part Workitems

- Create a Combinator Workitem

- Where `xrefgetapp.mergepdf+prod` is an Variable Argument activity

- Payload for `workitems/combine` endpoint

```csharp
var partWorkItems = [partWorkItem1,partWorkItem2];
```

```csharp
 var combinatorWorkItem = new WorkItem()
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
         }
     }
 };
```

- Payload for `workitems/combine` endpoint

```csharp
 var payload = new JObject
 {
     ["parts"] = JArray.FromObject(partsWorkItems),
     ["combinator"] = JObject.FromObject(combinatorWorkItem)
 };
```

**Explanation:**

- **parts**: The WorkItem payload should have at least one output argument with a `das://intermediate/` URL.
- **combinator**: A WorkItem payload for a variable argument (vararg) activity. The variable arguments don't need to be explicitly supplied because the outputs of the parts will be automatically wired here. Other, non-variable arguments can be provided explicitly as usual.

**For more information:**

- WorkItem Combine API Reference: [APIs | Autodesk Platform Services](https://aps.autodesk.com/en/docs/design-automation/v3/reference/http/workitems-POST/)

## **DAS Intermediate Storage**

The `das://intermediate` scheme allows you to store intermediate data between workitems. This can significantly improve performance by avoiding unnecessary downloads and uploads within a workflow.

##### A Sample Workitem

```csharp
var partworkItem = new WorkItem()
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
```

In this example, the URL for the result argument is assigned to an intermediate storage location.

**Combinator workflows** can leverage intermediate storage for part workitems. This helps avoid downloading and uploading artifacts that are only necessary for the combinator workflow itself.

## **Activity Variable Arguments**

An activity may need to process a variable number of inputs or generate a variable number of outputs. To describe such activities, authors use a specially named parameter: "..."

##### Sample VarArg Activity

```csharp
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
```

## Steps To Build

#### Prerequistes

- APS Account
  
  - If you don't have an APS account yet, sign up for one on [https://aps.autodesk.com](https://aps.autodesk.com/).
  - [Getting Started | Autodesk Platform Services Tutorials](https://tutorials.autodesk.io/)

- [Autodesk.SdkManager](https://www.nuget.org/packages/Autodesk.SdkManager)

- [Autodesk.Authentication](https://www.nuget.org/packages/Autodesk.Authentication)

- [Autodesk.OSS](https://www.nuget.org/packages/Autodesk.OSS)

- [Autodesk.DataManagement](https://www.nuget.org/packages/Autodesk.DataManagement)

- [Design Automation SDK](https://github.com/MadhukarMoogala/forge-api-dotnet-design.automation)
  
  - The current release does not yet support `refget`, so you need to use the fork where I made the fix.

- [PDFSharp](https://www.nuget.org/packages/PDFsharp/6.1.0-preview-3)
  
  - You need to use exactly this version ([6.1.0] or higher). Previous versions lower than the current version will throw an exception while reading PDFs produced by the `AutoCAD.PlotToPDF+prod` activity.
  
  

- Visual Studio 2022 with .NET workloads
  
  - .NET 8.0 to build entire sample project.

#### Build

**APS Credentials**

All projects that use APS services require APS credentials. Here's the process:

- After Cloning the Entire Repository** (Assuming you haven't already)

- **Create `appsettings.user.json`** file to the following projects:
  
  - DARunner
  
  - XrefGetFromACC
  
  - Go to properties and set  `Copy to Output Directory` to `Copy always` .

**appsettings.user.json**

```json
{
  "APS_CLIENT_ID": "",
  "APS_CLIENT_SECRET": "",
  "Forge": {
    "ClientId": "",
    "ClientSecret": "",
    "AuthenticationAddress": "https://developer.api.autodesk.com/authentication/v2/token",
    "DesignAutomation": {
      "BaseAddress": "https://developer.api.autodesk.com/da/us-east/",
      "WebSocket": {
        "Url": "wss://websockets.forgedesignautomation.io"
      }
    }
  }
}
```

```bash
git clone https://github.com/MadhukarMoogala/DASamples-DEVCON2024.git XrefGet
cd XrefGet
dotnet restore
dotnet build
```

**How To Run**

- Project: XrefGetFromACC

```bash
cd XrefGetFromACC
dotnet watch
```

- Project: MergePDF
  - It will pick up `first.pdf` and `second.pdf` from the `Files` folderd and merges to `final.pdf`

```bash
cd Files
dotnet run --project ..\MergePDF\MergePDF.csproj
```

- Project: DARunner

```bash
cd Files
dotnet run --project ..\DARunner\DARunner.csproj
```

**NOTE**

It's important to set the Files directory as the current working directory. All actions of picking artifacts take place from this directory.

## DEMO

### XrefGetFromACC

Todo

## Combinator

Todo

## License

This sample is licensed under the terms of the **Apache License 2.0**. Please see the [LICENSE](https://github.com/MadhukarMoogala/forge-api-dotnet-design.automation/blob/main/LICENSE) file for full details.

### Written by

Madhukar Moogala, [APS](https://aps.autodesk.com/)  , [@galakar](https://twitter.com/Galakar)
