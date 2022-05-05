using System;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Model;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Mrc;
using Zeliade.Math.LinAlg.ArrayOperations;

namespace AldrinAnalytics.Models
{
    public interface IAffinePriceModel : IJointModel
    {
        //double ComputeTRSPrice(DateTime t, double[] S, double spread);
        //double ComputeTRSFaireSpread(DateTime t, double[] S);

        double ComputeAssetLeg(DateTime t, double[] S);
        double ComputeFloatRateLeg(DateTime t, double[] S);
        double ComputeDuration(DateTime t, double[] S);

    }
    public class AffinePriceModel : JointModel, IAffinePriceModel
    {
        private readonly AssetLegReset _assetLeg;
        private readonly AssetLegFloatRate _rateLeg;
        private readonly Dictionary<Currency, IDiscountCurve<DateTime>> _discFwd;
        private readonly IDiscountCurve<DateTime> _discTRS;
        private readonly IDividendCurve _divs;
        private readonly IRepoCurve _repo;
        private readonly IForwardRateCurve _fixingRateCurve;
        private readonly Dictionary<DateTime, double[]> _assetLegCoeffsA;
        private readonly Dictionary<DateTime, double> _assetLegCoeffsB;
        private readonly Dictionary<DateTime, double[]> _rateLegCoeffsC;
        private readonly Dictionary<DateTime, double> _rateLegCoeffsD;
        private readonly Dictionary<DateTime, double[]> _rateLegCoeffsG;
        private readonly Dictionary<DateTime, double> _rateLegCoeffsH;

        public AffinePriceModel(DateTime referenceDate
            , IProcessGenerator<double[]> generator
            , Dictionary<Tuple<string, string>, IForwardForexCurve> fxStatic // mid fx
            , Dictionary<Type, Dictionary<RateReference, IForwardRateCurve>> fwdStatic
            , Dictionary<Type, IDividendCurve> divCurve
            , SecurityBasket basket
            , double[] bidAskpread
            , Dictionary<Currency, IDiscountCurve<DateTime>> discFwd
            , IDiscountCurve<DateTime> discTRS 
            , IRepoCurve repo
            , AssetLegReset assetLeg
            , AssetLegFloatRate rateLeg)
            : base(referenceDate, basket, generator, fxStatic, fwdStatic, divCurve, bidAskpread)
        {
            _assetLeg = assetLeg;
            _rateLeg = rateLeg;
            _divs = divCurve[typeof(MidQuote)];
            _discFwd = discFwd;
            _discTRS = discTRS;
            _repo = repo;
            _fixingRateCurve = fwdStatic[typeof(MidQuote)][rateLeg.FixingReference];

            _assetLegCoeffsA = new Dictionary<DateTime, double[]>();
            _assetLegCoeffsB = new Dictionary<DateTime, double>();
            _rateLegCoeffsC = new Dictionary<DateTime, double[]>();
            _rateLegCoeffsD = new Dictionary<DateTime, double>();
            _rateLegCoeffsG = new Dictionary<DateTime, double[]>();
            _rateLegCoeffsH = new Dictionary<DateTime, double>();

            foreach (var t in _assetLeg.ResetShedule.AllDates)
            {
                double[] A;
                double B;
                ComputeAssetLegCoeffs(t, out A, out B);
                _assetLegCoeffsA.Add(t, (double[])A.Clone());
                _assetLegCoeffsB.Add(t, B);

                double[] C;
                double[] G;
                double D;
                double H;
                ComputeRateLegCoeffs(t, out C, out D, out G, out H);
                _rateLegCoeffsC.Add(t, C);
                _rateLegCoeffsG.Add(t, G);
                _rateLegCoeffsD.Add(t, D);
                _rateLegCoeffsH.Add(t, H);
            }
        }

        private void ComputeForwardCoeffs(DateTime t, DateTime Ti, out double[] a, out double b)
        {
            a = new double[Underlying.Content.Count];

            var repoZc = _repo.ForwardZcPrice(t, Ti);

            for (int j = 0; j < Underlying.Content.Count; j++)
            {
                var item = Underlying.Content[j];
                var s = Underlying.GetComponent(item);

                var fxRate = 1.0;
                if (item.ReferenceCurrency.Code != Underlying.ReferenceCurrency.Code)
                {
                    var fxCurve = FXStatic[Tuple.Create(item.ReferenceCurrency.Code, Underlying.ReferenceCurrency.Code)];
                    fxRate = fxCurve.Forward(t, Ti);
                }

                var discountZc = _discFwd[item.Currency].ForwardZcPrice(t, Ti);
                a[j] = s.Weight * fxRate * repoZc / discountZc;
            }

            b = 0d;
            var allInBasket = _divs.AllInDividends(t, Ti);
            foreach (var item in allInBasket)
            {
                // Weight
                double weight = 0.0;
                var snTicker = item.Ticker as SingleNameTicker;
                if (snTicker != null)
                {
                    if (Underlying.Content.Contains(snTicker))
                        weight = Underlying.GetComponent(snTicker).Weight;
                    else
                        continue;
                }
                else { weight = 1; }

                var discountZcMat = _discFwd[item.PaymentCurrency].ForwardZcPrice(t, Ti);
                var discountZc1 = _discFwd[item.PaymentCurrency].ForwardZcPrice(t, item.PaymentDate);

                var discountRepo = _repo.ForwardZcPrice(t, item.PaymentDate);

                var fxRate = 1.0;
                if (item.PaymentCurrency.Code != Underlying.ReferenceCurrency.Code)
                {
                    var fxCurve = FXStatic[Tuple.Create(item.PaymentCurrency.Code, Underlying.ReferenceCurrency.Code)];
                    fxRate = fxCurve.Forward(t, item.PaymentDate);
                }

                b -= weight * (discountZc1 / discountRepo) / (discountZcMat / repoZc) * fxRate * item.GrossAmount * item.AllIn;
            }
        }

        private void ComputeAssetLegCoeffs(DateTime t, out double[] A, out double B)
        {
            A = new double[Underlying.Content.Count];
            B = 0d;

            double[] a1, a2;
            double b1, b2;
            ComputeForwardCoeffs(t, _assetLeg.Periods[0].Start, out a1, out b1);

            foreach (var p in _assetLeg.Periods)
            {
                var fxBasketToLeg = 1.0;
                if (_assetLeg.Underlying.Currency.Code != _assetLeg.Currency.Code)
                {
                    var ccyPair0 = Tuple.Create(_assetLeg.Underlying.Currency.Code, _assetLeg.Currency.Code);
                    fxBasketToLeg = FXStatic[ccyPair0].Forward(t, p.End);
                }

                ComputeForwardCoeffs(t, p.End, out a2, out b2);

                var zcPrice = _discTRS.ForwardZcPrice(t, p.End);

                A.PlusAssign(a2.Minus(a1).Multiply(zcPrice * fxBasketToLeg));

                B += (b2 - b1) * zcPrice * fxBasketToLeg;

                a2.CopyTo(a1, 0);
                b1 = b2;
            }

            var divs = _divs.AllInDividends(_assetLeg.Periods[0].Start, _assetLeg.Periods.Last().End);
            var tmp = 0d;
            foreach (var div in divs)
            {
                double weight;
                var snTicker = div.Ticker as SingleNameTicker;
                if (snTicker != null)
                {
                    weight = (Underlying != null) ? Underlying.GetComponent(snTicker).Weight : 1d;
                }
                else { weight = 1; }

                var fx = 1.0;
                if (div.PaymentCurrency.Code != _assetLeg.Currency.Code)
                {
                    var ccyPair = Tuple.Create(div.PaymentCurrency.Code, _assetLeg.Currency.Code);
                    fx = FXStatic[ccyPair].Forward(t, div.PaymentDate);
                }

                var discount = _discTRS.ForwardZcPrice(t, div.PaymentDate);

                tmp += _assetLeg.DivRatio * weight * discount * fx * div.GrossAmount;
            }

            B += tmp;

            //A.MultiplyAssign(_assetLeg.Quotity);
            //B *= _assetLeg.Quotity;
        }

        private void ComputeRateLegCoeffs(DateTime t, out double[] C, out double D,
            out double[] G, out double H)
        {
            double[] a;
            double b;

            C = new double[Underlying.Content.Count];
            D = 0d;
            G = new double[Underlying.Content.Count];
            H = 0d;
            foreach (var p in _rateLeg.Periods)
            {
                var disc = _discTRS.ForwardZcPrice(t, p.End);
                var period = _rateLeg.DayCountConvention.Count(p.Start, p.End);

                var fxRate = 1.0;
                if (_assetLeg.Currency.Code != Underlying.ReferenceCurrency.Code)
                {
                    var fxCurve = FXStatic[Tuple.Create(Underlying.ReferenceCurrency.Code, _assetLeg.Currency.Code)];
                    fxRate = fxCurve.Forward(t, p.End);
                }

                ComputeForwardCoeffs(t, p.End, out a, out b); //

                var fwdRate = _fixingRateCurve.Forward(p.Start, _rateLeg.Tenor);

                C.PlusAssign(a.Multiply(period * disc * fxRate));
                D += b * period * disc * fxRate;

                G.PlusAssign(a.Multiply(period * disc * fxRate * fwdRate));
                H += b * period * disc * fxRate * fwdRate;
            }

        }

        public double ComputeTRSPrice(DateTime t, double[] S, double spread)
        {
            double assetLegValue = _assetLegCoeffsA[t].InnerProd(S) + _assetLegCoeffsB[t];

            double rateLegValue = (_rateLegCoeffsC[t].InnerProd(S) + _rateLegCoeffsD[t]) * spread
                + (_rateLegCoeffsG[t].InnerProd(S) + _rateLegCoeffsH[t]);

            return assetLegValue - rateLegValue;
        }

        public double ComputeTRSFaireSpread(DateTime t, double[] S)
        {
            double assetLegValue = _assetLegCoeffsA[t].InnerProd(S) + _assetLegCoeffsB[t];

            double faireSpread = (assetLegValue - (_rateLegCoeffsG[t].InnerProd(S) + _rateLegCoeffsH[t])) / (_rateLegCoeffsC[t].InnerProd(S) + _rateLegCoeffsD[t]);

            return faireSpread;
        }

        public double ComputeAssetLeg(DateTime t, double[] S)
        {
            return _assetLegCoeffsA[t].InnerProd(S) + _assetLegCoeffsB[t];
        }

        public double ComputeFloatRateLeg(DateTime t, double[] S)
        {
            return _rateLegCoeffsG[t].InnerProd(S) + _rateLegCoeffsH[t];
        }

        public double ComputeDuration(DateTime t, double[] S)
        {
            return _rateLegCoeffsC[t].InnerProd(S) + _rateLegCoeffsD[t];
        }
    }
}
