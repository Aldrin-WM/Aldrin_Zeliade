using System;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using Zeliade.Common;
using Zeliade.Finance.Common.Model;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Mrc;
using Zeliade.Math.LinAlg.ArrayOperations;
using Zeliade.Math.Random;
using Zeliade.Math.Random.Distributions;
using Zeliade.Math.Random.Generator;

namespace AldrinAnalytics.Models
{
    public class BlackScholesGenerator : ProcessGenerator<double[]>
    {
        private readonly SecurityBasket _basket;
        private readonly IDividendCurve _divs;
        private readonly IRepoCurve _repo;
        private readonly Dictionary<Currency, IDiscountCurve<DateTime>> _disc;
        private readonly Dictionary<Tuple<string, string>, IForwardForexCurve> _fxCurve;
        private readonly DateTime _refDate;
        private readonly double[] _vars;
        private readonly double[,] _sqrtCovMatrix;
        private readonly Gauss _gauss;
        private readonly int _factorNumber;
        private readonly int _basketSize;
        private readonly Dictionary<int, double> _exDivs;
        private readonly Dictionary<Ticker, int> _tickerToIndex;
        private readonly List<SingleNameTicker> _tickers;
        private readonly List<DateTime> _divDates;
        private readonly List<double> _divTtms;
        private Dictionary<Currency, double> _ratio;
        private double[] _volDrift;
        private DateTime _currentRoughExDate;
        private int _maturityIndex;

        public System.Random Generator
        {
            get { return _gauss.Generator; }
            set { _gauss.Generator = value; }
        }

        public BlackScholesGenerator(int pathNumber
            , DateTime refDate
            , ISimulationDateFactory simDatesFactory
            , IStateInitializer<double[]> initializer
            , SecurityBasket basket
            , IDividendCurve divs
            , IRepoCurve repo
            , Dictionary<Currency, IDiscountCurve<DateTime>> disc
            , Dictionary<Tuple<string, string>, IForwardForexCurve> fxCurve
            , double[,] cov
            , int factorNumber)
            : base(pathNumber, 0d, simDatesFactory, initializer)
        {
            _basket = basket ?? throw new ArgumentNullException(nameof(basket));
            _divs = divs ?? throw new ArgumentNullException(nameof(divs));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _disc = disc ?? throw new ArgumentNullException(nameof(disc));
            _fxCurve = fxCurve ?? throw new ArgumentNullException(nameof(fxCurve));
            _refDate = refDate;
            Require.Argument(factorNumber <= cov.GetLength(0), nameof(factorNumber), Error.Msg("The factor number {0} should be lower than or equal to the cov size (1}", factorNumber, cov.GetLength(0)));

            _basketSize = cov.GetLength(0);
            _vars = new double[_basketSize];
            for (int i = 0; i < _basketSize; i++)
            { 
                _vars[i] = cov[i, i];
            }

            _factorNumber = Math.Min(factorNumber, _basketSize);
            _tickers = basket.Content.ToList();

            bool ok = Zeliade.Math.LinAlg.PrincipalComponentAnalysis.ComputeMatrixSqrt(cov, factorNumber, out _sqrtCovMatrix);

            // Preserve the variance
            for (int i = 0; i < _sqrtCovMatrix.GetLength(0); i++)
            {
                double x = _sqrtCovMatrix.Get(i).NormL2();
                if (x > 0.0)
                {
                    _sqrtCovMatrix.DivideAssign(i, x / Math.Sqrt(_vars[i]));
                }
            }

            _gauss = new Gauss();
            _gauss.Generator = new MT19937();

            var divData = divs.AllInDividends(refDate, refDate.AddYears(100));
            var exDates = divData.Select(x => x.ExDate).ToList();
            var payDates = divData.Select(x => x.PaymentDate).ToList();
            exDates.AddRange(payDates);
            exDates = exDates.Distinct().ToList();
            exDates.Sort();
            _divDates = exDates;
            _divTtms = exDates.Select(d => ReferenceTimeDayCount.Count(refDate, d)).ToList();

            _maturityIndex = 0;
            _exDivs = new Dictionary<int, double>();
            _tickers = basket.Content.ToList();
            _tickerToIndex = new Dictionary<Ticker, int>();
            for (int i = 0; i < _tickers.Count; i++)
            {
                _tickerToIndex.Add(_tickers[i], i);
            }
        }

        protected override void InternalReset()
        {
            _maturityIndex = 0;
        }

        protected override void PreStep()
        {
            var currentBegin = ReferenceTimeDayCount.RetrieveDate(_refDate, BeginStepDate);
            var currentEnd = ReferenceTimeDayCount.RetrieveDate(_refDate, EndStepDate);

            var repoZcBegin = _repo.ZcPrice(currentBegin);
            var repoZcEnd = _repo.ZcPrice(currentEnd);

            var discZcBegin = _disc.ToDictionary(x=>x.Key, x=>x.Value.ZcPrice(currentBegin));
            var discZcEnd = _disc.ToDictionary(x => x.Key, x => x.Value.ZcPrice(currentEnd));

            _ratio = _disc.ToDictionary(x => x.Key, x => (discZcBegin[x.Key]/ discZcEnd[x.Key] * (repoZcEnd / repoZcBegin)));

            //_ratio = (discZcBegin / discZcEnd) * (repoZcEnd / repoZcBegin);
            _volDrift = _vars.Multiply(-0.5 * EffectiveStep);

            // Dividends such that currentBegin< Ex Date <= currentEnd
            var divs = _divs.AllInDividendsByEx(currentBegin, currentEnd);

            _exDivs.Clear();

            foreach (var item in divs)
            {
                var ccyPair = Tuple.Create(item.PaymentCurrency.Code, item.Ticker.Currency.Code);
                var fx = _fxCurve[ccyPair].Forward(item.ExDate);
                var amount = item.GrossAmount * item.AllIn * fx;
                if (item.Ticker is SingleNameTicker)
                {
                    var idx = _tickerToIndex[item.Ticker];
                    if (_exDivs.ContainsKey(idx))
                    {
                        _exDivs[idx] += amount;
                    }
                    else
                    {
                        _exDivs.Add(idx, amount);
                    }
                }
                else
                {
                    _currentRoughExDate = item.ExDate;
                    _exDivs.Add(-1, amount); // dans la ccy du panier
                }
            }
        }

        protected override void Step()
        {
            double[] noise = new double[_factorNumber];
            for (int i = 0; i < _factorNumber; i++)
            {
                noise[i] = _gauss.Next() * System.Math.Sqrt(EffectiveStep);
            }

            var inc = _sqrtCovMatrix.Prod(noise);

            for (int i = 0; i < _basketSize; i++)
            {
                NextBufferState[i] = CurrentBufferState[i] * _ratio[_tickers[i].Currency] * System.Math.Exp(_volDrift[i] + inc[i]);
            }

            // Dividends
            if (_exDivs.Count > 0)
            {
                foreach (var item in _exDivs)
                {
                    if (item.Key == -1)
                    {
                        var bv = BasketValue(NextBufferState);
                        var alpha = item.Value / bv;
                        for (int i = 0; i < _basketSize; i++)
                        {
                            var amount = NextBufferState[i] * alpha;
                            NextBufferState[i] -= amount;
                        }
                    }
                    else
                    {
                        NextBufferState[item.Key] -= item.Value;
                    }

                }

            }
        }

        private double BasketValue(double[] components)
        {
            var v = 0d;
            for (int i = 0; i < _basketSize; i++)
            {
                var ccyPair = Tuple.Create(_tickers[i].Currency.Code, _basket.Currency.Code);
                var fx = _fxCurve[ccyPair].Forward(_currentRoughExDate);
                v += _basket.GetComponent(_tickers[i]).Weight * components[i] * fx;
            }
            return v;
        }

        protected override double InternalNextSimulationDate(double target)
        {
            if (_maturityIndex < _divTtms.Count && _divTtms[_maturityIndex] <= target)
            {
                var output = _divTtms[_maturityIndex];
                ++_maturityIndex;
                return output;
            }
            else
            {
                return target;
            }

        }
    }


}
