using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using System;
using System.Linq;
using Zeliade.Common;
using Zeliade.Finance.Common.RateCurves;

namespace AldrinAnalytics.Pricers
{
    public interface IForwardCurve
    {
        DateTime CurveDate { get; }
        Ticker Underlying { get; }
        double Spot { get; }
        double Forward(DateTime d);
    }


    public class SingleAssetForwardCurve : IForwardCurve
    {
        private readonly SingleNameTicker _ticker;
        private readonly Security _security;
        private readonly IDiscountCurve<DateTime> _disc;
        private readonly IDividendCurve _divs;
        private readonly IRepoCurve _repo;

        public SingleAssetForwardCurve(
            Security security
            , IDiscountCurve<DateTime> disc
            , IDividendCurve divs
            , IRepoCurve repo)
        {   
            // TODO : verifier que security n'a qu'un seul quote

            _security = Require.ArgumentNotNull(security, "security");
            var snTicker = security.Underlying as SingleNameTicker;
            Require.Argument(snTicker!=null, "security.Underlying"
                , Error.Msg("The security underlying should be of type {0} but is of type {1}", typeof(SingleNameTicker), security.Underlying.GetType()));

            _ticker = snTicker;
            _disc = disc ?? throw new ArgumentNullException(nameof(disc));
            _divs = divs ?? throw new ArgumentNullException(nameof(divs));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        public double Spot
        {
            get
            {
                return _security.Quotes.First().Value;
            }
        }

        public DateTime CurveDate { get { return _disc.CurveDate; } }

        public Ticker Underlying { get { return _ticker; } }

        public double Forward(DateTime d)
        {

            var discountRepoMat = _repo.ZcPrice(d);
            var discountZcMat = _disc.ZcPrice(d);
            var output =  Spot * discountRepoMat / discountZcMat;

            var allIn = _divs.AllInDividends(CurveDate, d);

            foreach (var item in allIn)
            {
                var discountZc = _disc.ZcPrice(item.PaymentDate);

                var discountRepo = _repo.ZcPrice(item.PaymentDate);

                output -= (discountZc / discountRepo) / (discountZcMat / discountRepoMat) *  item.GrossAmount * item.AllIn;
            }

            return output;
        }
    }
}
