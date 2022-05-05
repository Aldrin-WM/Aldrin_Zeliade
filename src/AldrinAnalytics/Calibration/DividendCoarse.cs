using System;
using System.Runtime.Serialization;
using AldrinAnalytics.Instruments;
using Zeliade.Common;
using Zeliade.Common.ManagedXLLTools.FakeImpl;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Mrc;

namespace AldrinAnalytics.Calibration
{
    public class DividendCoarse: InstrumentBase, IHaveUnderlying, IHavePillar
    {
        private const string XllName = "DividendCoarse";

        [DataMember]
        public DateTime ExDate { get; private set; }
        [DataMember]        
        public Currency Ccy { get; private set; }

        [DataMember]
        public Ticker Underlying { get; private set; }

        public DateTime Pillar { get { return ExDate; } }

        private DividendCoarse(MidQuote quoteDiv
            , DateTime exDate
            , Currency ccy
            , Ticker underlying)
            : base(quoteDiv.TimeStamp)
        {
            ExDate = exDate;
            Ccy = Require.ArgumentNotNull(ccy, "ccy");

            Require.ArgumentNotNull(quoteDiv, "quoteDiv");
            Underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
            AddQuote(quoteDiv);
        }

        protected DividendCoarse(DividendCoarse other)
            : base(other)
        {
            ExDate = other.ExDate;
            Underlying = other.Underlying;
            Ccy = other.Ccy;
        }

        [WorksheetFunction(XllName + ".NewMid")]
        public static DividendCoarse NewMid(DateTime quoteDate, double value
            , DateTime exDate, Ticker underlying)
        {
            return new DividendCoarse(new MidQuote(quoteDate, value, "DivCoarse")
                , exDate, underlying.ReferenceCurrency, underlying);
        }

        [WorksheetFunction(XllName + ".New")]
        public static DividendCoarse New(DateTime quoteDate
            , double bid
            , double ask
            , double mid
            , DateTime exDate
            , Currency ccy
            , Ticker underlying)
        {
            var t= new DividendCoarse(new MidQuote(quoteDate, mid, "DivCoarse")
                , exDate, ccy, underlying);
            t.AddQuote(new BidQuote(quoteDate, bid));
            t.AddQuote(new AskQuote(quoteDate, ask));
            return t;
        }

        protected override IInstrument DoDeepCopy()
        {
            return new DividendCoarse(this);
        }
    }
}
