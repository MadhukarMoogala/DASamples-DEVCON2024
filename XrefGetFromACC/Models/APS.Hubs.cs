﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.DataManagement;
using Autodesk.DataManagement.Http;
using Autodesk.DataManagement.Model;
namespace XrefGetFromACC.Models
{
    public partial class APS
    {
        public async Task<IEnumerable<HubsData>> GetHubs(Tokens tokens)
        {
            var dataManagementClient = new DataManagementClient(_sdkManager);
            var hubs = await dataManagementClient.GetHubsAsync(accessToken: tokens.InternalToken);
            return hubs.Data;
        }

        public async Task<IEnumerable<ProjectsData>> GetProjects(string hubId, Tokens tokens)
        {
            var dataManagementClient = new DataManagementClient(_sdkManager);
            var projects = await dataManagementClient.GetHubProjectsAsync(hubId, accessToken: tokens.InternalToken);
            return projects.Data;
        }

        public async Task<IEnumerable<TopFoldersData>> GetTopFolders(string hubId, string projectId, Tokens tokens)
        {
            var dataManagementClient = new DataManagementClient(_sdkManager);
            var folders = await dataManagementClient.GetProjectTopFoldersAsync(hubId, projectId, accessToken: tokens.InternalToken);
            return folders.Data;
        }

        public async Task<IEnumerable<FolderContentsData>> GetFolderContents(string projectId, string folderId, Tokens tokens)
        {
            var dataManagementClient = new DataManagementClient(_sdkManager);
            var contents = await dataManagementClient.GetFolderContentsAsync(projectId, folderId, accessToken: tokens.InternalToken);
            return contents.Data;
        }

        public async Task<IEnumerable<VersionsData>> GetVersions(string projectId, string itemId, Tokens tokens)
        {
            var dataManagementClient = new DataManagementClient(_sdkManager);
            var versions = await dataManagementClient.GetItemVersionsAsync(projectId, itemId, accessToken: tokens.InternalToken);
            return versions.Data;
        }
    }
}
