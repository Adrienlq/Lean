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

using System.Collections.Generic;
using System.Linq;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Algorithm.Framework.Execution
{
    /// <summary>
    /// Provides a base class to manage symbol data and common tasks between execution models
    /// </summary>
    public abstract class BaseExecutionModel
    {
        private readonly Dictionary<Symbol, decimal> _unorderedQuantities;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseExecutionModel"/> class
        /// </summary>
        protected BaseExecutionModel()
        {
            _unorderedQuantities = new Dictionary<Symbol, decimal>();
        }

        /// <summary>
        /// Submit orders for the specified portolio targets.
        /// This model is free to delay or spread out these orders as it sees fit
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="targets">The portfolio targets to be ordered</param>
        public void Execute(QCAlgorithmFramework algorithm, IEnumerable<IPortfolioTarget> targets)
        {
            foreach (var target in targets)
            {
                // calculate the quantity we need to order to meet our target quantity
                var security = algorithm.Securities[target.Symbol];
                var holdings = security.Holdings.Quantity;
                var openOrderQuantity = algorithm.Transactions.GetOpenOrders(target.Symbol).Sum(o => o.Quantity);
                var quantity = target.Quantity - holdings - openOrderQuantity;
                _unorderedQuantities[target.Symbol] = quantity;
            }

            ExecuteTargets(algorithm, _unorderedQuantities);
        }

        /// <summary>
        /// Uses the model-specific symbol data to execute on the request portfolio targets.
        /// The symbol data provided here only contains the data for symbols whose portfolio
        /// targets have NOT yet been reached.
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="unordedQuantities">Data for symbols who still need to submit more orders to reach their portfolio target</param>
        protected abstract void ExecuteTargets(QCAlgorithmFramework algorithm, Dictionary<Symbol, decimal> unordedQuantities);

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public virtual void OnSecuritiesChanged(QCAlgorithmFramework algorithm, SecurityChanges changes)
        {
            //NOP
        }
    }
}