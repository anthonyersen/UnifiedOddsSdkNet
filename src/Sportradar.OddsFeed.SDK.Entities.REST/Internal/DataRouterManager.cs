﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using Metrics;
using Sportradar.OddsFeed.SDK.Common;
using Sportradar.OddsFeed.SDK.Common.Internal;
using Sportradar.OddsFeed.SDK.Entities.REST.CustomBet;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.Caching;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.Caching.Events;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO.CustomBet;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO.Lottery;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.EntitiesImpl;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.EntitiesImpl.CustomBet;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.Enums;
using Sportradar.OddsFeed.SDK.Messages;
using Sportradar.OddsFeed.SDK.Messages.EventArguments;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal
{
    /// <summary>
    /// The implementation of data router manager
    /// </summary>
    /// <seealso cref="IDataRouterManager" />
    internal class DataRouterManager : IDataRouterManager
    {
        /// <summary>
        /// Occurs when data from Sports API arrives
        /// </summary>
        public event EventHandler<RawApiDataEventArgs> RawApiDataReceived;

        /// <summary>
        /// The execution log
        /// </summary>
        private readonly ILog _executionLog = SdkLoggerFactory.GetLoggerForExecution(typeof(DataRouterManager));
        /// <summary>
        /// The sport event summary provider
        /// </summary>
        private readonly IDataProvider<SportEventSummaryDTO> _sportEventSummaryProvider;
        /// <summary>
        /// The sport event fixture provider
        /// </summary>
        private readonly IDataProvider<FixtureDTO> _sportEventFixtureProvider;
        /// <summary>
        /// The sport event fixture provider without cache
        /// </summary>
        private readonly IDataProvider<FixtureDTO> _sportEventFixtureChangeFixtureProvider;
        /// <summary>
        /// All tournaments for all sports provider
        /// </summary>
        private readonly IDataProvider<EntityList<SportDTO>> _allTournamentsForAllSportsProvider;
        /// <summary>
        /// All sports provider
        /// </summary>
        private readonly IDataProvider<EntityList<SportDTO>> _allSportsProvider;
        /// <summary>
        /// The sport events for date provider
        /// </summary>
        private readonly IDataProvider<EntityList<SportEventSummaryDTO>> _sportEventsForDateProvider;
        /// <summary>
        /// The sport events for tournament provider
        /// </summary>
        private readonly IDataProvider<EntityList<SportEventSummaryDTO>> _sportEventsForTournamentProvider;
        /// <summary>
        /// The player profile provider
        /// </summary>
        private readonly IDataProvider<PlayerProfileDTO> _playerProfileProvider;
        /// <summary>
        /// The competitor provider
        /// </summary>
        private readonly IDataProvider<CompetitorProfileDTO> _competitorProvider;
        /// <summary>
        /// The simple team profile provider
        /// </summary>
        private readonly IDataProvider<SimpleTeamProfileDTO> _simpleTeamProvider;
        /// <summary>
        /// The tournament seasons provider
        /// </summary>
        private readonly IDataProvider<TournamentSeasonsDTO> _tournamentSeasonsProvider;
        /// <summary>
        /// The ongoing sport event provider
        /// </summary>
        private readonly IDataProvider<MatchTimelineDTO> _ongoingSportEventProvider;
        /// <summary>
        /// The ongoing sport event provider
        /// </summary>
        private readonly IDataProvider<SportCategoriesDTO> _sportCategoriesProvider;
        /// <summary>
        /// The invariant market descriptions provider
        /// </summary>
        private readonly IDataProvider<EntityList<MarketDescriptionDTO>> _invariantMarketDescriptionsProvider;
        /// <summary>
        /// The variant market description provider
        /// </summary>
        private readonly IDataProvider<MarketDescriptionDTO> _variantMarketDescriptionProvider;
        /// <summary>
        /// The variant descriptions provider
        /// </summary>
        private readonly IDataProvider<EntityList<VariantDescriptionDTO>> _variantDescriptionsProvider;
        /// <summary>
        /// The lottery draw summary provider
        /// </summary>
        private readonly IDataProvider<DrawDTO> _lotteryDrawSummaryProvider;
        /// <summary>
        /// The lottery draw fixture provider
        /// </summary>
        private readonly IDataProvider<DrawDTO> _lotteryDrawFixtureProvider;
        /// <summary>
        /// The lottery schedule provider
        /// </summary>
        private readonly IDataProvider<LotteryDTO> _lotteryScheduleProvider;
        /// <summary>
        /// The lotter list provider
        /// </summary>
        private readonly IDataProvider<EntityList<LotteryDTO>> _lotteryListProvider;
        /// <summary>
        /// The available selections provider
        /// </summary>
        private readonly IDataProvider<AvailableSelectionsDTO> _availableSelectionsProvider;
        /// <summary>
        /// The calculate probability provider
        /// </summary>
        private readonly ICalculateProbabilityProvider _calculateProbabilityProvider;
        /// <summary>
        /// The fixture changes provider
        /// </summary>
        private readonly IDataProvider<IEnumerable<FixtureChangeDTO>> _fixtureChangesProvider;

        /// <summary>
        /// The list sport events provider
        /// </summary>
        private readonly IDataProvider<EntityList<SportEventSummaryDTO>> _listSportEventProvider;

        /// <summary>
        /// The cache manager
        /// </summary>
        private readonly ICacheManager _cacheManager;

        /// <summary>
        /// The is WNS available
        /// </summary>
        private readonly bool _isWnsAvailable;

        /// <summary>
        /// The exception handling strategy
        /// </summary>
        internal readonly ExceptionHandlingStrategy ExceptionHandlingStrategy;

        /// <summary>
        /// The exception handling strategy
        /// </summary>
        private readonly CultureInfo _defaultLocale;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataRouterManager"/> class
        /// </summary>
        /// <param name="cacheManager">A <see cref="ICacheManager"/> used to interact among caches</param>
        /// <param name="producerManager">A <see cref="IProducerManager"/> used to get WNS producer</param>
        /// <param name="exceptionHandlingStrategy">An <see cref="Common.ExceptionHandlingStrategy"/> used to handle exception when fetching data</param>
        /// <param name="defaultLocale">An <see cref="CultureInfo"/> representing the default locale</param>
        /// <param name="sportEventSummaryProvider">The sport event summary provider</param>
        /// <param name="sportEventFixtureProvider">The sport event fixture provider</param>
        /// <param name="sportEventFixtureChangeFixtureProvider">The sport event fixture provider without cache</param>
        /// <param name="allTournamentsForAllSportsProvider">All tournaments for all sports provider</param>
        /// <param name="allSportsProvider">All sports provider</param>
        /// <param name="sportEventsForDateProvider">The sport events for date provider</param>
        /// <param name="sportEventsForTournamentProvider">The sport events for tournament provider</param>
        /// <param name="playerProfileProvider">The player profile provider</param>
        /// <param name="competitorProvider">The competitor provider</param>
        /// <param name="simpleTeamProvider">The simple team provider</param>
        /// <param name="tournamentSeasonsProvider">The tournament seasons provider</param>
        /// <param name="ongoingSportEventProvider">The ongoing sport event provider</param>
        /// <param name="sportCategoriesProvider">The sport categories provider</param>
        /// <param name="invariantMarketDescriptionsProvider">The invariant market description provider</param>
        /// <param name="variantMarketDescriptionProvider">The variant market description provider</param>
        /// <param name="variantDescriptionsProvider">The variant descriptions provider</param>
        /// <param name="drawSummaryProvider">Lottery draw summary provider</param>
        /// <param name="drawFixtureProvider">Lottery draw fixture provider</param>
        /// <param name="lotteryScheduleProvider">Lottery schedule provider (single lottery with schedule)</param>
        /// <param name="lotteryListProvider">Lottery list provider</param>
        /// <param name="availableSelectionsProvider">Available selections provider</param>
        /// <param name="calculateProbabilityProvider">The probability calculation provider</param>
        /// <param name="fixtureChangesProvider">Fixture changes provider</param>
        /// <param name="listSportEventProvider">List sport events provider</param>
        public DataRouterManager(ICacheManager cacheManager,
                                 IProducerManager producerManager,
                                 ExceptionHandlingStrategy exceptionHandlingStrategy,
                                 CultureInfo defaultLocale,
                                 IDataProvider<SportEventSummaryDTO> sportEventSummaryProvider,
                                 IDataProvider<FixtureDTO> sportEventFixtureProvider,
                                 IDataProvider<FixtureDTO> sportEventFixtureChangeFixtureProvider,
                                 IDataProvider<EntityList<SportDTO>> allTournamentsForAllSportsProvider,
                                 IDataProvider<EntityList<SportDTO>> allSportsProvider,
                                 IDataProvider<EntityList<SportEventSummaryDTO>> sportEventsForDateProvider,
                                 IDataProvider<EntityList<SportEventSummaryDTO>> sportEventsForTournamentProvider,
                                 IDataProvider<PlayerProfileDTO> playerProfileProvider,
                                 IDataProvider<CompetitorProfileDTO> competitorProvider,
                                 IDataProvider<SimpleTeamProfileDTO> simpleTeamProvider,
                                 IDataProvider<TournamentSeasonsDTO> tournamentSeasonsProvider,
                                 IDataProvider<MatchTimelineDTO> ongoingSportEventProvider,
                                 IDataProvider<SportCategoriesDTO> sportCategoriesProvider,
                                 IDataProvider<EntityList<MarketDescriptionDTO>> invariantMarketDescriptionsProvider,
                                 IDataProvider<MarketDescriptionDTO> variantMarketDescriptionProvider,
                                 IDataProvider<EntityList<VariantDescriptionDTO>> variantDescriptionsProvider,
                                 IDataProvider<DrawDTO> drawSummaryProvider,
                                 IDataProvider<DrawDTO> drawFixtureProvider,
                                 IDataProvider<LotteryDTO> lotteryScheduleProvider,
                                 IDataProvider<EntityList<LotteryDTO>> lotteryListProvider,
                                 IDataProvider<AvailableSelectionsDTO> availableSelectionsProvider,
                                 ICalculateProbabilityProvider calculateProbabilityProvider,
                                 IDataProvider<IEnumerable<FixtureChangeDTO>> fixtureChangesProvider,
                                 IDataProvider<EntityList<SportEventSummaryDTO>> listSportEventProvider)
        {
            Contract.Requires(cacheManager != null);
            Contract.Requires(sportEventSummaryProvider != null);
            Contract.Requires(sportEventFixtureProvider != null);
            Contract.Requires(sportEventFixtureChangeFixtureProvider != null);
            Contract.Requires(allTournamentsForAllSportsProvider != null);
            Contract.Requires(allSportsProvider != null);
            Contract.Requires(sportEventsForDateProvider != null);
            Contract.Requires(sportEventsForTournamentProvider != null);
            Contract.Requires(playerProfileProvider != null);
            Contract.Requires(competitorProvider != null);
            Contract.Requires(simpleTeamProvider != null);
            Contract.Requires(tournamentSeasonsProvider != null);
            Contract.Requires(ongoingSportEventProvider != null);
            Contract.Requires(sportCategoriesProvider != null);
            Contract.Requires(invariantMarketDescriptionsProvider != null);
            Contract.Requires(variantMarketDescriptionProvider != null);
            Contract.Requires(variantDescriptionsProvider != null);
            Contract.Requires(drawSummaryProvider != null);
            Contract.Requires(drawFixtureProvider != null);
            Contract.Requires(lotteryScheduleProvider != null);
            Contract.Requires(lotteryListProvider != null);
            Contract.Requires(availableSelectionsProvider != null);
            Contract.Requires(calculateProbabilityProvider != null);
            Contract.Requires(fixtureChangesProvider != null);
            Contract.Requires(listSportEventProvider != null);

            _cacheManager = cacheManager;
            var wnsProducer = producerManager.Get(7);
            _isWnsAvailable = wnsProducer.IsAvailable && !wnsProducer.IsDisabled;
            ExceptionHandlingStrategy = exceptionHandlingStrategy;
            _defaultLocale = defaultLocale;
            _sportEventSummaryProvider = sportEventSummaryProvider;
            _sportEventFixtureProvider = sportEventFixtureProvider;
            _sportEventFixtureChangeFixtureProvider = sportEventFixtureChangeFixtureProvider;
            _allTournamentsForAllSportsProvider = allTournamentsForAllSportsProvider;
            _allSportsProvider = allSportsProvider;
            _sportEventsForDateProvider = sportEventsForDateProvider;
            _sportEventsForTournamentProvider = sportEventsForTournamentProvider;
            _playerProfileProvider = playerProfileProvider;
            _competitorProvider = competitorProvider;
            _simpleTeamProvider = simpleTeamProvider;
            _tournamentSeasonsProvider = tournamentSeasonsProvider;
            _ongoingSportEventProvider = ongoingSportEventProvider;
            _sportCategoriesProvider = sportCategoriesProvider;
            _invariantMarketDescriptionsProvider = invariantMarketDescriptionsProvider;
            _variantMarketDescriptionProvider = variantMarketDescriptionProvider;
            _variantDescriptionsProvider = variantDescriptionsProvider;
            _lotteryDrawSummaryProvider = drawSummaryProvider;
            _lotteryDrawFixtureProvider = drawFixtureProvider;
            _lotteryScheduleProvider = lotteryScheduleProvider;
            _lotteryListProvider = lotteryListProvider;
            _availableSelectionsProvider = availableSelectionsProvider;
            _calculateProbabilityProvider = calculateProbabilityProvider;
            _fixtureChangesProvider = fixtureChangesProvider;
            _listSportEventProvider = listSportEventProvider;

            _sportEventSummaryProvider.RawApiDataReceived += OnRawApiDataReceived;
            _sportEventFixtureProvider.RawApiDataReceived += OnRawApiDataReceived;
            _sportEventFixtureChangeFixtureProvider.RawApiDataReceived += OnRawApiDataReceived;
            _allTournamentsForAllSportsProvider.RawApiDataReceived += OnRawApiDataReceived;
            _allSportsProvider.RawApiDataReceived += OnRawApiDataReceived;
            _sportEventsForDateProvider.RawApiDataReceived += OnRawApiDataReceived;
            _sportEventsForTournamentProvider.RawApiDataReceived += OnRawApiDataReceived;
            _playerProfileProvider.RawApiDataReceived += OnRawApiDataReceived;
            _competitorProvider.RawApiDataReceived += OnRawApiDataReceived;
            _simpleTeamProvider.RawApiDataReceived += OnRawApiDataReceived;
            _tournamentSeasonsProvider.RawApiDataReceived += OnRawApiDataReceived;
            _ongoingSportEventProvider.RawApiDataReceived += OnRawApiDataReceived;
            _sportCategoriesProvider.RawApiDataReceived += OnRawApiDataReceived;
            _invariantMarketDescriptionsProvider.RawApiDataReceived += OnRawApiDataReceived;
            _variantMarketDescriptionProvider.RawApiDataReceived += OnRawApiDataReceived;
            _variantDescriptionsProvider.RawApiDataReceived += OnRawApiDataReceived;
            _lotteryDrawSummaryProvider.RawApiDataReceived += OnRawApiDataReceived;
            _lotteryDrawFixtureProvider.RawApiDataReceived += OnRawApiDataReceived;
            _lotteryScheduleProvider.RawApiDataReceived += OnRawApiDataReceived;
            _lotteryListProvider.RawApiDataReceived += OnRawApiDataReceived;
            //_availableSelectionsProvider.RawApiDataReceived += OnRawApiDataReceived;
            //_calculateProbabilityProvider.RawApiDataReceived += OnRawApiDataReceived;
            _fixtureChangesProvider.RawApiDataReceived += OnRawApiDataReceived;
            _listSportEventProvider.RawApiDataReceived += OnRawApiDataReceived;
        }

        private void OnRawApiDataReceived(object sender, RawApiDataEventArgs e)
        {
           RawApiDataReceived?.Invoke(sender, e);
        }

        /// <summary>
        /// Lists the invariants members as required by the code contracts
        /// </summary>
        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(_cacheManager != null);
            Contract.Invariant(_sportEventSummaryProvider != null);
            Contract.Invariant(_sportEventFixtureProvider != null);
            Contract.Invariant(_sportEventFixtureChangeFixtureProvider != null);
            Contract.Invariant(_allTournamentsForAllSportsProvider != null);
            Contract.Invariant(_allSportsProvider != null);
            Contract.Invariant(_sportEventsForDateProvider != null);
            Contract.Invariant(_sportEventsForTournamentProvider != null);
            Contract.Invariant(_playerProfileProvider != null);
            Contract.Invariant(_competitorProvider != null);
            Contract.Invariant(_simpleTeamProvider != null);
            Contract.Invariant(_tournamentSeasonsProvider != null);
            Contract.Invariant(_ongoingSportEventProvider != null);
            Contract.Invariant(_sportCategoriesProvider != null);
            Contract.Invariant(_invariantMarketDescriptionsProvider != null);
            Contract.Invariant(_variantMarketDescriptionProvider != null);
            Contract.Invariant(_variantDescriptionsProvider != null);
            Contract.Invariant(_lotteryDrawSummaryProvider != null);
            Contract.Invariant(_lotteryDrawFixtureProvider != null);
            Contract.Invariant(_lotteryScheduleProvider != null);
            Contract.Invariant(_lotteryListProvider != null);
            Contract.Invariant(_availableSelectionsProvider != null);
            Contract.Invariant(_calculateProbabilityProvider != null);
            Contract.Invariant(_fixtureChangesProvider != null);
            Contract.Invariant(_listSportEventProvider != null);
        }

        /// <summary>
        /// Get sport event summary as an asynchronous operation
        /// </summary>
        /// <param name="id">The id of the sport event to be fetched</param>
        /// <param name="culture">The language to be fetched</param>
        /// <param name="requester">The cache item which invoked request</param>
        /// <returns>Task</returns>
        public async Task GetSportEventSummaryAsync(URN id, CultureInfo culture, ISportEventCI requester)
        {
            Metric.Context("DataRouterManager").Meter("GetSportEventSummaryAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetSportEventSummaryAsync", Unit.Requests);
            using (var t = timer.NewContext($"{id} [{culture.TwoLetterISOLanguageName}]"))
            {
                WriteLog($"Executing GetSportEventSummaryAsync for id={id} and culture={culture.TwoLetterISOLanguageName}.", true);

                SportEventSummaryDTO result = null;
                int restCallTime;
                try
                {
                    result = await _sportEventSummaryProvider.GetDataAsync(id.ToString(), culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting sport event summary for id={id} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null)
                {
                    await _cacheManager.SaveDtoAsync(id, result, culture, DtoType.SportEventSummary, requester).ConfigureAwait(false);
                }
                WriteLog($"Executing GetSportEventSummaryAsync for id={id} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Get sport event fixture as an asynchronous operation
        /// </summary>
        /// <param name="id">The id of the sport event to be fetched</param>
        /// <param name="culture">The language to be fetched</param>
        /// <param name="useCachedProvider">Should the cached provider be used</param>
        /// <param name="requester">The cache item which invoked request</param>
        /// <returns>Task</returns>
        public async Task GetSportEventFixtureAsync(URN id, CultureInfo culture, bool useCachedProvider, ISportEventCI requester)
        {
            Metric.Context("DataRouterManager").Meter("GetSportEventFixtureAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetSportEventFixtureAsync", Unit.Requests);
            using (var t = timer.NewContext($"{id} [{culture.TwoLetterISOLanguageName}]"))
            {
                WriteLog($"Executing {(useCachedProvider ? "cached" : "non-cached")} GetSportEventFixtureAsync for id={id} and culture={culture.TwoLetterISOLanguageName}.", true);

                FixtureDTO result = null;
                int restCallTime;
                try
                {
                    var provider = useCachedProvider
                                       ? _sportEventFixtureProvider
                                       : _sportEventFixtureChangeFixtureProvider;
                    result = await provider.GetDataAsync(id.ToString(), culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int) t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int) t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting sport event fixture for id={id} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null)
                {
                    await _cacheManager.SaveDtoAsync(id, result, culture, DtoType.Fixture, requester).ConfigureAwait(false);
                }
                WriteLog($"Executing GetSportEventFixtureAsync for id={id} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Get all tournaments for sport as an asynchronous operation.
        /// </summary>
        /// <param name="culture">The culture to be fetched</param>
        /// <returns>Task</returns>
        public async Task GetAllTournamentsForAllSportAsync(CultureInfo culture)
        {
            Metric.Context("DataRouterManager").Meter("GetAllTournamentsForAllSportAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetAllTournamentsForAllSportAsync", Unit.Requests);
            using (var t = timer.NewContext($"{culture.TwoLetterISOLanguageName}"))
            {
                WriteLog($"Executing GetAllTournamentsForAllSportAsync for culture={culture.TwoLetterISOLanguageName}.", true);

                EntityList<SportDTO> result = null;
                int restCallTime;
                try
                {
                    result = await _allTournamentsForAllSportsProvider.GetDataAsync(culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting all tournaments for all sports for lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null && result.Items.Any())
                {
                    await _cacheManager.SaveDtoAsync(URN.Parse($"sr:sports:{result.Items.Count()}"), result, culture, DtoType.SportList, null).ConfigureAwait(false);
                }
                WriteLog($"Executing GetAllTournamentsForAllSportAsync for culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Gets all categories for sport endpoint
        /// </summary>
        /// <param name="id">The id of the sport to be fetched</param>
        /// <param name="culture">The language to be fetched</param>
        public async Task GetSportCategoriesAsync(URN id, CultureInfo culture)
        {
            Metric.Context("DataRouterManager").Meter("GetSportCategoriesAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetSportCategoriesAsync", Unit.Requests);
            using (var t = timer.NewContext($"{id} [{culture.TwoLetterISOLanguageName}]"))
            {
                WriteLog($"Executing GetSportCategoriesAsync for id={id} and culture={culture.TwoLetterISOLanguageName}.", true);

                SportCategoriesDTO result = null;
                int restCallTime;
                try
                {
                    result = await _sportCategoriesProvider.GetDataAsync(id.ToString(), culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int) t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int) t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting sport categories for id={id} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result?.Categories != null)
                {
                    await _cacheManager.SaveDtoAsync(id, result, culture, DtoType.SportCategories, null).ConfigureAwait(false);
                }
                WriteLog($"Executing GetSportCategoriesAsync for id={id} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Get all sports as an asynchronous operation.
        /// </summary>
        /// <param name="culture">The culture to be fetched</param>
        /// <returns>Task</returns>
        public async Task GetAllSportsAsync(CultureInfo culture)
        {
            Metric.Context("DataRouterManager").Meter("GetAllSportsAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetAllSportsAsync", Unit.Requests);
            using (var t = timer.NewContext($"{culture.TwoLetterISOLanguageName}"))
            {
                WriteLog($"Executing GetAllSportsAsync for culture={culture.TwoLetterISOLanguageName}.", true);

                EntityList<SportDTO> result = null;
                int restCallTime;
                try
                {
                    result = await _allSportsProvider.GetDataAsync(culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int) t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int) t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting all sports for lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null && result.Items.Any())
                {
                    await _cacheManager.SaveDtoAsync(URN.Parse($"sr:sports:{result.Items.Count()}"), result, culture, DtoType.SportList, null).ConfigureAwait(false);
                }
                WriteLog($"Executing GetAllSportsAsync for culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Gets the currently live sport events
        /// </summary>
        /// <param name="culture">The culture</param>
        /// <returns>The list of the sport event ids with the sportId each belongs to</returns>
        public async Task<IEnumerable<Tuple<URN, URN>>> GetLiveSportEventsAsync(CultureInfo culture)
        {
            Metric.Context("DataRouterManager").Meter("GetLiveSportEventsAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetLiveSportEventsAsync", Unit.Requests);
            using (var t = timer.NewContext($"{culture.TwoLetterISOLanguageName}"))
            {
                WriteLog($"Executing GetLiveSportEventsAsync for culture={culture.TwoLetterISOLanguageName}.", true);

                EntityList<SportEventSummaryDTO> result = null;
                int restCallTime;
                try
                {
                    result = await _sportEventsForDateProvider.GetDataAsync(culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting live sport events and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null && result.Items.Any())
                {
                    await _cacheManager.SaveDtoAsync(URN.Parse($"sr:sportevents:{result.Items.Count()}"), result, culture, DtoType.SportEventSummaryList, null).ConfigureAwait(false);
                    var urns = new List<Tuple<URN, URN>>();
                    foreach (var item in result.Items)
                    {
                        urns.Add(new Tuple<URN, URN>(item.Id, item.SportId));
                    }
                    WriteLog($"Executing GetLiveSportEventsAsync for culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
                    return urns;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the sport events for specific date
        /// </summary>
        /// <param name="date">The date</param>
        /// <param name="culture">The culture</param>
        /// <returns>The list of the sport event ids with the sportId it belongs to</returns>
        public async Task<IEnumerable<Tuple<URN, URN>>> GetSportEventsForDateAsync(DateTime date, CultureInfo culture)
        {
            Metric.Context("DataRouterManager").Meter("GetSportEventsForDateAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetSportEventsForDateAsync", Unit.Requests);
            using (var t = timer.NewContext($"{date.ToShortDateString()} [{culture.TwoLetterISOLanguageName}]"))
            {
                WriteLog($"Executing GetSportEventsForDateAsync for date={date.ToShortDateString()} and culture={culture.TwoLetterISOLanguageName}.", true);

                EntityList<SportEventSummaryDTO> result = null;
                var dateId = date.ToString("yyyy-MM-dd");
                int restCallTime;
                try
                {
                    result = await _sportEventsForDateProvider.GetDataAsync(dateId, culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting sport events for date {dateId} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null && result.Items.Any())
                {
                    await _cacheManager.SaveDtoAsync(URN.Parse($"sr:sportevents:{result.Items.Count()}"), result, culture, DtoType.SportEventSummaryList, null).ConfigureAwait(false);
                    var urns = new List<Tuple<URN, URN>>();
                    foreach (var item in result.Items)
                    {
                        urns.Add(new Tuple<URN, URN>(item.Id, item.SportId));
                    }
                    WriteLog($"Executing GetSportEventsForDateAsync for date={date.ToShortDateString()} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
                    return urns;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the sport events for specific tournament (tournament schedule)
        /// </summary>
        /// <param name="id">The id of the tournament</param>
        /// <param name="culture">The culture to be fetched</param>
        /// <param name="requester">The cache item which invoked request</param>
        /// <returns>The list of ids of the sport events with the sportId belonging to specified tournament</returns>
        public async Task<IEnumerable<Tuple<URN, URN>>> GetSportEventsForTournamentAsync(URN id, CultureInfo culture, ISportEventCI requester)
        {
            Metric.Context("DataRouterManager").Meter("GetSportEventsForTournamentAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetSportEventsForTournamentAsync", Unit.Requests);
            using (var t = timer.NewContext($"{id} [{culture.TwoLetterISOLanguageName}]"))
            {
                WriteLog($"Executing GetSportEventsForTournamentAsync for tournament id={id} and culture={culture.TwoLetterISOLanguageName}.", true);

                EntityList<SportEventSummaryDTO> result = null;
                int restCallTime;
                try
                {
                    result = await _sportEventsForTournamentProvider.GetDataAsync(id.ToString(), culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    if (!e.Message.Contains("No schedule for this tournament") && !e.Message.Contains("This is a place-holder tournament."))
                    {
                        _executionLog.Error($"Error getting sport events for tournament for id={id} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                        if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                        {
                            throw;
                        }
                    }
                    else
                    {
                        _executionLog.Debug($"Error getting sport events for tournament for id={id} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    }
                }

                if (result != null && result.Items.Any())
                {
                    await _cacheManager.SaveDtoAsync(URN.Parse($"sr:sportevents:{result.Items.Count()}"), result, culture, DtoType.SportEventSummaryList, requester).ConfigureAwait(false);
                    var urns = new List<Tuple<URN, URN>>();
                    foreach (var item in result.Items)
                    {
                        urns.Add(new Tuple<URN, URN>(item.Id, item.SportId));
                    }
                    WriteLog($"Executing GetSportEventsForTournamentAsync for tournament id={id} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
                    return urns;
                }
            }
            return null;
        }

        /// <summary>
        /// Get player profile as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the player</param>
        /// <param name="culture">The culture to be fetched</param>
        /// <param name="requester">The cache item which invoked request</param>
        /// <returns>Task</returns>
        public async Task GetPlayerProfileAsync(URN id, CultureInfo culture, ISportEventCI requester)
        {
            Metric.Context("DataRouterManager").Meter("GetPlayerProfileAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetPlayerProfileAsync", Unit.Requests);
            using (var t = timer.NewContext($"{id} [{culture.TwoLetterISOLanguageName}]"))
            {
                WriteLog($"Executing GetPlayerProfileAsync for id={id} and culture={culture.TwoLetterISOLanguageName}.");

                PlayerProfileDTO result = null;
                int restCallTime;
                try
                {
                    result = await _playerProfileProvider.GetDataAsync(id.ToString(), culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting player profile for id={id} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null && result.Id.Equals(id))
                {
                    await _cacheManager.SaveDtoAsync(id, result, culture, DtoType.PlayerProfile, requester).ConfigureAwait(false);
                }
                WriteLog($"Executing GetPlayerProfileAsync for id={id} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Get competitor as an asynchronous operation
        /// </summary>
        /// <param name="id">The id of the competitor</param>
        /// <param name="culture">The culture to be fetched</param>
        /// <param name="requester">The cache item which invoked request</param>
        /// <returns>Task</returns>
        public async Task GetCompetitorAsync(URN id, CultureInfo culture, ISportEventCI requester)
        {
            Metric.Context("DataRouterManager").Meter("GetCompetitorAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetCompetitorAsync", Unit.Requests);
            using (var t = timer.NewContext($"{id} [{culture.TwoLetterISOLanguageName}]"))
            {
                WriteLog($"Executing GetCompetitorAsync for id={id} and culture={culture.TwoLetterISOLanguageName}.", true);

                CompetitorProfileDTO competitorResult = null;
                SimpleTeamProfileDTO simpleTeamResult = null;
                int restCallTime;
                try
                {
                    if (id.Type.Equals(SdkInfo.SimpleTeamIdentifier, StringComparison.InvariantCultureIgnoreCase)
                        || id.ToString().StartsWith(SdkInfo.OutcometextVariantValue, StringComparison.InvariantCultureIgnoreCase))
                    {
                        simpleTeamResult = await _simpleTeamProvider.GetDataAsync(id.ToString(), culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    }
                    else
                    {
                        competitorResult = await _competitorProvider.GetDataAsync(id.ToString(), culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    }
                    restCallTime = (int) t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int) t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting competitor profile for id={id} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}",
                                        e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (simpleTeamResult != null)
                {
                    await _cacheManager.SaveDtoAsync(id, simpleTeamResult, culture, DtoType.SimpleTeamProfile, requester).ConfigureAwait(false);
                    if (!simpleTeamResult.Competitor.Id.Equals(id))
                    {
                        await _cacheManager.SaveDtoAsync(simpleTeamResult.Competitor.Id, simpleTeamResult, culture, DtoType.SimpleTeamProfile, requester).ConfigureAwait(false);
                    }
                }
                if (competitorResult != null && competitorResult.Competitor.Id.Equals(id))
                {
                    await _cacheManager.SaveDtoAsync(id, competitorResult, culture, DtoType.CompetitorProfile, requester).ConfigureAwait(false);
                }
                WriteLog($"Executing GetCompetitorAsync for id={id} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Get seasons for tournament as an asynchronous operation
        /// </summary>
        /// <param name="id">The id of the tournament</param>
        /// <param name="culture">The culture to be fetched</param>
        /// <param name="requester">The cache item which invoked request</param>
        /// <returns>The list of ids of the seasons for specified tournament</returns>
        public async Task<IEnumerable<URN>> GetSeasonsForTournamentAsync(URN id, CultureInfo culture, ISportEventCI requester)
        {
            Metric.Context("DataRouterManager").Meter("GetSeasonsForTournamentAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetSeasonsForTournamentAsync", Unit.Requests);
            using (var t = timer.NewContext($"{id} [{culture.TwoLetterISOLanguageName}]"))
            {
               WriteLog($"Executing GetSeasonsForTournamentAsync for tournament id={id} and culture={culture.TwoLetterISOLanguageName}.", true);

                TournamentSeasonsDTO result = null;
                int restCallTime;
                try
                {
                    result = await _tournamentSeasonsProvider.GetDataAsync(id.ToString(), culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;

                    if (e.Message.Contains("No seasons for tournament") || e.Message.Contains("This is a place-holder tournament.") || e.Message.Contains("NotFound"))
                    {
                        message = message.Contains(".")
                            ? message.Substring(0, message.IndexOf(".", StringComparison.InvariantCultureIgnoreCase) + 1)
                            : message;
                        _executionLog.Debug($"Error getting seasons for tournament id={id} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}");
                    }
                    else
                    {
                        _executionLog.Error($"Error getting seasons for tournament id={id} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                        if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                        {
                            throw;
                        }
                    }
                }

                if (result?.Tournament != null)
                {
                    await _cacheManager.SaveDtoAsync(result.Tournament.Id, result, culture, DtoType.TournamentSeasons, requester).ConfigureAwait(false);
                    if (result.Seasons != null)
                    {
                        var urns = new List<URN>();
                        foreach (var item in result.Seasons)
                        {
                            urns.Add(item.Id);
                        }
                        WriteLog($"Executing GetSeasonsForTournamentAsync for tournament id={id} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
                        return urns;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Get information about ongoing event as an asynchronous operation (match timeline)
        /// </summary>
        /// <param name="id">The id of the sport event</param>
        /// <param name="culture">The culture to be fetched</param>
        /// <param name="requester">The cache item which invoked request</param>
        /// <returns>Task</returns>
        public async Task GetInformationAboutOngoingEventAsync(URN id, CultureInfo culture, ISportEventCI requester)
        {
            Metric.Context("DataRouterManager").Meter("GetInformationAboutOngoingEventAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetInformationAboutOngoingEventAsync", Unit.Requests);
            using (var t = timer.NewContext($"{id} [{culture.TwoLetterISOLanguageName}]"))
            {
               WriteLog($"Executing GetInformationAboutOngoingEventAsync for id={id} and culture={culture.TwoLetterISOLanguageName}.", true);

                MatchTimelineDTO result = null;
                int restCallTime;
                try
                {
                    result = await _ongoingSportEventProvider.GetDataAsync(id.ToString(), culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;

                    if (e.Message.Contains("NotFound"))
                    {
                        message = message.Contains(".")
                                      ? message.Substring(0, message.IndexOf(".", StringComparison.InvariantCultureIgnoreCase) + 1)
                                      : message;
                        _executionLog.Debug($"Error getting match timeline for id={id} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}");
                    }
                    else
                    {
                        _executionLog.Error($"Error getting match timeline for id={id} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                        if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                        {
                            throw;
                        }
                    }
                }

                if (result != null)
                {
                    await _cacheManager.SaveDtoAsync(result.SportEvent.Id, result, culture, DtoType.MatchTimeline, requester).ConfigureAwait(false);
                }
                WriteLog($"Executing GetInformationAboutOngoingEventAsync for id={id} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Gets the market descriptions (static)
        /// </summary>
        /// <param name="culture">The culture to be fetched</param>
        /// <returns>Task</returns>
        public async Task GetMarketDescriptionsAsync(CultureInfo culture)
        {
            Metric.Context("DataRouterManager").Meter("GetMarketDescriptionsAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetMarketDescriptionsAsync", Unit.Requests);
            using (var t = timer.NewContext($"[{culture.TwoLetterISOLanguageName}]"))
            {
               WriteLog($"Executing GetMarketDescriptionsAsync for culture={culture.TwoLetterISOLanguageName}.", true);

                EntityList<MarketDescriptionDTO> result = null;
                int restCallTime;
                try
                {
                    result = await _invariantMarketDescriptionsProvider.GetDataAsync(culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting market descriptions for lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null)
                {
                    await _cacheManager.SaveDtoAsync(URN.Parse("sr:markets:" + result.Items?.Count()), result, culture, DtoType.MarketDescriptionList, null).ConfigureAwait(false);
                }
                WriteLog($"Executing GetMarketDescriptionsAsync for culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Gets the variant market description (dynamic)
        /// </summary>
        /// <param name="id">The id of the market</param>
        /// <param name="variant">The variant URN</param>
        /// <param name="culture">The culture to be fetched</param>
        /// <returns>Task</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public async Task GetVariantMarketDescriptionAsync(int id, string variant, CultureInfo culture)
        {
            Metric.Context("DataRouterManager").Meter("GetVariantMarketDescriptionAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetVariantMarketDescriptionAsync", Unit.Requests);
            using (var t = timer.NewContext($"{id} [{culture.TwoLetterISOLanguageName}]"))
            {
               WriteLog($"Executing GetVariantMarketDescriptionAsync for id={id}, variant={variant} and culture={culture.TwoLetterISOLanguageName}.", true);

                MarketDescriptionDTO result = null;
                int restCallTime;
                try
                {
                    result = await _variantMarketDescriptionProvider.GetDataAsync(id.ToString(), culture.TwoLetterISOLanguageName, variant).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting variant market description for id={id} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null)
                {
                    await _cacheManager.SaveDtoAsync(URN.Parse("sr:variant:" + result.Id), result, culture, DtoType.MarketDescription, null).ConfigureAwait(false);
                }
                WriteLog($"Executing GetVariantMarketDescriptionAsync for id={id}, variant={variant} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Gets the variant descriptions (static)
        /// </summary>
        /// <param name="culture">The culture to be fetched</param>
        /// <returns>Task</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public async Task GetVariantDescriptionsAsync(CultureInfo culture)
        {
            Metric.Context("DataRouterManager").Meter("GetVariantDescriptionsAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetVariantDescriptionsAsync", Unit.Requests);
            using (var t = timer.NewContext($"[{culture.TwoLetterISOLanguageName}]"))
            {
               WriteLog($"Executing GetVariantDescriptionsAsync for culture={culture.TwoLetterISOLanguageName}.", true);

                EntityList<VariantDescriptionDTO> result = null;
                int restCallTime;
                try
                {
                    result = await _variantDescriptionsProvider.GetDataAsync(culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting variant descriptions for lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null)
                {
                    await _cacheManager.SaveDtoAsync(URN.Parse("sr:variants:" + result.Items?.Count()), result, culture, DtoType.VariantDescriptionList, null).ConfigureAwait(false);
                }
                WriteLog($"Executing GetVariantDescriptionsAsync for culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Gets the <see cref="DrawDTO" /> from lottery draw summary endpoint
        /// </summary>
        /// <param name="drawId">The id of the draw to be fetched</param>
        /// <param name="culture">The language to be fetched</param>
        /// <param name="requester">The cache item which invoked request</param>
        /// <returns>Task</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>This gets called only if WNS is available</remarks>
        public async Task GetDrawSummaryAsync(URN drawId, CultureInfo culture, ISportEventCI requester)
        {
            if (!_isWnsAvailable)
            {
                //WriteLog("Calling GetDrawSummaryAsync is ignored since producer WNS is not available.", true);
                return;
            }
            Metric.Context("DataRouterManager").Meter("GetDrawSummaryAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetDrawSummaryAsync", Unit.Requests);
            using (var t = timer.NewContext($"{drawId} [{culture.TwoLetterISOLanguageName}]"))
            {
                WriteLog($"Executing GetDrawSummaryAsync for id={drawId} and culture={culture.TwoLetterISOLanguageName}.", true);

                DrawDTO result = null;
                int restCallTime;
                try
                {
                    result = await _lotteryDrawSummaryProvider.GetDataAsync(drawId.ToString(), culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting draw summary for id={drawId} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null)
                {
                    await _cacheManager.SaveDtoAsync(result.Id, result, culture, DtoType.LotteryDraw, requester).ConfigureAwait(false);
                }
                WriteLog($"Executing GetDrawSummaryAsync for id={drawId} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Gets the <see cref="DrawDTO" /> from the lottery draw fixture endpoint
        /// </summary>
        /// <param name="drawId">The id of the draw to be fetched</param>
        /// <param name="culture">The language to be fetched</param>
        /// <param name="requester">The cache item which invoked request</param>
        /// <returns>Task</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>This gets called only if WNS is available</remarks>
        public async Task GetDrawFixtureAsync(URN drawId, CultureInfo culture, ISportEventCI requester)
        {
            if (!_isWnsAvailable)
            {
                //WriteLog("Calling GetDrawFixtureAsync is ignored since producer WNS is not available.", true);
                return;
            }
            Metric.Context("DataRouterManager").Meter("GetDrawFixtureAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetDrawFixtureAsync", Unit.Requests);
            using (var t = timer.NewContext($"{drawId} [{culture.TwoLetterISOLanguageName}]"))
            {
                WriteLog($"Executing GetDrawFixtureAsync for id={drawId} and culture={culture.TwoLetterISOLanguageName}.", true);

                DrawDTO result = null;
                int restCallTime;
                try
                {
                    result = await _lotteryDrawFixtureProvider.GetDataAsync(drawId.ToString(), culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting draw fixture for id={drawId} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null)
                {
                    await _cacheManager.SaveDtoAsync(result.Id, result, culture, DtoType.LotteryDraw, requester).ConfigureAwait(false);
                }
                WriteLog($"Executing GetDrawFixtureAsync for id={drawId} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Gets the lottery draw schedule
        /// </summary>
        /// <param name="lotteryId">The id of the lottery</param>
        /// <param name="culture">The culture to be fetched</param>
        /// <param name="requester">The cache item which invoked request</param>
        /// <returns>The lottery with its schedule</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>This gets called only if WNS is available</remarks>
        public async Task GetLotteryScheduleAsync(URN lotteryId, CultureInfo culture, ISportEventCI requester)
        {
            if (!_isWnsAvailable)
            {
                //WriteLog("Calling GetLotteryScheduleAsync is ignored since producer WNS is not available.", true);
                return;
            }
            Metric.Context("DataRouterManager").Meter("GetLotteryScheduleAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetLotteryScheduleAsync", Unit.Requests);
            using (var t = timer.NewContext($"{lotteryId} [{culture.TwoLetterISOLanguageName}]"))
            {
                WriteLog($"Executing GetLotteryScheduleAsync for id={lotteryId} and culture={culture.TwoLetterISOLanguageName}.", true);

                LotteryDTO result = null;
                int restCallTime;
                try
                {
                    result = await _lotteryScheduleProvider.GetDataAsync(lotteryId.ToString(), culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting lottery schedule for id={lotteryId} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null)
                {
                    await _cacheManager.SaveDtoAsync(result.Id, result, culture, DtoType.Lottery, null).ConfigureAwait(false);
                }
                WriteLog($"Executing GetLotteryScheduleAsync for id={lotteryId} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
            }
        }

        /// <summary>
        /// Gets the list of all lotteries
        /// </summary>
        /// <param name="culture">The culture to be fetched</param>
        /// <returns>The list of all lotteries ids</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>This gets called only if WNS is available</remarks>
        public async Task<IEnumerable<URN>> GetAllLotteriesAsync(CultureInfo culture)
        {
            if (!_isWnsAvailable)
            {
                //WriteLog("Calling GetAllLotteriesAsync is ignored since producer WNS is not available.", true);
                return null;
            }
            Metric.Context("DataRouterManager").Meter("GetAllLotteriesAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetAllLotteriesAsync", Unit.Requests);
            using (var t = timer.NewContext($"[{culture.TwoLetterISOLanguageName}]"))
            {
                WriteLog($"Executing GetAllLotteriesAsync for culture={culture.TwoLetterISOLanguageName}.", true);

                EntityList<LotteryDTO> result = null;
                int restCallTime;
                try
                {
                    result = await _lotteryListProvider.GetDataAsync(culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting all lotteries for lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result?.Items != null)
                {
                    await _cacheManager.SaveDtoAsync(URN.Parse($"sr:lotteries:{result.Items.Count()}"), result, culture, DtoType.LotteryList, null).ConfigureAwait(false);
                    var urns = new List<URN>();
                    foreach (var item in result.Items)
                    {
                        urns.Add(item.Id);
                    }
                    WriteLog($"Executing GetAllLotteriesAsync for tournament culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
                    return urns;
                }
                WriteLog($"Executing GetAllLotteriesAsync for culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms. {SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)} No data.");
            }
            return null;
        }

        /// <summary>
        /// Get available selections as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the event</param>
        /// <returns>The available selections for event</returns>
        public async Task<IAvailableSelections> GetAvailableSelectionsAsync(URN id)
        {
            Metric.Context("DataRouterManager").Meter("GetAvailableSelectionsAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetAvailableSelectionsAsync", Unit.Requests);
            using (var t = timer.NewContext($"{id}"))
            {
                WriteLog($"Executing GetAvailableSelectionsAsync for id={id}.", true);

                AvailableSelectionsDTO result = null;
                int restCallTime;
                try
                {
                    result = await _availableSelectionsProvider.GetDataAsync(id.ToString()).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting available selections for id={id}. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null)
                {
                    await _cacheManager.SaveDtoAsync(id, result, _defaultLocale, DtoType.AvailableSelections, null).ConfigureAwait(false);
                }
                WriteLog($"Executing GetAvailableSelectionsAsync for id={id} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
                return new AvailableSelections(result);
            }
        }

        public async Task<ICalculation> CalculateProbability(IEnumerable<ISelection> selections)
        {
            Metric.Context("DataRouterManager").Meter("CalculateProbability", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("CalculateProbability", Unit.Requests);
            using (var t = timer.NewContext())
            {
                WriteLog("Executing CalculateProbability.", true);

                CalculationDTO result = null;
                int restCallTime;
                try
                {
                    result = await _calculateProbabilityProvider.GetDataAsync(selections).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error calculating probabilities. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                WriteLog($"Executing CalculateProbability took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
                return new Calculation(result);
            }
        }

        public async Task<IEnumerable<IFixtureChange>> GetFixtureChangesAsync(CultureInfo culture)
        {
            Metric.Context("DataRouterManager").Meter("GetFixtureChangesAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetFixtureChangesAsync", Unit.Requests);
            using (var t = timer.NewContext())
            {
                WriteLog("Executing GetFixtureChangesAsync.", true);

                IEnumerable<FixtureChangeDTO> result = null;
                int restCallTime;
                try
                {
                    result = await _fixtureChangesProvider.GetDataAsync(culture.TwoLetterISOLanguageName).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting fixture changes. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                WriteLog($"Executing GetFixtureChangesAsync took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
                return result?.Select(f => new FixtureChange(f)).ToList();
            }
        }

        /// <summary>
        /// Gets the list of almost all events we are offering prematch odds for.
        /// </summary>
        /// <param name="startIndex">Starting record (this is an index, not time)</param>
        /// <param name="limit">How many records to return (max: 1000)</param>
        /// <param name="culture">The culture</param>
        /// <returns>The list of the sport event ids with the sportId it belongs to</returns>
        public async Task<IEnumerable<Tuple<URN, URN>>> GetListOfSportEventsAsync(int startIndex, int limit, CultureInfo culture)
        {
            Metric.Context("DataRouterManager").Meter("GetListOfSportEventsAsync", Unit.Calls);
            var timer = Metric.Context("DataRouterManager").Timer("GetListOfSportEventsAsync", Unit.Requests);
            using (var t = timer.NewContext($"{startIndex} [{culture.TwoLetterISOLanguageName}]"))
            {
                WriteLog($"Executing GetListOfSportEventsAsync with startIndex={startIndex}, limit={limit} and culture={culture.TwoLetterISOLanguageName}.", true);

                EntityList<SportEventSummaryDTO> result = null;
                int restCallTime;
                try
                {
                    result = await _listSportEventProvider.GetDataAsync(culture.TwoLetterISOLanguageName, startIndex.ToString(), limit.ToString()).ConfigureAwait(false);
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                }
                catch (Exception e)
                {
                    restCallTime = (int)t.Elapsed.TotalMilliseconds;
                    var message = e.InnerException?.Message ?? e.Message;
                    _executionLog.Error($"Error getting list of sport events for startIndex={startIndex}, limit={limit} and lang:[{culture.TwoLetterISOLanguageName}]. Message={message}", e.InnerException ?? e);
                    if (ExceptionHandlingStrategy == ExceptionHandlingStrategy.THROW)
                    {
                        throw;
                    }
                }

                if (result != null && result.Items.Any())
                {
                    await _cacheManager.SaveDtoAsync(URN.Parse($"sr:sportevents:{result.Items.Count()}"), result, culture, DtoType.SportEventSummaryList, null).ConfigureAwait(false);
                    var urns = new List<Tuple<URN, URN>>();
                    foreach (var item in result.Items)
                    {
                        urns.Add(new Tuple<URN, URN>(item.Id, item.SportId));
                    }
                    WriteLog($"Executing ListOfSportEventsAsync with startIndex={startIndex}, limit={limit} and culture={culture.TwoLetterISOLanguageName} took {restCallTime} ms.{SavingTook(restCallTime, (int)t.Elapsed.TotalMilliseconds)}");
                    return urns;
                }
            }
            return null;
        }

        private void WriteLog(string text, bool useDebug = false)
        {
            if (useDebug)
            {
                _executionLog.Debug(text);
            }
            else
            {
                _executionLog.Info(text);
            }
        }

        private static string SavingTook(int restTime, int totalTime)
        {
            var difference = totalTime - restTime;
            if (difference > 10)
            {
                return $" Saving took {difference} ms.";
            }
            return string.Empty;
        }
    }
}
