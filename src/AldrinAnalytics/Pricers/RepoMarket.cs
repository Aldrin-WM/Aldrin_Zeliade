using System;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Pricers
{

    public class RepoMarket : GenericMarket<Ticker, IRepoCurve>
    {

        private const string XllName = "RepoMarket";

        [WorksheetFunction(XllName + ".New")]
        public RepoMarket(DateTime marketDate) : base(marketDate)
        {
        }

        [WorksheetFunction(XllName + ".AddSheet")]
        public RepoMarket AddSheet(Ticker ticker, DataQuoteSheet sheet, RepoCurveBootstrapper bootstrapper)
        {
            base.AddSheet(ticker, sheet, bootstrapper);
            return this;
        }
    }


}
