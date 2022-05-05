using AldrinAnalytics.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zeliade.Finance.Common.RateCurves;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Pricers
{
    public class LiborDiscountingMarket : GenericMarket<LiborReference, IDiscountCurve<DateTime>>
    {
        private const string XllName = "LiborDiscountingMarket";

        [WorksheetFunction(XllName + ".New")]
        public LiborDiscountingMarket(DateTime marketDate) : base(marketDate)
        {
        }
    }
}
