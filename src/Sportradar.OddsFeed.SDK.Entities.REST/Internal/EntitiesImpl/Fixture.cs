﻿/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.Caching.CI;
using Sportradar.OddsFeed.SDK.Entities.REST.Internal.DTO;
using Sportradar.OddsFeed.SDK.Messages;

namespace Sportradar.OddsFeed.SDK.Entities.REST.Internal.EntitiesImpl
{
    /// <summary>
    /// A implementation of <see cref="IFixture"/> used to return results to user
    /// </summary>
    internal class Fixture : EntityPrinter, IFixture
    {
        /// <summary>
        /// Gets a <see cref="DateTime"/> specifying when the fixture associated with the current <see cref="IFixture"/>
        /// is scheduled to start
        /// </summary>
        public DateTime? StartTime { get; }

        /// <summary>
        /// Gets a <see cref="DateTime"/> specifying the live time in case the fixture represented by current
        /// <see cref="IFixture"/> instance was re-schedule, or a null reference if the fixture was not re-scheduled
        /// </summary>
        public DateTime? NextLiveTime { get; }

        /// <summary>
        /// Gets a value indicating whether the start time of the fixture represented by current
        /// <see cref="IFixture" /> instance has been confirmed
        /// </summary>
        /// <value><c>true</c> if [start time confirmed]; otherwise, <c>false</c>.</value>
        public bool? StartTimeConfirmed { get; }

        /// <summary>
        /// Gets a value indicating whether the start time is to be determent
        /// </summary>
        /// <value><c>null</c> if [start time TBD] contains no value, <c>true</c> if [start time TBD]; otherwise, <c>false</c>.</value>
        public bool? StartTimeTBD { get; }

        /// <summary>
        /// When sport event is postponed this field indicates with which event it is replaced
        /// </summary>
        /// <value>The <see cref="URN"/> this event is replaced by</value>
        public URN ReplacedBy { get; }

        /// <summary>
        /// Gets a <see cref="IReadOnlyDictionary{String, String}" /> containing additional information about the
        /// fixture represented by current <see cref="IFixture" /> instance
        /// </summary>
        /// <value>The extra information.</value>
        public IReadOnlyDictionary<string, string> ExtraInfo { get; }

        /// <summary>
        /// Gets a <see cref="IEnumerable{ITvChannel}" /> representing TV channels covering the sport event
        /// represented by the current <see cref="IFixture" /> instance
        /// </summary>
        /// <value>The tv channels.</value>
        public IEnumerable<ITvChannel> TvChannels { get; }

        /// <summary>
        /// Gets a <see cref="ICoverageInfo" /> instance specifying what coverage is available for the sport event
        /// associated with current instance
        /// </summary>
        /// <value>The coverage information.</value>
        public ICoverageInfo CoverageInfo { get; }

        /// <summary>
        /// Gets a <see cref="IProductInfo" /> instance providing sportradar related information about the sport event associated
        /// with the current instance.
        /// </summary>
        /// <value>The product information.</value>
        public IProductInfo ProductInfo { get; }

        /// <summary>
        /// Gets the reference ids
        /// </summary>
        public IReference References { get; }

        /// <summary>
        /// Gets the scheduled start time changes
        /// </summary>
        /// <value>The scheduled start time changes</value>
        public IEnumerable<IScheduledStartTimeChange> ScheduledStartTimeChanges { get; }

        internal Fixture(FixtureDTO fixtureDto)
        {
            Contract.Requires(fixtureDto != null);

            StartTime = fixtureDto.StartTime;
            NextLiveTime = fixtureDto.NextLiveTime;
            StartTimeConfirmed = fixtureDto.StartTimeConfirmed;
            StartTimeTBD = fixtureDto.StartTimeTBD;
            ReplacedBy = fixtureDto.ReplacedBy;
            ExtraInfo = fixtureDto.ExtraInfo;
            if (fixtureDto.TvChannels != null)
            {

                if (TvChannels == null)
                {
                    TvChannels = fixtureDto.TvChannels.Select(tvChannelDTO => new TvChannel(tvChannelDTO.Name, tvChannelDTO.StartTime, tvChannelDTO.StreamUrl)).ToList();
                }
                else
                {
                    var tvChannels = TvChannels.ToList();
                    foreach (var tvChannelDTO in fixtureDto.TvChannels)
                    {
                        var tvChannel = tvChannels.Find(f => f.Name.Equals(tvChannelDTO.Name, StringComparison.InvariantCultureIgnoreCase));
                        if (tvChannel != null)
                        {
                            tvChannels.Remove(tvChannel);
                        }
                        tvChannels.Add(new TvChannel(tvChannelDTO.Name, tvChannelDTO.StartTime, tvChannelDTO.StreamUrl));
                    }
                    TvChannels = tvChannels;
                }
            }
            if (fixtureDto.CoverageInfo != null)
            {
                CoverageInfo = new CoverageInfo(fixtureDto.CoverageInfo.Level, fixtureDto.CoverageInfo.IsLive, fixtureDto.CoverageInfo.Includes, fixtureDto.CoverageInfo.CoveredFrom);
            }
            if (fixtureDto.ProductInfo != null)
            {
                ProductInfo = new ProductInfo(fixtureDto.ProductInfo.IsAutoTraded,
                                               fixtureDto.ProductInfo.IsInHostedStatistics,
                                               fixtureDto.ProductInfo.IsInLiveCenterSoccer,
                                               fixtureDto.ProductInfo.IsInLiveScore,
                                               fixtureDto.ProductInfo.ProductInfoLinks?.Select(t=> new ProductInfoLink(t.Reference, t.Name)),
                                               fixtureDto.ProductInfo.StreamingChannels?.Select(t=> new StreamingChannel(t.Id, t.Name)));
            }

            if (fixtureDto.ReferenceIds != null)
            {
                References = new Reference(new ReferenceIdCI(fixtureDto.ReferenceIds));
            }

            if (fixtureDto.ScheduledStartTimeChanges != null)
            {
                ScheduledStartTimeChanges = fixtureDto.ScheduledStartTimeChanges.Select(s => new ScheduledStartTimeChange(s));
            }
        }

        /// <summary>
        /// Constructs and returns a <see cref="string"/> containing the id of the current instance
        /// </summary>
        /// <returns>A <see cref="string"/> containing the id of the current instance.</returns>
        protected override string PrintI()
        {
            return $"StartTime={StartTime}, StartTimeConfirmed={StartTimeConfirmed}";
        }

        /// <summary>
        /// Constructs and returns a <see cref="string"/> containing compacted representation of the current instance
        /// </summary>
        /// <returns>A <see cref="string"/> containing compacted representation of the current instance.</returns>
        protected override string PrintC()
        {
            var info = ExtraInfo == null ? string.Empty : string.Join(", ", ExtraInfo.Keys.Select(k => $"{k}={ExtraInfo[k]}"));
            return $"StartTime={StartTime}, StartTimeConfirmed={StartTimeConfirmed}, StartTimeTBD={StartTimeTBD}, ReplacedBy={ReplacedBy}, ExtraInfo=[{info}]";
        }

        /// <summary>
        /// Constructs and return a <see cref="string"/> containing details of the current instance
        /// </summary>
        /// <returns>A <see cref="string"/> containing details of the current instance.</returns>
        protected override string PrintF()
        {
            var tv = TvChannels == null ? string.Empty : string.Join(", ", TvChannels.Select(k => ((TvChannel)k).ToString("f")));
            var res = PrintC();
            res += $", TvChannels=[{tv}], CoverageInfo=[{((CoverageInfo)CoverageInfo)?.ToString("f")}], ProductInfo=[{((ProductInfo)ProductInfo)?.ToString("f")}]";
            return res;
        }

        /// <summary>
        /// Constructs and returns a string containing a JSON representation of the current instance
        /// </summary>
        /// <returns>a <see cref="string" /> containing a JSON representation of the current instance.</returns>
        protected override string PrintJ()
        {
            return PrintJ(GetType(), this);
        }
    }
}
