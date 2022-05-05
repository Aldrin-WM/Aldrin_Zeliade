using System;
using Zeliade.Common;

namespace AldrinAnalytics.Instruments
{
    public class CurrencyPair : Ticker
    {
        public string ForeignCurrency { get; private set; }
        public string DomesticCurrency { get { return ReferenceCurrency.Code; } }

        public CurrencyPair(string domesticCurrency, string foreignCurrency) 
            : base(string.Format("{0}/{1}", domesticCurrency, foreignCurrency), domesticCurrency)
        {
            ForeignCurrency = Require.ArgumentNotNullOrEmpty(foreignCurrency, "foreignCurrency");
        }
    }
}
