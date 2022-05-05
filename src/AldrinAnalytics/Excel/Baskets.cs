using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Excel
{
    public class Baskets
    {
        private const string XllName = "Baskets";

        [WorksheetFunction(XllName + ".GetBasket")]
        public static SecurityBasket GetBasket(string name, string refCurrency
            , string[] ticker
            , double[] weights
            , DataQuoteSheet snSheet
            )
        {
            Require.ArgumentNotNullOrEmpty(name, "name");
            Require.ArgumentNotNullOrEmpty(refCurrency, "refCurrency");
            Require.ArgumentNotNull(ticker, "ticker");
            Require.ArgumentNotNull(weights, "weights");
            Require.ArgumentNotNull(snSheet, "snMarket");
            Require.ArgumentNotEmpty(ticker, "ticker");
            Require.ArgumentEqualArrayLength(ticker, weights, "ticker", "weights");

            foreach (var item in snSheet)
            {
                Require.ArgumentIsInstanceOf<SingleNameSecurity>(item, "sheet.Data.item");
            }

            var dico = snSheet.Data.ToDictionary(x => (x as SingleNameSecurity).SingleName.Name, x=>x as SingleNameSecurity);

            var sb = new SecurityBasket(name, refCurrency);
            for (int i = 0; i < ticker.Length; i++)
            {
                var sn = dico[ticker[i]];
                var bc = new BasketComponent() { Underlying = sn.SingleName, Weight = weights[i] };
                sb.AddComponent(bc);
            }

            return sb;
        }
    }

    public class BasketSet : IEnumerable<SecurityBasket>
    {
        private const string XllName = "BasketSet";

        private readonly Dictionary<string, SecurityBasket> _set;

        [WorksheetFunction(XllName + ".New")]
        public BasketSet()
        {
            _set = new Dictionary<string, SecurityBasket>();
        }

        [WorksheetFunction(XllName + ".AddBasket")]
        public BasketSet AddBasket(SecurityBasket b)
        {
            if (_set.ContainsKey(b.Name))
            {
                throw new ArgumentException(string.Format("The basket {0} is already registered in the basket set !", b.Name));
            }
            _set.Add(b.Name, b);

            return this;
        }

        [WorksheetFunction(XllName + ".GetBasket")]
        public SecurityBasket GetBasket(string name)
        {
            SecurityBasket b = null;
            if (!_set.TryGetValue(name, out b))
            {
                throw new ArgumentException(string.Format("The basket {0} is not registered in the basket set !", b.Name));
            }
            return b;
        }

        public bool Contains(string name)
        {
            return _set.ContainsKey(name);
        }

        public IEnumerator<SecurityBasket> GetEnumerator()
        {
            return _set.Values.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
