using System;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Models;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Model;
using Zeliade.Finance.Common.Pricer;
using Zeliade.Finance.Common.Pricer.MonteCarlo;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;

namespace AldrinAnalytics.Pricers
{
    public class AssetProductMCPricer : GenericPricer
    {
        private readonly JointHistoricalFixings _fixings;
        private readonly BlackScholesBasket _model;
        private readonly MonteCarloEngineSetting _mcSetting;
        private readonly int _maxPathNumber;
        private readonly double _confidenceLevel;
        private readonly bool _confidenceAsTarget;
        private readonly double _halfInterval;

        public AssetProductMCPricer(IGenericMarket<Ticker, IDividendCurve> divMarket
            , IGenericMarket<Ticker, IRepoCurve> repoMarket
            , IGenericMarket<Currency, IDiscountCurve<DateTime>> oisMarket
            , IGenericMarket<RateReference, IForwardRateCurve> fwdMarket
            , IGenericMarket<LiborReference, IDiscountCurve<DateTime>> liborDiscMarket
            , IGenericMarket<SingleNameTicker, SingleNameSecurity> singleNameMarket
            , IGenericMarket<CurrencyPair, IForwardForexCurve> fxMarket
            , IAssetProduct product
            , ICollateralScheme collat
            , JointHistoricalFixings fixings
            , BlackScholesBasket model
            , MonteCarloEngineSetting mcSetting
            , int maxPathNumber
            , double confidence
            , bool confidenceAsTarget
            , double halfInterval
            )
            : base(divMarket, repoMarket, oisMarket, fwdMarket, liborDiscMarket, singleNameMarket, fxMarket, product, collat)
        {

            _model = Require.ArgumentNotNull(model, "cov");
            _mcSetting = Require.ArgumentNotNull(mcSetting, "mcSetting");
            _maxPathNumber = maxPathNumber;
            _confidenceLevel = confidence;
            _confidenceAsTarget = confidenceAsTarget;
            _halfInterval = halfInterval;
            _fixings = fixings; // null value allowed

        }

        protected override TrsPricingRequest InternalPrice(IAssetProduct product
            , IPricingRequest request
            , bool isBasePrice)
        {
            var trsReq = Require.ArgumentIsInstanceOf<TrsPricingRequest>(request, "request");

            Require.ArgumentNotNull(product, "product");

            if (product.Legs.Count > 1)
            {
                Require.ArgumentListCount(2, product.Legs, "product.Legs");
                Require.Argument(product.Legs[0].Currency == product.Legs[1].Currency, "product.Legs"
                    , Error.Msg("The two legs of the given instrument {0} should be in the same payment currency but are respectively in {1} and {2} !"
                    , product.Id, product.Legs[0].Currency, product.Legs[1].Currency));

            }

            var basket = Require.ArgumentIsInstanceOf<SecurityBasket>(product.Legs.First().Underlying, "product.Legs.First().Underlying");

            var asof = _singleNameMarket.MarketDate;

            // Simulation au mid
            var spots = basket.Content.Select(x => _singleNameMarket.Get(x, typeof(MidQuote)).CurrentValue<MidQuote>()).ToArray();

            var longStep = new LongStepSimulationDateFactory();

            var initializer = new ConstantInitializer<double[]>(spots);

            // Discounting curves
            var discCurves = new Dictionary<Currency, IDiscountCurve<DateTime>>();
            foreach (var ccy in basket.StockCurrencies)
            {
                discCurves.Add(ccy, _oisMarket.Get(ccy, typeof(MidQuote)));
            }

            // Fx dependencies (common to generator and fixing model)
            var fxCurves = new Dictionary<Tuple<string, string>, IForwardForexCurve>();

            // Genarator : div->stock & stock->basket
            //// 1) div currency -> stock currency (dependency used only in the generator, to calculate the ex div jump following div payment)
            var tmpDivCurve = _divMarket.Get(basket, typeof(MidQuote));
            foreach (var stockTodivCcy in tmpDivCurve.PaymentCurrencies)
            {
                var key = Tuple.Create(stockTodivCcy.DomesticCurrency, stockTodivCcy.ForeignCurrency);
                if (!fxCurves.ContainsKey(key))
                {
                    fxCurves.Add(key, _fxMarket.Get(stockTodivCcy, typeof(MidQuote)));
                }
                //ugly fix
                key = Tuple.Create(stockTodivCcy.ForeignCurrency, stockTodivCcy.DomesticCurrency);
                var fxinv = new CurrencyPair(stockTodivCcy.ForeignCurrency, stockTodivCcy.DomesticCurrency);
                if (!fxCurves.ContainsKey(key))
                {
                    fxCurves.Add(key, _fxMarket.Get(fxinv, typeof(MidQuote)));
                }


                if (!discCurves.ContainsKey(stockTodivCcy.Currency))
                {
                    discCurves.Add(stockTodivCcy.Currency, _oisMarket.Get(stockTodivCcy.Currency, typeof(MidQuote)));
                }
            }
            //// 2) stock ccy -> basketcurrency
            foreach (var ccyPair in basket.StockToBasket)
            {
                var key = Tuple.Create(ccyPair.DomesticCurrency, ccyPair.ForeignCurrency);
                if (!fxCurves.ContainsKey(key))
                {
                    fxCurves.Add(key, _fxMarket.Get(ccyPair, typeof(MidQuote)));
                }
                //ugly fix
                key = Tuple.Create(ccyPair.ForeignCurrency, ccyPair.DomesticCurrency);
                var fxinv = new CurrencyPair(ccyPair.ForeignCurrency, ccyPair.DomesticCurrency);
                if (!fxCurves.ContainsKey(key))
                {
                    fxCurves.Add(key, _fxMarket.Get(fxinv, typeof(MidQuote)));
                }
            }

            // Leg dependencies
            foreach (var leg in product.Legs)
            {
                foreach (var ccyPair in leg.FxUnderlyings)
                {
                    var key = Tuple.Create(ccyPair.DomesticCurrency, ccyPair.ForeignCurrency);
                    if (!fxCurves.ContainsKey(key))
                    {
                        fxCurves.Add(key, _fxMarket.Get(ccyPair, typeof(MidQuote)));
                    }
                    //ugly fix
                    key = Tuple.Create(ccyPair.ForeignCurrency, ccyPair.DomesticCurrency);
                    var fxinv = new CurrencyPair(ccyPair.ForeignCurrency, ccyPair.DomesticCurrency);
                    if (!fxCurves.ContainsKey(key))
                    {
                        fxCurves.Add(key, _fxMarket.Get(fxinv, typeof(MidQuote)));
                    }
                }
            }

            // TODO CHECKS BASKET
            var cov = _model.Covariance;

            var repoCurve = _repoMarket.Get(basket, typeof(MidQuote));
            var gen = new BlackScholesGenerator(1
                , asof
                , longStep
                , initializer
                , basket
                , _divMarket.Get(basket, typeof(MidQuote))
                , repoCurve
                , discCurves
                , fxCurves
                , cov
                , _model.FactorNumber);

            var fwdDict = new Dictionary<Type, Dictionary<RateReference, IForwardRateCurve>>();
            var qTypes = new List<Type>() { typeof(MidQuote) };
            if (!trsReq.IsMidPrice)
            {
                qTypes.Add(typeof(BidQuote));
                qTypes.Add(typeof(AskQuote));
            }

            foreach (var leg in product.Legs)
            {
                var rateLeg = leg as IRateLeg;
                if (rateLeg == null)
                    continue;

                var rateRef = rateLeg.FixingReference;
                foreach (var typ in qTypes)
                {
                    Dictionary<RateReference, IForwardRateCurve> tmp = null;
                    if (!fwdDict.TryGetValue(typ, out tmp))
                    {
                        tmp = new Dictionary<RateReference, IForwardRateCurve>();
                        fwdDict.Add(typ, tmp);
                    }
                    tmp.Add(rateRef, _fwdMarket.Get(rateRef, typ));
                }
            }

            var divCurve = new Dictionary<Type, IDividendCurve>()
            {
                {typeof(MidQuote), _divMarket.Get(basket, typeof(MidQuote))}
            };

            if (!trsReq.IsMidPrice)
            {
                divCurve.Add(typeof(BidQuote), _divMarket.Get(basket, typeof(BidQuote)));
                divCurve.Add(typeof(AskQuote), _divMarket.Get(basket, typeof(AskQuote)));
            }

            // Half spread
            double[] spread;
            if (trsReq.IsMidPrice)
                spread = Enumerable.Repeat(0d, basket.Content.Count).ToArray();
            else
            {
                var spotsBid = basket.Content.Select(x => _singleNameMarket.Get(x, typeof(BidQuote)).CurrentValue<BidQuote>()).ToArray();
                var spotsAsk = basket.Content.Select(x => _singleNameMarket.Get(x, typeof(AskQuote)).CurrentValue<AskQuote>()).ToArray();
                spread = Enumerable.Range(0, spotsAsk.Length).Select(i => spotsAsk[i] - spotsBid[i]).ToArray();
            }

            // Discounting of the TRS
            var disc = GetDiscountCurve(product, product.Legs.First(), trsReq.Reference);

            IProcess proc = null;
            if (product.Legs.Count == 2)
            {
                var leg1AsReset = product.Legs[0] as AssetLegReset;
                var leg2AsFloatRate = product.Legs[1] as AssetLegFloatRate;

                if (leg2AsFloatRate != null && leg1AsReset != null)
                {
                    var discCurvesForAffineModel = new Dictionary<Currency, IDiscountCurve<DateTime>>();
                    foreach (var ccy in basket.StockCurrencies)
                    {
                        discCurvesForAffineModel.Add(ccy, _oisMarket.Get(ccy, typeof(MidQuote)));
                    }
                    foreach (var stockTodivCcy in tmpDivCurve.PaymentCurrencies)
                    {
                        var divPayCcy = new Currency(stockTodivCcy.DomesticCurrency); 
                        if (!discCurvesForAffineModel.ContainsKey(divPayCcy))
                        {
                            discCurvesForAffineModel.Add(divPayCcy, _oisMarket.Get(divPayCcy, typeof(MidQuote)));
                        }                                
                    }

                    proc = new AffinePriceModel(asof, gen, fxCurves, fwdDict, divCurve, basket, spread, discCurvesForAffineModel, disc, repoCurve, leg1AsReset, leg2AsFloatRate);
                }
                else
                {
                    proc = new JointModel(asof, basket, gen, fxCurves, fwdDict, divCurve, spread);
                }
            }
            else
            {
                proc = new JointModel(asof, basket, gen, fxCurves, fwdDict, divCurve, spread);
            }           

            // Histo Model
            var histoModel = _fixings != null ? new HistoricalJointModel(_fixings, asof, fwdDict) : null;

            // Numeraire
            var cashNum = new CashProcess(disc);

            // Product
            var legAsProduct = product.ToProduct(asof, trsReq.IsMidPrice, trsReq.Reference);

            // Pricer
            var setting = new MonteCarloEngineSetting() { BlockSize = _mcSetting.BlockSize };

            MonteCarloEngine pricer = null;
            if (histoModel == null)
                pricer = MonteCarloEngine.PricerWithoutDataSource(asof, proc, cashNum, legAsProduct, setting);
            else
                pricer = new MonteCarloEngine(asof, histoModel, proc, cashNum, legAsProduct, setting);

            EuropeanPrice euroPrice;
            Mean duration;
            Mean fltComp;
            if (!_confidenceAsTarget)
            {
                euroPrice = EuropeanPrice.EuropeanPriceWithFixedPathNumber(_maxPathNumber, _confidenceLevel);
                duration = Mean.MeanWithFixedPathNumber("Duration", true, true, true, _confidenceLevel, _maxPathNumber);
                fltComp = Mean.MeanWithFixedPathNumber("FltRateComp", true, true, true, _confidenceLevel, _maxPathNumber);
            }
            else
            {
                euroPrice = EuropeanPrice.EuropeanPriceWithTargetConfidence(_maxPathNumber, _confidenceLevel, _halfInterval);
                duration = Mean.MeanWithTargetConfidence("Duration", true, true, true, _confidenceLevel, _maxPathNumber, _halfInterval);
                fltComp = Mean.MeanWithTargetConfidence("FltRateComp", true, true, true, _confidenceLevel, _maxPathNumber, _halfInterval);
            }
            var req = new TaskRequest(euroPrice);

            var productAsTrs = product as TotalReturnSwap;
            bool leg1IsFlt = false;
            bool leg2IsFlt = false;
            if (productAsTrs != null)
            {
                var trsLeg1AsRate = productAsTrs.Leg1 as IRateLeg;
                var trsLeg2AsRate = productAsTrs.Leg2 as IRateLeg;
                leg2IsFlt = trsLeg1AsRate == null && trsLeg2AsRate != null;
                leg1IsFlt = trsLeg1AsRate != null && trsLeg2AsRate == null;
            }

            // (leg1IsFlt || leg2IsFlt) && isBasePrice
            if ( (leg1IsFlt || leg2IsFlt)  )
            {
                req.AddTask(duration);
                req.AddTask(fltComp);
            }
            pricer.Price(req);

            var output = new TrsPricingRequest(trsReq.ToDo, trsReq.Reference);

            output.DirtyPrice = euroPrice.GetPrice(output.Reference);
            output.DirtyPriceConfidence = euroPrice.GetConfidence(output.Reference);
            output.InstrumentId = product.Id;
            output.InstrumentType = product.GetType();
            output.Currency = product.Legs[0].Currency.Code; // The two legs are in a common currency
            
            var leg1 = new TrsPricingRequest(output.ToDo, output.Reference);
            var sign = output.Reference == product.Legs[0].PayerParty ? -1d : 1d;
            leg1.DirtyPrice = sign * euroPrice.GetLegPrice(product.Legs[0].PayerParty, product.Legs[0].ReceiverParty);
            leg1.DirtyPriceConfidence = sign * euroPrice.GetLegConfidence(product.Legs[0].PayerParty, product.Legs[0].ReceiverParty);
            leg1.InstrumentId = product.Legs[0].Id;
            leg1.InstrumentType = product.Legs[0].GetType();
            leg1.Currency = product.Legs[0].Currency.Code;
            output.Leg1 = leg1;

            // Equity delta sign
            bool leg1IsPerf = (product.Legs[0] as IPerformanceLeg) != null;

            bool leg2IsPerf = false;
            if (product.Legs.Count == 2)
            {
                var leg2 = new TrsPricingRequest(output.ToDo, output.Reference);
                leg2.DirtyPrice = (-sign) * euroPrice.GetLegPrice(product.Legs[1].PayerParty, product.Legs[1].ReceiverParty);
                leg2.DirtyPriceConfidence = euroPrice.GetLegConfidence(product.Legs[1].PayerParty, product.Legs[1].ReceiverParty);
                leg2.InstrumentId = product.Legs[1].Id;
                leg2.InstrumentType = product.Legs[1].GetType();
                leg2.Currency = product.Legs[1].Currency.Code;
                output.Leg2 = leg2;

                leg2IsPerf = (product.Legs[1] as IPerformanceLeg) != null;

            }

            // Fair spread      
            //leg2IsFlt && isBasePrice
            if (leg2IsFlt )
            {
                double signPrice = output.Reference == product.Legs[1].PayerParty ? -1d : 1d;
                output.Leg2.Duration = signPrice * duration.Means.First();
                output.Leg2.FloatRateComponent = signPrice * fltComp.Means.First();
            }
            //leg1IsFlt && isBasePrice
            else if (leg1IsFlt )
            {
                double signPrice = output.Reference == product.Legs[0].PayerParty ? -1d : 1d;
                output.Leg1.Duration = signPrice * duration.Means.First();
                output.Leg1.FloatRateComponent = signPrice * fltComp.Means.First();
            }

            // Sign for equity delta
            if (leg1IsPerf)
            {
                output.SignBasketDelta = output.Reference == product.Legs[0].PayerParty ? -1d : 1d;
            }
            else if (leg2IsPerf)
            {
                output.SignBasketDelta = output.Reference == product.Legs[1].PayerParty ? -1d : 1d;
            }

            return output;
        }

        /// <summary>
        /// For test purpose
        /// </summary>
        /// <param name="product"></param>
        /// <param name="request"></param>
        /// <param name="isBasePrice"></param>
        /// <returns></returns>
        public IProcess Process(IAssetProduct product
            , IPricingRequest request
            , bool isBasePrice)
        {
            var trsReq = Require.ArgumentIsInstanceOf<TrsPricingRequest>(request, "request");

            Require.ArgumentNotNull(product, "product");

            if (product.Legs.Count > 1)
            {
                Require.ArgumentListCount(2, product.Legs, "product.Legs");
                Require.Argument(product.Legs[0].Currency == product.Legs[1].Currency, "product.Legs"
                    , Error.Msg("The two legs of the given instrument {0} should be in the same payment currency but are respectively in {1} and {2} !"
                    , product.Id, product.Legs[0].Currency, product.Legs[1].Currency));

            }

            var basket = Require.ArgumentIsInstanceOf<SecurityBasket>(product.Legs.First().Underlying, "product.Legs.First().Underlying");

            var asof = _singleNameMarket.MarketDate;

            // Simulation au mid
            var spots = basket.Content.Select(x => _singleNameMarket.Get(x, typeof(MidQuote)).CurrentValue<MidQuote>()).ToArray();

            var longStep = new LongStepSimulationDateFactory();

            var initializer = new ConstantInitializer<double[]>(spots);

            // Discounting curves
            var discCurves = new Dictionary<Currency, IDiscountCurve<DateTime>>();
            foreach (var ccy in basket.StockCurrencies)
            {
                discCurves.Add(ccy, _oisMarket.Get(ccy, typeof(MidQuote)));
            }

            // Fx dependencies (common to generator and fixing model)
            var fxCurves = new Dictionary<Tuple<string, string>, IForwardForexCurve>();

            // Genarator : div->stock & stock->basket
            //// 1) div currency -> stock currency (dependency used only in the generator, to calculate the ex div jump following div payment)
            var tmpDivCurve = _divMarket.Get(basket, typeof(MidQuote));
            foreach (var stockTodivCcy in tmpDivCurve.PaymentCurrencies)
            {
                var key = Tuple.Create(stockTodivCcy.ForeignCurrency, stockTodivCcy.DomesticCurrency);
                if (!fxCurves.ContainsKey(key))
                {
                    fxCurves.Add(key, _fxMarket.Get(stockTodivCcy, typeof(MidQuote)));
                }
            }
            //// 2) stock ccy -> basketcurrency
            foreach (var ccyPair in basket.StockToBasket)
            {
                var key = Tuple.Create(ccyPair.ForeignCurrency, ccyPair.DomesticCurrency);
                if (!fxCurves.ContainsKey(key))
                {
                    fxCurves.Add(key, _fxMarket.Get(ccyPair, typeof(MidQuote)));
                }
            }

            // Leg dependencies
            foreach (var leg in product.Legs)
            {
                foreach (var ccyPair in leg.FxUnderlyings)
                {
                    var key = Tuple.Create(ccyPair.ForeignCurrency, ccyPair.DomesticCurrency);
                    if (!fxCurves.ContainsKey(key))
                    {
                        fxCurves.Add(key, _fxMarket.Get(ccyPair, typeof(MidQuote)));
                    }
                }
            }

            // TODO CHECKS BASKET
            var cov = _model.Covariance;

            var repoCurve = _repoMarket.Get(basket, typeof(MidQuote));
            var gen = new BlackScholesGenerator(1
                , asof
                , longStep
                , initializer
                , basket
                , _divMarket.Get(basket, typeof(MidQuote))
                , repoCurve
                , discCurves
                , fxCurves
                , cov
                , _model.FactorNumber);

            var fwdDict = new Dictionary<Type, Dictionary<RateReference, IForwardRateCurve>>();
            var qTypes = new List<Type>() { typeof(MidQuote) };
            if (!trsReq.IsMidPrice)
            {
                qTypes.Add(typeof(BidQuote));
                qTypes.Add(typeof(AskQuote));
            }

            foreach (var leg in product.Legs)
            {
                var rateLeg = leg as IRateLeg;
                if (rateLeg == null)
                    continue;

                var rateRef = rateLeg.FixingReference;
                foreach (var typ in qTypes)
                {
                    Dictionary<RateReference, IForwardRateCurve> tmp = null;
                    if (!fwdDict.TryGetValue(typ, out tmp))
                    {
                        tmp = new Dictionary<RateReference, IForwardRateCurve>();
                        fwdDict.Add(typ, tmp);
                    }
                    tmp.Add(rateRef, _fwdMarket.Get(rateRef, typ));
                }
            }

            var divCurve = new Dictionary<Type, IDividendCurve>()
            {
                {typeof(MidQuote), _divMarket.Get(basket, typeof(MidQuote))}
            };

            if (!trsReq.IsMidPrice)
            {
                divCurve.Add(typeof(BidQuote), _divMarket.Get(basket, typeof(BidQuote)));
                divCurve.Add(typeof(AskQuote), _divMarket.Get(basket, typeof(AskQuote)));
            }

            // Half spread
            double[] spread;
            if (trsReq.IsMidPrice)
                spread = Enumerable.Repeat(0d, basket.Content.Count).ToArray();
            else
            {
                var spotsBid = basket.Content.Select(x => _singleNameMarket.Get(x, typeof(BidQuote)).CurrentValue<BidQuote>()).ToArray();
                var spotsAsk = basket.Content.Select(x => _singleNameMarket.Get(x, typeof(AskQuote)).CurrentValue<AskQuote>()).ToArray();
                spread = Enumerable.Range(0, spotsAsk.Length).Select(i => spotsAsk[i] - spotsBid[i]).ToArray();
            }

            // Discounting of the TRS
            var disc = GetDiscountCurve(product, product.Legs.First(), trsReq.Reference);

            IProcess proc = null;
            if (product.Legs.Count == 2)
            {
                var leg1AsReset = product.Legs[0] as AssetLegReset;
                var leg2AsFloatRate = product.Legs[1] as AssetLegFloatRate;

                if (leg2AsFloatRate != null && leg1AsReset != null)
                {
                    var discCurvesForAffineModel = new Dictionary<Currency, IDiscountCurve<DateTime>>();
                    foreach (var ccy in basket.StockCurrencies)
                    {
                        discCurvesForAffineModel.Add(ccy, _oisMarket.Get(ccy, typeof(MidQuote)));
                    }
                    foreach (var stockTodivCcy in tmpDivCurve.PaymentCurrencies)
                    {
                        var divPayCcy = new Currency(stockTodivCcy.ForeignCurrency);
                        if (!discCurvesForAffineModel.ContainsKey(divPayCcy))
                        {
                            discCurvesForAffineModel.Add(divPayCcy, _oisMarket.Get(divPayCcy, typeof(MidQuote)));
                        }
                    }

                    proc = new AffinePriceModel(asof, gen, fxCurves, fwdDict, divCurve, basket, spread, discCurvesForAffineModel, disc, repoCurve, leg1AsReset, leg2AsFloatRate);
                }
                else
                {
                    proc = new JointModel(asof, basket, gen, fxCurves, fwdDict, divCurve, spread);
                }
            }
            else
            {
                proc = new JointModel(asof, basket, gen, fxCurves, fwdDict, divCurve, spread);
            }

            return proc;
            
        }
    }
}
