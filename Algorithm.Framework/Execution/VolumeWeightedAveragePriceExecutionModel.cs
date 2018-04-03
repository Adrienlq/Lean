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
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.Framework.Execution
{
    /// <summary>
    /// Execution model that submits orders while the current market price is more favorable that the current volume weighted average price.
    /// </summary>
    public class VolumeWeightedAveragePriceExecutionModel : BaseExecutionModel
    {
        private readonly Dictionary<Symbol, SymbolData> _symbolData = new Dictionary<Symbol, SymbolData>();

        /// <summary>
        /// Uses the model-specific symbol data to execute on the request portfolio targets.
        /// The symbol data provided here only contains the data for symbols whose portfolio
        /// targets have NOT yet been reached.
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

                if (PriceIsFavorable(data, unorderedQuantity))
                {
                    // maximum order size as 1% of current bar's volume
                    var maxOrderSize = OrderSizing.PercentVolume(data.Security, 0.01m);
                    var orderSize = Math.Min(maxOrderSize, Math.Abs(unorderedQuantity));

                    // round down to even lot size
                    orderSize -= orderSize % data.Security.SymbolProperties.LotSize;
                    if (orderSize != 0)
                    {
                        algorithm.MarketOrder(data.Security.Symbol, Math.Sign(unorderedQuantity) * orderSize);
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
                    _symbolData[added.Symbol] = new SymbolData(algorithm, added);
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
        /// Determines if the current price is better than VWAP
        /// </summary>
        private bool PriceIsFavorable(SymbolData data, decimal unorderedQuantity)
        {
            if (data.Security.Price == 0m)
            {
                return false;
            }

            var vwap = data.VWAP;
            if (unorderedQuantity > 0)
            {
                var price = data.Security.BidPrice == 0
                    ? data.Security.Price
                    : data.Security.BidPrice;

                if (price > vwap)
                {
                    return false;
                }
            }
            else
            {
                var price = data.Security.AskPrice == 0
                    ? data.Security.AskPrice
                    : data.Security.Price;

                if (price < vwap)
                {
                    return false;
                }
            }

            return true;
        }

        private class SymbolData
        {
            public Security Security { get; }
            public IntradayVwap VWAP { get; }
            public IDataConsolidator Consolidator { get; }

            public SymbolData(QCAlgorithmFramework algorithm, Security security)
            {
                Security = security;
                Consolidator = algorithm.ResolveConsolidator(security.Symbol, security.Resolution);
                var name = algorithm.CreateIndicatorName(security.Symbol, "VWAP", security.Resolution);
                VWAP = new IntradayVwap(name);

                Consolidator.DataConsolidated += (sender, consolidated) => VWAP.Update((BaseData) consolidated);
            }
        }
    }
}