/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/
using System;
using System.Diagnostics.Contracts;
using Sportradar.OddsFeed.SDK.Entities.REST.Enums;
using Sportradar.OddsFeed.SDK.Messages;
using Sportradar.OddsFeed.SDK.Messages.REST;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO
{
    /// <summary>
    /// A data-transfer-object containing basic information about a sport event
    /// </summary>
    public class SportEventSummaryDTO
    {
        /// <summary>
        /// Gets a <see cref="URN"/> specifying the id of the sport event associated with the current instance
        /// </summary>
        public URN Id { get; }

        /// <summary>
        /// Gets a <see cref="URN"/> specifying the id of the sport associated with the current instance
        /// </summary>
        public URN SportId { get; }

        /// <summary>
        /// Gets a <see cref="DateTime"/> specifying the scheduled start date for the sport event associated with the current instance
        /// </summary>
        public DateTime? Scheduled { get; }

        /// <summary>
        /// Gets a <see cref="DateTime"/> specifying the scheduled end time of the sport event associated with the current instance
        /// </summary>
        public DateTime? ScheduledEnd { get; }

        /// <summary>
        /// Gets a name o f the associated sport event
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets a <see cref="SportEventType"/> specifying the type of the associated sport event or a null reference if property is not applicable
        /// for the associated sport event
        /// </summary>
        /// <seealso cref="SportEventType"/>
        public SportEventType? Type { get; }

        /// <summary>
        /// Gets a <see cref="Nullable{bool}"/> specifying if the start time to be determined is set
        /// for the associated sport event
        /// </summary>
        public bool? StartTimeTbd { get; }

        /// <summary>
        /// Gets a <see cref="URN"/> specifying the replacement sport event
        /// for the associated sport event
        /// </summary>
        public URN ReplacedBy { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SportEventSummaryDTO"/> class
        /// </summary>
        /// <param name="sportEvent">A <see cref="sportEvent"/> containing basic information about the event</param>
        internal SportEventSummaryDTO(sportEvent sportEvent)
        {
            Contract.Requires(sportEvent != null);
            Contract.Requires(!string.IsNullOrEmpty(sportEvent.id));

            Id = URN.Parse(sportEvent.id);
            Scheduled = sportEvent.scheduledSpecified
                ? (DateTime?)sportEvent.scheduled
                : null;
            ScheduledEnd = sportEvent.scheduled_endSpecified
                ? (DateTime?)sportEvent.scheduled_end
                : null;
            if (sportEvent.tournament?.sport != null)
            {
                URN sportId;
                if (URN.TryParse(sportEvent.tournament.sport.id, out sportId))
                {
                    SportId = sportId;
                }
            }
            Name = sportEvent.name;
            SportEventType? type;
            if (RestMapperHelper.TryGetSportEventType(sportEvent.type, out type))
            {
                Type = type;
            }
            if (!string.IsNullOrEmpty(sportEvent.replaced_by))
            {
                URN replacedBy;
                if (URN.TryParse(sportEvent.replaced_by, out replacedBy))
                {
                    ReplacedBy = replacedBy;
                }
            }
            StartTimeTbd = sportEvent.start_time_tbdSpecified ? (bool?)sportEvent.start_time_tbd : null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SportEventSummaryDTO"/> class
        /// </summary>
        /// <param name="parentStage">A <see cref="parentStage"/> containing basic information about the event</param>
        protected SportEventSummaryDTO(parentStage parentStage)
        {
            Contract.Requires(parentStage != null);
            Contract.Requires(!string.IsNullOrEmpty(parentStage.id));

            Id = URN.Parse(parentStage.id);
            Scheduled = parentStage.scheduledSpecified
                ? (DateTime?)parentStage.scheduled
                : null;
            ScheduledEnd = parentStage.scheduled_endSpecified
                ? (DateTime?)parentStage.scheduled_end
                : null;
            //URN sportId;
            //if (URN.TryParse(parentStage.tournament?.sport?.id, out sportId))
            //{
            //    SportId = sportId;
            //}
            Name = parentStage.name;
            SportEventType? type;
            if (RestMapperHelper.TryGetSportEventType(parentStage.type, out type))
            {
                Type = type;
            }
            if (!string.IsNullOrEmpty(parentStage.replaced_by))
            {
                URN replacedBy;
                if (URN.TryParse(parentStage.replaced_by, out replacedBy))
                {
                    ReplacedBy = replacedBy;
                }
            }
            StartTimeTbd = parentStage.start_time_tbdSpecified ? (bool?)parentStage.start_time_tbd : null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SportEventSummaryDTO"/> class
        /// </summary>
        /// <param name="childStage">A <see cref="sportEventChildrenSport_event"/> containing basic information about the event</param>
        protected SportEventSummaryDTO(sportEventChildrenSport_event childStage)
        {
            Contract.Requires(childStage != null);
            Contract.Requires(!string.IsNullOrEmpty(childStage.id));

            Id = URN.Parse(childStage.id);
            Scheduled = childStage.scheduledSpecified
                ? (DateTime?)childStage.scheduled
                : null;
            ScheduledEnd = childStage.scheduled_endSpecified
                ? (DateTime?)childStage.scheduled_end
                : null;
            //URN sportId;
            //if (URN.TryParse(childStage.tournament?.sport?.id, out sportId))
            //{
            //    SportId = sportId;
            //}
            Name = childStage.name;
            SportEventType? type;
            if (RestMapperHelper.TryGetSportEventType(childStage.type, out type))
            {
                Type = type;
            }
            if (!string.IsNullOrEmpty(childStage.replaced_by))
            {
                URN replacedBy;
                if (URN.TryParse(childStage.replaced_by, out replacedBy))
                {
                    ReplacedBy = replacedBy;
                }
            }
            StartTimeTbd = childStage.start_time_tbdSpecified ? (bool?)childStage.start_time_tbd : null;
        }
    }
}