using System;
using Zeliade.Common;
using Zeliade.Finance.Common.Product;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Instruments
{
    public class SingleNameTicker : Ticker
    {
        private const string XllName = "SingleNameTicker";

        public string Industry { get; private set; }
        public string IndustrySub { get; private set; }

        public Currency DividendCurrency { get; private set; }

        public SingleNameTicker(string name, string ccy, string divCcy): base(name, ccy)
        {
            Industry = "";
            IndustrySub = "";
            DividendCurrency = new Currency(divCcy);
        }

        [WorksheetFunction(XllName + ".New")]
        public SingleNameTicker(string name, string ccy
            , string divCcy
            , string industry
            , string industrySub) : base(name, ccy)
        {
            Industry = Require.ArgumentNotNull(industry, "industry");
            IndustrySub = Require.ArgumentNotNull(industrySub, "industrySub");
            DividendCurrency = new Currency(divCcy);
        }

    }
}
