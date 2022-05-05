using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Models;
using Zeliade.Common;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Pricer;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;

namespace AldrinAnalytics.Pricers
{

    public class AssetLegStdFormula : IPricer
    {

        private readonly IForwardCurve _fwdCurve;
        private readonly IDiscountCurve<DateTime> _discCurve;
        private readonly IDividendCurve _divcCurve;
        private readonly Dictionary<Tuple<string, string>, IForwardForexCurve> _fxFwd;
        private readonly AssetLegStd _instrument;
        private readonly DateTime _asof;

        public AssetLegStdFormula(DateTime asof, IForwardCurve fwdCurve
            , IDiscountCurve<DateTime> discCurve
            , IDividendCurve divcCurve
            , Dictionary<Tuple<string, string>, IForwardForexCurve> fxFwd
            , AssetLegStd instrument
            )
        {
            _fwdCurve = fwdCurve ?? throw new ArgumentNullException(nameof(fwdCurve));
            _discCurve = discCurve ?? throw new ArgumentNullException(nameof(discCurve));
            _divcCurve = divcCurve ?? throw new ArgumentNullException(nameof(divcCurve));
            _instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
            _fxFwd = fxFwd ?? throw new ArgumentNullException(nameof(fxFwd));
            _asof = asof;

            Require.Argument(_instrument.Quotity.HasValue, "instrument", Error.Msg("The given leg {0} should have a defined quotity !", instrument.Id));

        }

        public IPricingRequest Price(IPricingRequest request)
        {
#if LOGGING
            var now = LogPath.StartTime;
            var locLog = new StreamWriter(string.Format("{0}\\{1}_{2}_{3}.csv", LogPath.Value, _instrument.Id, now, GetHashCode()));
            var locLogDiv = new StreamWriter(string.Format("{0}\\{1}_{2}_{3}_divs.csv", LogPath.Value, _instrument.Id, now, GetHashCode()));
            locLog.WriteLine("Start;End;disc;fwdStart;fwdEnd; fx; NPV INC");
            locLogDiv.WriteLine("Start;End;ExDate;PaymentDate;GrossAmount;AllIn;Ticker.Name;PaymentCurrency; weight;discount;fx");

#endif

            var output = Require.ArgumentIsInstanceOf<TrsPricingRequest>(request, "request");
            Require.Argument(output.Reference == _instrument.PayerParty || output.Reference == _instrument.ReceiverParty, "request"
                , Error.Msg("The input request reference counterparty {0} does not match any of the instrument counterparties {1} and {2}"
                    , output.Reference, _instrument.PayerParty, _instrument.ReceiverParty));

            double sign = output.Reference == _instrument.PayerParty ? -1d : 1d;

            double npv = 0d;
            var basket = _instrument.Underlying as SecurityBasket;

            foreach (var p in _instrument.Periods)
            {
                if (p.End <= _asof)
                {
                    continue;
                }

                var disc = _discCurve.ZcPrice(p.End);
                var fwdStart = _fwdCurve.Forward(p.Start);
                var fwdEnd = _fwdCurve.Forward(p.End);

                var fxBasketToLeg = 1.0;
                if (_instrument.Underlying.Currency.Code != _instrument.Currency.Code)
                {
                    var ccyPair0 = Tuple.Create(_instrument.Underlying.Currency.Code, _instrument.Currency.Code);
                    fxBasketToLeg = _fxFwd[ccyPair0].Forward(p.End);
                }

                npv += disc * (fwdEnd - fwdStart) * fxBasketToLeg;

#if LOGGING
                locLog.WriteLine(string.Format("{0};{1};{2};{3};{4};{5};{6}", p.Start, p.End, disc, fwdStart, fwdEnd, fxBasketToLeg, disc * (fwdEnd - fwdStart) * fxBasketToLeg));
#endif


                var divs = _divcCurve.AllInDividends(p.Start, p.End);
                foreach (var div in divs)
                {
                    if (div.PaymentDate <= _asof)
                        continue;


                    double weight;
                    var snTicker = div.Ticker as SingleNameTicker;
                    if (snTicker != null)
                    {
                        weight = (basket != null) ? basket.GetComponent(snTicker).Weight : 1d;
                    }
                    else
                    {
                        //weight = (basket != null) ? basket.SumWeights:1d;
                        //weight /= basket.Content.Count;

                        weight = 1;
                    }
                    var fx = 1.0;
                    if (div.PaymentCurrency.Code != _instrument.Currency.Code)
                    {
                        var ccyPair = Tuple.Create(div.PaymentCurrency.Code, _instrument.Currency.Code);
                        fx = _fxFwd[ccyPair].Forward(div.PaymentDate);
                    }

                    var discount = _discCurve.ZcPrice(div.PaymentDate);

                    npv += _instrument.DivRatio * weight * discount * fx * div.GrossAmount;

#if LOGGING
                    locLogDiv.WriteLine("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10}", p.Start, p.End, div.ExDate, div.PaymentDate
                        , div.GrossAmount, div.AllIn, div.Ticker.Name, div.PaymentCurrency, weight, discount, fx);
#endif

                }
            }

            npv = npv * sign * _instrument.Quotity.Value;

            output.Currency = _instrument.Currency.Code;
            output.DirtyPrice = npv;
            output.InstrumentId = _instrument.Id;
            output.InstrumentType = _instrument.GetType();

            output.SignBasketDelta = sign;

#if LOGGING
            locLog.Close();
            locLogDiv.Close();
#endif

            return request;
        }
    }

    public class AssetLegStdPricer : GenericPricer
    {
        private readonly IDividendMarket _rawDivMarket;
        private readonly JointHistoricalFixings _fixings;

        public AssetLegStdPricer(IDividendMarket divMarket
            , IGenericMarket<Ticker, IRepoCurve> repoMarket
            , IGenericMarket<Currency, IDiscountCurve<DateTime>> oisMarket
            , IGenericMarket<RateReference, IForwardRateCurve> fwdMarket
            , IGenericMarket<LiborReference, IDiscountCurve<DateTime>> liborDiscMarket
            , IGenericMarket<SingleNameTicker, SingleNameSecurity> singleNameMarket
            , IGenericMarket<CurrencyPair, IForwardForexCurve> fxMarket
            , IAssetLeg product
            , ICollateralScheme collat
            , JointHistoricalFixings fixings
            )
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
            Require.ArgumentIsInstanceOf<AssetLegStd>(product, "product");
            _rawDivMarket = divMarket;
            _fixings = fixings; // null value allowed
        }

        protected override TrsPricingRequest InternalPrice(IAssetProduct p, IPricingRequest request, bool isBasePrice)
        {
            var leg = Require.ArgumentIsInstanceOf<IAssetLeg>(p, "p");
            var trsReq = Require.ArgumentIsInstanceOf<TrsPricingRequest>(request, "request");
            IForwardCurve fwdCurve = null;
            IDiscountCurve<DateTime> discCurve = null;
            IDividendCurve divCurve = null;

            var flagAssetPayer = (trsReq.Reference == leg.PayerParty);

            var spotQuoteType = typeof(MidQuote);
            var divQuoteType = typeof(MidQuote);
            var repoQuoteType = typeof(MidQuote);
            var OISQuoteType = typeof(MidQuote);
            var FXQuoteType = typeof(MidQuote);

            if (!trsReq.IsMidPrice)
            {
                spotQuoteType = flagAssetPayer ? typeof(AskQuote) : typeof(BidQuote);
                divQuoteType = flagAssetPayer ? typeof(AskQuote) : typeof(BidQuote);
                repoQuoteType = flagAssetPayer ? typeof(BidQuote) : typeof(AskQuote);
                OISQuoteType = flagAssetPayer ? typeof(AskQuote) : typeof(BidQuote);
                FXQuoteType = flagAssetPayer ? typeof(AskQuote) : typeof(BidQuote);
            }

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
                currencies = currencies.Distinct().ToList();

                var oisCurves = currencies.ToDictionary(x => x, x => _oisMarket.Get(x, OISQuoteType));

                //FX forward curve
                var fxm = new Dictionary<Tuple<string, string>, IForwardForexCurve>();
                foreach (var ccy in currencies)
                {
                    var ccyPair = new CurrencyPair( ccy.Code, leg.Underlying.ReferenceCurrency.Code
                        );

                    var fwdFxCurve = _fxMarket.Get(ccyPair, FXQuoteType);

                    fxm.Add(Tuple.Create(ccy.Code, leg.Underlying.ReferenceCurrency.Code), fwdFxCurve);

                }

                // Forward curve
                var histo = new HistoricalJointModelSingleQuoteType(_fixings, spotQuoteType
                    , divQuoteType, typeof(MidQuote), typeof(MidQuote)
                    , FXQuoteType);

                fwdCurve = new ForwardBasket(securityBasket
                    , equityMarket, oisCurves, divCurve
                    , repoCurve, fxm, histo);

                histo.Underlying = securityBasket;
            }

            var divCurrencies1 = _rawDivMarket.GetPaymentCurrencies(leg.Underlying);
            divCurrencies1 = divCurrencies1.Distinct().ToList();

            // Forex curves to change divs in the leg payment currency
            var fxDict = new Dictionary<Tuple<string, string>, IForwardForexCurve>();
            foreach (var ccy in divCurrencies1)
            {
                var ccyPair = new CurrencyPair( ccy.Code, leg.Currency.Code);

                var fwdFxCurve = _fxMarket.Get(ccyPair, FXQuoteType);

                fxDict.Add(Tuple.Create(ccy.Code, leg.Currency.Code), fwdFxCurve);
            }

            var pricer = new AssetLegStdFormula(_asof, fwdCurve, discCurve, divCurve
                , fxDict, leg as AssetLegStd);

            var pricingRequest = new TrsPricingRequest(trsReq.ToDo, trsReq.Reference);

            pricer.Price(pricingRequest);

            return pricingRequest;

        }

    }

}
