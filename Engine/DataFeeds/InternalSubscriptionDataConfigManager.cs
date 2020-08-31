/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Class in charge of handling Leans internal subscriptions
    /// </summary>
    public class InternalSubscriptionDataConfigManager
    {
        private readonly Resolution _resolution;
        private readonly IAlgorithm _algorithm;
        private UserDefinedUniverse _universe;

        /// <summary>
        /// Creates a new instances
        /// </summary>
        /// <param name="algorithm">The associated algorithm</param>
        /// <param name="resolution">The resolution to use for the internal subscriptions</param>
        public InternalSubscriptionDataConfigManager(IAlgorithm algorithm, Resolution resolution)
        {
            _resolution = resolution;
            _algorithm = algorithm;
        }

        /// <summary>
        /// Notifies about a removed subscription request
        /// </summary>
        /// <param name="request">The removed subscription request</param>
        public void AddedSubscriptionRequest(SubscriptionRequest request)
        {
            if (PreFilter(request))
            {
                var lowResolution = request.Configuration.Resolution > Resolution.Minute;
                var alreadyInternal = _universe != null && _universe.ContainsMember(request.Configuration.Symbol);
                if (lowResolution && !alreadyInternal)
                {
                    if (_universe == null)
                    {
                        // lazy on demand initialization
                        Initialize();
                    }
                    // low resolution subscriptions we will add internal Resolution.Minute subscriptions
                    // if we don't already have this symbol added
                    _universe.Add(new SubscriptionDataConfig(request.Configuration, resolution: _resolution, isInternalFeed: true));
                }
                else if (!lowResolution && alreadyInternal)
                {
                    // the user added a higher resolution configuration, we can remove the internal we added
                    _universe.Remove(request.Configuration.Symbol);
                }
            }
        }

        /// <summary>
        /// Notifies about an added subscription request
        /// </summary>
        /// <param name="request">The added subscription request</param>
        public void RemovedSubscriptionRequest(SubscriptionRequest request)
        {
            if (PreFilter(request) && _universe != null && _universe.ContainsMember(request.Configuration.Symbol))
            {
                var userConfigs = _algorithm.SubscriptionManager.SubscriptionDataConfigService
                    .GetSubscriptionDataConfigs(request.Configuration.Symbol).ToList();

                if (userConfigs.Count == 0 || userConfigs.Any(config => config.Resolution <= Resolution.Minute))
                {
                    // if we had a config and the user no longer has a config for this symbol we remove the internal subscription
                    _universe.Remove(request.Configuration.Symbol);
                }
            }
        }

        /// <summary>
        /// True for for live trading, non internal, non universe subscriptions, non custom data subscriptions
        /// </summary>
        private bool PreFilter(SubscriptionRequest request)
        {
            return _algorithm.LiveMode && !request.Configuration.IsInternalFeed && !request.IsUniverseSubscription && !request.Configuration.IsCustomData;
        }

        /// <summary>
        /// Late lazy initialization when required
        /// </summary>
        private void Initialize()
        {
            // create a new universe, these subscription settings don't currently get used
            // since universe selection proper is never invoked on this type of universe
            var universeSymbol = new Symbol(SecurityIdentifier.GenerateEquity("internal", Market.USA), "internal");
            var uconfig = new SubscriptionDataConfig(typeof(TradeBar),
                universeSymbol,
                _resolution,
                TimeZones.NewYork,
                TimeZones.NewYork,
                fillForward: false,
                extendedHours: true,
                isInternalFeed: true);
            var universeSetting = new UniverseSettings(_resolution, 1, false, true, TimeSpan.Zero);
            _universe = new UserDefinedUniverse(uconfig, universeSetting, Time.MaxTimeSpan, new List<Symbol>());

            _algorithm.UniverseManager.Add(universeSymbol, _universe);
        }
    }
}
