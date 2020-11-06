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
        private static string StorageKey = "YFcSuytGFVkQeR9MqQodZrZk0U0JntTK6SPlX0qCaCdMYIo25kL25xLsbwphwQPK60Red5Ky+SzyHJFl2TZC8A==";
        private readonly Lazy<Task> initializeTask;
        private TelemetryClient telemetryClient;
        private BlobContainerClient reportContainerClient;
        private static readonly string reportContainerName = "reports";

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
                    csv.AppendLine(entity.Timestamp.Date.ToString("d") + "," + entity.Name + "," +
                        entity.Yesterday + "," + entity.Today + "," + entity.Blockers);
                }
                var blobName = string.Format("{0}/{1}report.csv", conversationId, uuid);
                await File.WriteAllTextAsync(tmpPath, csv.ToString());
                var blobClient = this.reportContainerClient.GetBlobClient(blobName);
                FileStream uploadFileStream = File.OpenRead(tmpPath);
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
            StorageSharedKeyCredential key = new StorageSharedKeyCredential("pglb3kyqys7lu", StorageKey);
            // Create a SAS token that's valid for one hour.
            BlobSasBuilder sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = reportContainerName,
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
