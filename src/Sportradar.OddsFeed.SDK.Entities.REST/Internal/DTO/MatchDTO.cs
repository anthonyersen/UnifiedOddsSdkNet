﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/
using System.Diagnostics.Contracts;
using System.Linq;
using Sportradar.OddsFeed.SDK.Messages.REST;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO
{
    /// <summary>
    /// A data-transfer-object containing basic information about a match
    /// </summary>
    public class MatchDTO : CompetitionDTO
    {
        /// <summary>
        /// Gets a <see cref="SportEntityDTO"/> instance specifying the season to which the sport event associated with the current instance belongs to.
        /// </summary>
        public SportEntityDTO Season { get; }

        /// <summary>
        /// Gets a <see cref="RoundDTO"/> representing the tournament round to which the associated sport event belongs to.
        /// </summary>
        public RoundDTO Round { get; }

        /// <summary>
        /// Gets a <see cref="TournamentDTO"/> representing the tournament to which the associated sport event belongs to.
        /// </summary>
        public TournamentDTO Tournament { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MatchDTO"/> class
        /// </summary>
        /// <param name="sportEvent">A <see cref="sportEvent"/> instance containing basic information about the sport event</param>
        internal MatchDTO(sportEvent sportEvent)
            : base(sportEvent)
        {
            Contract.Requires(sportEvent != null);

            if (sportEvent.season != null)
            {
                Contract.Assume(!string.IsNullOrEmpty(sportEvent.season.id));
                Contract.Assume(!string.IsNullOrEmpty(sportEvent.season.name));
                Season = new SportEntityDTO(sportEvent.season.id, sportEvent.season.name);
            }
            if (sportEvent.tournament_round != null)
            {
                Round = new RoundDTO(sportEvent.tournament_round);
            }
            if (sportEvent.tournament != null)
            {
                Contract.Assume(!string.IsNullOrEmpty(sportEvent.tournament.id));
                Contract.Assume(!string.IsNullOrEmpty(sportEvent.tournament.name));
                Tournament = new TournamentDTO(sportEvent.tournament);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MatchDTO"/> class
        /// </summary>
        /// <param name="matchSummary">A <see cref="matchSummaryEndpoint"/> instance containing basic information about the sport event</param>
        internal MatchDTO(matchSummaryEndpoint matchSummary)
            : base(matchSummary)
        {
            Contract.Requires(matchSummary != null);

            if (matchSummary.sport_event.season != null)
            {
                Contract.Assume(!string.IsNullOrEmpty(matchSummary.sport_event.season.id));
                Contract.Assume(!string.IsNullOrEmpty(matchSummary.sport_event.season.name));
                Season = new SportEntityDTO(matchSummary.sport_event.season.id, matchSummary.sport_event.season.name);
            }
            if (matchSummary.sport_event.tournament_round != null)
            {
                Round = new RoundDTO(matchSummary.sport_event.tournament_round);
            }
            if (matchSummary.sport_event.tournament != null)
            {
                Contract.Assume(!string.IsNullOrEmpty(matchSummary.sport_event.tournament.id));
                Contract.Assume(!string.IsNullOrEmpty(matchSummary.sport_event.tournament.name));
                Tournament = new TournamentDTO(matchSummary.sport_event.tournament);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MatchDTO"/> class
        /// </summary>
        /// <param name="fixture">A <see cref="fixture"/> instance containing basic information about the sport event</param>
        /// <remarks>Not all properties are filled via fixture (i.e.Venue, Conditions,..)</remarks>
        internal MatchDTO(fixture fixture)
            : this(new matchSummaryEndpoint
                            {
                                sport_event = new sportEvent
                                {
                                    id = fixture.id,
                                    name = fixture.name,
                                    type = fixture.type,
                                    scheduledSpecified = fixture.scheduledSpecified,
                                    scheduled = fixture.scheduled,
                                    scheduled_endSpecified = fixture.scheduled_endSpecified,
                                    scheduled_end = fixture.scheduled_end,
                                    liveodds = fixture.liveodds,
                                    season = fixture.season,
                                    tournament = fixture.tournament,
                                    tournament_round = fixture.tournament_round,
                                    competitors = fixture.competitors?.Select(t => new teamCompetitor
                                                                                {
                                                                                    abbreviation = t.abbreviation,
                                                                                    country = t.country,
                                                                                    id = t.id,
                                                                                    name = t.name,
                                                                                    qualifier = t.qualifier,
                                                                                    @virtual = t.@virtual,
                                                                                    virtualSpecified = t.virtualSpecified,
                                                                                    country_code = t.country_code,
                                                                                    reference_ids = t.reference_ids,
                                                                                    division = t.division,
                                                                                    divisionSpecified = t.divisionSpecified
                                                                                }).ToArray(),
                                    parent = fixture.parent,
                                    races = fixture.races
                                }
                            })
        {
            Venue = fixture.venue == null
                ? null
                : new VenueDTO(fixture.venue);
        }
    }
}
