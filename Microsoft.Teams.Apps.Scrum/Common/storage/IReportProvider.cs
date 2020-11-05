using Microsoft.Teams.Apps.Scrum.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Teams.Apps.Scrum.Common.storage
{
    public interface IReportProvider
    {
        Task<string> SaveReportAndGetUri(List<ScrumDetailsEntity> scrumUpdates, string conversationId, DateTimeOffset startTime, DateTimeOffset endTime);
    }
}
