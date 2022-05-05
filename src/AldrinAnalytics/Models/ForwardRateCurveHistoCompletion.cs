using System;
using AldrinAnalytics.Instruments;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Mrc;

namespace AldrinAnalytics.Models
{
    public abstract class ForwardRateCurveBase : IForwardRateCurve
    {
        protected readonly IForwardRateCurve _baseCurve;

        protected ForwardRateCurveBase(IForwardRateCurve baseCurve)
        {
            _baseCurve = baseCurve ?? throw new ArgumentNullException(nameof(baseCurve));
        }

        public IRateConventionData CurveConventionData { get { return _baseCurve.CurveConventionData; } }

        public DateTime CurveDate { get { return _baseCurve.CurveDate; } }

        public string Currency { get { return _baseCurve.Currency; } }

        public DataQuoteSheet BaseSheet { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        public object Clone()
        {
            throw new NotImplementedException();
        }

        public IRateCurve DeepCopy()
        {
            throw new NotImplementedException();
        }

        public abstract double Forward(DateTime fixingDate, IPeriod tenor);

        public IFixedConventionForwardCurve SingleTenorCurve(IPeriod tenor)
        {
            throw new NotImplementedException();
        }
    }

    public class ForwardRateCurveHistoCompletion : ForwardRateCurveBase
    {
        private readonly LiborReference _libor;
        private readonly OisReference _ois;
        private readonly IJointModelSingleQuoteType _histoModel;
        private readonly bool _isLibor;

        public ForwardRateCurveHistoCompletion(IForwardRateCurve baseCurve
            , RateReference refTicker
            , IJointModelSingleQuoteType histoModel)
            : base(baseCurve)
        {
            var ticker = refTicker ?? throw new ArgumentNullException(nameof(refTicker));
            _libor = ticker as LiborReference;
            _ois = ticker as OisReference;
            Require.Argument(_libor != null || _ois != null, nameof(refTicker), Error.Msg("The input rate reference should be of type {0} or {1} but is of type {2}", typeof(LiborReference), typeof(OisReference), ticker.GetType()));
            _isLibor = _libor != null;
            _histoModel = histoModel ?? throw new ArgumentNullException(nameof(histoModel));
        }

        public override double Forward(DateTime fixingDate, IPeriod tenor)
        {
            if (fixingDate >= CurveDate)
                return _baseCurve.Forward(fixingDate, tenor);
            else
            {
                if (_isLibor)
                {
                    _histoModel.CurrentDate = fixingDate;
                    return _histoModel.LiborFixing(_libor);
                }
                else if (_ois.Tenor.DayDuration==1d)
                {
                    _histoModel.CurrentDate = fixingDate;
                    return _histoModel.OisFixing(_ois);
                }
                else // rate is obtained by compounding overnight fixings
                {
                    var histoDates = _histoModel.AvailableOisDates(fixingDate, CurveDate);
                    var oneDay = _ois.DayCount.Count(fixingDate, fixingDate.AddDays(1));
                    double comp = 1d;
                    double duration = 0d;
                    foreach (var d in histoDates)
                    {
                        _histoModel.CurrentDate = d;
                        var fix = _histoModel.OisFixing(_ois);
                        comp *= 1 + oneDay * fix;
                        duration += oneDay;
                    }

                    var endDate = tenor.Next(fixingDate, 1);
                    if (endDate > CurveDate)
                    {
                        var remainingPeriod = Periods.Get(string.Format("{0}D", (int)(endDate - CurveDate).TotalDays));
                        var fwd = _baseCurve.Forward(CurveDate, remainingPeriod);
                        var remainingYearFraction = _ois.DayCount.Count(CurveDate, endDate);
                        comp *= 1 + fwd * remainingYearFraction;
                        duration += remainingYearFraction;
                    }

                    var output = (comp - 1d) / duration;
                    return output;
                }

            }
        }

    }



}
