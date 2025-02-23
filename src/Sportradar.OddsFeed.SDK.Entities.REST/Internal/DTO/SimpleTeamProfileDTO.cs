/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System.Diagnostics.Contracts;
using Sportradar.OddsFeed.SDK.Messages.REST;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO
{
    /// <summary>
    /// A data-transfer-object representing competitor's (simple team's) profile
    /// </summary>
    public class SimpleTeamProfileDTO
    {
        /// <summary>
        /// A <see cref="CompetitorDTO"/> representing the competitor represented by the current profile
        /// </summary>
        public readonly CompetitorDTO Competitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleTeamProfileDTO"/> class
        /// </summary>
        /// <param name="record">A <see cref="simpleTeamProfileEndpoint"/> containing information about the profile</param>
        public SimpleTeamProfileDTO(simpleTeamProfileEndpoint record)
        {
            Contract.Requires(record != null);
            Contract.Requires(record.competitor != null);

            Competitor = new CompetitorDTO(record.competitor);
        }
    }
}