using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Zeliade.Finance.Common.Calibration;
using Zeliade.Finance.Common.Calibration.RateCurves;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Common.RateCurves;
using Zeliade.Finance.Mrc;

namespace UnitTests
{
    [TestClass]
    public class DividendCoarseTest
    {
        /// <summary>
        /// Check the DividendCoarse mechanism. The Forwrad and TRS prices should be unchanged if we replace single name dividend with index dividend with an average dividend (all in kept the same).
        /// </summary>
        [TestMethod]
        public void ConsistencyWithyDividendsTest()
        {
            MarketConventionsFactory.DefaultConfiguration();

            // Asof
            DateTime asof = new DateTime(2020, 6, 15);
            var eur = new Currency("EUR");

            // Basket
            SecurityBasket basket = new SecurityBasket("BASKET", eur.Code);
            var ticker1 = new SingleNameTicker("SN1", eur.Code, eur.Code);
            basket.AddComponent(new BasketComponent() { Underlying = ticker1, Weight = 0.5 });
            var ticker2 = new SingleNameTicker("SN2", eur.Code, eur.Code);
            basket.AddComponent(new BasketComponent() { Underlying = ticker2, Weight = 0.5 });

            // Discounting
            var OISDiscountingEUR = DiscountCurveBootstrapper<DateTime>.FlatRateCurve(asof, eur.Code, 0.001, CompoundingRateType.Continuously);

            var disc = new Dictionary<Currency, IDiscountCurve<DateTime>>();
            disc.Add(eur, OISDiscountingEUR);

            // Dividend curve
            var tickerIndex = new SecurityBasket("RefIndex", eur.Code);
            var divQuotes = new List<IInstrument>();
            var aiQuotes = new List<IInstrument>();

            for (int i = 1; i <= 3; i++)
            {
                divQuotes.Add(DividendEstimate.NewMid(asof, 2d, asof.AddYears(i), asof.AddYears(i), ticker1));
                divQuotes.Add(DividendEstimate.NewMid(asof, 4d, asof.AddYears(i), asof.AddYears(i), ticker2));

                aiQuotes.Add(AllIn.NewMid(asof, 0.8, asof.AddYears(i), ticker1));
                aiQuotes.Add(AllIn.NewMid(asof, 0.8, asof.AddYears(i), ticker2));
            }

            var divSheet = new DataQuoteSheet(asof, divQuotes.Concat(aiQuotes));

            var divBootstrapper = new DividendCurveBootstrapper();
            var divCurves = divBootstrapper.Bootstrap(divSheet);
            var divCurve = divCurves[typeof(MidQuote)];

            // Repo curve
            var repoCurve = new RepoCurve(basket, asof, Enumerable.Range(1, 10).Select(i => asof.AddYears(i)).ToList()
                , Enumerable.Range(1, 10).Select(i => 0.002).ToList()
                , DayCountConventions.Get(DayCountConventions.Codings.Actual360));

            // Fx market
            var fxm = new Dictionary<Tuple<string, string>, IForwardForexCurve>();
            //// EUR/EUR
            fxm.Add(Tuple.Create(eur.Code, eur.Code), new FxForwardCurve(OISDiscountingEUR, OISDiscountingEUR, new CurrencyPair(eur.Code, eur.Code), 1d));

            // Equity market
            var eqm = new Dictionary<SingleNameTicker, double>();
            eqm.Add(ticker1, 100d);
            eqm.Add(ticker2, 150d);


            // TRS schedule
            int nYears = 3;
            double quotity = 10;
            double divRatio = 1;
            var period = Periods.Get("1D");

            var bc = BusinessCenters.Get(BusinessCenters.Codings.WEND);
            var bdc = BusinessDayConventions.Get(BusinessDayConventions.Codings.Following);
            var schedule = BusinessSchedule.NewParametric(asof, asof.AddYears(nYears), period, bc, bdc);

            AssetLegStd assetLeg = AssetLegStd.New(schedule, "Payer", "Receiver", eur.Code, basket, quotity, divRatio, "Id0");

            ///// price 1
            // Forward curve
            var fwdBasket = new ForwardBasket(basket, eqm, disc, divCurve, repoCurve, fxm, null);
            var forwardPrice1 = fwdBasket.Forward(asof.AddYears(nYears));

            var assetLegpricer = new AssetLegStdFormula(asof, fwdBasket, disc[eur], divCurve, fxm, assetLeg);

            var request = new TrsPricingRequest(PricingTask.Price, "Payer");
            assetLegpricer.Price(request);

            var TRSPrice1 = request.DirtyPrice;

            ///// price 2
            divQuotes.Clear();
            divQuotes.Add(DividendEstimate.NewMid(asof, 2d, asof.AddYears(1), asof.AddYears(1), ticker1));
            divQuotes.Add(DividendEstimate.NewMid(asof, 4d, asof.AddYears(1), asof.AddYears(1), ticker2));
            divQuotes.Add(DividendCoarse.NewMid(asof, 3d, asof.AddYears(2), tickerIndex));
            divQuotes.Add(DividendCoarse.NewMid(asof, 3d, asof.AddYears(3), tickerIndex));

            aiQuotes.Clear();
            aiQuotes.Add(AllIn.NewMid(asof, 0.8, asof.AddYears(1), ticker1));
            aiQuotes.Add(AllIn.NewMid(asof, 0.8, asof.AddYears(1), ticker2));
            aiQuotes.Add(AllInCoarse.NewMid(asof, 0.8, asof.AddYears(2), tickerIndex));
            aiQuotes.Add(AllInCoarse.NewMid(asof, 0.8, asof.AddYears(3), tickerIndex));

            divSheet = new DataQuoteSheet(asof, divQuotes.Concat(aiQuotes));
            divCurves = divBootstrapper.Bootstrap(divSheet);
            divCurve = divCurves[typeof(MidQuote)];

            fwdBasket = new ForwardBasket(basket, eqm, disc, divCurve, repoCurve, fxm, null);
            var forwardPrice2 = fwdBasket.Forward(asof.AddYears(nYears));

            assetLegpricer = new AssetLegStdFormula(asof, fwdBasket, disc[eur], divCurve, fxm,assetLeg);
            request = new TrsPricingRequest(PricingTask.Price, "Payer");
            assetLegpricer.Price(request);

            var TRSPrice2 = request.DirtyPrice;
            
            //
            double tolerance = 1e-10;

            Console.WriteLine("Check the DividendCoarse mechanism. The forward price should be unchanged if we used index dividend with an average dividend.");
            Console.WriteLine("{0}", forwardPrice1);
            Console.WriteLine("{0}", forwardPrice2);

            Console.WriteLine("Check the DividendCoarse mechanism. The TRS price should be unchanged if we used index dividend with an average dividend.");
            Console.WriteLine("{0}", TRSPrice1);
            Console.WriteLine("{0}", TRSPrice2);

            Assert.AreEqual(forwardPrice1, forwardPrice2, tolerance);
            Assert.AreEqual(TRSPrice1, TRSPrice2, tolerance);
        }
    }


}
