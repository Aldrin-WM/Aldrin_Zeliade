using AldrinAnalytics.Calibration;
using AldrinAnalytics.Instruments;
using AldrinAnalytics.Pricers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Zeliade.Finance.Common.Calibration.RateCurves;
using Zeliade.Finance.Common.Product;
using Zeliade.Finance.Mrc;

namespace UnitTests
{
    [TestClass]
    public class ForwadWithNullDividendTest
    {
        [TestMethod]
        public void CloseFormulaTestNullDividend()
        {
            MarketConventionsFactory.DefaultConfiguration();

            DateTime asof = new DateTime(2020, 6, 15);
            var usd = new Currency("USD");

            var spotPrice = 100.0;

            var ticker1 = new SingleNameTicker("SN1", usd.Code, usd.Code);
            var security = Security.NewMid(asof, spotPrice, ticker1);

            var oisRate = 0.001;
            var OISDiscountingUSD = DiscountCurveBootstrapper<DateTime>.FlatRateCurve(asof, usd.Code, oisRate, CompoundingRateType.Continuously);


            var divList = Enumerable.Range(1, 10).Select(i => 
            new DividendData(){GrossAmount = 0.0 , AllIn = 0.0 , PaymentCurrency = ticker1.ReferenceCurrency, PaymentDate = asof.AddYears(i).AddDays(2), ExDate = asof.AddYears(i), Ticker = ticker1,
            });
            var divCurve = new DividendCurve(asof, divList.ToList());

            var repoRate = 0.002;
            var repoCurve = new RepoCurve(security.Underlying, asof, Enumerable.Range(1, 10).Select(i => asof.AddYears(i)).ToList()
                , Enumerable.Range(1, 10).Select(i => repoRate).ToList()
                , DayCountConventions.Get(DayCountConventions.Codings.Actual360));

            var assetForwardCurve = new SingleAssetForwardCurve(security, OISDiscountingUSD, divCurve, repoCurve);

            DateTime fwdDate = asof.AddYears(3);

            var fwdPrice = assetForwardCurve.Forward(fwdDate);

            var discountRepoMat = repoCurve.ZcPrice(fwdDate);
            var discountZcMat = OISDiscountingUSD.ZcPrice(fwdDate);

            double closeFormula = spotPrice * discountRepoMat / discountZcMat;

            double tolerance = 1e-4;

            Console.WriteLine("{0}", fwdPrice);
            Console.WriteLine("{0}", closeFormula);

            Assert.AreEqual(fwdPrice, closeFormula, tolerance);
        }
    }
}
