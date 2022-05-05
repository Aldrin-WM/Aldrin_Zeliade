using System;
using AldrinAnalytics.Pricers;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Excel
{
    public class PricingSettingSet : GenericSet<string, IPricingSetting>
    {

        private const string XllName = "PricingSettingSet";

        [WorksheetFunction(XllName + ".New")]
        public PricingSettingSet() : base()
        { }

        [WorksheetFunction(XllName + ".AddSetting")]
        public override GenericSet<string, IPricingSetting> Add(string key, IPricingSetting value)
        {
            base.Add(key, value);
            return this;
        }
    }
}
