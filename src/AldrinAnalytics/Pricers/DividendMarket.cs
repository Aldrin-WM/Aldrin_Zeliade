using System;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using Zeliade.Finance.Common.Calibration;
using System.Collections.Generic;
using Zeliade.Finance.Common.Product;
using System.Linq;
using Zeliade.Common;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Pricers
{
    public interface IDividendMarket : IGenericMarket<Ticker, IDividendCurve>
    {
        List<Currency> GetPaymentCurrencies(Ticker u);
    }
    
    public class DividendMarket : GenericMarket<Ticker, IDividendCurve>, IDividendMarket
    {
        private const string XllName = "DividendMarket";

        private readonly Dictionary<Ticker, List<Currency>> _divPaymentCcy;

        [WorksheetFunction(XllName + ".New")]
        public DividendMarket(DateTime marketDate) : base(marketDate)
        {
            _divPaymentCcy = new Dictionary<Ticker, List<Currency>>();
        }

        [WorksheetFunction(XllName + ".Copy")]
        public DividendMarket(DividendMarket other) : base(other.MarketDate)
        {            
            _divPaymentCcy = new Dictionary<Ticker, List<Currency>>();
            foreach (var item in other._sheets)
            {
                AddSheet(item.Key, item.Value, other._bootstrappers[item.Key]);
            }                        
        }

        public List<Currency> GetPaymentCurrencies(Ticker u)
        {
            Require.Argument(_divPaymentCcy.ContainsKey(u), "u", Error.Msg("The ticker {0} is not registered in the market !", u));
            return _divPaymentCcy[u].ToList();
        }

        protected override void InternalAddSheet(Ticker underlying, DataQuoteSheet sheet)
        {
            Require.Argument(!_divPaymentCcy.ContainsKey(underlying), "underlying", Error.Msg("The ticker {0} is already registered in the market !", underlying));
            var l0 = sheet.Data.Where(x => x is DividendEstimate)
                .Select(x => (x as DividendEstimate).Ccy).ToList();
            var l1 = sheet.Data.Where(x => x is DividendCoarse)
                .Select(x => (x as DividendCoarse).Ccy).ToList();
            var l = new List<Currency>();
            l.AddRange(l0);
            l.AddRange(l1);
            l = l.Distinct().ToList();
            _divPaymentCcy.Add(underlying, l);
        }
    }
}
