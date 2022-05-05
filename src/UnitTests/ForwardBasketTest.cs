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
    public class ForwardBasketTest
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentException),
            "Tickers in a SecurityBasket should have different names")]
        public void TickersShouldHaveDiffNamesTest()
        {
            MarketConventionsFactory.DefaultConfiguration();

            // Asof
            DateTime asof = new DateTime(2020, 6, 15);

            var eur = new Currency("EUR");

            // Equity market
            var ticker1 = new SingleNameTicker("SN1", eur.Code, eur.Code);
            var ticker2 = new SingleNameTicker("SN1", eur.Code, eur.Code);

            // Basket
            SecurityBasket basket = new SecurityBasket("BASKET", eur.Code);
            basket.AddComponent(new BasketComponent() { Underlying = ticker1, Weight = 0.5 });
            basket.AddComponent(new BasketComponent() { Underlying = ticker2, Weight = 0.5 });
        }

        /// <summary>
        /// Dividends after the Forward date do not influence the forward value.
        /// </summary>
        [TestMethod]
        public void ConsistencyWithyDividendsTest()
        {
            MarketConventionsFactory.DefaultConfiguration();

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
            var aiQuotes = Enumerable.Range(1, 10).Select(i => AllIn.NewMid(asof, 0.88, asof.AddYears(i), ticker1)).ToList();
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
            fxm.Add(Tuple.Create(usd.Code, eur.Code), new FxForwardCurve(OISDiscountingUSD, OISDiscountingEUR, new CurrencyPair(usd.Code, eur.Code), 1.1));
            //// EUR/EUR...
            fxm.Add(Tuple.Create(eur.Code, eur.Code), new FxForwardCurve(OISDiscountingEUR, OISDiscountingEUR, new CurrencyPair(eur.Code, eur.Code), 1d));

            // Forward curve
            // Equity market
            var eqm = new Dictionary<SingleNameTicker, double>();
            eqm.Add(ticker1, 100d);
            eqm.Add(ticker2, 150d);

            // price 1
            var fwdBasket = new ForwardBasket(basket, eqm, disc, divCurve, repoCurve, fxm, null);
            int nYears = 2;
            var price1 = fwdBasket.Forward(asof.AddYears(nYears));

            // price 2
            // change a dividend with an ex-div after the forward maturity. The price should'nt change.
            divQuotes[nYears] = DividendEstimate.NewMid(asof, 0.0, asof.AddYears(nYears+1), asof.AddYears(nYears+1).AddDays(2), ticker1);

            divSheet = new DataQuoteSheet(asof, divQuotes.Cast<IInstrument>().Concat(aiQuotes.Cast<IInstrument>()));
            divCurves = divBootstrapper.Bootstrap(divSheet);
            divCurve = divCurves[typeof(MidQuote)];
            var price2 = fwdBasket.Forward(asof.AddYears(nYears));

            double tolerance = 1e-10;

            Console.WriteLine("{0}", price1);
            Console.WriteLine("{0}", price2);

            Assert.AreEqual(price1, price2, tolerance);

        }
    }
}
