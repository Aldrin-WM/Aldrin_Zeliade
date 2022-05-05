using System;
using System.Collections.Generic;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Calibration.RateCurves.Instruments;
using Zeliade.Finance.Mrc;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Pricers;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.Calibration.RateCurves;
using System.Linq;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Excel
{
    public class InstrumentTypes
    {
        public const string OisType = "ois";
        public const string CashType = "cash";
        public const string FutureType = "future";
        public const string SwapType = "swap";
        public const string BasisSwapType = "basisswap";
    }

    public class DataSheets
    {
        private const string XllName = "DataSheets";

        [WorksheetFunction(XllName + ".OisSheet")]
        public static DataQuoteSheet OisSheet(DateTime quoteDate
            , string[] tenor
            , double[] bid
            , double[] ask
            , double[] mid
            , IBusinessCenter businessCenter
            , IBusinessDayConvention businessConvention
            , IDayCountFraction fixedDayCountConvention
            , IDayCountFraction floatingDayCountConvention
            , string currency
            )
        {
            Require.ArgumentNotNull(tenor, "tenor");
            Require.ArgumentNotNull(bid, "bid");
            Require.ArgumentNotNull(ask, "ask");
            Require.ArgumentNotNull(mid, "mid");
            Require.ArgumentNotNull(businessCenter, "businessCenter");
            Require.ArgumentNotNull(businessConvention, "businessConvention");
            Require.ArgumentNotNull(fixedDayCountConvention, "fixedDayCountConvention");
            Require.ArgumentNotNull(floatingDayCountConvention, "floatingDayCountConvention");
            Require.ArgumentNotNull(currency, "currency");

            Require.ArgumentNotEmpty(tenor, "tenor");

            Require.ArgumentEqualArrayLength(tenor, bid, "tenor", "bid");
            Require.ArgumentEqualArrayLength(tenor, ask, "tenor", "ask");
            Require.ArgumentEqualArrayLength(tenor, mid, "tenor", "mid");

            var data = new List<IInstrument>();
            for (int i = 0; i < tenor.Length; i++)
            {
                var ois = new OvernightIndexSwapQuote(quoteDate,
                    Periods.Get(tenor[i]),
                    0,
                    businessCenter,
                    businessConvention,
                    fixedDayCountConvention,
                    floatingDayCountConvention,
                    currency);

                // TODO CHECK VALUES
                ois.AddQuote(new BidQuote(quoteDate, bid[i]));
                ois.AddQuote(new AskQuote(quoteDate, ask[i]));
                ois.AddQuote(new MidQuote(quoteDate, mid[i]));

                data.Add(ois);
            }

            return new DataQuoteSheet(quoteDate, data);
        }


        [WorksheetFunction(XllName + ".FutureSwapSheet")]
        public static DataQuoteSheet CashFraSwapSheet(DateTime quoteDate
            , string[] type
            , object[] tenor
            , double[] bid
            , double[] ask
            , double[] mid
            , IBusinessCenter businessCenter
            , IBusinessDayConvention businessConvention
            , IDayCountFraction fixedDayCountConvention
            , IDayCountFraction floatingDayCountConvention
            , IPeriod fixFreq
            , IPeriod fltFreq
            , string currency
            )
        {
            Require.ArgumentNotNull(tenor, "tenor");
            Require.ArgumentNotNull(bid, "bid");
            Require.ArgumentNotNull(ask, "ask");
            Require.ArgumentNotNull(mid, "mid");
            Require.ArgumentNotNull(businessCenter, "businessCenter");
            Require.ArgumentNotNull(businessConvention, "businessConvention");
            Require.ArgumentNotNull(fixedDayCountConvention, "fixedDayCountConvention");
            Require.ArgumentNotNull(floatingDayCountConvention, "floatingDayCountConvention");
            Require.ArgumentNotNull(currency, "currency");

            Require.ArgumentNotEmpty(type, "type");

            Require.ArgumentEqualArrayLength(type, tenor, "type", "tenor");
            Require.ArgumentEqualArrayLength(type, bid, "type", "bid");
            Require.ArgumentEqualArrayLength(type, ask, "type", "ask");
            Require.ArgumentEqualArrayLength(type, mid, "type", "mid");

            var data = new List<IInstrument>();
            for (int i = 0; i < tenor.Length; i++)
            {
                RateInstrument inst = null;
                switch (type[i])
                {
                    case InstrumentTypes.CashType:
                        var p = Periods.Get((string)tenor[i]);
                        inst = DepositQuote.StandardQuote(quoteDate, p, 0,
                            businessCenter, businessConvention, floatingDayCountConvention,
                            currency);
                        break;
                    case InstrumentTypes.FutureType:
                        var mat = DateTime.FromOADate((double)tenor[i]);
                        inst = new FraQuote(quoteDate, mat, fltFreq, currency, 0,
                            businessCenter, businessConvention, floatingDayCountConvention);
                        break;
                    case InstrumentTypes.SwapType: 
                        var pswap = Periods.Get((string)tenor[i]);
                        inst = new SwapQuote(quoteDate, pswap, 0, businessCenter, businessConvention,
                            fixedDayCountConvention, floatingDayCountConvention,
                            fixFreq, fltFreq, currency);
                        break;

                    default:
                        throw new ArgumentException("The instrument type {0} is not supported !", type[i]);                        
                }

                inst.AddQuote(new BidQuote(quoteDate, bid[i]));
                inst.AddQuote(new AskQuote(quoteDate, ask[i]));
                inst.AddQuote(new MidQuote(quoteDate, mid[i]));
                data.Add(inst);
            }
                
            return new DataQuoteSheet(quoteDate, data);
        }

        [WorksheetFunction(XllName + ".DividendEstimateSheet")]
        public static DataQuoteSheet DividendEstimateSheet(
            DateTime quoteDate
            , string[] ticker
            , DateTime[] exDate
            , DateTime[] paymentDate
            , string[] currency
            , double[] bid
            , double[] ask
            , double[] mid
            , double[] bidAllIn
            , double[] askAllIn
            , double[] midAllIn
            , DataQuoteSheet snSheet
            )
        {
            Require.ArgumentNotNull(exDate, "exDate");
            Require.ArgumentNotNull(paymentDate, "paymentDate");
            Require.ArgumentNotNull(bid, "bid");
            Require.ArgumentNotNull(ask, "ask");
            Require.ArgumentNotNull(mid, "mid");
            Require.ArgumentNotNull(ticker, "ticker");
            Require.ArgumentNotNull(currency, "currency");

            Require.ArgumentNotEmpty(exDate, "exDate");
            Require.ArgumentEqualArrayLength(exDate, ticker, "exDate", "ticker");
            Require.ArgumentEqualArrayLength(exDate, paymentDate, "exDate", "paymentDate");
            Require.ArgumentEqualArrayLength(exDate, bid, "exDate", "bid");
            Require.ArgumentEqualArrayLength(exDate, ask, "exDate", "ask");
            Require.ArgumentEqualArrayLength(exDate, mid, "exDate", "mid");

            foreach (var item in snSheet)
            {
                Require.ArgumentIsInstanceOf<SingleNameSecurity>(item, "sheet.Data.item");
            }

            var dico = snSheet.Data.ToDictionary(x => (x as SingleNameSecurity).SingleName.Name, x => x as SingleNameSecurity);


            var data = new List<IInstrument>();
            for (int i = 0; i < exDate.Length; i++)
            {
                var t = dico[ticker[i]];
                var ccy = new Currency(currency[i]);
                var div = DividendEstimate.New(quoteDate, bid[i], ask[i]
                    , mid[i], exDate[i], paymentDate[i], ccy, t.SingleName);
                var allIn = AllIn.New(quoteDate, bidAllIn[i], askAllIn[i]
                    , midAllIn[i], exDate[i], t.SingleName);
                data.Add(div);
                data.Add(allIn);
            }

            return new DataQuoteSheet(quoteDate, data);
        }

        [WorksheetFunction(XllName + ".DividendRoughSheet")]
        public static DataQuoteSheet DividendRoughSheet(
           DateTime quoteDate
           , string[] ticker
           , DateTime[] exDate
           , string[] currency
           , double[] bid
           , double[] ask
           , double[] mid
           , double[] bidAllIn
           , double[] askAllIn
           , double[] midAllIn
           , BasketSet basketSet
           )
        {
            Require.ArgumentNotNull(exDate, "exDate");
            Require.ArgumentNotNull(bid, "bid");
            Require.ArgumentNotNull(ask, "ask");
            Require.ArgumentNotNull(mid, "mid");
            Require.ArgumentNotNull(bidAllIn, "bidAllIn");
            Require.ArgumentNotNull(askAllIn, "askAllIn");
            Require.ArgumentNotNull(midAllIn, "midAllIn");
            Require.ArgumentNotNull(ticker, "ticker");
            Require.ArgumentNotNull(currency, "currency");

            Require.ArgumentNotEmpty(ticker, "ticker");
            Require.ArgumentEqualArrayLength(ticker, exDate, "ticker", "exDate");
            Require.ArgumentEqualArrayLength(ticker, bid, "ticker", "bid");
            Require.ArgumentEqualArrayLength(ticker, ask, "ticker", "ask");
            Require.ArgumentEqualArrayLength(ticker, mid, "ticker", "mid");
            Require.ArgumentEqualArrayLength(ticker, bidAllIn, "ticker", "bidAllIn");
            Require.ArgumentEqualArrayLength(ticker, askAllIn, "ticker", "askAllIn");
            Require.ArgumentEqualArrayLength(ticker, midAllIn, "ticker", "midAllIn");

            var data = new List<IInstrument>();
            for (int i = 0; i < ticker.Length; i++)
            {
                if (exDate[i]< quoteDate)
                {
                    throw new ArgumentException(string.Format("Invalid ex date found at index {0} : {1} is lower than the asof {2}", i, exDate[i], quoteDate));
                }

                var t = basketSet.GetBasket(ticker[i]);
                var ccy = new Currency(currency[i]);
                var div = DividendCoarse.New(quoteDate, bid[i], ask[i]
                    , mid[i], exDate[i], ccy, t);
                var allIn = AllInCoarse.New(quoteDate, bidAllIn[i], askAllIn[i]
                    , midAllIn[i], exDate[i], t);

                data.Add(div);
                data.Add(allIn);
            }

            return new DataQuoteSheet(quoteDate, data);
        }

        [WorksheetFunction(XllName + ".RepoSheet")]
        public static DataQuoteSheet RepoSheet(DateTime quoteDate
            , Ticker ticker
            , string[] type
            , DateTime[] maturity
            , double[] bid
            , double[] ask
            , double[] mid
            , CompoundingRateType compounding
            , IDayCountFraction dayCountConv
            )
        {
            Require.ArgumentNotNull(ticker, "ticker");
            Require.ArgumentNotNull(type, "type");
            Require.ArgumentNotNull(maturity, "maturity");
            Require.ArgumentNotNull(bid, "bid");
            Require.ArgumentNotNull(ask, "ask");
            Require.ArgumentNotNull(mid, "mid");
            Require.ArgumentNotNull(dayCountConv, "dayCountConv");

            Require.ArgumentNotEmpty(type, "type");
            Require.ArgumentEqualArrayLength(type, maturity, "type", "maturity");
            Require.ArgumentEqualArrayLength(type, bid, "type", "bid");
            Require.ArgumentEqualArrayLength(type, ask, "type", "ask");
            Require.ArgumentEqualArrayLength(type, mid, "type", "mid");

            var data = new List<IInstrument>();
            
            for (int i = 0; i < type.Length; i++)
            {
                var repo = RepoRate.New(quoteDate, bid[i], ask[i]
                    , mid[i], ticker, maturity[i], compounding
                    , dayCountConv);

                data.Add(repo);
            }

            return new DataQuoteSheet(quoteDate, data);
        }


        [WorksheetFunction(XllName + ".ForexSheet")]
        public static DataQuoteSheet ForexSheet(DateTime quoteDate
            , string[] from
            , string[] to
            , double[] bid
            , double[] ask
            , double[] mid            
            )
        {
            // TODO CHECKS
            var data = new List<IInstrument>();
            for (int i = 0; i < from.Length; i++)
            {
                var ccyPair = new CurrencyPair(from[i], to[i]);
                var q = CurrencyPairSecurity.New(quoteDate, bid[i], ask[i], mid[i], ccyPair);
                data.Add(q);
            }
            return new DataQuoteSheet(quoteDate, data);

        }

    }
}
