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

        public BlobService(string connectionString)
        {
            this.initializeTask = new Lazy<Task>(() => this.InitializeAsync(connectionString));
        }

        private async Task InitializeAsync(string connectionString)
        {
            try
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                this.reportContainerClient = blobServiceClient.GetBlobContainerClient("test");
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackException(ex);
                this.telemetryClient.TrackTrace($"Exception : {ex.Message}");
            }
        }

        public async Task<string> SaveReportAndGetUri(List<ScrumDetailsEntity> scrumUpdates, string conversationId, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            await this.EnsureInitializedAsync();
            var csv = new StringBuilder();
            csv.AppendLine("Date,Name,Yesterday,Today,Blockers");
            foreach (ScrumDetailsEntity entity in scrumUpdates)
            {
                csv.AppendLine(entity.Timestamp.ToString() + "," + entity.Name + "," +
                    entity.Yesterday + "," + entity.Today + "," + entity.Blockers);
            }
            var imagePath = Path.Combine(Environment.CurrentDirectory, "architecture-resize.png");
            
            return this.GetBlobSasUri(this.reportContainerClient, "", null);
        }

        private string GetBlobSasUri(BlobContainerClient container, string blobName, string storedPolicyName = null)
        {
            StorageSharedKeyCredential key = new StorageSharedKeyCredential("27xnwgjign2tq", StorageKey);
            // Create a SAS token that's valid for one hour.
            BlobSasBuilder sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = "test",
                BlobName = "sample_test.txt",
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
