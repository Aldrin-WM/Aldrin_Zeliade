using System;
using System.Runtime.Serialization;
using AldrinAnalytics.Instruments;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Product;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Calibration
{
    [DataContract]
    public class DividendEstimate : InstrumentBase, IHavePillar, IHaveUnderlying
    {
        private const string XllName = "DividendEstimate";

        [DataMember]
        public DateTime ExDate { get; private set; }
        [DataMember]
        public DateTime PaymentDate { get; private set; }
        [DataMember]
        public Ticker Underlying { get; private set; }
        [DataMember]
        public Currency Ccy { get; private set; }

        public DateTime Pillar { get { return ExDate; } }        

        private DividendEstimate(MidQuote quoteDiv
            , DateTime exDate
            , DateTime paymentDate
            , Currency ccy
            , Ticker underlying)
            : base(quoteDiv.TimeStamp)
        {
            ExDate = exDate;
            PaymentDate = paymentDate;
            Ccy = Require.ArgumentNotNull(ccy, "ccy");

            Require.ArgumentNotNull(quoteDiv, "quoteDiv");
            //Require.ArgumentRange(ValueRange.GreaterThan(quoteDiv.TimeStamp), exDate, "exDate");
            Require.ArgumentRange(ValueRange.LessOrEqual(paymentDate), exDate, "exDate");
            Underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
            AddQuote(quoteDiv);
        }

        protected DividendEstimate(DividendEstimate other)
            : base(other)
        {
            ExDate = other.ExDate;
            PaymentDate = other.PaymentDate;
            Underlying = other.Underlying;
            Ccy = other.Ccy;
        }

        public static DividendEstimate NewMid(DateTime quoteDate, double divEstimate
            , DateTime exDate, DateTime paymentDate, Ticker underlying)
        {
            return new DividendEstimate(new MidQuote(quoteDate, divEstimate, "Div")
                , exDate, paymentDate, underlying.ReferenceCurrency, underlying);
        }

        public static DividendEstimate New(DateTime quoteDate
            , double bid, double ask, double mid
            , DateTime exDate, DateTime paymentDate
            , Currency ccy
            , Ticker underlying)
        {
            var t = new DividendEstimate(new MidQuote(quoteDate, mid, "Div")
                , exDate, paymentDate, ccy, underlying);
            t.AddQuote(new BidQuote(quoteDate, bid));
            t.AddQuote(new AskQuote(quoteDate, ask));
            return t;
        }

        protected override IInstrument DoDeepCopy()
        {
            return new DividendEstimate(this);
        }
    }

}
