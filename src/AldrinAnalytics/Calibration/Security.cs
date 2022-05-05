using System;
using AldrinAnalytics.Instruments;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Common;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Calibration
{
    public class Security : InstrumentBase
    {
        private const string XllName = "Security";

        public Ticker Underlying {get; private set;}

        protected Security(DateTime ts
            , Ticker underlying)
            :base(ts)
        {
            Underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
        }

        public Security(Security other)
            : base(other)
        {
            Underlying = other.Underlying;
        }

        [WorksheetFunction(XllName + ".NewMid")]
        public static Security NewMid(DateTime quoteDate
           , double value
           , Ticker ticker)
        {
            var output = new Security(quoteDate, ticker);
            output.AddQuote(new MidQuote(quoteDate, value));
            return output;
        }

        protected override IInstrument DoDeepCopy()
        {
            return new Security(this);
        }

    }

    public class SingleNameSecurity : Security
    {
        private const string XllName = "SingleNameSecurity";

        public SingleNameTicker SingleName { get; private set; }

        protected SingleNameSecurity(DateTime ts
            , SingleNameTicker underlying)
            : base(ts, underlying)
        {
            SingleName = underlying ?? throw new ArgumentNullException(nameof(underlying));
        }

        public SingleNameSecurity(SingleNameSecurity other)
            : base(other)
        {
            SingleName = other.SingleName;
        }

        [WorksheetFunction(XllName + ".NewMid")]
        public static SingleNameSecurity NewMid(DateTime quoteDate
           , double value
           , SingleNameTicker ticker)
        {
            var output = new SingleNameSecurity(quoteDate, ticker);
            output.AddQuote(new MidQuote(quoteDate, value));
            return output;
        }

        [WorksheetFunction(XllName + ".New")]
        public static SingleNameSecurity New(DateTime quoteDate
          , double bid
          , double ask
          , double mid
          , SingleNameTicker ticker)
        {
            var output = new SingleNameSecurity(quoteDate, ticker);
            output.AddQuote(new BidQuote(quoteDate, bid));
            output.AddQuote(new AskQuote(quoteDate, ask));
            output.AddQuote(new MidQuote(quoteDate, mid));
            return output;
        }

        protected override IInstrument DoDeepCopy()
        {
            return new SingleNameSecurity(this);
        }
    }

    public class CurrencyPairSecurity : Security
    {
        private const string XllName = "CurrencyPairSecurity";

        public CurrencyPair CcyPair { get; private set; }

        private CurrencyPairSecurity(DateTime ts
            , CurrencyPair underlying)
            : base(ts, underlying)
        {
            CcyPair = Require.ArgumentNotNull(underlying, "underlying");
        }

        public CurrencyPairSecurity(CurrencyPairSecurity other)
            : base(other)
        {
            CcyPair = other.CcyPair;
        }

        [WorksheetFunction(XllName + ".NewMid")]
        public static CurrencyPairSecurity NewMid(DateTime quoteDate
           , double value
           , CurrencyPair ticker)
        {

            var output = new CurrencyPairSecurity(quoteDate, ticker);
            output.AddQuote(new MidQuote(quoteDate, value));
            return output; 
        }

        [WorksheetFunction(XllName + ".New")]
        public static CurrencyPairSecurity New(DateTime quoteDate
           , double bid
           , double ask
           , double mid
           , CurrencyPair ticker)
        {
            var output = new CurrencyPairSecurity(quoteDate, ticker);
            output.AddQuote(new MidQuote(quoteDate, mid));
            output.AddQuote(new BidQuote(quoteDate, bid));
            output.AddQuote(new AskQuote(quoteDate, ask));
            return output;
        }

        protected override IInstrument DoDeepCopy()
        {
            return new CurrencyPairSecurity(this);
        }
    }
}
