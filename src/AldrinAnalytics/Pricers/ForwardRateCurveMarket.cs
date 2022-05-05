using AldrinAnalytics.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Common.Calibration;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Pricers
{
    public class ForwardRateCurveMarket : GenericMarket<RateReference, IForwardRateCurve>
    {
        private const string XllName = "ForwardRateCurveMarket";

        [WorksheetFunction(XllName + ".New")]
        public ForwardRateCurveMarket(DateTime marketDate) : base(marketDate)
        {
        }

        [WorksheetFunction(XllName + ".GetCurve")]
        public IForwardRateCurve GetCurve(RateReference ticker, string quoteType)
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
