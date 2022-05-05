using System;
using System.Collections.Generic;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using Zeliade.Common;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Pricers
{

    public class FxCurveMarket : GenericMarket<CurrencyPair, IForwardForexCurve>
    {
        //private const string XllName = "FxCurveMarket";

        public FxCurveMarket(DateTime marketDate) : base(marketDate)
        {
        }
    }
}
