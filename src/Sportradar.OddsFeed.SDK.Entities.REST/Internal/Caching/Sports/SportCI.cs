﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO;
using Sportradar.OddsFeed.SDK.Messages;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.Caching.Sports
{
    /// <summary>
    /// Represents a cached sport entity
    /// </summary>
    /// <seealso cref="CacheItem" />
    internal class SportCI : CacheItem
    {
        /// <summary>
        /// Gets <see cref="IEnumerable{URN}"/> specifying the id's of child categories
        /// </summary>
        /// 
        public IEnumerable<URN> CategoryIds
        {
            get;
            private set;
        }

        /// <summary>
        /// The loaded categories for tournament
        /// </summary>
        private readonly List<CultureInfo> _loadedCategories = new List<CultureInfo>();

        /// <summary>
        /// Lock object used for loading categories
        /// </summary>
        private readonly SemaphoreSlim _loadedCategoriesSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// The <see cref="IDataRouterManager"/> used to obtain categories
        /// </summary>
        private readonly IDataRouterManager _dataRouterManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="SportCI"/> class.
        /// </summary>
        /// <param name="data">A <see cref="SportDTO" /> instance containing sport data</param>
        /// <param name="dataRouterManager">The <see cref="IDataRouterManager"/> used to obtain categories</param>
        /// <param name="culture">A <see cref="CultureInfo"/> specifying the language of the provided data</param>
        public SportCI(SportDTO data, IDataRouterManager dataRouterManager, CultureInfo culture)
            : base(data.Id, data.Name, culture)
        {
            if (data.Categories != null)
            {
                CategoryIds = new ReadOnlyCollection<URN>(data.Categories.Select(i => i.Id).ToList());
            }

            _dataRouterManager = dataRouterManager;
        }

        /// <summary>
        /// Merges the information from the provided <see cref="CacheItem" /> to data held by current instance
        /// </summary>
        /// <param name="item">A <see cref="CacheItem" /> containing the data to be merged to current instance</param>
        /// <param name="culture">A <see cref="CultureInfo" /> specifying the culture of data in the passed <see cref="CacheItem" /></param>
        public override void Merge(CacheItem item, CultureInfo culture)
        {
            base.Merge(item, culture);
            var sportCacheItem = item as SportCI;
            if (sportCacheItem?.CategoryIds != null && sportCacheItem.CategoryIds.Any())
            {
                CategoryIds = new ReadOnlyCollection<URN>(sportCacheItem.CategoryIds.Concat(CategoryIds ?? new List<URN>()).Distinct().ToList());
            }
        }

        public async Task LoadCategoriesAsync(IEnumerable<CultureInfo> cultures)
        {
            var wantedCultures = cultures as List<CultureInfo> ?? cultures.ToList();
            try
            {
                await _loadedCategoriesSemaphore.WaitAsync().ConfigureAwait(false);
                wantedCultures = LanguageHelper.GetMissingCultures(wantedCultures, _loadedCategories).ToList();
                if (!wantedCultures.Any())
                {
                    return;
                }

                foreach (var culture in wantedCultures)
                {
                    await _dataRouterManager.GetSportCategoriesAsync(Id, culture).ConfigureAwait(false);
                }
                _loadedCategories.AddRange(wantedCultures);
            }
            finally
            {
                _loadedCategoriesSemaphore.Release();
            }
        }
    }
}
