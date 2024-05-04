using Autodesk.Oss.Model;
using Autodesk.Oss;

namespace XrefGetFromACC.Models
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
    public partial class APS
    {
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

        public async Task<ObjectDetails> GetObjectId(string objectKey, string tempDir)
        {
            await EnsureBucketExists(_bucket);
            var auth = await GetInternalToken();
            var ossClient = new OssClient(_sdkManager);
            ObjectDetails objectDetails;
            using (var tempFile = new TemporaryFile(tempDir))
            {
                // use the file through tempFile.FilePath...
                objectDetails = await ossClient.Upload(_bucket, objectKey,tempFile.FilePath,auth.AccessToken, new System.Threading.CancellationToken());
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
           
            var s3resp = await ossClient.CreateSignedResourceAsync(_bucket, objectKey, "read", 
                                                                   useCdn:true, createSignedResource: signedResponseBody,
                                                                   accessToken: auth.AccessToken);
            return s3resp.SignedUrl;
        } 
    }
}
