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
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.Framework.Execution
{
    /// <summary>
    /// Execution model that submits orders while the current market prices is at least the configured number of standard
    /// deviations away from the mean in the favorable direction (below/above for buy/sell respectively)
    /// </summary>
    public class StandardDeviationExecutionModel : BaseExecutionModel
    {
        private readonly int _period;
        private readonly decimal _deviations;
        private readonly Dictionary<Symbol, SymbolData> _symbolData;

        /// <summary>
        /// Initializes a new instance of the <see cref="StandardDeviationExecutionModel"/> class
        /// </summary>
        /// <param name="period">Period of the standard deviation indicator, created in the security's configured resolution</param>
        /// <param name="deviations">The number of deviations away from the mean before submitting an order</param>
        public StandardDeviationExecutionModel(
            int period = 60,
            decimal deviations = 2m
            )
        {
            _period = period;
            _deviations = deviations;
            _symbolData = new Dictionary<Symbol, SymbolData>();
        }

        /// <summary>
        /// Executes market orders if the standard deviation of price is more than the configured number of deviations
        /// in the favorable direction.
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="unordedQuantities">Data for symbols who still need to submit more orders to reach their portfolio target</param>
        protected override void ExecuteTargets(QCAlgorithmFramework algorithm, Dictionary<Symbol, decimal> unordedQuantities)
        {
            foreach (var kvp in unordedQuantities)
            {
                var symbol = kvp.Key;
                var unorderedQuantity = kvp.Value;

                SymbolData data;
                if (!_symbolData.TryGetValue(symbol, out data))
                {
                    continue;
                }

                if (data.STD.IsReady && PriceIsFavorable(data, unorderedQuantity))
                {
                    // set maximum order size at $20k
                    var maxOrderSize = OrderSizing.Value(data.Security, 20 * 1000);
                    var orderSize = Math.Min(maxOrderSize, Math.Abs(unorderedQuantity));

                    // round down to even lot size
                    orderSize -= orderSize % data.Security.SymbolProperties.LotSize;
                    if (orderSize != 0)
                    {
                        algorithm.MarketOrder(symbol, Math.Sign(unorderedQuantity) * orderSize);
                    }
                }
            }
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public override void OnSecuritiesChanged(QCAlgorithmFramework algorithm, SecurityChanges changes)
        {
            foreach (var added in changes.AddedSecurities)
            {
                if (!_symbolData.ContainsKey(added.Symbol))
                {
                    _symbolData[added.Symbol] = new SymbolData(algorithm, added, _period);
                }
            }

            foreach (var removed in changes.RemovedSecurities)
            {
                SymbolData data;
                if (_symbolData.TryGetValue(removed.Symbol, out data))
                {
                    algorithm.SubscriptionManager.RemoveConsolidator(removed.Symbol, data.Consolidator);
                }
            }
        }


        /// <summary>
        /// Determines if the current price is more than the configured number of standard deviations
        /// away from the mean in the favorable direction.
        /// </summary>
        private bool PriceIsFavorable(SymbolData data, decimal unorderedQuantity)
        {
            if (unorderedQuantity > 0)
            {
                var threshold = data.SMA + _deviations * data.STD;

                if (data.Security.BidPrice > threshold)
                {
                    return false;
                }
            }
            else
            {
                var threshold = data.SMA - _deviations * data.STD;

                if (data.Security.Price < threshold)
                {
                    return false;
                }
            }

            return true;
        }

        class SymbolData
        {
            public Security Security { get; }
            public StandardDeviation STD { get; }
            public SimpleMovingAverage SMA { get; }
            public IDataConsolidator Consolidator { get; }

            public SymbolData(QCAlgorithmFramework algorithm, Security security, int period)
            {
                Security = security;
                Consolidator = algorithm.ResolveConsolidator(security.Symbol, security.Resolution);
                var smaName = algorithm.CreateIndicatorName(security.Symbol, "SMA" + period, security.Resolution);
                SMA = new SimpleMovingAverage(smaName, period);
                var stdName = algorithm.CreateIndicatorName(security.Symbol, "STD" + period, security.Resolution);
                STD = new StandardDeviation(stdName, period);

                Consolidator.DataConsolidated += (sender, consolidated) =>
                {
                    SMA.Update(consolidated.EndTime, consolidated.Value);
                    STD.Update(consolidated.EndTime, consolidated.Value);
                };
            }
        }
    }
}
