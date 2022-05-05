using System;
using System.Collections.Generic;
using System.Linq;
using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Models;
using AldrinAnalytics.Pricers;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Calibration.RateCurves;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Mrc;

namespace Examples
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            MarketConventionsFactory.DefaultConfiguration();
            //BasicExample.Run();t.GetTicker
            //MarketExample.Run();
            MonteCarloExample.Run();
        }

    }


    class BasicExample
    {

        public static void Run()
        {
            // Asof
            DateTime asof = new DateTime(2020, 6, 15);

            var usd = new Currency("USD");
            var eur = new Currency("EUR");

            // Basket
            SecurityBasket basket = new SecurityBasket("BASKET", eur.Code);
            var ticker1 = new SingleNameTicker("SN1", usd.Code, usd.Code);
            basket.AddComponent(new BasketComponent() { Underlying = ticker1, Weight = 0.5 });
            var ticker2 = new SingleNameTicker("SN2", eur.Code, eur.Code);
            basket.AddComponent(new BasketComponent() { Underlying = ticker2, Weight = 0.5 });

            // Discounting
            var OISDiscountingEUR = DiscountCurveBootstrapper<DateTime>.FlatRateCurve(asof, eur.Code, 0.001, CompoundingRateType.Continuously);
            var OISDiscountingUSD = DiscountCurveBootstrapper<DateTime>.FlatRateCurve(asof, usd.Code, 0.001, CompoundingRateType.Continuously);

            var disc = new Dictionary<Currency, IDiscountCurve<DateTime>>();
            disc.Add(eur, OISDiscountingEUR);
            disc.Add(usd, OISDiscountingUSD);

            // Dividend curve
            var divQuotes = Enumerable.Range(1, 10).Select(i => DividendEstimate.NewMid(asof, 3d, asof.AddYears(i), asof.AddYears(i).AddDays(2), ticker1)).ToList();
            var aiQuotes = Enumerable.Range(1, 10).Select(i => AllIn.NewMid(asof, 0.88, asof.AddYears(i)
                 , ticker1)).ToList();
            var divSheet = new DataQuoteSheet(asof, divQuotes.Cast<IInstrument>().Concat(aiQuotes.Cast<IInstrument>()));
            var divBootstrapper = new DividendCurveBootstrapper();
            var divCurves = divBootstrapper.Bootstrap(divSheet);
            var divCurve = divCurves[typeof(MidQuote)];

            // Repo curve
            var repoCurve = new RepoCurve(basket, asof, Enumerable.Range(1, 10).Select(i => asof.AddYears(i)).ToList()
                , Enumerable.Range(1, 10).Select(i => 0.002).ToList()
                , DayCountConventions.Get(DayCountConventions.Codings.Actual360));

            // Fx market
            var fxm = new Dictionary<Tuple<string, string>, IForwardForexCurve>();
            var fwdFxCurve = new FxForwardCurve(OISDiscountingUSD, OISDiscountingEUR
                , new CurrencyPair(usd.Code, eur.Code), 0.95);
            fxm.Add(Tuple.Create(usd.Code, eur.Code), fwdFxCurve); 
            //// EUR/EUR...
            fxm.Add(Tuple.Create(eur.Code, eur.Code), new FxForwardCurve(OISDiscountingEUR, OISDiscountingEUR
                 , new CurrencyPair(eur.Code, eur.Code)
                 , 1d));

            // Forward curve
            // Equity market
            var eqm = new Dictionary<SingleNameTicker, double>();
            eqm.Add(ticker1, 100d);
            eqm.Add(ticker2, 150d);

            var fwdBasket = new ForwardBasket(basket, eqm, disc, divCurve, repoCurve, fxm, null);

            var schedule = BusinessSchedule.NewParametric(asof, asof.AddYears(2), Periods.Get("3M"));

            AssetLegStd leg1 = AssetLegStd.New(schedule, "Payer", "Receiver", eur.Code, basket, 1e6, 1d, "Id0"); 

            var assetLegpricer = new AssetLegStdFormula(asof, fwdBasket, disc[eur], divCurve, fxm, leg1);

            var request = new TrsPricingRequest(PricingTask.Price, "Payer");
            assetLegpricer.Price(request);

            Console.WriteLine(request.DirtyPrice);

            IDayCountFraction fltDayCount = DayCountConventions.Get(DayCountConventions.Codings.Actual360);
            var convention = new DefaultRateConventionData(BusinessDayConventions.None, BusinessCenters.None, fltDayCount);
            var forwardCurve = ForwardCurveBootstrapper.FlatRateCurve(asof, eur.Code, Periods.Get("6M"), convention, 0.05, CompoundingRateType.Annually, fltDayCount) as IForwardRateCurve;
            var libor = new LiborReference(eur.Code, Periods.Get("6M"), fltDayCount);
            AssetLegFloatRate leg2 = new AssetLegFloatRate(schedule, "Receiver", "Payer", eur.Code,
                basket, libor, 0.001, fltDayCount, 1, "Id1");

            //var fixedLegPricer = new AssetLegFloatRateFormula(asof, fwdBasket, disc[eur], forwardCurve, leg2);


            request = new TrsPricingRequest(PricingTask.Price, "Payer");
            //fixedLegPricer.Price(request);

            Console.WriteLine(request.DirtyPrice);
        }

    }
}
