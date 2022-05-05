using System;
using System.Runtime.Serialization;
using AldrinAnalytics.Instruments;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Calibration
{
    [DataContract]
    public class AllIn : InstrumentBase, IHavePillar, IHaveUnderlying
    {
        private const string XllName = "AllIn";

        [DataMember]
        public Ticker Underlying { get; private set; }
        [DataMember]
        public DateTime Pillar { get; private set; } 

        private AllIn(MidQuote quote
            , DateTime pillar
            , Ticker underlying)
            : base(quote.TimeStamp)
        {
            Pillar = pillar;
            Require.ArgumentNotNull(quote, "quote");
            Require.ArgumentRange(ValueRange.GreaterThan(quote.TimeStamp), pillar, "pillar");
            Underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
            AddQuote(quote);
        }

        protected AllIn(AllIn other)
            : base(other)
        {
            Pillar = other.Pillar;
            Underlying = other.Underlying;
        }

        [WorksheetFunction(XllName + ".NewMid")]
        public static AllIn NewMid(DateTime quoteDate, double allIn
            , DateTime exDate, Ticker underlying)
        {
            return new AllIn(new MidQuote(quoteDate, allIn, "AllIn")
                , exDate, underlying);
        }

        [WorksheetFunction(XllName + ".New")]
        public static AllIn New(DateTime quoteDate, double bid, double ask, double mid
            , DateTime exDate, Ticker underlying)
        {
            var t = new AllIn(new MidQuote(quoteDate, mid, "AllIn")
                , exDate, underlying);
            t.AddQuote(new BidQuote(quoteDate, bid));
            t.AddQuote(new AskQuote(quoteDate, ask));
            return t;
        }

        protected override IInstrument DoDeepCopy()
        {
            return new AllIn(this);
        }
    }

}
