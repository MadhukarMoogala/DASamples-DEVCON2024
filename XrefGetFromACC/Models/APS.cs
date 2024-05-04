
using System;
using Autodesk.SDKManager;
using Autodesk.Authentication.Model;
using System.Collections.Generic;

namespace XrefGetFromACC.Models
{

    public class Tokens
    {
        public string? InternalToken;
        public string? PublicToken;
        public string? RefreshToken;
        public DateTime ExpiresAt;
    }
    public partial class APS
    {
        private readonly SDKManager _sdkManager;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _callbackUri;
        private readonly string _bucket;
        private readonly List<Scopes> InternalTokenScopes = [Scopes.ViewablesRead,Scopes.BucketCreate, Scopes.BucketRead, Scopes.DataRead, Scopes.DataWrite, Scopes.DataCreate,Scopes.CodeAll];
        private readonly List<Scopes> PublicTokenScopes = [Scopes.ViewablesRead];

        public APS(string clientId, string clientSecret, string callbackUri, string? bucket = null)
        {
            _sdkManager = SdkManagerBuilder.Create().Build();
            _clientId = clientId;
            _clientSecret = clientSecret;
            _callbackUri = callbackUri;
            _bucket = string.IsNullOrEmpty(bucket) ? $"xrefget-{DateTimeOffset.Now.ToUnixTimeSeconds()}" : bucket;
        }
    }
}
