using System;
using System.Linq;
using System.Collections.Generic;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Calibration.RateCurves;
using Zeliade.Finance.Common.Pricer;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Mrc;

namespace AldrinAnalytics.Pricers
{

    public abstract class GenericPricer : IPricer
    {

        protected readonly GenericMarketProxy<Ticker, IDividendCurve> _divMarket;
        protected readonly GenericMarketProxy<Ticker, IRepoCurve> _repoMarket;
        protected readonly GenericMarketProxy<Currency, IDiscountCurve<DateTime>> _oisMarket;
        protected readonly GenericMarketProxy<RateReference, IForwardRateCurve> _fwdMarket;
        protected readonly GenericMarketProxy<LiborReference, IDiscountCurve<DateTime>> _liborDiscMarket;
        protected readonly GenericMarketProxy<SingleNameTicker, SingleNameSecurity> _singleNameMarket;
        protected readonly GenericMarketProxy<CurrencyPair, IForwardForexCurve> _fxMarket;
        protected readonly IAssetProduct _product;
        protected readonly ICollateralScheme _collat;
        protected readonly DateTime _asof;

        public GenericPricer(IGenericMarket<Ticker, IDividendCurve> divMarket
            , IGenericMarket<Ticker, IRepoCurve> repoMarket
            , IGenericMarket<Currency, IDiscountCurve<DateTime>> oisMarket
            , IGenericMarket<RateReference, IForwardRateCurve> fwdMarket
            , IGenericMarket<LiborReference, IDiscountCurve<DateTime>> liborDiscMarket
            , IGenericMarket<SingleNameTicker, SingleNameSecurity> singleNameMarket
            , IGenericMarket<CurrencyPair, IForwardForexCurve> fxMarket
            , IAssetProduct product
            , ICollateralScheme collat)
        {
            _divMarket = new GenericMarketProxy<Ticker, IDividendCurve>(Require.ArgumentNotNull(divMarket, "divMarket"));
            _repoMarket = new GenericMarketProxy<Ticker, IRepoCurve>(Require.ArgumentNotNull(repoMarket, "repoMarket"));
            _oisMarket = new GenericMarketProxy<Currency, IDiscountCurve<DateTime>>(Require.ArgumentNotNull(oisMarket, "oisMarket"));
            _fwdMarket = new GenericMarketProxy<RateReference, IForwardRateCurve>(Require.ArgumentNotNull(fwdMarket, "fwdMarket"));
            _liborDiscMarket = new GenericMarketProxy<LiborReference, IDiscountCurve<DateTime>>(Require.ArgumentNotNull(liborDiscMarket, "liborDiscMarket"));
            _singleNameMarket = new GenericMarketProxy<SingleNameTicker, SingleNameSecurity>(Require.ArgumentNotNull(singleNameMarket, "singleNameMarket"));
            _fxMarket = new GenericMarketProxy<CurrencyPair, IForwardForexCurve>(Require.ArgumentNotNull(fxMarket, "fxMarket"));
            _collat = collat ?? throw new ArgumentNullException(nameof(collat));
            _product = product;

            _asof = _singleNameMarket.MarketDate;
            // TODO CHECK COHERENCE DATES COURBES
        }

        public IPricingRequest Price(IPricingRequest request)
        {
            var localRequest = Require.ArgumentIsInstanceOf<TrsPricingRequest>(request, "request");

            ResetContext();
            var basePrice = InternalPrice(_product, localRequest, true) as TrsPricingRequest;

            localRequest.DirtyPrice = basePrice.DirtyPrice;
            localRequest.DirtyPriceConfidence = basePrice.DirtyPriceConfidence;
            localRequest.FixRateComponent = basePrice.FixRateComponent;
            localRequest.Duration = basePrice.Duration;
            localRequest.FloatRateComponent = basePrice.FloatRateComponent;
            localRequest.Currency = basePrice.Currency;
            localRequest.InstrumentId = basePrice.InstrumentId;
            localRequest.InstrumentType = basePrice.InstrumentType;
            localRequest.Leg1 = basePrice.Leg1;
            localRequest.Leg2 = basePrice.Leg2;
            localRequest.SignBasketDelta = basePrice.SignBasketDelta;

            if (localRequest.ToDo.Contains(PricingTask.RepoDelta) || localRequest.ToDo.Contains(PricingTask.All))
            {
                Action<TrsPricingRequest, Symbol, IFiniteDifferenceDelta, double> updater = (r, t, findiff, delta) => r.AddRepoDelta((Ticker)t, findiff, delta);
                DoFinDiff(_repoMarket, localRequest, basePrice, updater);
            }

            if (localRequest.ToDo.Contains(PricingTask.EpsilonDelta) || localRequest.ToDo.Contains(PricingTask.All))
            {
                Action<TrsPricingRequest, Symbol, IFiniteDifferenceDelta, double> updater = (r, t, findiff, delta) => r.AddDivDelta((Ticker)t, findiff, delta);
                DoFinDiff(_divMarket, localRequest, basePrice, updater);
            }

            if (localRequest.ToDo.Contains(PricingTask.EquityDelta) || localRequest.ToDo.Contains(PricingTask.All))
            {
                Action<TrsPricingRequest, Symbol, IFiniteDifferenceDelta, double> updater = (r, t, findiff, delta) => r.AddEquityDelta((Ticker)t, findiff, delta);
                DoFinDiff(_singleNameMarket, localRequest, basePrice, updater, ignoreSubDependencies: false);

            }

            if (localRequest.ToDo.Contains(PricingTask.FxDelta) || localRequest.ToDo.Contains(PricingTask.All))
            {
                Action<TrsPricingRequest, Symbol, IFiniteDifferenceDelta, double> updater = (r, t, findiff, delta) => r.AddFxDelta((Ticker)t, findiff, delta);
                DoFinDiff(_fxMarket, localRequest, basePrice, updater);
            }

            if (localRequest.ToDo.Contains(PricingTask.OisDelta) || localRequest.ToDo.Contains(PricingTask.All))
            {
                Action<TrsPricingRequest, Symbol, IFiniteDifferenceDelta, double> updater = (r, t, findiff, delta) => r.AddOisDelta((Currency)t, findiff, delta);
                DoFinDiff<Currency, IDiscountCurve<DateTime>>(_oisMarket, localRequest, basePrice, updater);
            }

            if (localRequest.ToDo.Contains(PricingTask.FwdFixings) || localRequest.ToDo.Contains(PricingTask.All))
            {
                Action<TrsPricingRequest, Symbol, IFiniteDifferenceDelta, double> updater = (r, t, findiff, delta) => r.AddFwdFixingsDeltas((RateReference)t, findiff, delta);
                DoFinDiff<RateReference, IForwardRateCurve>(_fwdMarket, localRequest, basePrice, updater);
            }

            if (localRequest.ToDo.Contains(PricingTask.LiborDiscounting) || localRequest.ToDo.Contains(PricingTask.All))
            {
                Action<TrsPricingRequest, Symbol, IFiniteDifferenceDelta, double> updater = (r, t, findiff, delta) => r.AddLIborDiscountDeltas((LiborReference)t, findiff, delta);
                DoFinDiff<LiborReference, IDiscountCurve<DateTime>>(_liborDiscMarket, localRequest, basePrice, updater);
            }
            return localRequest;
        }

        private void DoFinDiff<K, T>(GenericMarketProxy<K, T> market
            , TrsPricingRequest localRequest
            , TrsPricingRequest basePrice
            , Action<TrsPricingRequest, Symbol, IFiniteDifferenceDelta, double> updateDelta, bool ignoreSubDependencies = true) where K : Symbol where T : class
        {
            var dependencies = new Dictionary<K, List<Type>>();
            foreach (var item in market.Dependencies)
            {
                List<Type> lt = null;
                if (!dependencies.TryGetValue(item.Item1, out lt))
                {
                    lt = new List<Type>();
                    dependencies.Add(item.Item1, lt);
                }
                if (!lt.Contains(item.Item2))
                    lt.Add(item.Item2);
            }


            foreach (var kv in market.Bumps)
            {
                bool select = false;
                IDependOnSymbol symbTmp = kv.Key as IDependOnSymbol;
                if (symbTmp!=null && !ignoreSubDependencies)
                {
                    select = dependencies.Keys.Any(k => symbTmp.DependOn(k));
                }
                else 
                {
                    select = dependencies.ContainsKey((K)kv.Key);
                }

                if (!select) continue;               
                    
                foreach (var bump in kv.Value)
                {
                    TrsPricingRequest upPrice = null;
                    Dictionary<K, ISheetBumpType> upperContext = bump.Upper.ToDictionary(x => (K)x.Key, x => x.Value);
                    if (bump.Upper.First().Value != null)
                    {
                        market.Context = upperContext;
                        upPrice = InternalPrice(_product, localRequest, false);
                    }

                    Dictionary<K, ISheetBumpType> lowerContext = bump.Lower.ToDictionary(x => (K)x.Key, x => x.Value);
                    TrsPricingRequest lowPrice = null;
                    if (bump.Lower.First().Value != null)
                    {
                        market.Context = lowerContext;
                        lowPrice = InternalPrice(_product, localRequest, false);
                    }

                    // Quote type for distance (only one choice is allowed)
                    var distLower = lowerContext.ToDictionary(x=>(Symbol)x.Key, x=>market.GetDistance(x.Key, lowerContext[x.Key], GetQuoteType(dependencies[x.Key])));
                    var distUpper = upperContext.ToDictionary(x => (Symbol)x.Key, x => market.GetDistance(x.Key, upperContext[x.Key], GetQuoteType(dependencies[x.Key])));




                    double delta;


                    if (bump.DeltaMethod.GetType().Name=="DerivativeBasket")
                    {
                        delta = bump.DeltaMethod.ComputeDerivativeDelta(basePrice, bump.Lower, distLower, lowPrice, bump.Upper, distUpper, upPrice);
                        if(basePrice.InstrumentType.Name == "AssetLegFloatRate")
                        {
                            AssetLegFloatRate _instrument = (AssetLegFloatRate)_product;
                            delta = delta / _instrument.Quotity.Value;
                        }
                        else if(basePrice.InstrumentType.Name == "AssetLegStd")
                        {
                            AssetLegStd _instrument = (AssetLegStd)_product;
                            delta = delta / _instrument.Quotity.Value;
                        }
                    }
                    else
                    {
                         delta = bump.DeltaMethod.Compute(basePrice, bump.Lower, distLower, lowPrice, bump.Upper, distUpper, upPrice);
                    }

                    updateDelta(localRequest, kv.Key, bump, delta);

                    var updateLeg1 = (upPrice != null && upPrice.Leg1 != null) || (lowPrice != null && lowPrice.Leg1 != null);

                    if (updateLeg1)
                    {
                        var lowerLeg1 = lowPrice != null ? lowPrice.Leg1 : null;
                        var upperLeg1 = upPrice != null ? upPrice.Leg1 : null;
                        var deltaLeg1 = bump.DeltaMethod.Compute(basePrice.Leg1, bump.Lower, distLower, lowerLeg1, bump.Upper, distUpper, upperLeg1);
                        updateDelta(localRequest.Leg1, kv.Key, bump, deltaLeg1);
                    }

                    var updateLeg2 = (upPrice != null && upPrice.Leg2 != null) || (lowPrice != null && lowPrice.Leg2 != null);

                    if (updateLeg2)
                    {
                        var lowerLeg2 = lowPrice != null ? lowPrice.Leg2 : null;
                        var upperLeg2 = upPrice != null ? upPrice.Leg2 : null;
                        var deltaLeg2 = bump.DeltaMethod.Compute(basePrice.Leg2, bump.Lower, distLower, lowerLeg2, bump.Upper, distUpper, upperLeg2);
                        updateDelta(localRequest.Leg2, kv.Key, bump, deltaLeg2);
                    }

                }

            }


            market.Context = null; // reset in base context
        }

        private Type GetQuoteType(List<Type> l)
        {
            if (l.Count == 1)
                return l.First();
            if (l.Count == 2)
            {
                if (l.Contains(typeof(MidQuote)))
                {
                    if (l.Contains(typeof(AskQuote)))
                        return typeof(AskQuote);
                    else
                        return typeof(BidQuote);
                }
                else
                    return typeof(MidQuote);
            }
            if (l.Count == 3)
            {
                return typeof(MidQuote);
            }

            throw new ArgumentException(string.Format("A dependency in more that 3 quote types has been detected !"));
        }

        private void ResetContext()
        {
            _divMarket.Context = null;
            _repoMarket.Context = null;
            _oisMarket.Context = null;
            _fwdMarket.Context = null;
            _singleNameMarket.Context = null;
            _fxMarket.Context = null;
            _liborDiscMarket.Context = null;
        }

        protected abstract TrsPricingRequest InternalPrice(IAssetProduct leg, IPricingRequest request, bool isBasePrice);

        // TODO : choix bid-ask
        protected IDiscountCurve<DateTime> GetDiscountCurve(IAssetProduct prod, IAssetLeg p, string reference)
        {
            if (_collat is NoCollateral)
            {
                var key = new LiborReference(p.Currency.Code, p.Tenor
                    , DayCountConventions.Get(DayCountConventions.Codings.Actual360) // NOT IN THE HASH
                    );
                return _liborDiscMarket.Get(key, typeof(MidQuote));
            }
            if (_collat is CashCollateral)
            {
                var locCollat = _collat as CashCollateral;
                var oisCurve = _oisMarket.Get(locCollat.Currency, typeof(MidQuote)); // OIS for the collateral currency
                return oisCurve;
            }
            if (_collat is SecurityCollateral)
            {
                var locCollat = _collat as SecurityCollateral;

                var repoCurve = _repoMarket.Get(p.Underlying, typeof(MidQuote));
                return repoCurve;
            }

            throw new ArgumentException(string.Format("The input collateral scheme {0} is not supported !", _collat.GetType()));
        }


    }
}
