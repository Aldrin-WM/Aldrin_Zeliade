using System;
using System.Linq;
using System.Collections.Generic;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Calibration.RateCurves.Instruments;
using AldrinAnalytics.Excel;
using Zeliade.Finance.Common.Model;
using Zeliade.Finance.Mrc;
using Zeliade.Finance.Common.RateCurves;

#if MXLL
using ManagedXLL;
#else
using Zeliade.Common.ManagedXLLTools.FakeImpl;
#endif

namespace AldrinAnalytics.Models
{
    public class JointHistoricalFixings
    {

        private const string XllName = "JointHistoricalFixings";

        private readonly Dictionary<DateTime, Dictionary<OisReference, OvernightIndexSwapQuote>> _ois; 
        private readonly Dictionary<DateTime, Dictionary<LiborReference, DepositQuote>> _libor;
        private readonly Dictionary<DateTime, Dictionary<Type, Dictionary<SecurityBasket, double[]>>> _equity;
        private readonly Dictionary<DateTime, Dictionary<Tuple<string, string>, CurrencyPairSecurity>> _fx;
        private readonly DividendMarket _divCurve;
        private readonly HistoricalFixings _equityHisto;
        private readonly Dictionary<DateTime, Dictionary<SingleNameTicker, SingleNameSecurity>> _equityAccess;

        [WorksheetFunction(XllName+ ".JointHistoricalFixings")]
        public JointHistoricalFixings(HistoricalFixings equityHisto
            , HistoricalFixings oisHisto
            , HistoricalFixings liborHisto
            , HistoricalFixings fxHisto
            , DividendMarket divCurve
            , IEnumerable<SecurityBasket> baskets
            )
        {
            _divCurve = divCurve ?? throw new ArgumentNullException(nameof(divCurve));

            // Ois
            _ois = new Dictionary<DateTime, Dictionary<OisReference, OvernightIndexSwapQuote>>();
            var oisDates = oisHisto.AvailableDates;
            foreach (var d in oisDates)
            {
                var currentSheet = oisHisto.DailySheet(d);
                var dico = new Dictionary<OisReference, OvernightIndexSwapQuote>();
                _ois.Add(d, dico);
                foreach (OvernightIndexSwapQuote inst in currentSheet)
                {
                    var oisRef = new OisReference(inst.Currency, inst.FloatingDayCountConvention);
                    dico.Add(oisRef, inst);
                }
            }

            // Libor
            _libor = new Dictionary<DateTime, Dictionary<LiborReference, DepositQuote>>();
            var liborDates = liborHisto.AvailableDates;
            foreach (var d in liborDates)
            {
                var currentSheet = liborHisto.DailySheet(d);
                var dico = new Dictionary<LiborReference, DepositQuote>();
                _libor.Add(d, dico);
                foreach (DepositQuote inst in currentSheet)
                {
                    var liborRef = new LiborReference(inst.Currency, inst.Tenor, inst.DayCountConvention);
                    dico.Add(liborRef, inst);
                }
            }

            // Fx
            _fx = new Dictionary<DateTime, Dictionary<Tuple<string, string>, CurrencyPairSecurity>>();
            var fxDates = fxHisto.AvailableDates;
            foreach (var d in fxDates)
            {
                var currentSheet = fxHisto.DailySheet(d);
                var dico = new Dictionary<Tuple<string, string>, CurrencyPairSecurity>();
                _fx.Add(d, dico);
                foreach (CurrencyPairSecurity inst in currentSheet)
                {
                    var fxRef = Tuple.Create(inst.CcyPair.ForeignCurrency, inst.CcyPair.DomesticCurrency);
                    dico.Add(fxRef, inst);
                }
            }

            // Equity
            _equity = new Dictionary<DateTime, Dictionary<Type, Dictionary<SecurityBasket, double[]>>>();
            _equityHisto = Require.ArgumentNotNull(equityHisto, nameof(equityHisto));
            _equityAccess = new Dictionary<DateTime, Dictionary<SingleNameTicker, SingleNameSecurity>>();
            var equityDates = equityHisto.AvailableDates;
            foreach (var d in equityDates)
            {
                var currentSheet = equityHisto.DailySheet(d);
                var dico = new Dictionary<SingleNameTicker, SingleNameSecurity>();
                _equityAccess.Add(d, dico);
                foreach (SingleNameSecurity inst in currentSheet)
                {                    
                    dico.Add(inst.SingleName, inst);
                }
            }

            foreach (var b in baskets)
            {
                AddBasket(b);
            }
        }

        public void AddBasket(SecurityBasket basket)
        {
            var dates = _equityHisto.AvailableDates;
            foreach (var d in dates)
            {
                Dictionary<Type, Dictionary<SecurityBasket, double[]>> dico = null;
                if (!_equity.TryGetValue(d, out dico))
                {
                    dico = new Dictionary<Type, Dictionary<SecurityBasket, double[]>>();
                    _equity.Add(d, dico);
                }

                AddVector<MidQuote>(basket, d, dico);
                AddVector<BidQuote>(basket, d, dico);
                AddVector<AskQuote>(basket, d, dico);

            }
        }

        private void AddVector<T>(SecurityBasket basket, DateTime d, Dictionary<Type, Dictionary<SecurityBasket, double[]>> dico) where T:IDataQuote
        {
            Dictionary<SecurityBasket, double[]> tmp = null;
            Type targetType = typeof(T);
            if (!dico.TryGetValue(targetType, out tmp))
            {
                tmp = new Dictionary<SecurityBasket, double[]>();
                dico.Add(targetType, tmp);
            }

            var vector = basket.Content.Select(x => _equityAccess[d][x].GetQuote<T>().Value).ToArray();
            tmp.Add(basket, vector);
        }

        public List<DividendData> Dividends(DateTime currentDate, SecurityBasket underlying, DateTime since, Type quoteType)
        {
            return _divCurve.Get(underlying, null, quoteType).AllInDividends(since, currentDate);
        }

        public double FxValue(DateTime currentDate, Tuple<string, string> ccyPair, Type quoteType)
        {
            return _fx[currentDate][ccyPair].Quotes.Where(x => x.GetType() == quoteType).First().Value;
        }

        public double LiborFixing(DateTime currentDate, LiborReference reference, Type quoteType)
        {
            var quotes = _libor[currentDate][reference].Quotes;
            return quotes.Where(x => x.GetType() == quoteType).First().Value;
        }

        public double OisFixing(DateTime currentDate, OisReference reference, Type quoteType)
        {
            var quotes = _ois[currentDate][reference].Quotes;
            return quotes.Where(x => x.GetType() == quoteType).First().Value;
        }

        public double[] StockValues(DateTime currentDate, SecurityBasket underlying, Type quoteType)
        {
            return _equity[currentDate][quoteType][underlying];
        }

        public List<DateTime> AvailableOisDates(DateTime from, DateTime to)
        {
            return _ois.Keys.Where(d => d >= from && d < to).ToList();
        }
    }

    public class HistoricalJointModelSingleQuoteType : IJointModelSingleQuoteType
    {
        private readonly JointHistoricalFixings _fixings;
        private readonly Type _equityType;
        private readonly Type _divType;
        private readonly Type _oisType;
        private readonly Type _liborType;
        private readonly Type _fxType;

        public HistoricalJointModelSingleQuoteType(JointHistoricalFixings fixings, Type equityType, Type divType, Type oisType, Type liborType, Type fxType)
        {
            _fixings = fixings;// ?? throw new ArgumentNullException(nameof(fixings));
            _equityType = equityType ?? throw new ArgumentNullException(nameof(equityType));
            _divType = divType ?? throw new ArgumentNullException(nameof(divType));
            _oisType = oisType ?? throw new ArgumentNullException(nameof(oisType));
            _liborType = liborType ?? throw new ArgumentNullException(nameof(liborType));
            _fxType = fxType ?? throw new ArgumentNullException(nameof(fxType));
        }

        public SecurityBasket Underlying { get; set; }

        public double[] Value { get { throw new NotImplementedException(); } }

        public DateTime CurrentDate {get; set;}

        public int CurrentPath {get; set;}

        public int PathNumber {get; set;}

        public List<DateTime> AvailableOisDates(DateTime from, DateTime to)
        {
            return _fixings.AvailableOisDates(from, to);
        }

        public List<DividendData> Dividends(DateTime since)
        {
            return _fixings.Dividends(CurrentDate, Underlying, since, _equityType);
        }

        public void EnableObservationDates(IList<DateTime> targets)
        {
        }

        public double FxValue(Tuple<string, string> ccyPair)
        {
            return _fixings.FxValue(CurrentDate, ccyPair, _fxType);
        }

        public double LiborFixing(LiborReference reference)
        {
            return _fixings.LiborFixing(CurrentDate, reference, _liborType);
        }

        public double OisFixing(OisReference reference)
        {
            return _fixings.OisFixing(CurrentDate, reference, _oisType);
        }

        public void Reset()
        {
        }

        public double[] StockValues()
        {
            return _fixings.StockValues(CurrentDate, Underlying, _equityType);
        }

        public double[] ValueAt(DateTime d, int pathIndex)
        {
            throw new NotImplementedException();
        }
    }

    // Pour MC
    public class HistoricalJointModel : IJointModel
    {
        private readonly JointHistoricalFixings _fixings;
        private readonly DateTime _asof;
        private readonly Dictionary<Type, Dictionary<RateReference, IForwardRateCurve>> _asofOisCurve;

        public HistoricalJointModel(JointHistoricalFixings fixings, DateTime asof
            , Dictionary<Type, Dictionary<RateReference, IForwardRateCurve>> asofOisCurve)
        {
            _fixings = fixings ?? throw new ArgumentNullException(nameof(fixings));
            _asof = asof;
            _asofOisCurve = asofOisCurve ?? throw new ArgumentNullException(nameof(asofOisCurve));
        }

        public SecurityBasket Underlying { get; set; }

        public double[] Value { get { throw new NotImplementedException(); } }

        public DateTime CurrentDate {get; set;}
        public int CurrentPath {get; set;}
        public int PathNumber {get; set;}

        public List<DividendData> Dividends(DateTime since, Type quoteType)
        {
            return _fixings.Dividends(CurrentDate, Underlying, since, quoteType);
        }

        public void EnableObservationDates(IList<DateTime> targets)
        {
        }

        public double FxValue(Tuple<string, string> ccyPair, Type quoteType)
        {
            return _fixings.FxValue(CurrentDate, ccyPair, quoteType);
        }

        public double LiborFixing(LiborReference reference, Type quoteType)
        {
            return _fixings.LiborFixing(CurrentDate, reference, quoteType);
        }

        public double OisFixing(OisReference reference, Type quoteType)
        {
            if (reference.Tenor.DayDuration == 1d)
            {
                return _fixings.OisFixing(CurrentDate, reference, quoteType);
            }
            else // rate is obtained by compounding overnight fixings
            {
                var histoDates = _fixings.AvailableOisDates(CurrentDate, _asof);
                var oneDay = reference.DayCount.Count(CurrentDate, CurrentDate.AddDays(1));
                double comp = 1d;
                double duration = 0d;
                foreach (var d in histoDates)
                {
                    var fix = _fixings.OisFixing(CurrentDate, reference, quoteType);
                    comp *= 1 + oneDay * fix;
                    duration += oneDay;
                }

                var endDate = reference.Tenor.Next(CurrentDate, 1);
                if (endDate > _asof)
                {
                    var remainingPeriod = Periods.Get(string.Format("{0}D", (int)(endDate - CurrentDate).TotalDays));
                    var curve = _asofOisCurve[quoteType][reference];
                    var fwd = curve.Forward(_asof, remainingPeriod);
                    var remainingYearFraction = reference.DayCount.Count(_asof, endDate);
                    comp *= 1 + fwd * remainingYearFraction;
                    duration += remainingYearFraction;
                }

                var output = (comp - 1d) / duration;
                return output;
            }
            
        }

        public void Reset()
        {
        }

        public double[] StockValues(Type quoteType)
        {
            return _fixings.StockValues(CurrentDate, Underlying, quoteType);
        }

        public double[] ValueAt(DateTime d, int pathIndex)
        {
            throw new NotImplementedException();
        }
    }
}

