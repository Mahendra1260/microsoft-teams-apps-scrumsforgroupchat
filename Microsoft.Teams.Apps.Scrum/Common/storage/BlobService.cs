using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Microsoft.ApplicationInsights;
using Microsoft.Teams.Apps.Scrum.Common.storage;
using Microsoft.Teams.Apps.Scrum.Models;

namespace Microsoft.Teams.Apps.Scrum.storage
{
    public class BlobService : IReportProvider
    {
        private static string StorageKey = "LuD6BVuaclPdj63ZSa1TGetZ83fdO+BkA8fPNO4CIqswFb25xyVx7kDH/ViG8GZNf8UxW4u6ilulxVY5Aet5CA==";
        private readonly Lazy<Task> initializeTask;
        private TelemetryClient telemetryClient;
        private BlobContainerClient reportContainerClient;
        private static readonly string reportContainerName = "reports";

        // https://27xnwgjign2tq.blob.core.windows.net/test?sv=2019-12-12&st=2020-11-04T13%3A41%3A24Z&se=2020-11-04T14%3A41%3A24Z&sr=b&sp=r&sig=61ZOV6TVjDlfPfA9X6AcNvpgAFkvnraZNPEzu0DZv2s%3D&comp=list&restype=container

        public BlobService(string connectionString)
        {
            this.initializeTask = new Lazy<Task>(() => this.InitializeAsync(connectionString));
        }

        private async Task InitializeAsync(string connectionString)
        {
            try
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                this.reportContainerClient = blobServiceClient.GetBlobContainerClient(reportContainerName);
                await this.reportContainerClient.CreateIfNotExistsAsync();                
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackException(ex);
                this.telemetryClient.TrackTrace($"Exception : {ex.Message}");
            }
        }

        public async Task<string> SaveReportAndGetUri(List<ScrumDetailsEntity> scrumUpdates, string conversationId, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            var blobUri = "";
            var uuid = System.Guid.NewGuid();
            var tmpPath = Path.Combine(Environment.CurrentDirectory, uuid + ".csv");
            try
            {
                await this.EnsureInitializedAsync();
                var csv = new StringBuilder();
                csv.AppendLine("Date,Name,Yesterday,Today,Blockers");
                foreach (ScrumDetailsEntity entity in scrumUpdates)
                {
                    csv.AppendLine(entity.Timestamp.ToString() + "," + entity.Name + "," +
                        entity.Yesterday + "," + entity.Today + "," + entity.Blockers);
                }
                var blobName = string.Format("/{0}/{1}/{2}/report.csv", conversationId, startTime.Date.ToString(), endTime.Date.ToString());
                await File.WriteAllTextAsync(tmpPath, csv.ToString());
                var blobClient = this.reportContainerClient.GetBlobClient(blobName);
                using FileStream uploadFileStream = File.OpenRead(tmpPath);
                await blobClient.UploadAsync(uploadFileStream, true);
                uploadFileStream.Close();
                blobUri = this.GetBlobSasUri(this.reportContainerClient, blobName, null);
            }
            finally {
                if (File.Exists(tmpPath)) {
                    File.Delete(tmpPath);
                }
            }
            return blobUri;
        }

        private string GetBlobSasUri(BlobContainerClient container, string blobName, string storedPolicyName = null)
        {
            StorageSharedKeyCredential key = new StorageSharedKeyCredential("27xnwgjign2tq", StorageKey);
            // Create a SAS token that's valid for one hour.
            BlobSasBuilder sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = "test",
                BlobName = blobName,
                Resource = "b",
            };

            if (storedPolicyName == null)
            {
                sasBuilder.StartsOn = DateTimeOffset.UtcNow;
                sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(1);
                sasBuilder.SetPermissions(BlobContainerSasPermissions.Read);
            }
            else
            {
                sasBuilder.Identifier = storedPolicyName;
            }

            // Use the key to get the SAS token.
            string sasToken = sasBuilder.ToSasQueryParameters(key).ToString();

            Console.WriteLine("SAS for blob is: {0}", sasToken);
            Console.WriteLine();

            return $"{container.GetBlockBlobClient(blobName).Uri}?{sasToken}";
        }

        private async Task EnsureInitializedAsync()
        {
            await this.initializeTask.Value;
        }
    }
    
}
