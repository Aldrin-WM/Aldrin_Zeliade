using System;
using System.Runtime.Serialization;
using AldrinAnalytics.Instruments;
using Zeliade.Common;
using Zeliade.Common.ManagedXLLTools.FakeImpl;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Mrc;

namespace AldrinAnalytics.Calibration
{
    [DataContract]
    public class AllInCoarse : InstrumentBase, IHavePillar, IHaveUnderlying
    {
        private const string XllName = "AllInCoarse";

        [DataMember]
        public DateTime ExDate { get; private set; }
        [DataMember]
        public Ticker Underlying { get; private set; }

        public DateTime Pillar { get { return ExDate; } }

        private AllInCoarse(MidQuote quoteDiv
            , DateTime exDate
            , Ticker underlying)
            : base(quoteDiv.TimeStamp)
        {
            ExDate = exDate;

            Require.ArgumentNotNull(quoteDiv, "quoteDiv");
            Underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
            AddQuote(quoteDiv);
        }

        protected AllInCoarse(AllInCoarse other)
            : base(other)
        {
            ExDate = other.ExDate;
            Underlying = other.Underlying;
        }

        [WorksheetFunction(XllName + ".NewMid")]
        public static AllInCoarse NewMid(DateTime quoteDate, double value
            , DateTime exDate, Ticker underlying)
        {
            return new AllInCoarse(new MidQuote(quoteDate, value, "AllInCoarse")
                , exDate, underlying);
        }

        [WorksheetFunction(XllName + ".New")]
        public static AllInCoarse New(DateTime quoteDate
            , double bid
            , double ask
            , double mid
            , DateTime exDate, Ticker underlying)
        {
            var t = new AllInCoarse(new MidQuote(quoteDate, mid, "AllInCoarse")
                , exDate, underlying);
            t.AddQuote(new BidQuote(quoteDate, bid));
            t.AddQuote(new AskQuote(quoteDate, ask));
            return t;
        }

        protected override IInstrument DoDeepCopy()
        {
            return new AllInCoarse(this);
        }
    }
}
