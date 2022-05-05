using System;
using System.Collections.Generic;
using AldrinAnalytics.Pricers;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Excel
{
    public class PricingContextSet  : GenericSet<string, GlobalMarket>
    {

        private const string XllName = "PricingContextSet";

        [WorksheetFunction(XllName + ".New")]
        public PricingContextSet() : base()
        {}

        [WorksheetFunction(XllName + ".AddContext")]
        public override GenericSet<string, GlobalMarket> Add(string key, GlobalMarket value)
        {
            base.Add(key, value);
            return this;
        }

    }
}
