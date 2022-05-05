using System;
using AldrinAnalytics.Instruments;
using Zeliade.Finance.Common.RateCurves;

namespace AldrinAnalytics.Pricers
{
    public interface IForwardForexCurve
    {
        DateTime CurveDate { get; }
        CurrencyPair Pair { get; }
        double Spot { get; }
        double Forward(DateTime maturity);

        double Forward(DateTime start, DateTime end);

    }


    public class FxCurveOne : IForwardForexCurve
    {
        public FxCurveOne(DateTime curveDate, CurrencyPair pair)
        {
            CurveDate = curveDate;
            Pair = pair ?? throw new ArgumentNullException(nameof(pair));
        }

        public DateTime CurveDate { get; private set; }

        public CurrencyPair Pair { get; private set; }

        public double Spot { get { return 1d; } }

        public double Forward(DateTime maturity)
        {
            return 1d;
        }

        public double Forward(DateTime start, DateTime end)
        {
            return 1d;
        }
    }

    public class FxForwardCurve : IForwardForexCurve
    {
        private readonly IDiscountCurve<DateTime> _domCurve;
        private readonly IDiscountCurve<DateTime> _foreignCurve;

        public DateTime CurveDate { get; private set; }

        public CurrencyPair Pair { get; private set; }

        public double Spot { get; private set; }

        public FxForwardCurve(IDiscountCurve<DateTime> domCurve
            , IDiscountCurve<DateTime> foreignCurve
            , CurrencyPair pair
            , double spot)
        {
            _domCurve = domCurve ?? throw new ArgumentNullException(nameof(domCurve));
            _foreignCurve = foreignCurve ?? throw new ArgumentNullException(nameof(foreignCurve));
            CurveDate = domCurve.CurveDate;
            Pair = pair ?? throw new ArgumentNullException(nameof(pair));
            Spot = spot;
        }

        public double Forward(DateTime maturity)
        {
            var zcDom = _domCurve.ZcPrice(maturity);
            var zcFor = _foreignCurve.ZcPrice(maturity);
            return Spot * zcDom / zcFor;
        }

        public double Forward(DateTime start, DateTime end)
        {
            var zcDom = _domCurve.ForwardZcPrice(start, end);
            var zcFor = _foreignCurve.ForwardZcPrice(start, end);
            return Spot * zcDom / zcFor;        
        }
    }

}
