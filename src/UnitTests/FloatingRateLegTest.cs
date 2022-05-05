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
    public class FloatingRateLegTest
    {
        /// <summary>
        /// Repo=Xibor=OIS=Div=0 => 
        /// Asset Leg = 0 and
        /// Floating Leg = Spread x T
        /// </summary>
        [TestMethod]
        public void RatesAndDivsNullTest()
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
            var OISDiscountingEUR = DiscountCurveBootstrapper<DateTime>.FlatRateCurve(asof, eur.Code, 0d, CompoundingRateType.Continuously);

            var disc = new Dictionary<Currency, IDiscountCurve<DateTime>>();
            disc.Add(eur, OISDiscountingEUR);

            // Dividend curve
            var tickerIndex = new SecurityBasket("RefIndex", eur.Code);
            var divQuotes = new List<IInstrument>();
            var aiQuotes = new List<IInstrument>();

            for (int i = 1; i <= 3; i++)
            {
                divQuotes.Add(DividendEstimate.NewMid(asof, 0, asof.AddYears(i), asof.AddYears(i), ticker1));
                divQuotes.Add(DividendEstimate.NewMid(asof, 0, asof.AddYears(i), asof.AddYears(i), ticker2));

                aiQuotes.Add(AllIn.NewMid(asof, 0.0, asof.AddYears(i), ticker1));
                aiQuotes.Add(AllIn.NewMid(asof, 0.0, asof.AddYears(i), ticker2));
            }

            var divSheet = new DataQuoteSheet(asof, divQuotes.Concat(aiQuotes));

            var divBootstrapper = new DividendCurveBootstrapper();
            var divCurves = divBootstrapper.Bootstrap(divSheet);
            var divCurve = divCurves[typeof(MidQuote)];

            // Repo curve
            var repoCurve = new RepoCurve(basket, asof, Enumerable.Range(1, 10).Select(i => asof.AddYears(i)).ToList()
                , Enumerable.Range(1, 10).Select(i => 0.00).ToList()
                , DayCountConventions.Get(DayCountConventions.Codings.Actual360));

            // Fx market
            var fxm = new Dictionary<Tuple<string, string>, IForwardForexCurve>();
            //// EUR/EUR
            fxm.Add(Tuple.Create(eur.Code, eur.Code), new FxForwardCurve(OISDiscountingEUR, OISDiscountingEUR, new CurrencyPair(eur.Code, eur.Code), 1d));

            // Forward curve
            // Equity market
            var eqm = new Dictionary<SingleNameTicker, double>();
            eqm.Add(ticker1, 100d);
            eqm.Add(ticker2, 150d);

            // TRS schedule
            int nYears = 3;
            double quotity = 10;
            double divRatio = 1;
            var period = Periods.Get("1D");

            var schedule = BusinessSchedule.NewParametric(asof, asof.AddYears(nYears), period);

            var fwdBasket = new ForwardBasket(basket, eqm, disc, divCurve, repoCurve, fxm, null);

            AssetLegStd leg1 = AssetLegStd.New(schedule, "Payer", "Receiver", eur.Code, basket, quotity, divRatio, "Id0");

            var assetLegpricer = new AssetLegStdFormula(asof, fwdBasket, disc[eur], divCurve, fxm, leg1);

            var request = new TrsPricingRequest(PricingTask.Price, "Payer");
            assetLegpricer.Price(request);

            var assetLegPrice1 = request.DirtyPrice;

            double tolerance = 1e-10;
            Assert.AreEqual(assetLegPrice1, 0.0, tolerance);

            IDayCountFraction fltDcf = DayCountConventions.Get(DayCountConventions.Codings.Actual360);
            var convention = new DefaultRateConventionData(BusinessDayConventions.None, BusinessCenters.None, fltDcf);
            var forwardCurve = ForwardCurveBootstrapper.FlatRateCurve(asof, eur.Code, Periods.Get("6M"), convention, 0.0, CompoundingRateType.Annually, fltDcf);
            var libor = new LiborReference(eur.Code, Periods.Get("6M"), fltDcf);

            var spread = 0.001;
            AssetLegFloatRate leg2 = new AssetLegFloatRate(schedule, "Receiver", "Payer", eur.Code, basket, libor, spread, fltDcf, 1, "Id1");

            var fixedLegPricer = new AssetLegFloatRateFormula(asof, fwdBasket, disc[eur], forwardCurve as IForwardRateCurve, leg2);

            request = new TrsPricingRequest(PricingTask.Price, "Payer");
            fixedLegPricer.Price(request);

            var floatingLegPrice1 = spread * fltDcf.Count(asof, schedule.Dates.Last()) * fwdBasket.Spot;
            var floatingLegPrice2 = request.DirtyPrice;

            Assert.AreEqual(floatingLegPrice1, floatingLegPrice2, tolerance);
        }
    }
}
