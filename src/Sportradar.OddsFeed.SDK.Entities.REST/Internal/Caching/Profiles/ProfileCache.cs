﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Metrics;
using Sportradar.OddsFeed.SDK.Common.Exceptions;
using Sportradar.OddsFeed.SDK.Common.Internal;
using Sportradar.OddsFeed.SDK.Common.Internal.Metrics;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.Caching.CI;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.Caching.Events;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.Enums;
using Sportradar.OddsFeed.SDK.Messages;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.Caching.Profiles
{
    /// <summary>
    /// A <see cref="IProfileCache"/> implementation using <see cref="ObjectCache"/> to cache fetched information
    /// </summary>
    internal class ProfileCache : SdkCache, IProfileCache, IDisposable, IHealthStatusProvider
    {
        /// <summary>
        /// A <see cref="ObjectCache"/> used to store fetched information
        /// </summary>
        private readonly ObjectCache _cache;

        /// <summary>
        /// The <see cref="IDataRouterManager"/> used to obtain data via REST request
        /// </summary>
        private readonly IDataRouterManager _dataRouterManager;

        /// <summary>
        /// Value indicating whether the current instance was already disposed
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// A <see cref="SemaphoreSlim"/> used to synchronize multi-threaded access
        /// </summary>
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        /// <summary>
        /// A <see cref="SemaphoreSlim"/> used to synchronize merging on cache item
        /// </summary>
        private readonly SemaphoreSlim _semaphoreCacheMerge = new SemaphoreSlim(1);

        /// <summary>
        /// The cache item policy
        /// </summary>
        private readonly CacheItemPolicy _normalCacheItemPolicy = new CacheItemPolicy {SlidingExpiration = TimeSpan.FromHours(24)};

        /// <summary>
        /// The cache item policy
        /// </summary>
        private readonly CacheItemPolicy _simpleTeamCacheItemPolicy = new CacheItemPolicy {AbsoluteExpiration = DateTimeOffset.MaxValue, Priority = CacheItemPriority.NotRemovable};

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileCache"/> class
    /// </summary>
    /// <param name="cache">A <see cref="ObjectCache"/> used to store fetched information</param>
    /// <param name="dataRouterManager">A <see cref="IDataRouterManager"/> used to fetch data</param>
    /// <param name="cacheManager">A <see cref="ICacheManager"/> used to interact among caches</param>
    public ProfileCache(ObjectCache cache,
                        IDataRouterManager dataRouterManager,
                        ICacheManager cacheManager)
            : base(cacheManager)
        {
            Contract.Requires(cache != null);
            Contract.Requires(dataRouterManager != null);

            _cache = cache;
            _dataRouterManager = dataRouterManager;
            _isDisposed = false;
        }

        /// <summary>
        /// Lists the invariants members as required by the code contracts
        /// </summary>
        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(_cache != null);
            Contract.Invariant(_dataRouterManager != null);
            Contract.Invariant(_semaphore != null);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (disposing)
            {
                _semaphore.Dispose();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously gets a <see cref="PlayerProfileCI"/> representing the profile for the specified player
        /// </summary>
        /// <param name="playerId">A <see cref="URN"/> specifying the id of the player for which to get the profile</param>
        /// <param name="cultures">A <see cref="IEnumerable{CultureInfo}"/> specifying languages in which the information should be available</param>
        /// <returns>A <see cref="Task{PlayerProfileCI}"/> representing the asynchronous operation</returns>
        /// <exception cref="CacheItemNotFoundException">The requested item was not found in cache and could not be obtained from the API</exception>
        public async Task<PlayerProfileCI> GetPlayerProfileAsync(URN playerId, IEnumerable<CultureInfo> cultures)
        {
            Contract.Requires(playerId != null);
            Contract.Requires(cultures != null && cultures.Any());

            Metric.Context("CACHE").Meter("ProfileCache->GetPlayerProfileAsync", Unit.Calls);

            await _semaphore.WaitAsync().ConfigureAwait(false);

            PlayerProfileCI cachedItem;
            try
            {
                cachedItem = (PlayerProfileCI) _cache.Get(playerId.ToString());
                var missingLanguages = LanguageHelper.GetMissingCultures(cultures, cachedItem?.Names.Keys).ToList();
                if (!missingLanguages.Any())
                {
                    return cachedItem;
                }

                var cultureTaskDictionary = missingLanguages.ToDictionary(c => c, c => _dataRouterManager.GetPlayerProfileAsync(playerId, c, null));

                await Task.WhenAll(cultureTaskDictionary.Values).ConfigureAwait(false);

                cachedItem = (PlayerProfileCI) _cache.Get(playerId.ToString());
            }
            catch (Exception ex)
            {
                if (ex is CommunicationException || ex is DeserializationException || ex is MappingException)
                {
                    throw new CacheItemNotFoundException($"An error occurred while fetching player profile for player {playerId} in cache", playerId.ToString(), ex);
                }
                throw;
            }
            finally
            {
                if (!_isDisposed)
                {
                    _semaphore.Release();
                }
            }
            return cachedItem;
        }

        /// <summary>
        /// Asynchronously gets <see cref="CompetitorCI"/> representing the profile for the specified competitor
        /// </summary>
        /// <param name="competitorId">A <see cref="URN"/> specifying the id of the competitor for which to get the profile</param>
        /// <param name="cultures">A <see cref="IEnumerable{CultureInfo}"/> specifying languages in which the information should be available</param>
        /// <returns>A <see cref="Task{PlayerProfileCI}"/> representing the asynchronous operation</returns>
        /// <exception cref="CacheItemNotFoundException">The requested item was not found in cache and could not be obtained from the API</exception>
        public async Task<CompetitorCI> GetCompetitorProfileAsync(URN competitorId, IEnumerable<CultureInfo> cultures)
        {
            Contract.Requires(competitorId != null);
            Contract.Requires(cultures != null && cultures.Any());

            Metric.Context("CACHE").Meter("ProfileCache->GetCompetitorProfileAsync", Unit.Calls);

            await _semaphore.WaitAsync().ConfigureAwait(false);

            CompetitorCI cachedItem;
            try
            {
                cachedItem = (CompetitorCI) _cache.Get(competitorId.ToString());
                var missingLanguages = LanguageHelper.GetMissingCultures(cultures, cachedItem?.Names.Keys).ToList();
                if (!missingLanguages.Any())
                {
                    return cachedItem;
                }

                var cultureTasks = missingLanguages.ToDictionary(c => c, c => _dataRouterManager.GetCompetitorAsync(competitorId, c, null));

                await Task.WhenAll(cultureTasks.Values).ConfigureAwait(false);

                cachedItem = (CompetitorCI) _cache.Get(competitorId.ToString());
            }
            catch (Exception ex)
            {
                if (ex is CommunicationException || ex is DeserializationException || ex is MappingException)
                {
                    throw new CacheItemNotFoundException("An error occurred while fetching competitor profile not found in cache", competitorId.ToString(), ex);
                }
                throw;
            }
            finally
            {
                if (!_isDisposed)
                {
                    _semaphore.Release();
                }
            }
            return cachedItem;
        }

        /// <summary>
        /// Registers the health check which will be periodically triggered
        /// </summary>
        public void RegisterHealthCheck()
        {
            HealthChecks.RegisterHealthCheck(CacheName, new Func<HealthCheckResult>(StartHealthCheck));
        }

        /// <summary>
        /// Starts the health check and returns <see cref="HealthCheckResult"/>
        /// </summary>
        public HealthCheckResult StartHealthCheck()
        {
            var keys = _cache.Select(w => w.Key).ToList();
            var details = $" [Players: {keys.Count(c => c.Contains("player"))}, Competitors: {keys.Count(c => c.Contains("competitor"))}, Teams: {keys.Count(c => c.Equals("team"))}, SimpleTeams: {keys.Count(c => c.Contains(SdkInfo.SimpleTeamIdentifier))}]";
            //var otherKeys = _cache.Where(w => !w.Key.Contains("competitor")).Select(s => s.Key);
            //CacheLog.Debug($"Ids: {string.Join(",", otherKeys)}");
            return _cache.Any() ? HealthCheckResult.Healthy($"Cache has {_cache.Count()} items{details}.") : HealthCheckResult.Unhealthy("Cache is empty.");
        }

        /// <summary>
        /// Set the list of <see cref="DtoType"/> in the this cache
        /// </summary>
        public override void SetDtoTypes()
        {
            RegisteredDtoTypes = new List<DtoType>
                                 {
                                     DtoType.Fixture,
                                     DtoType.MatchTimeline,
                                     DtoType.Competitor,
                                     DtoType.CompetitorProfile,
                                     DtoType.SimpleTeamProfile,
                                     DtoType.PlayerProfile,
                                     DtoType.SportEventSummary,
                                     DtoType.TournamentInfo,
                                     DtoType.RaceSummary,
                                     DtoType.MatchSummary
                                 };
        }

        /// <summary>
        /// Deletes the item from cache
        /// </summary>
        /// <param name="id">A <see cref="URN" /> representing the id of the item in the cache to be deleted</param>
        /// <param name="cacheItemType">A cache item type</param>
        public override void CacheDeleteItem(URN id, CacheItemType cacheItemType)
        {
            if (cacheItemType == CacheItemType.All
                || cacheItemType == CacheItemType.Competitor
                || cacheItemType == CacheItemType.Player)
            {
                _cache.Remove(id.ToString());
            }
        }

        /// <summary>
        /// Does item exists in the cache
        /// </summary>
        /// <param name="id">A <see cref="URN" /> representing the id of the item to be checked</param>
        /// <param name="cacheItemType">A cache item type</param>
        /// <returns><c>true</c> if exists, <c>false</c> otherwise</returns>
        public override bool CacheHasItem(URN id, CacheItemType cacheItemType)
        {
            if (cacheItemType == CacheItemType.All
                || cacheItemType == CacheItemType.Competitor
                || cacheItemType == CacheItemType.Player)
            {
                return _cache.Contains(id.ToString());
            }
            return false;
        }

        /// <summary>
        /// Adds the dto item to cache
        /// </summary>
        /// <param name="id">The identifier of the object</param>
        /// <param name="item">The item</param>
        /// <param name="culture">The culture</param>
        /// <param name="dtoType">Type of the dto</param>
        /// <param name="requester">The cache item which invoked request</param>
        /// <returns><c>true</c> if added, <c>false</c> otherwise</returns>
        protected override bool CacheAddDtoItem(URN id, object item, CultureInfo culture, DtoType dtoType, ISportEventCI requester)
        {
            //CacheLog.Debug($"Saving {id}.");
            if (_isDisposed)
            {
                return false;
            }

            var saved = false;
            switch (dtoType)
            {
                case DtoType.MatchSummary:
                    if (SaveCompetitorsFromSportEvent(item, culture))
                    {
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(MatchDTO), item.GetType());
                    }
                    break;
                case DtoType.RaceSummary:
                    if (SaveCompetitorsFromSportEvent(item, culture))
                    {
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(StageDTO), item.GetType());
                    }
                    break;
                case DtoType.TournamentInfo:
                    if (SaveCompetitorsFromSportEvent(item, culture))
                    {
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(TournamentInfoDTO), item.GetType());
                    }
                    break;
                case DtoType.SportEventSummary:
                    if (SaveCompetitorsFromSportEvent(item, culture))
                    {
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(SportEventSummaryDTO), item.GetType());
                    }
                    break;
                case DtoType.Sport:
                    break;
                case DtoType.Category:
                    break;
                case DtoType.Tournament:
                    break;
                case DtoType.PlayerProfile:
                    var playerProfile = item as PlayerProfileDTO;
                    if (playerProfile != null)
                    {
                        AddPlayerProfile(id, playerProfile, culture, true);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(PlayerProfileDTO), item.GetType());
                    }
                    break;
                case DtoType.Competitor:
                    var competitor = item as CompetitorDTO;
                    if (competitor != null)
                    {
                        AddCompetitor(id, competitor, culture, true);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(CompetitorDTO), item.GetType());
                    }
                    break;
                case DtoType.CompetitorProfile:
                    var competitorProfile = item as CompetitorProfileDTO;
                    if (competitorProfile != null)
                    {
                        AddCompetitorProfile(id, competitorProfile, culture, true);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(CompetitorProfileDTO), item.GetType());
                    }
                    break;
                case DtoType.SimpleTeamProfile:
                    var simpleTeamProfile = item as SimpleTeamProfileDTO;
                    if (simpleTeamProfile != null)
                    {
                        AddCompetitorProfile(id, simpleTeamProfile, culture, true);
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(SimpleTeamProfileDTO), item.GetType());
                    }
                    break;
                case DtoType.MarketDescription:
                    break;
                case DtoType.SportEventStatus:
                    break;
                case DtoType.MatchTimeline:
                    var matchTimeline = item as MatchTimelineDTO;
                    if (matchTimeline != null)
                    {
                        saved = SaveCompetitorsFromSportEvent(matchTimeline.SportEvent, culture);
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(MatchTimelineDTO), item.GetType());
                    }
                    break;
                case DtoType.TournamentSeasons:
                    break;
                case DtoType.Fixture:
                    if (SaveCompetitorsFromSportEvent(item, culture))
                    {
                        saved = true;
                    }
                    else
                    {
                        LogSavingDtoConflict(id, typeof(FixtureDTO), item.GetType());
                    }
                    break;
                case DtoType.SportList:
                    break;
                case DtoType.SportEventSummaryList:
                    break;
                case DtoType.MarketDescriptionList:
                    break;
                case DtoType.VariantDescription:
                    break;
                case DtoType.VariantDescriptionList:
                    break;
                case DtoType.Lottery:
                    break;
                case DtoType.LotteryDraw:
                    break;
                case DtoType.LotteryList:
                    break;
                case DtoType.BookingStatus:
                    break;
                case DtoType.SportCategories:
                    break;
                default:
                    ExecutionLog.Warn($"Trying to add unchecked dto type: {dtoType} for id: {id}.");
                    break;
            }
            //CacheLog.Debug($"Saving {id} COMPLETED.");
            return saved;
        }

        private bool SaveCompetitorsFromSportEvent(object item, CultureInfo culture)
        {
            var fixture = item as FixtureDTO;
            if (fixture != null)
            {
                if (fixture.Competitors != null && fixture.Competitors.Any())
                {
                    foreach (var teamCompetitorDTO in fixture.Competitors)
                    {
                        AddTeamCompetitor(teamCompetitorDTO.Id, teamCompetitorDTO, culture, true);
                    }
                }
                return true;
            }
            var match = item as MatchDTO;
            if (match != null)
            {
                if (match.Competitors != null && match.Competitors.Any())
                {
                    foreach (var teamCompetitorDTO in match.Competitors)
                    {
                        AddTeamCompetitor(teamCompetitorDTO.Id, teamCompetitorDTO, culture, true);
                    }
                }
                return true;
            }
            var stage = item as StageDTO;
            if(stage != null)
            {
                if (stage.Competitors != null && stage.Competitors.Any())
                {
                    foreach (var teamCompetitorDTO in stage.Competitors)
                    {
                        AddTeamCompetitor(teamCompetitorDTO.Id, teamCompetitorDTO, culture, true);
                    }
                }
                return true;
            }
            var tour = item as TournamentInfoDTO;
            if (tour != null)
            {
                if (tour.Competitors != null && tour.Competitors.Any())
                {
                    foreach (var competitorDTO in tour.Competitors)
                    {
                        AddCompetitor(competitorDTO.Id, competitorDTO, culture, true);
                    }
                }
                return true;
            }

            return false;
        }

        private void AddTeamCompetitor(URN id, TeamCompetitorDTO item, CultureInfo culture, bool useSemaphore)
        {
            if (_cache.Contains(id.ToString()))
            {
                try
                {
                    var ci = (CompetitorCI)_cache.Get(id.ToString());
                    var teamCI = ci as TeamCompetitorCI;
                    if (teamCI != null)
                    {
                        if (useSemaphore)
                        {
                            _semaphoreCacheMerge.Wait();
                        }
                        teamCI.Merge(item, culture);
                    }
                    else
                    {
                        if (useSemaphore)
                        {
                            _semaphoreCacheMerge.Wait();
                        }
                        teamCI = new TeamCompetitorCI(ci);
                        teamCI.Merge(item, culture);
                        _cache.Set(id.ToString(), teamCI, GetCorrectCacheItemPolicy(id));
                    }
                }
                catch (Exception ex)
                {
                    ExecutionLog.Error($"Error adding team competitor for id={id}, dto type={item?.GetType().Name} and lang={culture.TwoLetterISOLanguageName}.", ex);
                }
                finally
                {
                    if (useSemaphore)
                    {
                        if (!_isDisposed)
                        {
                            _semaphoreCacheMerge.Release();
                        }
                    }
                }
            }
            else
            {
                _cache.Add(id.ToString(), new TeamCompetitorCI(item, culture, _dataRouterManager), GetCorrectCacheItemPolicy(id));
            }

            if (item?.Players != null && item.Players.Any())
            {
                foreach (var player in item.Players)
                {
                    AddPlayerCompetitor(player.Id, player, culture, false);
                }
            }
        }

        private void AddCompetitor(URN id, CompetitorDTO item, CultureInfo culture, bool useSemaphore)
        {
            if (_cache.Contains(id.ToString()))
            {
                try
                {
                    var ci = (CompetitorCI) _cache.Get(id.ToString());
                    if (useSemaphore)
                    {
                        _semaphoreCacheMerge.Wait();
                    }
                    ci.Merge(item, culture);
                }
                catch (Exception ex)
                {
                    ExecutionLog.Error($"Error adding competitor for id={id}, dto type={item?.GetType().Name} and lang={culture.TwoLetterISOLanguageName}.", ex);
                }
                finally
                {
                    if (useSemaphore)
                    {
                        if (!_isDisposed)
                        {
                            _semaphoreCacheMerge.Release();
                        }
                    }
                }
            }
            else
            {
                _cache.Add(id.ToString(), new CompetitorCI(item, culture, _dataRouterManager), GetCorrectCacheItemPolicy(id));
            }

            if (item?.Players != null && item.Players.Any())
            {
                foreach (var player in item.Players)
                {
                    AddPlayerCompetitor(player.Id, player, culture, false);
                }
            }
        }

        private void AddCompetitorProfile(URN id, CompetitorProfileDTO item, CultureInfo culture, bool useSemaphore)
        {
            if (_cache.Contains(id.ToString()))
            {
                try
                {
                    var ci = (CompetitorCI) _cache.Get(id.ToString());
                    if (useSemaphore)
                    {
                        _semaphoreCacheMerge.Wait();
                    }
                    ci.Merge(item, culture);
                }
                catch (Exception ex)
                {
                    ExecutionLog.Error($"Error adding competitor for id={id}, dto type={item?.GetType().Name} and lang={culture.TwoLetterISOLanguageName}.", ex);
                }
                finally
                {
                    if (useSemaphore)
                    {
                        if (!_isDisposed)
                        {
                            _semaphoreCacheMerge.Release();
                        }
                    }
                }

                if (item?.Players != null && item.Players.Any())
                {
                    foreach (var player in item.Players)
                    {
                        AddPlayerProfile(player.Id, player, culture, false);
                    }
                }
            }
            else
            {
                _cache.Add(id.ToString(), new CompetitorCI(item, culture, _dataRouterManager), GetCorrectCacheItemPolicy(id));

                if (item.Players != null && item.Players.Any())
                {
                    foreach (var player in item.Players)
                    {
                        AddPlayerProfile(player.Id, player, culture, false);
                    }
                }
            }
        }

        private void AddCompetitorProfile(URN id, SimpleTeamProfileDTO item, CultureInfo culture, bool useSemaphore)
        {
            if (_cache.Contains(id.ToString()))
            {
                try
                {
                    var ci = (CompetitorCI)_cache.Get(id.ToString());
                    if (useSemaphore)
                    {
                        _semaphoreCacheMerge.Wait();
                    }
                    ci.Merge(item, culture);
                }
                catch (Exception ex)
                {
                    ExecutionLog.Error($"Error adding competitor for id={id}, dto type={item?.GetType().Name} and lang={culture.TwoLetterISOLanguageName}.", ex);
                }
                finally
                {
                    if (useSemaphore)
                    {
                        if (!_isDisposed)
                        {
                            _semaphoreCacheMerge.Release();
                        }
                    }
                }
            }
            else
            {
                _cache.Add(id.ToString(), new CompetitorCI(item, culture, _dataRouterManager), GetCorrectCacheItemPolicy(id));
            }
        }

        private void AddPlayerProfile(URN id, PlayerProfileDTO item, CultureInfo culture, bool useSemaphore)
        {
            if (_cache.Contains(id.ToString()))
            {
                try
                {
                    var ci = (PlayerProfileCI) _cache.Get(id.ToString());
                    if (useSemaphore)
                    {
                        _semaphoreCacheMerge.Wait();
                    }
                    ci.Merge(item, culture);
                }
                catch (Exception ex)
                {
                    ExecutionLog.Error($"Error adding player profile for id={id}, dto type={item?.GetType().Name} and lang={culture.TwoLetterISOLanguageName}.", ex);
                }
                finally
                {
                    if (useSemaphore)
                    {
                        if (!_isDisposed)
                        {
                            _semaphoreCacheMerge.Release();
                        }
                    }
                }
            }
            else
            {
                _cache.Add(id.ToString(), new PlayerProfileCI(item, culture, _dataRouterManager), GetCorrectCacheItemPolicy(id));
            }
        }

        private void AddPlayerCompetitor(URN id, PlayerCompetitorDTO item, CultureInfo culture, bool useSemaphore)
        {
            if (_cache.Contains(id.ToString()))
            {
                try
                {
                    if (id.Type.Equals(SdkInfo.PlayerProfileIdentifier, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var ci = (PlayerProfileCI)_cache.Get(id.ToString());
                        if (useSemaphore)
                        {
                            _semaphoreCacheMerge.Wait();
                        }

                        ci.Merge(item, culture);
                    }
                    else
                    {
                        var ci = (CompetitorCI)_cache.Get(id.ToString());
                        if (useSemaphore)
                        {
                            _semaphoreCacheMerge.Wait();
                        }

                        ci.Merge(item, culture);
                    }
                }
                catch (Exception ex)
                {
                    ExecutionLog.Error($"Error adding player profile for id={id}, dto type={item?.GetType().Name} and lang={culture.TwoLetterISOLanguageName}.", ex);
                }
                finally
                {
                    if (useSemaphore)
                    {
                        if (!_isDisposed)
                        {
                            _semaphoreCacheMerge.Release();
                        }
                    }
                }
            }
            else
            {
                if (id.Type.Equals(SdkInfo.PlayerProfileIdentifier, StringComparison.InvariantCultureIgnoreCase))
                {
                    _cache.Add(id.ToString(), new PlayerProfileCI(item, culture, _dataRouterManager), GetCorrectCacheItemPolicy(id));
                }
                else
                {
                    _cache.Add(id.ToString(), new CompetitorCI(item, culture, _dataRouterManager), GetCorrectCacheItemPolicy(id));
                }
            }
        }

        private CacheItemPolicy GetCorrectCacheItemPolicy(URN id)
        {
            return id.Type.Equals(SdkInfo.SimpleTeamIdentifier, StringComparison.InvariantCultureIgnoreCase)
                ? _simpleTeamCacheItemPolicy
                : _normalCacheItemPolicy;
        }
    }
}
