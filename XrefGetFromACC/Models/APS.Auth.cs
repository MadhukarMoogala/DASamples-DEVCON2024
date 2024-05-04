
using System;
using System.Threading.Tasks;
using Autodesk.Authentication;
using Autodesk.Authentication.Model;
using Newtonsoft.Json.Linq;


namespace XrefGetFromACC.Models
{
    public record Token(string AccessToken, DateTime ExpiresAt);
    public partial class APS
    {
        private Token? _internalTokenCache;
        private Token? _publicTokenCache;
        public string GetAuthorizationURL()
        {
            var authenticationClient = new AuthenticationClient(_sdkManager);
            return authenticationClient.Authorize(_clientId, ResponseType.Code, _callbackUri, InternalTokenScopes);
        }

        public async Task<Tokens> GenerateTokens(string code)
        {
            var authenticationClient = new AuthenticationClient(_sdkManager);
            var internalAuth = await authenticationClient.GetThreeLeggedTokenAsync(_clientId, _clientSecret, code, _callbackUri);
            var publicAuth = await authenticationClient.GetRefreshTokenAsync(_clientId, _clientSecret, internalAuth.RefreshToken, PublicTokenScopes);
            if(publicAuth == null || internalAuth == null)
            {
                throw new ApplicationException("Failed to get tokens.");
            }
            if(internalAuth.ExpiresIn == null   )
            {
                throw new ApplicationException("Failed to get refresh tokens.");
            }
            return new Tokens
            {
                PublicToken = publicAuth.AccessToken,
                InternalToken = internalAuth.AccessToken,
                RefreshToken = publicAuth._RefreshToken,
                ExpiresAt = DateTime.Now.ToUniversalTime().AddSeconds((double)internalAuth.ExpiresIn)
            };
        }

        public async Task<Tokens> RefreshTokens(Tokens tokens)
        {
            var authenticationClient = new AuthenticationClient(_sdkManager);
            var internalAuth = await authenticationClient.GetRefreshTokenAsync(_clientId, _clientSecret, tokens.RefreshToken, InternalTokenScopes);
            var publicAuth = await authenticationClient.GetRefreshTokenAsync(_clientId, _clientSecret, internalAuth._RefreshToken, PublicTokenScopes);
            if (internalAuth.ExpiresIn == null)
            {
                throw new ApplicationException("Failed to get refresh tokens.");
            }
            return new Tokens
            {
                PublicToken = publicAuth.AccessToken,
                InternalToken = internalAuth.AccessToken,
                RefreshToken = publicAuth._RefreshToken,
                ExpiresAt = DateTime.Now.ToUniversalTime().AddSeconds((double)internalAuth.ExpiresIn)
            };
        }

        public async Task<UserInfo> GetUserProfile(Tokens tokens)
        {
            var authenticationClient = new AuthenticationClient(_sdkManager);
            UserInfo userInfo = await authenticationClient.GetUserInfoAsync(tokens.InternalToken);
            return userInfo;
        }

        private async Task<Token> GetToken(List<Scopes> scopes)
        {
            var authenticationClient = new AuthenticationClient(_sdkManager);
            var auth = await authenticationClient.GetTwoLeggedTokenAsync(_clientId, _clientSecret, scopes);
            if(auth.ExpiresIn == null)
            {
                throw new ApplicationException("Failed to get tokens.");
            }
            return new Token(auth.AccessToken, DateTime.UtcNow.AddSeconds(value: (double)auth.ExpiresIn));
        }

        public async Task<Token> GetPublicToken()
        {
            if (_publicTokenCache == null || _publicTokenCache.ExpiresAt < DateTime.UtcNow)
                _publicTokenCache = await GetToken(new List<Scopes> { Scopes.ViewablesRead });
            return _publicTokenCache;
        }

        public async Task<Token> GetInternalToken()
        {
            if (_internalTokenCache == null || _internalTokenCache.ExpiresAt < DateTime.UtcNow)
                _internalTokenCache = await GetToken([Scopes.BucketCreate, Scopes.BucketRead, Scopes.DataRead, Scopes.DataWrite, Scopes.DataCreate]);
            return _internalTokenCache;
        }
    }
}
