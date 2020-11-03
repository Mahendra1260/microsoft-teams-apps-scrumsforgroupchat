// <copyright file="ScrumProvider.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.AskHR.Common.Providers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Net;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights;
    using Microsoft.Teams.Apps.Scrum.Common;
    using Microsoft.Teams.Apps.Scrum.Models;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;

    /// <summary>
    /// ScrumProviders which will help in fetching and storing information in storage table.
    /// </summary>
    public class ScrumProvider : IScrumProvider
    {
        private const string PartitionKey = "ScrumInfo";
        private const string UpdatesPartitionKey = "ScrumUpdates";

        private readonly Lazy<Task> initializeTask;
        private CloudTable scrumCloudTable;
        private CloudTable scrumUpdateTable;

        private TelemetryClient telemetryClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScrumProvider"/> class.
        /// </summary>
        /// <param name="connectionString">connection string of storage provided by DI.</param>
        public ScrumProvider(string connectionString)
        {
            this.initializeTask = new Lazy<Task>(() => this.InitializeAsync(connectionString));
         }

        /// <summary>
        /// Store or update scrum entity in table storage.
        /// </summary>
        /// <param name="scrum">scrumEntity.</param>
        /// <returns><see cref="Task"/> that represents configuration entity is saved or updated.</returns>
        public async Task<bool> SaveOrUpdateScrumAsync(ScrumEntity scrum)
        {
            try
            {
                scrum.PartitionKey = PartitionKey;
                scrum.RowKey = scrum.ThreadConversationId;
                var result = await this.StoreOrUpdateScrumEntityAsync(scrum, this.scrumCloudTable);
                return result.HttpStatusCode == (int)HttpStatusCode.NoContent;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackException(ex);
                this.telemetryClient.TrackTrace($"Exception : {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SaveOrUpdateScrumUpdatesAsync(ScrumDetailsEntity scrumDetail)
        {
            try
            {
                scrumDetail.PartitionKey = UpdatesPartitionKey;
                scrumDetail.RowKey = scrumDetail.UniqueRowKey;
                var result = await this.StoreOrUpdateScrumEntityAsync(scrumDetail, this.scrumUpdateTable);
                return result.HttpStatusCode == (int)HttpStatusCode.NoContent;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackException(ex);
                this.telemetryClient.TrackTrace($"Exception : {ex.Message}");
                return false;
            }
        }
   
        public async Task<Dictionary<string, List<ScrumDetailsEntity>>> GetScrumUpdates(string conversationId, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            TableQuery<ScrumDetailsEntity> scrumDetailsQuery = new TableQuery<ScrumDetailsEntity>().
                     Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, UpdatesPartitionKey))
                     .Where(TableQuery.GenerateFilterCondition("ThreadConversationId", QueryComparisons.Equal, conversationId))
                     .Where(TableQuery.GenerateFilterConditionForDate("Timestamp", QueryComparisons.GreaterThanOrEqual, startTime))
                     .Where(TableQuery.GenerateFilterConditionForDate("Timestamp", QueryComparisons.GreaterThanOrEqual, endTime));
            TableContinuationToken token = null;
            var updatesDictionary = new Dictionary<string, List<ScrumDetailsEntity>>();
            do
            {
                TableQuerySegment<ScrumDetailsEntity> resultSegment = await this.scrumUpdateTable.ExecuteQuerySegmentedAsync(scrumDetailsQuery, token);
                token = resultSegment.ContinuationToken;

                foreach (var scrumUpdate in resultSegment.Results)
                {
                    if (!updatesDictionary.ContainsKey(scrumUpdate.Name))
                    {
                        updatesDictionary[scrumUpdate.Name] = new List<ScrumDetailsEntity>();
                    }

                    updatesDictionary[scrumUpdate.Name].Add(scrumUpdate);
                }
            } while (token != null);
            return updatesDictionary;
        }

        /// <inheritdoc/>
        public async Task<ScrumEntity> GetScrumAsync(string conversationId)
        {
            var searchResult = new TableResult();
            try
            {
                await this.EnsureInitializedAsync();
                var searchOperation = TableOperation.Retrieve<ScrumEntity>(PartitionKey, conversationId);
                searchResult = await this.scrumCloudTable.ExecuteAsync(searchOperation);

                return (ScrumEntity)searchResult.Result;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackException(ex);
                this.telemetryClient.TrackTrace($"Exception : {ex.Message}");
                return (ScrumEntity)searchResult.Result;
            }
        }

        /// <summary>
        /// Store or update scrum entity in table storage.
        /// </summary>
        /// <param name="entity">entity.</param>
        /// <returns><see cref="Task"/> that represents configuration entity is saved or updated.</returns>
        private async Task<TableResult> StoreOrUpdateScrumEntityAsync(TableEntity entity, CloudTable table)
        {
            await this.EnsureInitializedAsync();
            TableOperation addOrUpdateOperation = TableOperation.InsertOrReplace(entity);
            return await table.ExecuteAsync(addOrUpdateOperation);
        }

        /// <summary>
        /// Create scrums table if it doesnt exists.
        /// </summary>
        /// <param name="connectionString">storage account connection string.</param>
        /// <returns><see cref="Task"/> representing the asynchronous operation task which represents table is created if its not existing.</returns>
        private async Task InitializeAsync(string connectionString)
        {
            try
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
                CloudTableClient cloudTableClient = storageAccount.CreateCloudTableClient();
                this.scrumCloudTable = cloudTableClient.GetTableReference("Scrum");
                this.scrumUpdateTable = cloudTableClient.GetTableReference("ScrumUpdates");
                await this.scrumCloudTable.CreateIfNotExistsAsync();
                await this.scrumUpdateTable.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackException(ex);
                this.telemetryClient.TrackTrace($"Exception : {ex.Message}");
            }
        }

        /// <summary>
        /// Initialization of InitializeAsync method which will help in creating table.
        /// </summary>
        /// <returns>Task.</returns>
        private async Task EnsureInitializedAsync()
        {
            await this.initializeTask.Value;
        }
    }
}
