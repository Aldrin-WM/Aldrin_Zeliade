using System;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Product;

namespace AldrinAnalytics.Pricers
{

    public class DividendData
    {
        public DateTime ExDate { get; set; }
        public DateTime PaymentDate { get; set; }
        public double GrossAmount { get; set; }
        public double AllIn { get; set; }
        public Currency PaymentCurrency { get; set; }
        public Ticker Ticker { get; set; }
    }

    public interface IDividendCurve
    {
        Ticker Underlying { get; }

        DateTime MarketDate { get; }

        List<DividendData> AllInDividends(DateTime start, DateTime end);

        List<DividendData> AllInDividendsByEx(DateTime start, DateTime end);

        DataQuoteSheet BaseSheet { get; }

        List<CurrencyPair> PaymentCurrencies { get; }
    }

    public class DividendCurve : IDividendCurve
    {
        private readonly List<DividendData> _data;
        private readonly List<DateTime> _exDates;

        public Ticker Underlying { get; private set; }

        public DateTime MarketDate { get; private set; }

        public DataQuoteSheet BaseSheet { get; private set; }

        // Stock Ccy -> div ccy
        public List<CurrencyPair> PaymentCurrencies { get; private set; }

        public DividendCurve(DateTime marketDate, IList<DividendData> data)
        {
            _data = data.ToList();
            _data.Sort((x, y) =>
            {
                if (x.ExDate < y.ExDate)
                    return -1;
                else if (x.ExDate == y.ExDate)
                    return 0;
                else
                    return 1;
            });

            _exDates = _data.Select(x => x.ExDate).ToList();

            Underlying = _data.First().Ticker;

            MarketDate = marketDate;

            PaymentCurrencies = data.Select(x => new CurrencyPair( x.PaymentCurrency.Code, x.Ticker.Currency.Code)).Distinct().ToList();
        }

        public List<DividendData> AllInDividends(DateTime start, DateTime end)
        {
            //var lower = Algorithms.BinarySearch(_exDates, start);
            //var upper = Algorithms.LowerBound(_exDates, end);
            //if (lower >= 0)
            //    ++lower;
            //else
            //    lower = ~lower;

            //var output = new DividendData[upper-lower];
            //_data.CopyTo(lower, output, 0, output.Length);
            //return _data.ToList();

            return _data.Where(x => x.PaymentDate > start && x.PaymentDate <= end).ToList();
        }

        public List<DividendData> AllInDividendsByEx(DateTime start, DateTime end)
        {
            return _data.Where(x => x.ExDate > start && x.ExDate <= end).ToList();
        }
    }
}
