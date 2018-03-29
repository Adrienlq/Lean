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

using QuantConnect.Securities;

namespace QuantConnect.Algorithm.Framework.Execution
{
    /// <summary>
    /// Provides methods for computing a maximum order size.
    /// </summary>
    public static class OrderSizing
    {
        /// <summary>
        /// Gets the maximum order size as a percentage of the current bar's volume.
        /// </summary>
        /// <param name="security">The security object</param>
        /// <param name="maximumPercentCurrentVolume">The maximum percentage of the current bar's volume</param>
        /// <returns>The fractional quantity of shares that equal the specified percentage of the current bar's volume</returns>
        public static decimal PercentVolume(Security security, decimal maximumPercentCurrentVolume)
        {
            return maximumPercentCurrentVolume * security.Volume;
        }

        /// <summary>
        /// Gets the maximum order size using a maximum order value in units of the account currency
        /// </summary>
        /// <param name="security">The security object</param>
        /// <param name="maximumOrderValueInAccountCurrency">The maximum order value in units of the account currency</param>
        /// <returns>The quantity of fractional of shares that yield the specified maximum order value</returns>
        public static decimal Value(Security security, decimal maximumOrderValueInAccountCurrency)
        {
            var priceInAccountCurrency = security.Price * security.QuoteCurrency.ConversionRate;

            if (priceInAccountCurrency == 0m)
            {
                return 0m;
            }

            return maximumOrderValueInAccountCurrency / priceInAccountCurrency;
        }
    }
}
