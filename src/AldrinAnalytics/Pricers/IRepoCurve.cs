using System;
using System.Linq;
using System.Collections.Generic;
using AldrinAnalytics.Instruments;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Mrc;
using Zeliade.Math.Interpolation;

namespace AldrinAnalytics.Pricers
{
    public interface IRepoCurve : IDiscountCurve<DateTime>
    {
        Ticker Underlying { get; }
    }

    public class RepoCurve : IRepoCurve
    {
        private readonly IDayCountFraction _dcf;
        private Linear _linearCurve;
        private readonly List<DateTime> _pillars;
        private readonly List<double> _rates;

        public Ticker Underlying { get; private set; }

        public DateTime CurveDate { get; private set; }

        public string Currency { get { return Underlying.ReferenceCurrency.Code; } }

        public DataQuoteSheet BaseSheet { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:AldrinAnalytics.Pricers.RepoCurve"/> class.
        /// </summary>
        /// <param name="ticker">Ticker.</param>
        /// <param name="curveDate">Curve date.</param>
        /// <param name="pillars">Pillars.</param>
        /// <param name="rates">Rates. Assumed to be in annual compounding</param>
        /// <param name="dayCountConvention">DayCountConvention. Day count convention for the provided rates</param>
        public RepoCurve(Ticker ticker
            , DateTime curveDate
            , IList<DateTime> pillars
            , IList<double> rates
            , IDayCountFraction dayCountConvention)
        {
            Underlying = Require.ArgumentNotNull(ticker, "ticker");
            _dcf = Require.ArgumentNotNull(dayCountConvention, "dayCountConvention");
            CurveDate = curveDate;
            Require.ArgumentListNotNullOrEmpty(pillars, "pillars");
            Require.ArgumentListNotNullOrEmpty(rates, "rates");
            Require.ArgumentListCount(pillars.Count, rates, "rates");
            Require.ArgumentRange(ValueRange.GreaterOrEqual(curveDate), pillars.First(), "pillars");

            var pillarsTtm = pillars.Select(p => ReferenceTimeDayCount.Count(curveDate, p)).ToArray();
            _linearCurve = new Linear(pillarsTtm, rates, ExtrapolationType.Flat);

            _pillars = pillars.ToList();
            _rates = rates.ToList();

        }

        public object Clone()
        {
            return DeepCopy();
        }

        public IRateCurve DeepCopy()
        {
            return new RepoCurve(Underlying, CurveDate, _pillars, _rates, _dcf);
        }

        public double ForwardZcPrice(DateTime startDate, DateTime endDate)
        {
            return ZcPrice(endDate) / ZcPrice(startDate);
        }

        public double ZcPrice(DateTime maturity)
        {
            var ttm = ReferenceTimeDayCount.Count(CurveDate, maturity);
            var rate = _linearCurve.Value(ttm);
            var period = _dcf.Count(CurveDate, maturity);
            return System.Math.Exp(-rate*period);                     
        }
    }
}
