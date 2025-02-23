/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;
using Sportradar.OddsFeed.SDK.Messages.REST;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO
{
    /// <summary>
    /// A data-transfer-object representation for product info
    /// </summary>
    internal class ProductInfoDTO
    {
        internal bool IsInLiveScore { get; }

        internal bool IsInHostedStatistics { get; }

        internal bool IsInLiveCenterSoccer { get; }

        internal bool IsAutoTraded { get; }

        internal IEnumerable<ProductInfoLinkDTO> ProductInfoLinks { get; }

        internal IEnumerable<StreamingChannelDTO> StreamingChannels { get; }

        internal ProductInfoDTO(productInfo productInfo)
        {
            Contract.Requires(productInfo != null);

            IsInLiveScore = productInfo.is_in_live_score != null;
            IsInHostedStatistics = productInfo.is_in_hosted_statistics != null;
            IsInLiveCenterSoccer = productInfo.is_in_live_center_soccer != null;
            IsAutoTraded = productInfo.is_auto_traded != null;

            ProductInfoLinks = productInfo.links != null && productInfo.links.Any()
                ? new ReadOnlyCollection<ProductInfoLinkDTO>(productInfo.links.Select(p => new ProductInfoLinkDTO(p)).ToList())
                : null;

            StreamingChannels = productInfo.streaming != null && productInfo.streaming.Any()
                ? new ReadOnlyCollection<StreamingChannelDTO>(productInfo.streaming.Select(s => new StreamingChannelDTO(s)).ToList())
                : null;
        }
    }
}