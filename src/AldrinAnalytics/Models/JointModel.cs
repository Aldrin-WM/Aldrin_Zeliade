using System;
using System.Collections.Generic;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Model;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Mrc;
using Zeliade.Math.LinAlg.ArrayOperations;

namespace AldrinAnalytics.Models
{
    public interface IEquityModelSingleQuoteType
    {
        double[] StockValues();
        SecurityBasket Underlying { get; }
    }

    public interface IFxModelSingleQuoteType
    {
        double FxValue(Tuple<string, string> ccyPair);

    }

    public interface IJointModelSingleQuoteType : IProcess<double[]>, IEquityModelSingleQuoteType, IFxModelSingleQuoteType
    {
        double OisFixing(OisReference reference);
        double LiborFixing(LiborReference reference);
        List<DividendData> Dividends(DateTime since);

        List<DateTime> AvailableOisDates(DateTime from, DateTime to);
    }

    public interface IJointModel : IProcess<double[]>
    {
        double FxValue(Tuple<string, string> ccyPair, Type quoteType);
        double OisFixing(OisReference reference, Type quoteType);
        double LiborFixing(LiborReference reference, Type quoteType);
        List<DividendData> Dividends(DateTime since, Type quoteType);
        double[] StockValues(Type quoteType);
        SecurityBasket Underlying { get; }
    }

    

    public class JointModel : BlockStorage<double[]>, IJointModel
    {
        private readonly Dictionary<Type, Dictionary<RateReference, IForwardRateCurve>> _fwdStatic;
        private readonly Dictionary<Type, double[]> _spreads;
        private readonly Dictionary<Type, IDividendCurve> _divcCurve;
        private readonly Dictionary<Tuple<string, string>, Dictionary<DateTime, double>> _cache;

        public Dictionary<Tuple<string, string>, IForwardForexCurve> FXStatic { get; private set; }
        public SecurityBasket Underlying { get; private set; }

        public JointModel(DateTime referenceDate
            , SecurityBasket basket
            , IProcessGenerator<double[]> generator // mid generator
            , Dictionary<Tuple<string, string>, IForwardForexCurve> fxStatic // mid fx
            , Dictionary<Type, Dictionary<RateReference, IForwardRateCurve>> fwdStatic
            , Dictionary<Type, IDividendCurve> divCurve
            , double[] bidAskpread // bid-ask
            )
            : base(1, referenceDate, generator, ReferenceTimeDayCount.Value)
        {
            Underlying = Require.ArgumentNotNull(basket, "basket");
            FXStatic = Require.ArgumentNotNull(fxStatic, nameof(fxStatic));
            _fwdStatic = Require.ArgumentNotNull(fwdStatic, nameof(fwdStatic));
            _divcCurve = Require.ArgumentNotNull(divCurve, nameof(divCurve));
            _cache = new Dictionary<Tuple<string, string>, Dictionary<DateTime, double>>();
            Require.ArgumentNotNull(bidAskpread, nameof(bidAskpread));
            Require.ArgumentArrayLength(basket.Content.Count, bidAskpread, nameof(bidAskpread));


            var zeroSpread = new double[basket.Content.Count];
            var bidSpread = new double[basket.Content.Count];
            var askSpread = new double[basket.Content.Count];

            for (int i = 0; i < bidSpread.Length; i++)
            {
                zeroSpread[i] = 0d;
                bidSpread[i] = - 0.5 * bidAskpread[i];
                askSpread[i] = 0.5 * bidAskpread[i];
            }

            _spreads = new Dictionary<Type, double[]>()
            {
                {typeof(MidQuote), zeroSpread },
                {typeof(BidQuote), bidSpread },
                {typeof(AskQuote), askSpread }
            };

        }

        public double FxValue(Tuple<string, string> ccyPair, Type quoteType)
        {
            //return _fxStatic[ccyPair].Forward(CurrentDate); // 70s

            //return 1.0; //29s

            // Avec cache : 50s 
            Dictionary<DateTime, double> dico = null;
            if (!_cache.TryGetValue(ccyPair, out dico))
            {
                dico = new Dictionary<DateTime, double>();
                _cache.Add(ccyPair, dico);
            }

            double fx;
            if (!dico.TryGetValue(CurrentDate, out fx))
            {
                fx = FXStatic[ccyPair].Forward(CurrentDate);
                dico.Add(CurrentDate, fx);
            }

            return fx;
        }

        public double OisFixing(OisReference reference, Type quoteType)
        {
            return _fwdStatic[quoteType][reference].Forward(CurrentDate, reference.Tenor);
        }

        public double LiborFixing(LiborReference reference, Type quoteType)
        {
            return _fwdStatic[quoteType][reference].Forward(CurrentDate, reference.Tenor);
        }
        public List<DividendData> Dividends(DateTime since, Type quoteType)
        {
            return _divcCurve[quoteType].AllInDividends(since, CurrentDate);
        }

        public double[] StockValues(Type quoteType)
        {
            return Value.Plus(_spreads[quoteType]);
        }

    }
}
