using System;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Calibration.RateCurves;
using Zeliade.Finance.Common.Calibration.RateCurves.Instruments;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Mrc;

namespace Examples
{
    public class MarketExample
    {

        public static void Run()
        {
            // Asof
            DateTime asof = new DateTime(2020, 6, 15);

            var usd = new Currency("USD");
            var eur = new Currency("EUR");

            // OIS market
            var eurZcRates = Enumerable.Range(1, 10).Select(i => new ZeroCouponRateQuote(asof, asof.AddYears(i)
                 , eur.Code, Periods.Get("3M"), ReferenceTimeDayCount.Value
                , CompoundingRateType.Continuously).AddQuote(new MidQuote(asof, 0.005))
                 );
            var eurSheet = new DataQuoteSheet(asof, eurZcRates);
            var usdZcRates = Enumerable.Range(1, 10).Select(i => new ZeroCouponRateQuote(asof, asof.AddYears(i)
                 , usd.Code, Periods.Get("3M"), ReferenceTimeDayCount.Value
                , CompoundingRateType.Continuously).AddQuote(new MidQuote(asof, 0.007))
                 );
            var usdSheet = new DataQuoteSheet(asof, usdZcRates);
            var oisMarket = new GenericMarket<Currency, IDiscountCurve<DateTime>>(asof);
            var oisBoot = new DiscountCurveBootstrapperFromZcRate(Periods.Get("3M"));
            oisMarket.AddSheet(eur, eurSheet, oisBoot);
            oisMarket.AddSheet(usd, usdSheet, oisBoot);
            oisMarket.SetBump(eur, new FlatBump<ZeroCouponRateQuote>(0.01, -0.01, QuoteBumpType.Relative, new Derivative()));

            // Mono curves
            var liborDiscMarket = new GenericMarket<LiborReference, IDiscountCurve<DateTime>>(asof);

            // Forward rate market
            var fwdMarket = new GenericMarket<RateReference, IForwardRateCurve>(asof);

            // Equity market
            var ticker1 = new SingleNameTicker("SN1", eur.Code, eur.Code);
            var ticker2 = new SingleNameTicker("SN2", usd.Code, usd.Code);
            var securityMarket = new SingleNameMarket(asof);
            securityMarket.AddSecurity(SingleNameSecurity.NewMid(asof, 100d, ticker1));
            securityMarket.AddSecurity(SingleNameSecurity.NewMid(asof, 150d, ticker2));
            securityMarket.SetBump(ticker1, new FlatBump<SingleNameSecurity>(0.01, -0.01, QuoteBumpType.Absolute, new Derivative()));
            securityMarket.SetBump(ticker1, new FlatBump<SingleNameSecurity>(0.02, -0.02, QuoteBumpType.Absolute, new Derivative()));
            securityMarket.SetBump(ticker2, new FlatBump<SingleNameSecurity>(0.01, -0.01, QuoteBumpType.Absolute, new Derivative()));

            // Fx market
            var fxMarket = new FxCurveMarket(asof);
            var eurusd = new CurrencyPair(eur.Code, usd.Code);
            var fxSheet = new DataQuoteSheet(asof, new IInstrument[] { CurrencyPairSecurity.NewMid(asof, 1.10, eurusd) });
            fxSheet.AddData(eurSheet, false);
            fxSheet.AddData(usdSheet, false);
            fxMarket.AddSheet(eurusd, fxSheet, new FxCurveBootstrapper<ZeroCouponRateQuote>(oisBoot));
            fxMarket.SetBump(eurusd, new FlatBump<CurrencyPairSecurity>(0.01, -0.01, QuoteBumpType.Absolute, new Derivative()));
            fxMarket.SetBump(eurusd, new FlatBump<ZeroCouponRateQuote>(0.0012, -0.0012, QuoteBumpType.Absolute, new Derivative()));
            var eureur = new CurrencyPair(eur.Code, eur.Code);
            var oneSheet = new DataQuoteSheet(asof, new IInstrument[] { CurrencyPairSecurity.NewMid(asof, 1.0, eureur) });
            fxMarket.AddSheet(eureur, oneSheet, new FxCurveBootstrapper<ZeroCouponRateQuote>(oisBoot));



            // Repo market
            //SecurityBasket basket = new SecurityBasket("BASKET", eur.Code);
            //basket.AddOrUpdateComponent(ticker1, new BasketComponent() { Underlying = ticker1, Weight = 0.5 });
            //basket.AddOrUpdateComponent(ticker1, new BasketComponent() { Underlying = ticker2, Weight = 0.5 });

            var repoMarket = new RepoMarket(asof);
            var repoBootstrapper = new RepoCurveBootstrapper();

            var repoRates = Enumerable.Range(1, 10).Select(i => RepoRate.NewMid(asof, 0.0022, ticker1
                 , new DateTime(2019,12,17).AddYears(i), CompoundingRateType.Simply
                 , DayCountConventions.Get(DayCountConventions.Codings.Actual360))
                ).ToList();
            var repoSheet = new DataQuoteSheet(asof, repoRates);

            repoMarket.AddSheet(ticker1, repoSheet, repoBootstrapper);

            var bump = new FullPillarAndUnderlyingBump<RepoRate>(0.01, -0.01, QuoteBumpType.Absolute, new Derivative());
            repoMarket.SetBump(ticker1, bump);

            // Dividends market
            var divQuotes = Enumerable.Range(1, 10).Select(i => DividendEstimate.NewMid(asof, 3d, asof.AddYears(i)
                 , asof.AddYears(i).AddDays(2), ticker1)).ToList();
            var allinQuotes = Enumerable.Range(1, 10).Select(i => AllIn.NewMid(asof, 0.88, asof.AddYears(i)
                 , ticker1)).ToList();

            var divSheet = new DataQuoteSheet(asof, divQuotes.Cast<IInstrument>().Concat(allinQuotes));
            var divMarket = new DividendMarket(asof);
            var divBootstrapper = new DividendCurveBootstrapper();
            divMarket.AddSheet(ticker1, divSheet, divBootstrapper);
            var bumpDiv = new FullPillarAndUnderlyingBump<DividendEstimate>(0.1, -0.1, QuoteBumpType.Absolute, new Derivative());
            var bumpAi = new FullPillarAndUnderlyingBump<AllIn>(0.01, -0.01, QuoteBumpType.Relative, new Derivative());
            divMarket.SetBump(ticker1, bumpDiv);
            divMarket.SetBump(ticker1, bumpAi);

            // Collat
            var collat = new CashCollateral(eur);

            var schedule = BusinessSchedule.NewParametric(asof, asof.AddYears(2), Periods.Get("3M"));

            AssetLegStd leg1 = AssetLegStd.New(schedule, "Payer", "Receiver", usd.Code, ticker1, 1e6, 1d, "Id0");

            var pricer = new AssetLegStdPricer(divMarket, repoMarket, oisMarket, fwdMarket
                , liborDiscMarket, securityMarket, fxMarket
                , leg1, collat
                , null);

            var request = new TrsPricingRequest(PricingTask.All, leg1.PayerParty);

            pricer.Price(request);
            Console.WriteLine(request);

            
        }

    }
}
