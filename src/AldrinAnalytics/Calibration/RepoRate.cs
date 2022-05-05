using System;
using System.Runtime.Serialization;
using AldrinAnalytics.Instruments;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Calibration.RateCurves;
using Zeliade.Finance.Mrc;
using Zeliade.Common;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Calibration
{
    [DataContract]
    public class RepoRate : InstrumentBase, IHavePillar, IHaveUnderlying
    {
        private const string XllName = "RepoRate";

        [DataMember]
        public Ticker Underlying { get; private set; }
        [DataMember]
        public CompoundingRateType Compounding { get; private set; }
        [DataMember]
        public IDayCountFraction DayCount { get; private set; }
        [DataMember]
        public DateTime Maturity { get; private set; }

        public DateTime Pillar { get { return Maturity; } }

        private RepoRate(DateTime ts
            , Ticker ticker
            , DateTime maturity
            , CompoundingRateType compounding
            , IDayCountFraction dayCount)
            : base(ts)
        {
            Compounding = compounding;
            DayCount = dayCount ?? throw new ArgumentNullException(nameof(dayCount));
            Underlying = ticker ?? throw new ArgumentNullException(nameof(ticker));
            Require.ArgumentRange(ValueRange.GreaterThan(ts), maturity, "maturity");
            Maturity = maturity;
        }

        public RepoRate(RepoRate other) : base(other)
        {
            Underlying = other.Underlying;
            Compounding = other.Compounding;
            DayCount = other.DayCount;
            Maturity = other.Maturity;
        }

        [WorksheetFunction(XllName + ".NewMid")]
        public static RepoRate NewMid(DateTime quoteDate
            , double value
            , Ticker ticker
            , DateTime maturity
            , CompoundingRateType compounding
            , IDayCountFraction dayCount)
        {
            var i = new RepoRate(quoteDate, ticker, maturity, compounding, dayCount);
            var q = new MidQuote(quoteDate, value);
            i.AddQuote(q);
            return i;
        }

        [WorksheetFunction(XllName + ".New")]
        public static RepoRate New(DateTime quoteDate
            , double bid
            , double ask
            , double mid
            , Ticker ticker
            , DateTime maturity
            , CompoundingRateType compounding
            , IDayCountFraction dayCount)
        {
            var i = new RepoRate(quoteDate, ticker, maturity, compounding, dayCount);
            i.AddQuote(new BidQuote(quoteDate, bid));
            i.AddQuote(new AskQuote(quoteDate, ask));
            i.AddQuote(new MidQuote(quoteDate, mid));
            return i;
        }
        protected override IInstrument DoDeepCopy()
        {
            return new RepoRate(this);
        }

    }

}
