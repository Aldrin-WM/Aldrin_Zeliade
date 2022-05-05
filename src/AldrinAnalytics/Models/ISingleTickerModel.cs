using AldrinAnalytics.Instruments;
using System;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Models
{
    public interface ISingleTickerModel
    {
         Ticker Underlying { get; }
    }

    public class StaticModel : ISingleTickerModel
    {
        private const string XllName = "StaticModel";
        public Ticker Underlying { get; private set; }

        [WorksheetFunction(XllName+".New")]
        public StaticModel(Ticker underlying)
        {
            Underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
        }
    }


}
