using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using AldrinAnalytics.Instruments;
using Zeliade.Finance.Common.Calibration;
using AldrinAnalytics.Calibration;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Pricers
{
    public class OisCurveMarket : GenericMarket<Currency, IDiscountCurve<DateTime>>
    {
        private const string XllName = "OisCurveMarket";

        [WorksheetFunction(XllName + ".New")]
        public OisCurveMarket(DateTime marketDate) : base(marketDate)
        {
        }

        [WorksheetFunction(XllName + ".AddSheet")]
        public OisCurveMarket AddSheet(string currency, DataQuoteSheet sheet, DiscountCurveBootstrapperOis bootstrapper)
        {
            var ccy = new Currency(currency);
            base.AddSheet(ccy, sheet, bootstrapper);
            return this;
        }

        [WorksheetFunction(XllName + ".GetCurve")]
        public IDiscountCurve<DateTime> GetCurve(Currency ticker, string quoteType)
        {
            Type typ = null;
            switch (quoteType)
            {
                case ("Mid"): typ = typeof(MidQuote); break;
                case ("Bid"): typ = typeof(BidQuote); break;
                case ("Ask"): typ = typeof(AskQuote); break;
                default: throw new ArgumentException(string.Format("The input quote type{0} is unknown. Should be either Mid, Bid Or Ask.", quoteType));
            }

            return Get(ticker, null, typ);
        }
    }
}
