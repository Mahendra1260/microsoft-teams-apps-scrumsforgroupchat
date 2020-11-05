// <copyright file="ScrumDetails.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.Scrum.Models
{
    using System;
    using Microsoft.WindowsAzure.Storage.Table;

    /// <summary>
    /// Holds Scrum details.
    /// </summary>
    public class ScrumDetailsEntity : TableEntity
    {
        public string ThreadConversationId { get; set; }

        public string UniqueRowKey { get; set; }

        public string Name { get; set; }
        /// <summary>
        /// Gets or sets yesterday.
        /// </summary>
        public string Yesterday { get; set; }

        /// <summary>
        /// Gets or sets today.
        /// </summary>
        public string Today { get; set; }

        /// <summary>
        /// Gets or sets blockers.
        /// </summary>
        public string Blockers { get; set; }

        /// <summary>
        /// Gets or sets members Id in group.
        /// </summary>
        public string MembersActivityIdMap { get; set; }

        /// <summary>
        /// date of update time
        /// </summary>
        public new DateTimeOffset UpdateTime { get; set; }
        
    }
}
