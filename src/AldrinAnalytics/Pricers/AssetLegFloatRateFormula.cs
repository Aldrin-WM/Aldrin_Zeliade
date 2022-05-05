using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Pricer;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;

namespace AldrinAnalytics.Pricers
{
    public class AssetLegFloatRateFormula : IPricer
    {
        private readonly IForwardCurve _fwdCurve;
        private readonly IDiscountCurve<DateTime> _discCurve;
        private readonly IForwardRateCurve _fixingRateCurve;
        private readonly Dictionary<Tuple<string, string>, IForwardForexCurve> _fxFwd;
        private readonly AssetLegFloatRate _instrument;
        private readonly DateTime _asof;

        public AssetLegFloatRateFormula(DateTime asof, IForwardCurve fwdCurve
            , IDiscountCurve<DateTime> discCurve, IForwardRateCurve fixingRateCurve
             , Dictionary<Tuple<string, string>, IForwardForexCurve> fxFwd
            , AssetLegFloatRate instrument)
        {
            _fwdCurve = fwdCurve ?? throw new ArgumentNullException(nameof(fwdCurve));
            _discCurve = discCurve ?? throw new ArgumentNullException(nameof(discCurve));
            _fixingRateCurve = fixingRateCurve ?? throw new ArgumentNullException(nameof(fixingRateCurve));
            _instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
            _fxFwd = fxFwd ?? throw new ArgumentNullException(nameof(fxFwd));
            _asof = asof;
            // TODO VERIFIER COHERENCE DATES COURBES.


            Require.Argument(_instrument.Quotity.HasValue, "instrument", Error.Msg("The given leg {0} should have a well defined quotity !", instrument.Id));
        }

        public IPricingRequest Price(IPricingRequest request)
        {
            var output = Require.ArgumentIsInstanceOf<TrsPricingRequest>(request, "request");
            Require.Argument(output.Reference == _instrument.PayerParty || output.Reference == _instrument.ReceiverParty, "request"
                , Error.Msg("The input request reference counterparty {0} does not match any of the instrument counterparties {1} and {2}"
                    , output.Reference, _instrument.PayerParty, _instrument.ReceiverParty));

            double sign = output.Reference == _instrument.PayerParty ? -1d : 1d;

            double npv0 = 0d;
            double npv1 = 0d;
            double duration = 0d;

            foreach (var p in _instrument.Periods)
            {
                if (p.End <= _asof)
                {
                    continue;
                }

                var disc = _discCurve.ZcPrice(p.End);
                var period = _instrument.DayCountConvention.Count(p.Start, p.End);
                var liborFwd = _fixingRateCurve.Forward(p.Start, _instrument.Period);

                var forward = _fwdCurve.Forward(p.Start); 
                var tmp = period * disc * forward;
                var fxBasketToLeg = 1.0;
                if (_instrument.Underlying.Currency.Code != _instrument.Currency.Code)
                {
                    var ccyPair0 = Tuple.Create(_instrument.Underlying.Currency.Code, _instrument.Currency.Code);
                    fxBasketToLeg = _fxFwd[ccyPair0].Forward(p.End);
                }

                duration += tmp* fxBasketToLeg;
                npv0 += tmp * liborFwd* fxBasketToLeg;
                npv1 += tmp * _instrument.Spread* fxBasketToLeg;
            }



            npv0 = npv0 * sign * _instrument.Quotity.Value;
            npv1 = npv1 * sign * _instrument.Quotity.Value;
            duration = duration * sign * _instrument.Quotity.Value;
            
            output.Currency = _instrument.Currency.Code;
            output.DirtyPrice = npv0+npv1;
            output.FixRateComponent = npv1;
            output.FloatRateComponent = npv0;
            output.Duration = duration;
            output.InstrumentId = _instrument.Id;
            output.InstrumentType = _instrument.GetType();

            output.SignBasketDelta = -sign;

            return request;
        }
    }

    public class AssetLegFloatRatePricer : GenericPricer
    {
        private readonly IDividendMarket _rawDivMarket;
        private readonly JointHistoricalFixings _fixings;

        public AssetLegFloatRatePricer(IDividendMarket divMarket
            , IGenericMarket<Ticker, IRepoCurve> repoMarket
            , IGenericMarket<Currency, IDiscountCurve<DateTime>> oisMarket
            , IGenericMarket<RateReference, IForwardRateCurve> fwdMarket
            , IGenericMarket<LiborReference, IDiscountCurve<DateTime>> liborDiscMarket
            , IGenericMarket<SingleNameTicker, SingleNameSecurity> singleNameMarket
            , IGenericMarket<CurrencyPair, IForwardForexCurve> fxMarket
            , IAssetLeg product
            , ICollateralScheme collat
            , JointHistoricalFixings fixings)
            : base(divMarket
                , repoMarket
                , oisMarket
                , fwdMarket
                , liborDiscMarket
                , singleNameMarket
                , fxMarket
                , product
                , collat)
        {
            Require.ArgumentIsInstanceOf<AssetLegFloatRate>(product, "product");
            _rawDivMarket = divMarket;
            _fixings = fixings;// Require.ArgumentNotNull(fixings, nameof(fixings));

        }

        protected override TrsPricingRequest InternalPrice(IAssetProduct p, IPricingRequest request, bool isBasePrice)
        {
            var leg = Require.ArgumentIsInstanceOf<IAssetLeg>(p, "p");
            var trsReq = Require.ArgumentIsInstanceOf<TrsPricingRequest>(request, "request");
            IForwardCurve fwdCurve = null;
            IDiscountCurve<DateTime> discCurve = null;
            IDividendCurve divCurve = null;

            var flagRatePayer = (trsReq.Reference == leg.PayerParty);

            var spotQuoteType = typeof(MidQuote);
            var divQuoteType = typeof(MidQuote);
            var repoQuoteType = typeof(MidQuote);
            var OISQuoteType = typeof(MidQuote);
            var FXQuoteType = typeof(MidQuote);
            var fixingRateQuoteType = typeof(MidQuote);

            if (!trsReq.IsMidPrice)
            {
                spotQuoteType = flagRatePayer ? typeof(AskQuote) : typeof(BidQuote);
                divQuoteType = flagRatePayer ? typeof(BidQuote) : typeof(AskQuote);
                repoQuoteType = flagRatePayer ? typeof(BidQuote) : typeof(AskQuote);
                OISQuoteType = flagRatePayer ? typeof(AskQuote) : typeof(BidQuote);
                FXQuoteType = flagRatePayer ? typeof(AskQuote) : typeof(BidQuote);
                fixingRateQuoteType = flagRatePayer ? typeof(AskQuote) : typeof(BidQuote);
            }

            // Histo Model
            var histo = new HistoricalJointModelSingleQuoteType(_fixings, spotQuoteType
                , divQuoteType, typeof(MidQuote), typeof(MidQuote)
                , FXQuoteType);

            // Discount curve
            discCurve = GetDiscountCurve(null, leg, trsReq.Reference);

            SingleNameTicker snTicker = leg.Underlying as SingleNameTicker;
            if (snTicker != null)
            {
                // Dividend curve
                divCurve = _divMarket.Get(leg.Underlying, divQuoteType);

                // Repo curve
                var repoCurve = _repoMarket.Get(leg.Underlying, repoQuoteType);

                // Forward curve
                var security = _singleNameMarket.Get(snTicker, spotQuoteType);
                var oisCurve = _oisMarket.Get(security.Underlying.ReferenceCurrency, OISQuoteType);
                fwdCurve = new SingleAssetForwardCurve(security, oisCurve, divCurve, repoCurve);
            }
            else
            {
                var listSecurities = (leg.Underlying as SecurityBasket).Content;
                var securityBasket = leg.Underlying as SecurityBasket;

                var equityMarket = listSecurities.ToDictionary(x => x,
                    x =>
                    {
                        var s = _singleNameMarket.Get(x, spotQuoteType);
                        return s.Quotes.First().Value; // TODO AMELIORER. LE QUOTE EST SENCE ETRE UNIQUE 
                    }
                        );

                // Dividend curve
                divCurve = _divMarket.Get(leg.Underlying, divQuoteType);

                // Repo curve
                var repoCurve = _repoMarket.Get(leg.Underlying, repoQuoteType);

                // discount curve
                var currencies = listSecurities.Select(x => x.ReferenceCurrency).Distinct().ToList();
                var divCurrencies = _rawDivMarket.GetPaymentCurrencies(leg.Underlying);
                currencies.AddRange(divCurrencies);
                currencies = currencies.Distinct().ToList(); var oisCurves = currencies.ToDictionary(x => x, x => _oisMarket.Get(x, OISQuoteType));

                //FX forward curve
                var fxm = new Dictionary<Tuple<string, string>, IForwardForexCurve>();
                foreach (var ccy in currencies)
                {
                    var ccyPair = new CurrencyPair( ccy.Code, leg.Underlying.ReferenceCurrency.Code);

                    var fwdFxCurve = _fxMarket.Get(ccyPair, FXQuoteType);

                    fxm.Add(Tuple.Create(ccyPair.DomesticCurrency, ccyPair.ForeignCurrency), fwdFxCurve);//there
                }

                // Forward curve
                fwdCurve = new ForwardBasket(securityBasket, equityMarket
                    , oisCurves, divCurve, repoCurve, fxm, histo);

                // Set the basket value in histo model
                histo.Underlying = securityBasket;
            }

            var floatLeg = leg as AssetLegFloatRate;
            var fixingRateCurve = _fwdMarket.Get(floatLeg.FixingReference, fixingRateQuoteType);


            var fxDict = new Dictionary<Tuple<string, string>, IForwardForexCurve>();
            if (floatLeg.Underlying.Currency.Code != floatLeg.Currency.Code)
            {
                var ccyPair0 = new CurrencyPair(floatLeg.Underlying.Currency.Code, floatLeg.Currency.Code);
                var fwdFxCurve = _fxMarket.Get(ccyPair0, FXQuoteType);

                fxDict.Add(Tuple.Create(ccyPair0.DomesticCurrency, leg.Currency.Code), fwdFxCurve);
            }

            var completionCurve = new ForwardRateCurveHistoCompletion(fixingRateCurve, floatLeg.FixingReference, histo);


            var pricer = new AssetLegFloatRateFormula(_asof, fwdCurve, discCurve, completionCurve, fxDict, floatLeg) ;

            var pricingRequest = new TrsPricingRequest(trsReq.ToDo, trsReq.Reference);

            pricer.Price(pricingRequest);

            return pricingRequest;

        }

    }
}
