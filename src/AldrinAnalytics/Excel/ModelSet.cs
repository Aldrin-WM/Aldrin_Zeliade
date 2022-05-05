using System;
using System.Collections.Generic;
using AldrinAnalytics.Models;
using AldrinAnalytics.Instruments;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;

#endif

namespace AldrinAnalytics.Excel
{
    public class ModelSet : GenericSet<Tuple<string, Ticker>, ISingleTickerModel>
    {
        private const string XllName = "ModelSet";

        [WorksheetFunction(XllName + ".New")]
        public ModelSet() : base()
        { }

        [WorksheetFunction(XllName + ".AddModel")]
        public ModelSet Add(string key, ISingleTickerModel value)
        {
            var x = Tuple.Create(key, value.Underlying);
            base.Add(x, value);
            return this;
        }        

    }
}
