using Autodesk.Authentication.Model;
using Autodesk.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Oss.Model;
using Autodesk.Oss;

namespace DARunner.Models
{
    public sealed class TemporaryFile : IDisposable
    {
        public TemporaryFile() :
          this(Path.GetTempPath())
        { }

        public TemporaryFile(string directory)
        {
            Create(Path.Combine(directory, Path.GetRandomFileName()));
        }

        ~TemporaryFile()
        {
            Delete();
        }

        public void Dispose()
        {
            Delete();
            GC.SuppressFinalize(this);
        }

        public string? FilePath { get; private set; }

        private void Create(string path)
        {
            FilePath = path;
            using (File.Create(FilePath)) { };
        }

        private void Delete()
        {
            if (FilePath == null) return;
            File.Delete(FilePath);
            FilePath = null;
        }
    }
    public record Token(string AccessToken, DateTime ExpiresAt);
    public partial class APS
    {
        private Token? _internalTokenCache;
        private Token? _publicTokenCache;
        private async Task<Token> GetToken(List<Scopes> scopes)
        {
            var authenticationClient = new AuthenticationClient(_sdkManager);
            var auth = await authenticationClient.GetTwoLeggedTokenAsync(_clientId, _clientSecret, scopes);
            if (auth.ExpiresIn == null)
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
                _internalTokenCache = await GetToken([Scopes.BucketCreate, Scopes.BucketRead, Scopes.DataRead, Scopes.DataWrite, Scopes.DataCreate,Scopes.CodeAll]);
            return _internalTokenCache;
        }

        public async Task<Token> DeleteBucketToken()
        {
            if (_internalTokenCache == null || _internalTokenCache.ExpiresAt < DateTime.UtcNow)
                _internalTokenCache = await GetToken([Scopes.BucketCreate, Scopes.BucketRead, Scopes.BucketDelete, Scopes.BucketUpdate, Scopes.DataRead, Scopes.DataWrite, Scopes.DataCreate, Scopes.DataSearch]);
            return _internalTokenCache;
        }
        private async Task EnsureBucketExists(string bucketKey)
        {
            const string region = "US";
            var auth = await GetInternalToken();
            var ossClient = new OssClient(_sdkManager);
            try
            {
                await ossClient.GetBucketDetailsAsync(bucketKey, accessToken: auth.AccessToken);
            }
            catch (OssApiException ex)
            {
                if (ex.HttpResponseMessage.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var payload = new CreateBucketsPayload
                    {
                        BucketKey = bucketKey,
                        PolicyKey = "transient"
                    };
                    await ossClient.CreateBucketAsync(region, payload, auth.AccessToken);
                }
                else
                {
                    throw;
                }
            }
        }

        public async Task<ObjectDetails> GetObjectId(string objectKey, string tempDir, bool isEmptyObject)
        {
            await EnsureBucketExists(_bucket);
            var auth = await GetInternalToken();
            var ossClient = new OssClient(_sdkManager);
            ObjectDetails objectDetails;
            if (isEmptyObject)
            {
                using (var tempFile = new TemporaryFile(tempDir))
                {
                    // use the file through tempFile.FilePath...
                    objectDetails = await ossClient.Upload(_bucket, objectKey, tempFile.FilePath, auth.AccessToken, new System.Threading.CancellationToken());
                }
            }
            else
            {
                if(File.Exists(tempDir))
                {

                    objectDetails = await ossClient.Upload(_bucket, objectKey, tempDir, auth.AccessToken, new System.Threading.CancellationToken());
                }
                else
                {
                    throw new ApplicationException("File does not exist.");
                }
            }            
            return objectDetails;
        }

        public async Task<string> GetSignedS3DownloadLink(string objectKey)
        {
            await EnsureBucketExists(_bucket);
            var auth = await GetInternalToken();
            var ossClient = new OssClient(_sdkManager);
            var signedResponseBody = new CreateSignedResource()
            {
                MinutesExpiration = 15,
                SingleUse = false
            };
            try {
                var responseMessage = await ossClient.HeadObjectDetailsAsync(_bucket, objectKey, accessToken: auth.AccessToken);
            }
            catch (OssApiException ex)
            {
                if (ex.HttpResponseMessage.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new ApplicationException("Object does not exist");
                }
                else
                {
                    throw;
                }
            }         
           
            var s3resp = await ossClient.CreateSignedResourceAsync(_bucket, objectKey, "read",
                                                                   useCdn: true, createSignedResource: signedResponseBody,
                                                                   accessToken: auth.AccessToken);

            return s3resp.SignedUrl;
        }

        public async Task CleanUp()
        {
            //delete all buckets
            var auth = await DeleteBucketToken();
            var ossClient = new OssClient(_sdkManager);
            try
            {
                var buckets = await ossClient.GetBucketsAsync(accessToken: auth.AccessToken);
                foreach (var bucket in buckets.Items)
                {
                    
                    var res = await ossClient.DeleteBucketAsync(bucket.BucketKey, auth.AccessToken, throwOnError:false);
                    if(res.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        Console.WriteLine($"Bucket {bucket.BucketKey} deleted.");
                    }
                    else
                    {
                        continue;
                    }
                }
            }
            catch (OssApiException)
            {

                throw;
            }
           
        }
    }
}
