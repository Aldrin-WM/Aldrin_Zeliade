using System;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;

namespace AldrinAnalytics.Pricers
{


    public class ForexDataMarket : GenericMarket<CurrencyPair, CurrencyPairSecurity>
    {
        public ForexDataMarket(DateTime marketDate)
            : base(marketDate)
        {
        }

        public ForexDataMarket Add(CurrencyPair ticker, CurrencyPairSecurity security)
        {
            Require.ArgumentNotNull(ticker, "ticker");
            Require.ArgumentNotNull(security, "security");

            var sheet = new DataQuoteSheet(MarketDate, new IInstrument[] { security });
            AddSheet(ticker, sheet, new SecurityBootstrapper<CurrencyPairSecurity>());
            return this;
        }

    }
}
